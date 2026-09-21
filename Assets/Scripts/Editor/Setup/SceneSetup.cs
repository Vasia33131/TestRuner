using ButchersGames.Cameras;
using ButchersGames.Core;
using ButchersGames.Levels;
using ButchersGames.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ButchersGames.EditorTools
{
    /// <summary>
    /// Prepares the active scene and Level_1.prefab for gameplay.
    /// Idempotent: running it again does not duplicate anything and keeps hand-edited waypoints.
    /// Scene changes support Undo; changes to the Level_1 prefab asset are saved immediately and are not undoable.
    /// </summary>
    public static class SceneSetup
    {
        private const string LevelPrefabPath = "Assets/Project/Level_1.prefab";
        private const string PlayerPrefabPath = "Assets/Visual/Mesh/LowPoly/player.prefab";

        private const string RoadObjectName = "ljhjuf";
        private const string RoadAssetFileName = "ljhjuf.fbx";
        private const string SpawnPointName = "PlayerSpawnPoint";
        private const string WaypointsRootName = "Waypoints";
        private const string WaypointNamePrefix = "WP_";

        private static readonly Vector2 CanvasReferenceResolution = new Vector2(1080f, 1920f);
        private const float CanvasMatchWidthOrHeight = 0.5f;

        // How far a waypoint may be outside of the road bounds before a warning is logged
        private const float RoadBoundsTolerance = 1f;

        // Road centerline as (x, z) in the local space of the ljhjuf road object.
        // Taken from the NurbsPath stored in ljhjuf.fbx; the last leg is extended
        // to the end of the road mesh. Meant to be adjusted by hand afterwards.
        private static readonly Vector2[] RoadCenterline =
        {
            new Vector2(0f, 0f),
            new Vector2(6.2f, 0f),
            new Vector2(12.1f, 0f),
            new Vector2(18.6f, -0.1f),
            new Vector2(22.5f, -2.9f),
            new Vector2(22.5f, -9.2f),
            new Vector2(22.5f, -15f),
            new Vector2(25.4f, -18.8f),
            new Vector2(31.4f, -18.8f),
            new Vector2(37.8f, -18.8f),
            new Vector2(42.8f, -20.3f),
            new Vector2(43.3f, -27.2f),
            new Vector2(43.3f, -40f),
            new Vector2(43.3f, -53f),
            new Vector2(43.3f, -66f),
        };

        [MenuItem("Tools/Runner/Setup Scene")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Runner] Setup Scene cannot run in Play mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Runner] No active scene loaded.");
                return;
            }

            // The Level prefab instance in the scene is refreshed when the asset is saved,
            // so nothing from it may be open in the Inspector at that moment
            Selection.activeObject = null;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Runner: Setup Scene");
            int undoGroup = Undo.GetCurrentGroup();

            FixTagsAndLayers.Apply();
            SetupLevelPrefab();

            EnsureGameManager();
            EnsureCanvasAndEventSystem();
            EnsurePlayer(scene);
            EnsureCameraFollow();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Runner] Scene '" + scene.name + "' is set up. Save the scene (Ctrl+S).");
        }

        #region Level prefab

        private static void SetupLevelPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LevelPrefabPath);
            try
            {
                Level level = root.GetComponent<Level>();
                if (level == null)
                {
                    Debug.LogError("[Runner] " + LevelPrefabPath + " has no Level component.");
                    return;
                }

                bool changed = FixSpawnPointName(root.transform);

                Transform waypointsRoot = root.transform.Find(WaypointsRootName);
                if (waypointsRoot == null)
                {
                    var container = new GameObject(WaypointsRootName);
                    waypointsRoot = container.transform;
                    waypointsRoot.SetParent(root.transform, false);
                    changed = true;
                }

                // Never overwrite points that already exist, they may have been adjusted by hand
                if (waypointsRoot.childCount == 0)
                    changed |= CreateWaypoints(root.transform, waypointsRoot, level);

                changed |= BindWaypoints(level, waypointsRoot);

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, LevelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool FixSpawnPointName(Transform levelRoot)
        {
            bool changed = false;
            foreach (Transform child in levelRoot)
            {
                string trimmed = child.name.Trim();
                if (trimmed == SpawnPointName && child.name != trimmed)
                {
                    child.name = trimmed;
                    changed = true;
                }
            }
            return changed;
        }

        private static bool CreateWaypoints(Transform levelRoot, Transform waypointsRoot, Level level)
        {
            Transform road = FindRoad(levelRoot);
            if (road == null)
            {
                Debug.LogError("[Runner] Road object '" + RoadObjectName + "' not found in " + LevelPrefabPath + ", Waypoints left empty.");
                return false;
            }

            // Keep the height chosen for the spawn point so the player stands where it was designed to
            float height = level.PlayerSpawnPoint != null
                ? levelRoot.InverseTransformPoint(level.PlayerSpawnPoint.position).y
                : levelRoot.InverseTransformPoint(road.position).y;

            Bounds roadBounds = GetRendererBounds(road);

            for (int i = 0; i < RoadCenterline.Length; i++)
            {
                Vector3 world = road.TransformPoint(new Vector3(RoadCenterline[i].x, 0f, RoadCenterline[i].y));
                Vector3 local = levelRoot.InverseTransformPoint(world);
                local.y = height;

                var point = new GameObject(WaypointNamePrefix + i.ToString("00")).transform;
                point.SetParent(waypointsRoot, false);
                point.localPosition = local;

                if (roadBounds.size != Vector3.zero && !IsInsideRoadBounds(roadBounds, world))
                    Debug.LogWarning("[Runner] Waypoint " + point.name + " is outside of the road bounds, please check it.");
            }

            return true;
        }

        private static bool IsInsideRoadBounds(Bounds bounds, Vector3 world)
        {
            bounds.Expand(RoadBoundsTolerance * 2f);
            return world.x >= bounds.min.x && world.x <= bounds.max.x
                && world.z >= bounds.min.z && world.z <= bounds.max.z;
        }

        private static Bounds GetRendererBounds(Transform road)
        {
            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (Renderer renderer in road.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        private static Transform FindRoad(Transform levelRoot)
        {
            Transform byName = levelRoot.Find(RoadObjectName);
            if (byName != null) return byName;

            foreach (Transform child in levelRoot)
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
                if (source == null) continue;

                string path = AssetDatabase.GetAssetPath(source);
                if (path.EndsWith(RoadAssetFileName)) return child;
            }
            return null;
        }

        private static bool BindWaypoints(Level level, Transform waypointsRoot)
        {
            var serializedLevel = new SerializedObject(level);
            SerializedProperty property = serializedLevel.FindProperty("waypoints");
            int count = waypointsRoot.childCount;

            bool same = property.arraySize == count;
            for (int i = 0; same && i < count; i++)
                same = property.GetArrayElementAtIndex(i).objectReferenceValue == waypointsRoot.GetChild(i);

            if (same) return false;

            property.arraySize = count;
            for (int i = 0; i < count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = waypointsRoot.GetChild(i);
            serializedLevel.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        #endregion

        #region Scene

        private static void EnsureGameManager()
        {
            LevelManager levelManager = Object.FindObjectOfType<LevelManager>(true);
            if (levelManager == null)
            {
                Debug.LogError("[Runner] LevelManager not found in the scene, GameManager was not added.");
                return;
            }

            GameManager gameManager = Object.FindObjectOfType<GameManager>(true);
            if (gameManager == null)
                gameManager = Undo.AddComponent<GameManager>(levelManager.gameObject);

            if (gameManager.LevelManager != levelManager)
            {
                Undo.RecordObject(gameManager, "Assign LevelManager");
                gameManager.LevelManager = levelManager;
                EditorUtility.SetDirty(gameManager);
            }
        }

        private static void EnsureCanvasAndEventSystem()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>(true);
            if (canvas == null)
            {
                var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
                canvasObject.layer = LayerMask.NameToLayer("UI");
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);

            Undo.RecordObject(scaler, "Configure Canvas Scaler");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CanvasReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CanvasMatchWidthOrHeight;
            EditorUtility.SetDirty(scaler);

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);

            if (Object.FindObjectOfType<EventSystem>(true) == null)
            {
                // Legacy Input Manager module, the Input System package is not used
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }
        }

        private static void EnsurePlayer(Scene scene)
        {
            int playerLayer = LayerMask.NameToLayer(FixTagsAndLayers.PlayerLayerName);
            if (playerLayer < 0)
            {
                Debug.LogError("[Runner] Layer '" + FixTagsAndLayers.PlayerLayerName + "' does not exist, player was not set up.");
                return;
            }

            GameObject player;
            PlayerController existing = Object.FindObjectOfType<PlayerController>(true);
            if (existing != null)
            {
                player = existing.gameObject;
            }
            else
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError("[Runner] Player prefab not found at " + PlayerPrefabPath);
                    return;
                }

                player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(player, "Create Player");

                Transform spawn = FindSpawnPoint();
                if (spawn != null)
                    player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                else
                    Debug.LogWarning("[Runner] PlayerSpawnPoint not found, player left at the origin.");
            }

            ConfigurePlayer(player, playerLayer);
        }

        private static void ConfigurePlayer(GameObject player, int playerLayer)
        {
            Undo.RecordObject(player, "Configure Player");
            player.tag = FixTagsAndLayers.PlayerTag;
            player.layer = playerLayer;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);

            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body == null)
                body = Undo.AddComponent<Rigidbody>(player);
            Undo.RecordObject(body, "Configure Player Rigidbody");
            body.isKinematic = true;
            body.useGravity = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);

            CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = Undo.AddComponent<CapsuleCollider>(player);
            Undo.RecordObject(capsule, "Configure Player Collider");
            capsule.isTrigger = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
        }

        private static Transform FindSpawnPoint()
        {
            LevelManager levelManager = Object.FindObjectOfType<LevelManager>(true);
            Level level = levelManager != null ? levelManager.ActiveLevel : null;
            if (level == null)
                level = Object.FindObjectOfType<Level>(true);
            if (level != null && level.PlayerSpawnPoint != null)
                return level.PlayerSpawnPoint;

            // Fallback: spawn point of the prefab asset itself
            GameObject levelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelPrefabPath);
            Level prefabLevel = levelPrefab != null ? levelPrefab.GetComponent<Level>() : null;
            return prefabLevel != null ? prefabLevel.PlayerSpawnPoint : null;
        }

        private static void EnsureCameraFollow()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[Runner] Main Camera not found, CameraFollow was not added.");
                return;
            }

            // The target is left empty on purpose, CameraFollow finds the player by the Player tag
            if (camera.GetComponent<CameraFollow>() == null)
                Undo.AddComponent<CameraFollow>(camera.gameObject);
        }

        #endregion
    }
}
