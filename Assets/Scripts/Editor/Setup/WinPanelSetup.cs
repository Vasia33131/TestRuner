using ButchersGames.Core;
using ButchersGames.Player;
using ButchersGames.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ButchersGames.EditorTools
{
    /// <summary>
    /// Builds the win panel (title, status, reward, multiplier scale with an arrow, buttons) under the
    /// overlay canvas and binds it to UIManager / GameManager.
    /// Idempotent: an existing panel is kept as it is (hand-edited layout survives) and only empty
    /// references are filled. Scene changes support Undo; the build settings change does not.
    /// </summary>
    public static class WinPanelSetup
    {
        private const string PanelName = "WinPanel";
        private const string UiLayerName = "UI";
        private const string BuiltInSprite = "UI/Skin/UISprite.psd";
        private const string FontPath = "Assets/Visual/Fonts/Inter-SemiBold SDF.asset";

        private static readonly Vector2 CanvasReferenceResolution = new Vector2(1080f, 1920f);
        private const float CanvasMatchWidthOrHeight = 0.5f;

        // All positions are canvas units relative to the panel center
        private const float TitleY = 560f;
        private const float StatusY = 430f;
        private const float MoneyY = 250f;
        private const float ScaleY = 20f;
        private const float MainButtonY = -260f;
        private const float SecondButtonY = -440f;

        private static readonly Vector2 TitleSize = new Vector2(1000f, 140f);
        private static readonly Vector2 StatusSize = new Vector2(900f, 90f);
        private static readonly Vector2 MoneySize = new Vector2(900f, 170f);
        private static readonly Vector2 ScaleSize = new Vector2(900f, 130f);
        private static readonly Vector2 ArrowSize = new Vector2(56f, 56f);
        private const float ArrowGap = 22f;
        private static readonly Vector2 MainButtonSize = new Vector2(820f, 160f);
        private static readonly Vector2 SecondButtonSize = new Vector2(520f, 110f);

        private const float TitleFontSize = 76f;
        private const float StatusFontSize = 56f;
        private const float MoneyFontSize = 130f;
        private const float SegmentFontSize = 64f;
        private const float MainButtonFontSize = 64f;
        private const float SecondButtonFontSize = 46f;

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color MainButtonColor = new Color(0.27f, 0.78f, 0.28f, 1f);
        private static readonly Color SecondButtonColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        private static readonly Color ArrowColor = Color.white;
        private static readonly Color[] SegmentColors =
        {
            new Color(0.98f, 0.82f, 0.2f, 1f),
            new Color(0.98f, 0.62f, 0.15f, 1f),
            new Color(0.95f, 0.4f, 0.15f, 1f),
            new Color(0.85f, 0.2f, 0.2f, 1f),
        };
        private static readonly string[] SegmentLabels = { "x2", "x3", "x4", "x5" };

        [MenuItem("Tools/Runner/Setup Win Panel")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Runner] Setup Win Panel cannot run in Play mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Runner] No active scene loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Runner: Setup Win Panel");
            int undoGroup = Undo.GetCurrentGroup();

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogWarning("[Runner] Cyrillic font not found, run Tools/Runner/Setup HUD first. The default font may miss letters.");

            EnsureEventSystem();
            Canvas canvas = EnsureOverlayCanvas(scene);
            UIManager uiManager = EnsureUIManager(canvas);
            WinPanelView view = EnsureWinPanel(canvas, font);
            BindUIManager(uiManager, view);
            BindGameManager(uiManager);
            EnsureSceneInBuildSettings(scene);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Runner] Win panel is set up. Save the scene (Ctrl+S).");
        }

        #region Scene objects

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>(true) != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        private static Canvas EnsureOverlayCanvas(Scene scene)
        {
            foreach (Canvas existing in Object.FindObjectsOfType<Canvas>(true))
            {
                if (existing.gameObject.scene == scene && existing.isRootCanvas && existing.renderMode == RenderMode.ScreenSpaceOverlay)
                    return existing;
            }

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

        private static UIManager EnsureUIManager(Canvas canvas)
        {
            UIManager manager = Object.FindObjectOfType<UIManager>(true);
            if (manager == null)
                manager = Undo.AddComponent<UIManager>(canvas.gameObject);
            return manager;
        }

        private static void BindUIManager(UIManager uiManager, WinPanelView view)
        {
            if (view == null) return;
            BindReference(uiManager, "winPanel", view.gameObject);
            BindReference(uiManager, "winPanelView", view);
        }

        private static void BindGameManager(UIManager uiManager)
        {
            GameManager gameManager = Object.FindObjectOfType<GameManager>(true);
            if (gameManager == null)
            {
                Debug.LogWarning("[Runner] GameManager not found, run Tools/Runner/Setup Scene first.");
                return;
            }

            BindReference(gameManager, "uiManager", uiManager);
            BindReference(gameManager, "player", Object.FindObjectOfType<PlayerController>(true));
        }

        // Scene reload needs the scene in the build list. Not undoable.
        private static void EnsureSceneInBuildSettings(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("[Runner] The scene is not saved yet, save it and run Setup Win Panel again to add it to Build Settings.");
                return;
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene entry in scenes)
            {
                if (entry.path == scene.path)
                {
                    if (!entry.enabled)
                        Debug.LogWarning("[Runner] The scene is in Build Settings but disabled, enable it or the scene reload will fail.");
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            System.Array.Copy(scenes, updated, scenes.Length);
            updated[scenes.Length] = new EditorBuildSettingsScene(scene.path, true);
            EditorBuildSettings.scenes = updated;
            Debug.Log("[Runner] Added " + scene.path + " to Build Settings.");
        }

        #endregion

        #region Win panel

        private static WinPanelView EnsureWinPanel(Canvas canvas, TMP_FontAsset font)
        {
            Transform root = canvas.transform.Find(PanelName);
            if (root == null)
                root = CreateWinPanel(canvas.transform, font);

            WinPanelView view = root.GetComponent<WinPanelView>();
            if (view == null)
                view = Undo.AddComponent<WinPanelView>(root.gameObject);

            BindReference(view, "titleText", FindComponent<TMP_Text>(root, "Title"));
            BindReference(view, "statusText", FindComponent<TMP_Text>(root, "Status"));
            BindReference(view, "moneyText", FindComponent<TMP_Text>(root, "Money"));
            BindReference(view, "claimMultiplierLabel", FindComponent<TMP_Text>(root, "ClaimMultiplierButton/Label"));
            BindReference(view, "scaleArea", FindComponent<RectTransform>(root, "Scale"));
            BindReference(view, "arrow", FindComponent<RectTransform>(root, "Scale/Arrow"));
            BindReference(view, "claimMultiplierButton", FindComponent<Button>(root, "ClaimMultiplierButton"));
            BindReference(view, "claimButton", FindComponent<Button>(root, "ClaimButton"));
            BindReference(view, "nextButton", FindComponent<Button>(root, "NextButton"));
            return view;
        }

        private static Transform CreateWinPanel(Transform parent, TMP_FontAsset font)
        {
            RectTransform root = CreateUIObject(PanelName, parent);
            Stretch(root);
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = DimColor;
            dim.raycastTarget = true;   // blocks taps on anything behind the panel

            CreateText("Title", root, font, TitleFontSize, new Vector2(0f, TitleY), TitleSize, "Уровень 1 ЗАВЕРШЕНО");
            CreateText("Status", root, font, StatusFontSize, new Vector2(0f, StatusY), StatusSize, "Бедный");
            CreateText("Money", root, font, MoneyFontSize, new Vector2(0f, MoneyY), MoneySize, "0");

            CreateScale(root, font);

            CreateButton("ClaimMultiplierButton", root, font, "Получить x2", MainButtonColor,
                MainButtonFontSize, new Vector2(0f, MainButtonY), MainButtonSize);
            CreateButton("ClaimButton", root, font, "Получить", SecondButtonColor,
                SecondButtonFontSize, new Vector2(0f, SecondButtonY), SecondButtonSize);
            Transform next = CreateButton("NextButton", root, font, "Далее", MainButtonColor,
                MainButtonFontSize, new Vector2(0f, MainButtonY), MainButtonSize);
            next.gameObject.SetActive(false);

            // Hidden until the finish; UIManager also hides it at start
            root.gameObject.SetActive(false);
            return root;
        }

        private static void CreateScale(Transform parent, TMP_FontAsset font)
        {
            RectTransform scale = CreateUIObject("Scale", parent);
            SetCentered(scale, new Vector2(0f, ScaleY), ScaleSize);

            int count = SegmentLabels.Length;
            for (int i = 0; i < count; i++)
            {
                RectTransform segment = CreateUIObject("Segment_" + SegmentLabels[i], scale);
                segment.anchorMin = new Vector2((float)i / count, 0f);
                segment.anchorMax = new Vector2((float)(i + 1) / count, 1f);
                segment.offsetMin = segment.offsetMax = Vector2.zero;

                Image image = segment.gameObject.AddComponent<Image>();
                image.color = SegmentColors[i % SegmentColors.Length];
                image.raycastTarget = false;

                RectTransform label = CreateUIObject("Label", segment);
                Stretch(label);
                ConfigureText(label.gameObject.AddComponent<TextMeshProUGUI>(), font, SegmentFontSize, SegmentLabels[i]);
            }

            // Diamond pointing at the bar from above; the view moves it along X only
            RectTransform arrow = CreateUIObject("Arrow", scale);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 1f);
            arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.sizeDelta = ArrowSize;
            arrow.anchoredPosition = new Vector2(0f, ArrowGap + ArrowSize.y * 0.5f);
            arrow.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image arrowImage = arrow.gameObject.AddComponent<Image>();
            arrowImage.color = ArrowColor;
            arrowImage.raycastTarget = false;
        }

        private static Transform CreateButton(string name, Transform parent, TMP_FontAsset font, string label,
            Color color, float fontSize, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateUIObject(name, parent);
            SetCentered(rect, position, size);

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltInSprite);
            image.type = Image.Type.Sliced;
            image.color = color;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            RectTransform labelRect = CreateUIObject("Label", rect);
            Stretch(labelRect);
            ConfigureText(labelRect.gameObject.AddComponent<TextMeshProUGUI>(), font, fontSize, label);
            return rect;
        }

        private static void CreateText(string name, Transform parent, TMP_FontAsset font, float fontSize,
            Vector2 position, Vector2 size, string value)
        {
            RectTransform rect = CreateUIObject(name, parent);
            SetCentered(rect, position, size);
            ConfigureText(rect.gameObject.AddComponent<TextMeshProUGUI>(), font, fontSize, value);
        }

        #endregion

        #region Helpers

        private static RectTransform CreateUIObject(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
            go.layer = LayerMask.NameToLayer(UiLayerName);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
