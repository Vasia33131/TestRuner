using ButchersGames.Feedback;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ButchersGames.EditorTools
{
    /// <summary>
    /// Builds the pickup feedback: particle prefabs, the floating text prefab, the particle material
    /// and the "Feedback" scene object (AudioManager + PickupFeedback) with clips bound.
    /// Idempotent: existing assets and objects are reused and never overwritten, only empty
    /// references are filled. Scene changes support Undo; created assets do not.
    /// </summary>
    public static class FeedbackSetup
    {
        private const string FeedbackFolder = "Assets/Project/Feedback";
        private const string MaterialPath = FeedbackFolder + "/FeedbackParticle.mat";
        private const string CoinBurstPath = FeedbackFolder + "/CoinBurst.prefab";
        private const string BottleBurstPath = FeedbackFolder + "/BottleBurst.prefab";
        private const string FloatingTextPath = FeedbackFolder + "/FloatingText.prefab";

        private const string CoinClipPath = "Assets/Sounds/AudioClip/collect_coin.ogg";
        private const string BottleClipPath = "Assets/Sounds/AudioClip/RemoveMoney.ogg";
        private const string StatusUpClipPath = "Assets/Sounds/AudioClip/shortcutrun_sfx_joueur_bonus_multiplier_x01_to_x05_variation01.ogg";

        // Created by Tools/Runner/Setup HUD, the default TMP font is used when it is missing
        private const string FontPath = "Assets/Visual/Fonts/Inter-SemiBold SDF.asset";

        private const string FeedbackObjectName = "Feedback";

        private static readonly Color GainColor = new Color(0.25f, 1f, 0.35f, 1f);
        private static readonly Color LossColor = new Color(1f, 0.25f, 0.25f, 1f);

        // World-space TMP: font size 10 is about one world unit per em
        private const float TextFontSize = 7f;
        private static readonly Vector2 TextRectSize = new Vector2(6f, 2f);

        private const int CoinBurstCount = 16;
        private const int BottleBurstCount = 22;
        private const float CoinBurstSpeed = 4f;
        private const float BottleBurstSpeed = 5f;

        [MenuItem("Tools/Runner/Setup Feedback")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Runner] Setup Feedback cannot run in Play mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Runner] No active scene loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Runner: Setup Feedback");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureFolder(FeedbackFolder);
            Material material = GetOrCreateMaterial();
            ParticleSystem coinBurst = GetOrCreateBurst(CoinBurstPath, "CoinBurst", GainColor, CoinBurstCount, CoinBurstSpeed, material);
            ParticleSystem bottleBurst = GetOrCreateBurst(BottleBurstPath, "BottleBurst", LossColor, BottleBurstCount, BottleBurstSpeed, material);
            FloatingText floatingText = GetOrCreateFloatingText();

            EnsureSceneObject(coinBurst, bottleBurst, floatingText);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Runner] Feedback is set up. Save the scene (Ctrl+S).");
        }

        #region Assets

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static Material GetOrCreateMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            bool urp = shader != null;
            if (!urp)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                Debug.LogWarning("[Runner] URP particle shader not found, the legacy one is used.");
            }

            var material = new Material(shader) { name = "FeedbackParticle" };
            var texture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");

            if (urp)
            {
                // Same state the URP material inspector sets for Surface Type = Transparent, Blend = Alpha
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                if (texture != null) material.SetTexture("_BaseMap", texture);
            }
            else if (texture != null)
            {
                material.mainTexture = texture;
            }

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static ParticleSystem GetOrCreateBurst(string path, string objectName, Color color, int count, float speed, Material material)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<ParticleSystem>();

            var root = new GameObject(objectName, typeof(ParticleSystem));
            ParticleSystem system = root.GetComponent<ParticleSystem>();
            ConfigureBurst(system, color, count, speed);
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ParticleSystem>();
        }

        private static void ConfigureBurst(ParticleSystem system, Color color, int count, float speed)
        {
            ParticleSystem.MainModule main = system.main;
            main.duration = 0.8f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = color;
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = fade;
        }

        private static FloatingText GetOrCreateFloatingText()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FloatingTextPath);
            if (existing != null) return existing.GetComponent<FloatingText>();

            var root = new GameObject("FloatingText", typeof(RectTransform));
            TextMeshPro text = root.AddComponent<TextMeshPro>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) text.font = font;
            text.rectTransform.sizeDelta = TextRectSize;
            text.fontSize = TextFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = GainColor;
            text.text = "+10 $";
            root.AddComponent<FloatingText>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, FloatingTextPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<FloatingText>();
        }

        #endregion

        #region Scene

        private static void EnsureSceneObject(ParticleSystem coinBurst, ParticleSystem bottleBurst, FloatingText floatingText)
        {
            PickupFeedback feedback = Object.FindObjectOfType<PickupFeedback>(true);
            if (feedback == null)
            {
                var go = new GameObject(FeedbackObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Create Feedback");
                feedback = Undo.AddComponent<PickupFeedback>(go);
            }

            if (Object.FindObjectOfType<AudioManager>(true) == null)
                Undo.AddComponent<AudioManager>(feedback.gameObject);

            BindReference(feedback, "floatingTextPrefab", floatingText);
            BindReference(feedback, "coinBurstPrefab", coinBurst);
            BindReference(feedback, "bottleBurstPrefab", bottleBurst);
            BindClip(feedback, "coinClip", CoinClipPath);
            BindClip(feedback, "bottleClip", BottleClipPath);
            BindClip(feedback, "statusUpClip", StatusUpClipPath);

            if (Object.FindObjectOfType<AudioListener>(true) == null)
                Debug.LogWarning("[Runner] No AudioListener in the scene, sounds will not be heard.");
        }

        private static void BindClip(Object owner, string field, string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning("[Runner] Audio clip not found: " + path);
                return;
            }
            BindReference(owner, field, clip);
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
