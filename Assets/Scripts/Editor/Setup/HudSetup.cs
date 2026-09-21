using System.IO;
using ButchersGames.Gameplay.Economy;
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
    /// Builds the HUD: the money counter on the overlay canvas and the status bar above the player.
    /// Idempotent: running it again creates nothing twice, keeps hand-edited layout and only fills
    /// empty references and start values. Scene changes support Undo; the TMP font asset is created
    /// once and is not undoable.
    /// </summary>
    public static class HudSetup
    {
        private const string FontFolder = "Assets/Visual/Fonts";

        // Tried in order, the first one that contains the whole Latin + Cyrillic set is used
        private static readonly string[] SourceFontPaths =
        {
            FontFolder + "/Inter-SemiBold.ttf",
            FontFolder + "/LiberationSans.ttf",
        };

        // Basic Latin, Cyrillic capitals and small letters, Yo
        private static readonly string GlyphSet = BuildGlyphSet();

        private const int FontSamplingSize = 64;
        private const int FontPadding = 8;
        private const int FontAtlasSize = 2048;

        private const string CanvasName = "Canvas";
        private const string MoneyCounterName = "MoneyCounter";
        private const string StatusBarName = "StatusBar";

        private static readonly Vector2 CanvasReferenceResolution = new Vector2(1080f, 1920f);
        private const float CanvasMatchWidthOrHeight = 0.5f;

        // Money counter layout (canvas units), anchored to the top-right corner
        private static readonly Vector2 MoneyCounterSize = new Vector2(380f, 110f);
        private static readonly Vector2 MoneyCounterMargin = new Vector2(40f, 80f);
        private const float MoneyIconSize = 76f;
        private const float MoneyIconLeft = 20f;
        private const float MoneyTextLeft = 110f;
        private const float MoneyTextRight = 24f;

        // Status bar layout (canvas units) and world size
        private static readonly Vector2 StatusBarSize = new Vector2(400f, 110f);
        private const float StatusBarWorldScale = 0.004f;
        private const float StatusNameHeight = 64f;
        private const float StatusBarHeight = 30f;
        private const float StatusBarInset = 20f;
        private const float StatusBarBottom = 8f;
        private const float StatusFillPadding = 4f;
        private const float HeadMargin = 0.3f;
        private const float FallbackHeadHeight = 2f;

        private const string DefaultPoorName = "Бедный";

        private const string UiLayerName = "UI";
        private const string BuiltInSprite = "UI/Skin/UISprite.psd";

        [MenuItem("Tools/Runner/Setup HUD")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Runner] Setup HUD cannot run in Play mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Runner] No active scene loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Runner: Setup HUD");
            int undoGroup = Undo.GetCurrentGroup();

            TMP_FontAsset font = GetOrCreateFont();
            WealthConfig config = FindConfig();
            if (config == null)
                Debug.LogWarning("[Runner] WealthConfig not found, run Tools/Runner/Setup Collectibles. The default status name is used.");

            Canvas canvas = EnsureOverlayCanvas(scene);
            MoneyCounterView counter = EnsureMoneyCounter(canvas, font);
            EnsureUIManager(canvas, counter);
            EnsureStatusBar(font, config);

            if (Object.FindObjectOfType<ScoreManager>(true) == null || Object.FindObjectOfType<WealthTracker>(true) == null)
                Debug.LogWarning("[Runner] ScoreManager / WealthTracker not found in the scene, run Tools/Runner/Setup Collectibles. The HUD will stay empty without them.");

            Undo.CollapseUndoOperations(undoGroup);
            BrokenSpritesFix.Run();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Runner] HUD is set up. Save the scene (Ctrl+S).");
        }

        #region Font

        private static string BuildGlyphSet()
        {
            var builder = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++) builder.Append(c);
            for (char c = 'А'; c <= 'я'; c++) builder.Append(c);
            builder.Append('Ё').Append('ё');
            return builder.ToString();
        }

        /// <summary>
        /// NotoSansCJK-Bold SDF.asset is a static atlas without Cyrillic, so a dynamic
        /// Latin + Cyrillic font asset is created from Inter (LiberationSans as a fallback).
        /// </summary>
        private static TMP_FontAsset GetOrCreateFont()
        {
            foreach (string sourcePath in SourceFontPaths)
            {
                var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GetFontAssetPath(sourcePath));
                if (existing != null) return existing;
            }

            foreach (string sourcePath in SourceFontPaths)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
                if (source == null) continue;

                TMP_FontAsset created = CreateFontAsset(source, GetFontAssetPath(sourcePath));
                if (created != null) return created;
            }

            Debug.LogError("[Runner] Could not create a Cyrillic TMP font, texts will use the default font and may miss letters.");
            return null;
        }

        private static string GetFontAssetPath(string sourcePath)
        {
            return FontFolder + "/" + Path.GetFileNameWithoutExtension(sourcePath) + " SDF.asset";
        }

        private static TMP_FontAsset CreateFontAsset(Font source, string assetPath)
        {
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                source, FontSamplingSize, FontPadding, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                FontAtlasSize, FontAtlasSize, AtlasPopulationMode.Dynamic, true);
            if (asset == null) return null;

            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, assetPath);

            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.atlasTexture.name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);

            asset.TryAddCharacters(GlyphSet, out string missing);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning("[Runner] " + source.name + " misses " + missing.Length + " characters, trying the next font.");
                AssetDatabase.DeleteAsset(assetPath);
                return null;
            }

            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.material);
            foreach (Texture2D atlas in asset.atlasTextures)
                EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();

            Debug.Log("[Runner] Created " + assetPath);
            return asset;
        }

        #endregion

        #region Canvas and money counter

        private static Canvas EnsureOverlayCanvas(Scene scene)
        {
            foreach (Canvas existing in Object.FindObjectsOfType<Canvas>(true))
            {
                // The world-space status bar under the player is also a root canvas, skip it
                if (existing.gameObject.scene == scene && existing.isRootCanvas && existing.renderMode == RenderMode.ScreenSpaceOverlay)
                    return existing;
            }

            var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
            canvasObject.layer = LayerMask.NameToLayer(UiLayerName);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = CanvasReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CanvasMatchWidthOrHeight;
            return canvas;
        }

        private static MoneyCounterView EnsureMoneyCounter(Canvas canvas, TMP_FontAsset font)
        {
            Transform root = canvas.transform.Find(MoneyCounterName);
            if (root == null)
                root = CreateMoneyCounter(canvas.transform, font);

            MoneyCounterView view = root.GetComponent<MoneyCounterView>();
            if (view == null)
                view = Undo.AddComponent<MoneyCounterView>(root.gameObject);

            Transform valueTransform = root.Find("Value");
            TMP_Text valueText = valueTransform != null ? valueTransform.GetComponent<TMP_Text>() : null;
            if (valueText == null)
            {
                Debug.LogWarning("[Runner] '" + MoneyCounterName + "/Value' text not found, the counter text was not bound.", root);
                return view;
            }

            BindReference(view, "valueText", valueText);
            BindReference(view, "punchTarget", root as RectTransform);

            SetText(valueText, "0");
            return view;
        }

        private static Transform CreateMoneyCounter(Transform parent, TMP_FontAsset font)
        {
            RectTransform root = CreateUIObject(MoneyCounterName, parent);
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = MoneyCounterSize;
            root.anchoredPosition = new Vector2(
                -(MoneyCounterMargin.x + MoneyCounterSize.x * 0.5f),
                -(MoneyCounterMargin.y + MoneyCounterSize.y * 0.5f));

            RectTransform background = CreateUIObject("Background", root);
            Stretch(background, Vector2.zero, Vector2.zero);
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.raycastTarget = false;
            Sprite panel = LoadSprite("Assets/Visual/Sprite/9grid_black_transparent.asset");
            if (panel != null)
            {
                backgroundImage.sprite = panel;
                backgroundImage.type = Image.Type.Sliced;
            }
            else
            {
                backgroundImage.color = new Color(0f, 0f, 0f, 0.55f);
            }

            RectTransform icon = CreateUIObject("Icon", root);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.sizeDelta = new Vector2(MoneyIconSize, MoneyIconSize);
            icon.anchoredPosition = new Vector2(MoneyIconLeft, 0f);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.sprite = LoadSprite(
                "Assets/Visual/Sprite/Dollar_Green.asset",
                "Assets/Visual/Sprite/gold_bills.asset",
                "Assets/Visual/Sprite/currency.asset");
            if (iconImage.sprite == null)
                Debug.LogWarning("[Runner] No bill icon sprite found, assign one to " + MoneyCounterName + "/Icon by hand.");

            RectTransform value = CreateUIObject("Value", root);
            Stretch(value, new Vector2(MoneyTextLeft, 8f), new Vector2(-MoneyTextRight, -8f));
            TextMeshProUGUI text = value.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(text, font, 64f, TextAlignmentOptions.MidlineRight);
            text.enableAutoSizing = true;
            text.fontSizeMin = 30f;
            text.fontSizeMax = 64f;

            return root;
        }

        private static void EnsureUIManager(Canvas canvas, MoneyCounterView counter)
        {
            UIManager manager = Object.FindObjectOfType<UIManager>(true);
            if (manager == null)
                manager = Undo.AddComponent<UIManager>(canvas.gameObject);

            // Start panel and the other references are left exactly as they are
            BindReference(manager, "moneyCounter", counter);
        }

        #endregion

        #region Status bar

        private static void EnsureStatusBar(TMP_FontAsset font, WealthConfig config)
        {
            PlayerController player = Object.FindObjectOfType<PlayerController>(true);
            if (player == null)
            {
                Debug.LogError("[Runner] Player not found in the scene, run Tools/Runner/Setup Scene first. The status bar was not created.");
                return;
            }

            Transform root = player.transform.Find(StatusBarName);
            if (root == null)
                root = CreateStatusBar(player.transform, font);

            StatusBarView view = root.GetComponent<StatusBarView>();
            if (view == null)
                view = Undo.AddComponent<StatusBarView>(root.gameObject);
            if (root.GetComponent<Billboard>() == null)
                Undo.AddComponent<Billboard>(root.gameObject);

            Transform nameTransform = root.Find("StatusName");
            Transform fillTransform = root.Find("BarBackground/Fill");
            TMP_Text nameText = nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null;
            Image fill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (nameText == null || fill == null)
            {
                Debug.LogWarning("[Runner] '" + StatusBarName + "' misses StatusName or BarBackground/Fill, references were not bound.", root);
                return;
            }

            BindReference(view, "statusText", nameText);
            BindReference(view, "fillImage", fill);
            BindReference(view, "tracker", Object.FindObjectOfType<WealthTracker>(true));

            // Start values: empty bar, first status
            SetText(nameText, config != null ? config.GetDisplayName(WealthStatus.Poor) : DefaultPoorName);
            var serializedView = new SerializedObject(view);
            SerializedProperty poorColor = serializedView.FindProperty("poorColor");

            Undo.RecordObject(fill, "Set Status Bar Start Values");
            fill.fillAmount = 0f;
            if (poorColor != null) fill.color = poorColor.colorValue;
            EditorUtility.SetDirty(fill);
        }

        private static Transform CreateStatusBar(Transform player, TMP_FontAsset font)
        {
            RectTransform root = CreateUIObject(StatusBarName, player);
            root.sizeDelta = StatusBarSize;
            root.pivot = new Vector2(0.5f, 0f);

            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // World size is fixed, so the parent scale must be cancelled out
            Vector3 parentScale = player.lossyScale;
            root.localScale = new Vector3(
                StatusBarWorldScale / SafeAbs(parentScale.x),
                StatusBarWorldScale / SafeAbs(parentScale.y),
                StatusBarWorldScale / SafeAbs(parentScale.z));
            root.position = GetHeadPosition(player);
            root.rotation = Quaternion.identity;

            RectTransform nameRect = CreateUIObject("StatusName", root);
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(0f, StatusNameHeight);
            nameRect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI nameText = nameRect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(nameText, font, 52f, TextAlignmentOptions.Center);

            Sprite barSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltInSprite);

            RectTransform background = CreateUIObject("BarBackground", root);
            background.anchorMin = new Vector2(0f, 0f);
            background.anchorMax = new Vector2(1f, 0f);
            background.pivot = new Vector2(0.5f, 0f);
            background.sizeDelta = new Vector2(-2f * StatusBarInset, StatusBarHeight);
            background.anchoredPosition = new Vector2(0f, StatusBarBottom);
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.sprite = barSprite;
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.color = new Color(0f, 0f, 0f, 0.6f);
            backgroundImage.raycastTarget = false;

            RectTransform fillRect = CreateUIObject("Fill", background);
            Stretch(fillRect, new Vector2(StatusFillPadding, StatusFillPadding), new Vector2(-StatusFillPadding, -StatusFillPadding));
            Image fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = barSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;

            return root;
        }

        /// <summary>Point above the head: the top of the player capsule plus a margin.</summary>
        private static Vector3 GetHeadPosition(Transform player)
        {
            CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
            float top;
            if (capsule != null && capsule.direction == 1)
            {
                Vector3 topLocal = capsule.center + Vector3.up * (capsule.height * 0.5f);
                top = player.TransformPoint(topLocal).y;
            }
            else
            {
                top = player.position.y + FallbackHeadHeight;
                Debug.LogWarning("[Runner] Player has no vertical CapsuleCollider, the status bar height is a guess, adjust it by hand.");
            }

            return new Vector3(player.position.x, top + HeadMargin, player.position.z);
        }

        private static float SafeAbs(float value)
        {
            return Mathf.Max(Mathf.Abs(value), 1e-4f);
        }

        #endregion

        #region Helpers

        private static WealthConfig FindConfig()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:WealthConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<WealthConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config != null) return config;
            }
            return null;
        }

        private static Sprite LoadSprite(params string[] paths)
        {
            foreach (string path in paths)
            {
                // The .asset sprites may reference a missing texture, their PNG copy is used then
                var sprite = BrokenSpritesFix.Resolve(AssetDatabase.LoadAssetAtPath<Sprite>(path));
                if (sprite != null && !BrokenSpritesFix.IsBroken(sprite)) return sprite;
            }
            return null;
        }

        private static RectTransform CreateUIObject(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
            go.layer = LayerMask.NameToLayer(UiLayerName);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void ConfigureText(TextMeshProUGUI text, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
        {
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text.text == value) return;
            Undo.RecordObject(text, "Set HUD Start Value");
            text.text = value;
            EditorUtility.SetDirty(text);
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
