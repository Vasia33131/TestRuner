using System.Collections.Generic;
using ButchersGames.Core;
using ButchersGames.Gameplay.Checkpoints;
using ButchersGames.Gameplay.Collectibles;
using ButchersGames.Gameplay.Finish;
using ButchersGames.Levels;
using ButchersGames.Player;
using ButchersGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ButchersGames.EditorTools
{
    /// <summary>
    /// Sets up the checkpoints and the level end doors in the open scene:
    /// - yellow objects (by material color) get SaveZone + a trigger, indexed along the path;
    /// - children of a zone become its flags (FlagRaiser); zones without children get a placeholder flag (after a confirmation);
    /// - purple objects (by material color) get MaterialSwapTarget;
    /// - door models get Door, a "LevelEnd" object gets DoorsController + FinishController;
    /// - a "FinishPanel" is built under the overlay canvas.
    /// Idempotent: existing components, references and objects are kept, only missing ones are added.
    /// </summary>
    public static class CheckpointsSetup
    {
        // Auto-detection only; at runtime everything comes from the inspector
        private const string DoorNameHint = "Door";
        private const float YellowHueMin = 40f / 360f;
        private const float YellowHueMax = 70f / 360f;
        private const float PurpleHueMin = 260f / 360f;
        private const float PurpleHueMax = 320f / 360f;
        private const float MinSaturation = 0.35f;
        private const float MinValue = 0.3f;

        private const string FlagPoleMaterialPath = "Assets/Visual/Default.mat";
        private const string FlagClothMaterialPath = "Assets/Visual/Material/Checkpoints.mat";
        private const string ActivatedMaterialPath = "Assets/Visual/Material/ZoneActivated.mat";
        private const string FontPath = "Assets/Visual/Fonts/Inter-SemiBold SDF.asset";
        private const string BuiltInSprite = "UI/Skin/UISprite.psd";
        private const string UiLayerName = "UI";

        private const float FlagRiseHeight = 1.5f;
        private const float FlagPoleLength = 2.4f;

        private const string FinishPanelName = "FinishPanel";
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color ButtonColor = new Color(0.27f, 0.78f, 0.28f, 1f);
        private static readonly Color SecondButtonColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        private static readonly Vector2 MainButtonPosition = new Vector2(0f, -260f);
        private static readonly Vector2 MainButtonSize = new Vector2(820f, 160f);
        private static readonly Vector2 SecondButtonPosition = new Vector2(0f, -440f);
        private static readonly Vector2 SecondButtonSize = new Vector2(520f, 110f);

        [MenuItem("Tools/Runner/Setup Checkpoints And Doors")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Runner] Setup Checkpoints And Doors cannot run in Play mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Runner] No active scene loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Runner: Setup Checkpoints And Doors");
            int undoGroup = Undo.GetCurrentGroup();

            PlayerController player = Object.FindObjectOfType<PlayerController>(true);
            Level level = Object.FindObjectOfType<Level>(true);
            Transform[] path = level != null ? level.Waypoints : null;
            if (!PathUtility.IsValid(path))
                Debug.LogWarning("[Runner] Level waypoints not found, zones and doors are ordered by distance from the player.");

            // Doors first, so their models are excluded from the color search below
            List<Door> doors = SetupDoors(scene, player, path);
            List<SaveZone> zones = SetupZones(scene, player, path);
            SetupFlags(zones);
            int purple = SetupPurpleTargets(scene);
            SetupCheckpointManager(player);
            FinishController finish = SetupLevelEnd(doors, player);
            SetupFinishPanel(scene, finish);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Runner] Checkpoints: " + zones.Count + " zones, " + purple + " purple objects, " + doors.Count +
                      " doors. Save the scene (Ctrl+S).");
        }

        #region Zones

        private static List<SaveZone> SetupZones(Scene scene, PlayerController player, Transform[] path)
        {
            var zones = new List<SaveZone>();
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>(true))
            {
                if (r.gameObject.scene != scene || IsExcluded(r.transform)) continue;

                SaveZone zone = r.GetComponent<SaveZone>();
                if (zone == null && !HasMaterialInHueRange(r, YellowHueMin, YellowHueMax)) continue;

                if (zone == null)
                {
                    if (r.GetComponent<Collider>() == null)
                        Undo.AddComponent<BoxCollider>(r.gameObject);
                    zone = Undo.AddComponent<SaveZone>(r.gameObject);
                }

                Collider zoneCollider = r.GetComponent<Collider>();
                if (!zoneCollider.isTrigger)
                {
                    Undo.RecordObject(zoneCollider, "Make Zone Trigger");
                    zoneCollider.isTrigger = true;
                }

                zones.Add(zone);
            }

            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            zones.Sort((a, b) => Order(path, origin, a.transform.position).CompareTo(Order(path, origin, b.transform.position)));

            for (int i = 0; i < zones.Count; i++)
            {
                var serialized = new SerializedObject(zones[i]);
                SerializedProperty index = serialized.FindProperty("zoneIndex");
                if (index.intValue < 0)
                {
                    index.intValue = i;
                    serialized.ApplyModifiedProperties();
                }
            }
            return zones;
        }

        private static void SetupFlags(List<SaveZone> zones)
        {
            var missing = new List<SaveZone>();
            foreach (SaveZone zone in zones)
            {
                if (zone.Flags.Count > 0)
                {
                    foreach (FlagRaiser flag in zone.Flags)
                    {
                        if (flag != null) DetachFlag(flag, zone.transform);
                    }
                    continue;
                }
                if (!SetupChildFlags(zone)) missing.Add(zone);
            }
            if (missing.Count == 0) return;

            bool create = EditorUtility.DisplayDialog("Setup Checkpoints",
                missing.Count + " zone(s) have no flags (no children). Create placeholder flags?\n\n" +
                "If the scene already has flag models, press \"Skip\", make them children of the zone and run the setup again.",
                "Create", "Skip");
            if (!create) return;

            Material pole = AssetDatabase.LoadAssetAtPath<Material>(FlagPoleMaterialPath);
            Material cloth = AssetDatabase.LoadAssetAtPath<Material>(FlagClothMaterialPath);
            foreach (SaveZone zone in missing)
            {
                AddFlags(zone, new List<FlagRaiser> { CreatePlaceholderFlag(zone, pole, cloth) });
            }
        }

        // Flags placed as children of the zone: every direct child gets a FlagRaiser (local Z -> 0, rotation -> 0)
        private static bool SetupChildFlags(SaveZone zone)
        {
            var flags = new List<FlagRaiser>(zone.GetComponentsInChildren<FlagRaiser>(true));
            if (flags.Count == 0)
            {
                foreach (Transform child in zone.transform)
                    flags.Add(Undo.AddComponent<FlagRaiser>(child.gameObject));
            }
            if (flags.Count == 0) return false;

            foreach (FlagRaiser flag in flags)
                DetachFlag(flag, zone.transform);

            AddFlags(zone, flags);
            return true;
        }

        // Out of the non-uniformly scaled strip (world pose kept); the zone stays the space of the target values
        private static void DetachFlag(FlagRaiser flag, Transform zone)
        {
            BindReference(flag, "space", zone);
            if (flag.transform.IsChildOf(zone))
                Undo.SetTransformParent(flag.transform, zone.parent, "Detach Flag From Zone");
        }

        private static void AddFlags(SaveZone zone, List<FlagRaiser> flags)
        {
            var serialized = new SerializedObject(zone);
            SerializedProperty list = serialized.FindProperty("flags");
            list.arraySize = flags.Count;
            for (int i = 0; i < flags.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = flags[i];
            serialized.ApplyModifiedProperties();
        }

        // The mount's local Z points up, so the flag rises along local Z as the FlagRaiser expects
        private static FlagRaiser CreatePlaceholderFlag(SaveZone zone, Material poleMaterial, Material clothMaterial)
        {
            Transform zoneTransform = zone.transform;
            var mount = new GameObject("FlagMount_Zone" + zone.ZoneIndex);
            Undo.RegisterCreatedObjectUndo(mount, "Create Flag");
            // A sibling, not a child: the strip is scaled non-uniformly and would squash the flag
            mount.transform.SetParent(zoneTransform.parent, false);

            Vector3 across = zoneTransform.right.normalized;
            Vector3 along = Vector3.ProjectOnPlane(zoneTransform.forward, Vector3.up);
            if (along.sqrMagnitude < 1e-6f) along = Vector3.forward;
            mount.transform.position = zoneTransform.TransformPoint(new Vector3(0.5f, 0.5f, 0f)) + across * 0.3f;
            mount.transform.rotation = Quaternion.LookRotation(Vector3.up, along.normalized);

            var flagObject = new GameObject("Flag");
            flagObject.transform.SetParent(mount.transform, false);
            // Starts sunk and tilted, the FlagRaiser brings it up to (0,0,0)
            flagObject.transform.localPosition = new Vector3(0f, 0f, -FlagRiseHeight);
            flagObject.transform.localRotation = Quaternion.Euler(30f, 0f, 25f);

            CreatePart("Pole", flagObject.transform, new Vector3(0f, 0f, FlagPoleLength * 0.5f),
                new Vector3(0.08f, 0.08f, FlagPoleLength), poleMaterial);
            CreatePart("Cloth", flagObject.transform, new Vector3(0.5f, 0f, FlagPoleLength - 0.35f),
                new Vector3(0.9f, 0.04f, 0.55f), clothMaterial);

            FlagRaiser raiser = flagObject.AddComponent<FlagRaiser>();
            var serialized = new SerializedObject(raiser);
            serialized.FindProperty("useTargetLocalZ").boolValue = true;
            serialized.FindProperty("targetLocalZ").floatValue = 0f;
            serialized.FindProperty("targetLocalEuler").vector3Value = Vector3.zero;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return raiser;
        }

        private static void CreatePart(string partName, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (material != null)
                part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static int SetupPurpleTargets(Scene scene)
        {
            int count = 0;
            Material activated = null;
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>(true))
            {
                if (r.gameObject.scene != scene || IsExcluded(r.transform)) continue;

                Material placeholder = FindMaterialInHueRange(r, PurpleHueMin, PurpleHueMax);
                MaterialSwapTarget target = r.GetComponent<MaterialSwapTarget>();
                if (target == null && placeholder == null) continue;

                if (target == null)
                {
                    target = Undo.AddComponent<MaterialSwapTarget>(r.gameObject);
                    BindReference(target, "placeholderMaterial", placeholder);
                }

                if (target.TargetMaterial == null)
                {
                    if (activated == null) activated = GetOrCreateActivatedMaterial();
                    BindReference(target, "targetMaterial", activated);
                }
                count++;
            }
            return count;
        }

        // Used only when a purple object has no Target Material yet; replace it in the inspector
        private static Material GetOrCreateActivatedMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ActivatedMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            Color color = new Color(0.2f, 0.8f, 0.35f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            AssetDatabase.CreateAsset(material, ActivatedMaterialPath);
            Debug.Log("[Runner] Created " + ActivatedMaterialPath + " as the default Target Material.");
            return material;
        }

        private static void SetupCheckpointManager(PlayerController player)
        {
            CheckpointManager manager = Object.FindObjectOfType<CheckpointManager>(true);
            if (manager == null)
            {
                var go = new GameObject("Checkpoints");
                Undo.RegisterCreatedObjectUndo(go, "Create Checkpoints");
                manager = go.AddComponent<CheckpointManager>();
            }
            BindReference(manager, "player", player);
        }

        #endregion

        #region Doors

        private static List<Door> SetupDoors(Scene scene, PlayerController player, Transform[] path)
        {
            var doors = new List<Door>(Object.FindObjectsOfType<Door>(true));
            doors.RemoveAll(d => d.gameObject.scene != scene);

            if (doors.Count == 0)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    CollectDoorModels(root.transform, doors);
            }

            Vector3 origin = player != null ? player.transform.position : Vector3.zero;
            doors.Sort((a, b) => Order(path, origin, a.transform.position).CompareTo(Order(path, origin, b.transform.position)));
            return doors;
        }

        // Outermost prefab instances of a model with "Door" in the asset name
        private static void CollectDoorModels(Transform t, List<Door> doors)
        {
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && t.GetComponentInChildren<Renderer>(true) != null)
            {
                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                if (!string.IsNullOrEmpty(assetPath) &&
                    System.IO.Path.GetFileNameWithoutExtension(assetPath).Contains(DoorNameHint))
                {
                    doors.Add(Undo.AddComponent<Door>(t.gameObject));
                    return;
                }
            }

            foreach (Transform child in t)
                CollectDoorModels(child, doors);
        }

        private static FinishController SetupLevelEnd(List<Door> doors, PlayerController player)
        {
            DoorsController controller = Object.FindObjectOfType<DoorsController>(true);
            if (controller == null)
            {
                var go = new GameObject("LevelEnd");
                Undo.RegisterCreatedObjectUndo(go, "Create LevelEnd");
                if (doors.Count > 0) go.transform.position = doors[0].transform.position;
                controller = go.AddComponent<DoorsController>();
            }

            var serialized = new SerializedObject(controller);
            SerializedProperty list = serialized.FindProperty("doors");
            if (list.arraySize == 0 && doors.Count > 0)
            {
                list.arraySize = doors.Count;
                for (int i = 0; i < doors.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
                serialized.ApplyModifiedProperties();
            }
            BindReference(controller, "player", player);

            FinishController finish = Object.FindObjectOfType<FinishController>(true);
            if (finish == null)
                finish = Undo.AddComponent<FinishController>(controller.gameObject);

            BindReference(controller, "finish", finish);
            BindReference(finish, "player", player);
            BindReference(finish, "gameManager", Object.FindObjectOfType<GameManager>(true));
            BindReference(finish, "checkpoints", Object.FindObjectOfType<CheckpointManager>(true));
            return finish;
        }

        #endregion

        #region Finish panel

        private static void SetupFinishPanel(Scene scene, FinishController finish)
        {
            Canvas canvas = null;
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>(true))
            {
                if (c.gameObject.scene == scene && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas = c;
                    break;
                }
            }
            if (canvas == null)
            {
                Debug.LogWarning("[Runner] No overlay canvas found, run Tools/Runner/Setup Win Panel first. The finish panel was not created.");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Transform root = canvas.transform.Find(FinishPanelName);
            if (root == null)
                root = CreateFinishPanel(canvas.transform, font);

            FinishPanelView view = root.GetComponent<FinishPanelView>();
            if (view == null)
                view = Undo.AddComponent<FinishPanelView>(root.gameObject);

            BindReference(view, "titleText", FindComponent<TMP_Text>(root, "Title"));
            BindReference(view, "resultText", FindComponent<TMP_Text>(root, "Result"));
            // Panels built by an older version have only ContinueButton, the choice buttons are added to them
            if (root.Find("MultiplyButton") == null)
                CreateButton("MultiplyButton", root, font, "x2", ButtonColor, 64f, MainButtonPosition, MainButtonSize);
            if (root.Find("ClaimButton") == null)
                CreateButton("ClaimButton", root, font, "Забрать", SecondButtonColor, 46f, SecondButtonPosition, SecondButtonSize);

            BindReference(view, "multiplyButton", FindComponent<Button>(root, "MultiplyButton"));
            BindReference(view, "claimButton", FindComponent<Button>(root, "ClaimButton"));
            BindReference(view, "continueButton", FindComponent<Button>(root, "ContinueButton"));
            BindReference(finish, "finishPanel", view);
        }

        private static Transform CreateFinishPanel(Transform parent, TMP_FontAsset font)
        {
            RectTransform root = CreateUIObject(FinishPanelName, parent);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = DimColor;

            CreateText("Title", root, font, 120f, new Vector2(0f, 360f), new Vector2(1000f, 180f), "Финиш");
            CreateText("Result", root, font, 60f, new Vector2(0f, 60f), new Vector2(1000f, 300f), "Пройдено дверей: 0 из 0\nДеньги: 0");

            CreateButton("ContinueButton", root, font, "Далее", ButtonColor, 64f, MainButtonPosition, MainButtonSize);

            root.gameObject.SetActive(false);
            return root;
        }

        private static void CreateButton(string objectName, Transform parent, TMP_FontAsset font, string text,
            Color color, float fontSize, Vector2 position, Vector2 size)
        {
            RectTransform button = CreateUIObject(objectName, parent);
            SetCentered(button, position, size);
            Image image = button.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltInSprite);
            image.type = Image.Type.Sliced;
            image.color = color;
            button.gameObject.AddComponent<Button>().targetGraphic = image;

            RectTransform label = CreateUIObject("Label", button);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            ConfigureText(label.gameObject.AddComponent<TextMeshProUGUI>(), font, fontSize, text);
        }

        private static void CreateText(string objectName, Transform parent, TMP_FontAsset font, float size,
            Vector2 position, Vector2 rectSize, string value)
        {
            RectTransform rect = CreateUIObject(objectName, parent);
            SetCentered(rect, position, rectSize);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(text, font, size, value);
            text.enableWordWrapping = true;
        }

        private static RectTransform CreateUIObject(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
            go.layer = LayerMask.NameToLayer(UiLayerName);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void ConfigureText(TextMeshProUGUI text, TMP_FontAsset font, float size, string value)
        {
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = value;
        }

        #endregion

        #region Helpers

        // Pickups, the player, doors and the flags created here are never zones or placeholders
        private static bool IsExcluded(Transform t)
        {
            return t.GetComponentInParent<Collectible>(true) != null
                || t.GetComponentInParent<Door>(true) != null
                || t.GetComponentInParent<PlayerController>(true) != null
                || t.GetComponentInParent<FlagRaiser>(true) != null
                || t.GetComponentInParent<Canvas>(true) != null;
        }

        private static float Order(Transform[] path, Vector3 origin, Vector3 position)
        {
            return PathUtility.IsValid(path) ? PathUtility.GetProgress(path, position) : Vector3.Distance(origin, position);
        }

        private static bool HasMaterialInHueRange(Renderer r, float hueMin, float hueMax)
        {
            return FindMaterialInHueRange(r, hueMin, hueMax) != null;
        }

        private static Material FindMaterialInHueRange(Renderer r, float hueMin, float hueMax)
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;

                Color color;
                if (m.HasProperty("_BaseColor")) color = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color")) color = m.GetColor("_Color");
                else continue;

                Color.RGBToHSV(color, out float h, out float s, out float v);
                if (h >= hueMin && h <= hueMax && s >= MinSaturation && v >= MinValue)
                    return m;
            }
            return null;
        }

        private static T FindComponent<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        // An existing reference is never replaced, it may have been assigned by hand
        private static void BindReference(Object owner, string field, Object value)
        {
            if (owner == null || value == null) return;

            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError("[Runner] Field '" + field + "' not found on " + owner.GetType().Name + ".");
                return;
            }

            if (property.objectReferenceValue != null) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        #endregion
    }
}
