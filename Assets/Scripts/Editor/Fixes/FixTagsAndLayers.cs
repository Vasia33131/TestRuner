using System;
using ButchersGames.Core;
using UnityEditor;
using UnityEngine;

namespace ButchersGames.EditorTools
{
    /// <summary>
    /// Cleans tags with stray whitespace, makes sure the runner tags and layers exist
    /// and restricts the Collectible layer to collide only with the Player layer.
    /// Safe to run any number of times.
    /// </summary>
    public static class FixTagsAndLayers
    {
        public const string PlayerTag = GameTags.Player;
        public const string PlayerLayerName = "Player";
        public const string CollectibleLayerName = "Collectible";

        private const string TagManagerPath = "ProjectSettings/TagManager.asset";
        private const string DynamicsManagerPath = "ProjectSettings/DynamicsManager.asset";

        private const int PlayerLayerSlot = 8;
        private const int CollectibleLayerSlot = 9;
        private const int FirstUserLayer = 8;
        private const int LayerCount = 32;

        private static readonly string[] RequiredTags = { "Player", "Pickup", "Obstacle", "Finish" };

        // Unity always provides these tags itself; adding them to the custom list would create duplicates
        private static readonly string[] BuiltInTags = { "Untagged", "Respawn", "Finish", "EditorOnly", "MainCamera", "Player", "GameController" };

        [MenuItem("Tools/Runner/Fix Tags And Layers")]
        public static void Run()
        {
            if (Apply())
                Debug.Log("[Runner] Tags and layers fixed.");
            else
                Debug.Log("[Runner] Tags and layers are already up to date.");
        }

        /// <returns>True if any project setting was changed.</returns>
        public static bool Apply()
        {
            bool changed = false;

            UnityEngine.Object[] tagAssets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (tagAssets == null || tagAssets.Length == 0)
            {
                Debug.LogError("[Runner] Cannot load " + TagManagerPath);
                return false;
            }

            var tagManager = new SerializedObject(tagAssets[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            SerializedProperty layers = tagManager.FindProperty("layers");

            bool tagsChanged = FixTags(tags);
            bool layersChanged = EnsureLayer(layers, PlayerLayerName, PlayerLayerSlot);
            layersChanged |= EnsureLayer(layers, CollectibleLayerName, CollectibleLayerSlot);

            if (tagsChanged || layersChanged)
            {
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                changed = true;
            }

            int playerLayer = FindLayerIndex(layers, PlayerLayerName);
            int collectibleLayer = FindLayerIndex(layers, CollectibleLayerName);
            if (playerLayer >= 0 && collectibleLayer >= 0)
                changed |= ConfigureCollisionMatrix(playerLayer, collectibleLayer);

            return changed;
        }

        private static bool FixTags(SerializedProperty tags)
        {
            bool changed = false;

            // Remove tags like 'Player ' that only differ from a required tag by whitespace
            for (int i = tags.arraySize - 1; i >= 0; i--)
            {
                string tag = tags.GetArrayElementAtIndex(i).stringValue;
                string clean = tag.Trim();
                if (clean != tag && Array.IndexOf(RequiredTags, clean) >= 0)
                {
                    tags.DeleteArrayElementAtIndex(i);
                    changed = true;
                }
            }

            foreach (string required in RequiredTags)
            {
                if (Array.IndexOf(BuiltInTags, required) >= 0) continue;
                if (ContainsTag(tags, required)) continue;

                int index = tags.arraySize;
                tags.InsertArrayElementAtIndex(index);
                tags.GetArrayElementAtIndex(index).stringValue = required;
                changed = true;
            }

            return changed;
        }

        private static bool ContainsTag(SerializedProperty tags, string tag)
        {
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return true;
            }
            return false;
        }

        private static bool EnsureLayer(SerializedProperty layers, string layerName, int preferredSlot)
        {
            if (FindLayerIndex(layers, layerName) >= 0) return false;

            int slot = -1;
            if (preferredSlot < layers.arraySize && IsLayerSlotFree(layers, preferredSlot))
            {
                slot = preferredSlot;
            }
            else
            {
                for (int i = FirstUserLayer; i < Mathf.Min(LayerCount, layers.arraySize); i++)
                {
                    if (IsLayerSlotFree(layers, i))
                    {
                        slot = i;
                        break;
                    }
                }
            }

            if (slot < 0)
            {
                Debug.LogError("[Runner] No free user layer slot for layer '" + layerName + "'.");
                return false;
            }

            layers.GetArrayElementAtIndex(slot).stringValue = layerName;
            return true;
        }

        private static bool IsLayerSlotFree(SerializedProperty layers, int index)
        {
            return string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue);
        }

        private static int FindLayerIndex(SerializedProperty layers, string layerName)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return i;
            }
            return -1;
        }

        private static bool ConfigureCollisionMatrix(int playerLayer, int collectibleLayer)
        {
            bool changed = false;

            // Collectible collides with Player only
            for (int other = 0; other < LayerCount; other++)
            {
                bool shouldIgnore = other != playerLayer;
                if (Physics.GetIgnoreLayerCollision(collectibleLayer, other) == shouldIgnore) continue;

                Physics.IgnoreLayerCollision(collectibleLayer, other, shouldIgnore);
                changed = true;
            }

            if (changed)
            {
                UnityEngine.Object[] dynamicsAssets = AssetDatabase.LoadAllAssetsAtPath(DynamicsManagerPath);
                if (dynamicsAssets != null && dynamicsAssets.Length > 0)
                    EditorUtility.SetDirty(dynamicsAssets[0]);
                AssetDatabase.SaveAssets();
            }

            return changed;
        }
    }
}
