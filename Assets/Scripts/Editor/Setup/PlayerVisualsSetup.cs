using System.IO;
using System.Linq;
using ButchersGames.Cameras;
using ButchersGames.Core;
using ButchersGames.Player;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace ButchersGames.EditorTools
{
    /// <summary>
    /// Puts the walk animation on the player model and moves the camera closer to the player.
    /// Idempotent: the controller is created once, existing references are kept.
    /// </summary>
    public static class PlayerVisualsSetup
    {
        private const string WalkClipModelPath = "Assets/player (1)@Strut Walking.fbx";
        private const string WalkClipSearch = "Strut Walking";
        private const string WalkClipName = "StrutWalking";

        private const string ControllerFolder = "Assets/Visual/Animation";
        private const string ControllerPath = ControllerFolder + "/Player.controller";

        // Camera a bit closer than the old (0, 5, -7) / (0, 1, 5)
        private static readonly Vector3 CameraOffset = new Vector3(0f, 3.8f, -5.2f);
        private static readonly Vector3 CameraLookAtOffset = new Vector3(0f, 1f, 4f);

        [MenuItem("Tools/Runner/Setup Player Visuals")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Runner] Setup Player Visuals cannot run in Play mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Runner] No active scene loaded.");
                return;
            }

            AnimationClip clip = ImportWalkClip();
            AnimatorController controller = clip != null ? GetOrCreateController(clip) : null;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Runner: Setup Player Visuals");
            int undoGroup = Undo.GetCurrentGroup();

            PlayerController player = Object.FindObjectOfType<PlayerController>(true);
            if (player == null)
                Debug.LogError("[Runner] Player not found in the scene, run Tools/Runner/Setup Scene first.");
            else if (controller != null)
                SetupAnimator(player, controller, clip);

            SetupCamera();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Runner] Player visuals are set up. Save the scene (Ctrl+S).");
        }

        #region Animation

        private static string FindWalkClipModel()
        {
            if (AssetImporter.GetAtPath(WalkClipModelPath) is ModelImporter) return WalkClipModelPath;

            foreach (string guid in AssetDatabase.FindAssets(WalkClipSearch + " t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter) return path;
            }
            return null;
        }

        /// <summary>Makes the clip loop and returns it.</summary>
        private static AnimationClip ImportWalkClip()
        {
            string path = FindWalkClipModel();
            if (path == null)
            {
                Debug.LogError("[Runner] Walk animation '" + WalkClipSearch + "' not found in Assets.");
                return null;
            }

            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                Debug.LogError("[Runner] '" + path + "' contains no animation.");
                return null;
            }

            bool changed = importer.clipAnimations == null || importer.clipAnimations.Length == 0;

            // The file has a single root ('Armature'). Without this Unity merges it into the model root:
            // the curve paths lose the 'Armature/' prefix and the Armature rotation turns the whole model.
            if (!importer.preserveHierarchy)
            {
                importer.preserveHierarchy = true;
                changed = true;
            }

            ModelImporterClipAnimation walk = clips[0];
            if (!walk.loopTime || walk.name != WalkClipName)
            {
                walk.loopTime = true;
                walk.name = WalkClipName;
                changed = true;
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null)
                Debug.LogError("[Runner] Could not load the animation clip from '" + path + "'.");
            return clip;
        }

        private static AnimatorController GetOrCreateController(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null) return controller;

            if (!AssetDatabase.IsValidFolder(ControllerFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(ControllerFolder).Replace('\\', '/'), Path.GetFileName(ControllerFolder));

            controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);
            Debug.Log("[Runner] Created " + ControllerPath);
            return controller;
        }

        private static void SetupAnimator(PlayerController player, AnimatorController controller, AnimationClip clip)
        {
            GameObject model = FindModelRoot(player);
            if (model == null)
            {
                Debug.LogError("[Runner] Player model with an 'Armature' was not found under the player, the animation was not assigned.", player);
                return;
            }

            CheckBindings(clip, model.transform);
            AlignModelFacing(model, clip, player.transform);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(model);

            Undo.RecordObject(animator, "Setup Player Animator");
            if (animator.runtimeAnimatorController == null)
                animator.runtimeAnimatorController = controller;
            // The path follower moves the player, the clip is in place
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);

            PlayerAnimation playerAnimation = player.GetComponent<PlayerAnimation>();
            if (playerAnimation == null)
                playerAnimation = Undo.AddComponent<PlayerAnimation>(player.gameObject);

            var serialized = new SerializedObject(playerAnimation);
            SetIfEmpty(serialized, "player", player);
            SetIfEmpty(serialized, "animator", animator);
            serialized.ApplyModifiedProperties();
        }

        /// <summary>The object that holds the model skeleton: the curves of the clip start at its 'Armature' child.</summary>
        private static GameObject FindModelRoot(PlayerController player)
        {
            PlayerAppearance appearance = player.GetComponentInChildren<PlayerAppearance>(true);
            if (appearance != null && appearance.transform.Find("Armature") != null)
                return appearance.gameObject;

            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Armature" && child.parent != null && child.parent != player.transform)
                    return child.parent.gameObject;
            }
            return null;
        }

        /// <summary>Logs how many animated objects of the clip exist under the model: a generic clip only moves the ones it finds.</summary>
        private static void CheckBindings(AnimationClip clip, Transform model)
        {
            var paths = AnimationUtility.GetCurveBindings(clip).Select(b => b.path).Distinct().ToArray();
            string[] missing = paths.Where(p => p.Length > 0 && model.Find(p) == null).ToArray();
            bool animatesRoot = paths.Any(p => p.Length == 0);

            if (missing.Length == 0 && !animatesRoot)
            {
                Debug.Log("[Runner] Walk clip: all " + paths.Length + " animated bones found on '" + model.name + "'.");
                return;
            }

            if (animatesRoot)
                Debug.LogWarning("[Runner] Walk clip animates the model root itself, the model may turn. Check 'Preserve Hierarchy' on the clip file.", model);
            if (missing.Length > 0)
                Debug.LogWarning("[Runner] Walk clip: " + missing.Length + "/" + paths.Length + " bones not found on '" + model.name + "', e.g. '" + missing[0] + "'.", model);
        }

        /// <summary>
        /// Turns the model around Y so that the body in the walk pose faces the running direction of the player.
        /// The facing is taken from the hips: left hip -> right hip is the body's right side.
        /// </summary>
        private static void AlignModelFacing(GameObject model, AnimationClip clip, Transform player)
        {
            Transform leftHip = FindBone(model.transform, "LeftUpLeg");
            Transform rightHip = FindBone(model.transform, "RightUpLeg");
            if (leftHip == null || rightHip == null)
            {
                Debug.LogWarning("[Runner] Hip bones not found, the model facing was not checked. Rotate the model by hand if it walks sideways.", model);
                return;
            }

            // Sample the pose in animation mode, so the scene gets no changes from it
            Vector3 right;
            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(model, clip, 0f);
                AnimationMode.EndSampling();
                right = rightHip.position - leftHip.position;
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            right.y = 0f;
            Vector3 runDirection = player.forward;
            runDirection.y = 0f;
            if (right.sqrMagnitude < 1e-6f || runDirection.sqrMagnitude < 1e-6f) return;

            Vector3 bodyForward = Vector3.Cross(right, Vector3.up).normalized;
            float angle = Vector3.SignedAngle(bodyForward, runDirection.normalized, Vector3.up);
            if (Mathf.Abs(angle) < 5f)
            {
                Debug.Log("[Runner] Player model already faces the running direction.");
                return;
            }

            Undo.RecordObject(model.transform, "Align Player Model Facing");
            model.transform.rotation = Quaternion.AngleAxis(angle, Vector3.up) * model.transform.rotation;
            EditorUtility.SetDirty(model.transform);
            Debug.Log("[Runner] Player model turned by " + angle.ToString("0") + " degrees to face the running direction.", model);
        }

        private static Transform FindBone(Transform root, string suffix)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith(suffix));
        }

        private static void SetIfEmpty(SerializedObject serialized, string field, Object value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null && property.objectReferenceValue == null)
                property.objectReferenceValue = value;
        }

        #endregion

        #region Camera

        private static void SetupCamera()
        {
            CameraFollow follow = Object.FindObjectOfType<CameraFollow>(true);
            if (follow == null)
            {
                Debug.LogWarning("[Runner] CameraFollow not found, the camera was not moved.");
                return;
            }

            Undo.RecordObject(follow, "Move Camera Closer");
            follow.Offset = CameraOffset;
            follow.LookAtOffset = CameraLookAtOffset;
            EditorUtility.SetDirty(follow);

            Transform target = follow.Target;
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(GameTags.Player);
                target = player != null ? player.transform : null;
            }
            if (target == null) return;

            // Show the new framing in the Scene/Game view right away
            Undo.RecordObject(follow.transform, "Move Camera Closer");
            Quaternion yaw = follow.RotateWithTarget ? Quaternion.Euler(0f, target.eulerAngles.y, 0f) : Quaternion.identity;
            follow.transform.position = target.position + yaw * CameraOffset;
            follow.transform.LookAt(target.position + yaw * CameraLookAtOffset);
        }

        #endregion
    }
}
