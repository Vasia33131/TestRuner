using ButchersGames.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ButchersGames.EditorTools
{
    /// <summary>
    /// Sprites in Assets/Visual/Sprite/*.asset point to textures that are not in the project, so UI Images
    /// using them are drawn as white rectangles. The same pictures exist as PNG in Assets/Visual/Texture2D:
    /// this tool imports those PNGs as sprites (keeping the 9-slice border) and puts them into the Images.
    /// </summary>
    public static class BrokenSpritesFix
    {
        private const string TextureFolder = "Assets/Visual/Texture2D";

        [MenuItem("Tools/Runner/Fix Broken UI Sprites")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Runner] Fix Broken UI Sprites cannot run in Play mode.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Runner] No active scene loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Runner: Fix Broken UI Sprites");
            int undoGroup = Undo.GetCurrentGroup();

            int fixedCount = 0;
            foreach (Image image in Object.FindObjectsOfType<Image>(true))
            {
                if (image.gameObject.scene != scene || !IsBroken(image.sprite)) continue;

                Sprite replacement = Resolve(image.sprite);
                if (replacement == image.sprite)
                {
                    Debug.LogWarning("[Runner] No PNG found for sprite '" + image.sprite.name + "' on '" + image.name + "', assign a sprite by hand.", image);
                    continue;
                }

                Undo.RecordObject(image, "Fix Sprite");
                image.sprite = replacement;
                EditorUtility.SetDirty(image);
                fixedCount++;
            }

            HideUnusedMoneyText();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Runner] Fixed " + fixedCount + " UI sprite(s). Save the scene (Ctrl+S).");
        }

        /// <summary>A sprite whose texture is missing renders as a plain white rectangle.</summary>
        public static bool IsBroken(Sprite sprite)
        {
            return sprite != null && sprite.texture == null;
        }

        /// <summary>Returns a working sprite with the same name, or the given sprite if there is nothing better.</summary>
        public static Sprite Resolve(Sprite sprite)
        {
            if (!IsBroken(sprite)) return sprite;
            Sprite png = LoadPngSprite(sprite.name, sprite.border);
            return png != null ? png : sprite;
        }

        /// <summary>Loads Assets/Visual/Texture2D/&lt;name&gt;.png as a sprite, switching its importer to Sprite if needed.</summary>
        public static Sprite LoadPngSprite(string spriteName, Vector4 border)
        {
            string path = TextureFolder + "/" + spriteName + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                changed = true;
            }
            if (importer.spriteBorder == Vector4.zero && border != Vector4.zero)
            {
                importer.spriteBorder = border;
                changed = true;
            }
            if (changed)
                importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // UIManager falls back to moneyText only without a MoneyCounterView, so that text just shows "New Text"
        private static void HideUnusedMoneyText()
        {
            UIManager manager = Object.FindObjectOfType<UIManager>(true);
            if (manager == null || manager.MoneyCounter == null || manager.MoneyText == null) return;

            GameObject fallback = manager.MoneyText.gameObject;
            if (!fallback.activeSelf || fallback.GetComponentInParent<MoneyCounterView>(true) != null) return;

            Undo.RecordObject(fallback, "Hide Unused Money Text");
            fallback.SetActive(false);
            Debug.Log("[Runner] Hid '" + fallback.name + "': the money is shown by '" + manager.MoneyCounter.name + "'.", fallback);
        }
    }
}
