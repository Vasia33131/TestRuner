using ButchersGames.Gameplay.Collectibles;
using ButchersGames.Gameplay.Economy;
using ButchersGames.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButchersGames.EditorTools
{
    /// <summary>
    /// Prepares pickups (bills and bottles), the Managers object and the WealthConfig asset.
    /// Idempotent: running it again adds nothing twice and keeps hand-tuned collider sizes.
    /// Scene changes support Undo; the WealthConfig asset is created once and is not undoable.
    /// </summary>
    public static class CollectiblesSetup
    {
        private const string BillsContainerName = "bills";
        private const string BottlesContainerName = "bottle";
        private const string ManagersName = "Managers";
        private const string PickupTag = "Pickup";
        private const string ConfigFolder = "Assets/Project";
        private const string ConfigPath = ConfigFolder + "/WealthConfig.asset";

        private const int ExpectedBills = 68;
        private const int ExpectedBottles = 18;

        // Colliders are never thinner than this in world units, flat bills would be hard to hit otherwise
        private const float MinColliderWorldSize = 0.3f;

        [MenuItem("Tools/Runner/Setup Collectibles")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Runner] Setup Collectibles cannot run in Play mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Runner] No active scene loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Runner: Setup Collectibles");
            int undoGroup = Undo.GetCurrentGroup();

            // Makes sure the Pickup tag, the Collectible layer and the collision matrix exist
            FixTagsAndLayers.Apply();

            int collectibleLayer = LayerMask.NameToLayer(FixTagsAndLayers.CollectibleLayerName);
            if (collectibleLayer < 0)
            {
                Debug.LogError("[Runner] Layer '" + FixTagsAndLayers.CollectibleLayerName + "' does not exist, pickups were not set up.");
            }
            else
            {
                SetupContainer<MoneyPickup>(scene, BillsContainerName, ExpectedBills, collectibleLayer);
                SetupContainer<BottlePickup>(scene, BottlesContainerName, ExpectedBottles, collectibleLayer);
            }

            EnsurePlayerCanTrigger();
            EnsureManagers();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Runner] Collectibles are set up. Save the scene (Ctrl+S).");
        }

        #region Pickups

        private static void SetupContainer<T>(Scene scene, string containerName, int expectedCount, int layer) where T : Collectible
        {
            Transform container = FindRootByName(scene, containerName);
            if (container == null)
            {
                Debug.LogError("[Runner] Container '" + containerName + "' not found in the scene root.");
                return;
            }

            int processed = 0;
            foreach (Transform child in container)
            {
                if (SetupPickup<T>(child.gameObject, layer)) processed++;
            }

            if (container.childCount != expectedCount)
                Debug.LogWarning("[Runner] '" + containerName + "' has " + container.childCount + " children, expected " + expectedCount + ".");
            Debug.Log("[Runner] '" + containerName + "': " + processed + "/" + container.childCount + " pickups ready (" + typeof(T).Name + ").");
        }

        private static Transform FindRootByName(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == objectName) return root.transform;
            }
            return null;
        }

        private static bool SetupPickup<T>(GameObject item, int layer) where T : Collectible
        {
            if (!TryGetLocalBounds(item.transform, out Bounds localBounds))
            {
                Debug.LogWarning("[Runner] '" + item.name + "' has no Renderer, skipped.", item);
                return false;
            }

            if (item.tag != PickupTag || item.layer != layer)
            {
                Undo.RecordObject(item, "Set Pickup Tag And Layer");
                item.tag = PickupTag;
                item.layer = layer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }

            BoxCollider box = item.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(item);
                FitCollider(box, item.transform, localBounds);
            }

            if (!box.isTrigger)
            {
                Undo.RecordObject(box, "Make Collider Trigger");
                box.isTrigger = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(box);
            }

            if (item.GetComponent<T>() == null)
            {
                if (item.GetComponent<Collectible>() != null)
                    Debug.LogWarning("[Runner] '" + item.name + "' already has a different pickup script.", item);
                else
                    Undo.AddComponent<T>(item);
            }

            return true;
        }

        private static void FitCollider(BoxCollider box, Transform item, Bounds localBounds)
        {
            Vector3 size = localBounds.size;
            Vector3 scale = item.lossyScale;
            for (int i = 0; i < 3; i++)
                size[i] = Mathf.Max(size[i], MinColliderWorldSize / Mathf.Max(Mathf.Abs(scale[i]), 1e-4f));

            Undo.RecordObject(box, "Fit Pickup Collider");
            box.center = localBounds.center;
            box.size = size;
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        }

        /// <summary>
        /// Bounds of all renderers in the local space of the root, so a rotated object
        /// gets a tight box instead of a loose world-space AABB.
        /// </summary>
        private static bool TryGetLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasBounds = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds source;
                Transform space;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (renderer is MeshRenderer && filter != null && filter.sharedMesh != null)
                {
                    source = filter.sharedMesh.bounds;
                    space = renderer.transform;
                }
                else
                {
                    source = renderer.bounds;
                    space = null;
                }

                Vector3 min = source.min;
                Vector3 max = source.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);

                    Vector3 world = space != null ? space.TransformPoint(point) : point;
                    Vector3 local = root.InverseTransformPoint(world);

                    if (!hasBounds)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }

            return hasBounds;
        }

        #endregion

        #region Player

        private static void EnsurePlayerCanTrigger()
        {
            PlayerController player = Object.FindObjectOfType<PlayerController>(true);
            if (player == null)
            {
                Debug.LogWarning("[Runner] Player not found in the scene, run Tools/Runner/Setup Scene first. Pickups will not trigger without it.");
                return;
            }

            GameObject go = player.gameObject;

            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body == null)
                body = Undo.AddComponent<Rigidbody>(go);

            if (!body.isKinematic || body.useGravity)
            {
                Undo.RecordObject(body, "Make Player Rigidbody Kinematic");
                body.isKinematic = true;
                body.useGravity = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            }

            // The Collectible layer only collides with the Player layer
            int playerLayer = LayerMask.NameToLayer(FixTagsAndLayers.PlayerLayerName);
            bool wrongLayer = playerLayer >= 0 && go.layer != playerLayer;
            bool wrongTag = !go.CompareTag(FixTagsAndLayers.PlayerTag);
            if (wrongLayer || wrongTag)
            {
                Undo.RecordObject(go, "Configure Player");
                if (wrongLayer) go.layer = playerLayer;
                if (wrongTag) go.tag = FixTagsAndLayers.PlayerTag;
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }

            if (go.GetComponentInChildren<Collider>() == null)
                Debug.LogWarning("[Runner] Player has no Collider, pickups will not trigger.", go);
        }

        #endregion

        #region Managers

        private static void EnsureManagers()
        {
            ScoreManager score = Object.FindObjectOfType<ScoreManager>(true);
            WealthTracker tracker = Object.FindObjectOfType<WealthTracker>(true);

            GameObject managers;
            if (score != null) managers = score.gameObject;
            else if (tracker != null) managers = tracker.gameObject;
            else
            {
                managers = new GameObject(ManagersName);
                Undo.RegisterCreatedObjectUndo(managers, "Create Managers");
            }

            if (score == null)
                Undo.AddComponent<ScoreManager>(managers);
            if (tracker == null)
                tracker = Undo.AddComponent<WealthTracker>(managers);

            WealthConfig config = GetOrCreateConfig();
            BindTracker(tracker, config);
        }

        private static WealthConfig GetOrCreateConfig()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:WealthConfig"))
            {
                var existing = AssetDatabase.LoadAssetAtPath<WealthConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing != null) return existing;
            }

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder("Assets", "Project");

            var config = ScriptableObject.CreateInstance<WealthConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Runner] Created " + ConfigPath);
            return config;
        }

        private static void BindTracker(WealthTracker tracker, WealthConfig config)
        {
            var serialized = new SerializedObject(tracker);

            // Existing references are never replaced, they may have been assigned by hand
            SerializedProperty configProperty = serialized.FindProperty("config");
            if (configProperty.objectReferenceValue == null)
                configProperty.objectReferenceValue = config;

            SerializedProperty appearanceProperty = serialized.FindProperty("appearance");
            if (appearanceProperty.objectReferenceValue == null)
            {
                PlayerAppearance appearance = Object.FindObjectOfType<PlayerAppearance>(true);
                if (appearance != null)
                    appearanceProperty.objectReferenceValue = appearance;
                else
                    Debug.LogWarning("[Runner] PlayerAppearance not found, the tracker will look for it at runtime.");
            }

            if (serialized.hasModifiedProperties)
                serialized.ApplyModifiedProperties();
        }

        #endregion
    }
}
