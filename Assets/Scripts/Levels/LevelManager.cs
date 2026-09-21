using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ButchersGames.Levels
{
    /// <summary>
    /// Picks the level prefab from the LevelsList, places it under itself and keeps the progress in PlayerPrefs.
    /// In Editor Mode the level is chosen by hand in the inspector and the progress is not touched.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        private const string CurrentLevelPrefsKey = "Current Level";
        private const string CompleteLevelCountPrefsKey = "Complete Lvl Count";
        private const string LastLevelIndexPrefsKey = "Last Level Index";
        private const string CurrentAttemptPrefsKey = "Current Attempt";

        public static LevelManager Default { get; private set; }

        /// <summary>1-based number of the level shown to the player.</summary>
        public static int CurrentLevel
        {
            get
            {
                if (Default == null) return CompleteLevelCount + 1;
                return (CompleteLevelCount < Default.Levels.Count ? Default.CurrentLevelIndex : CompleteLevelCount) + 1;
            }
            set => PlayerPrefs.SetInt(CurrentLevelPrefsKey, value);
        }

        public static int CompleteLevelCount
        {
            get => PlayerPrefs.GetInt(CompleteLevelCountPrefsKey);
            set => PlayerPrefs.SetInt(CompleteLevelCountPrefsKey, value);
        }

        public static int LastLevelIndex
        {
            get => PlayerPrefs.GetInt(LastLevelIndexPrefsKey);
            set => PlayerPrefs.SetInt(LastLevelIndexPrefsKey, value);
        }

        public static int CurrentAttempt
        {
            get => PlayerPrefs.GetInt(CurrentAttemptPrefsKey);
            set => PlayerPrefs.SetInt(CurrentAttemptPrefsKey, value);
        }

        public int CurrentLevelIndex;

        [SerializeField] private bool editorMode;
        [SerializeField] private LevelsList levels;

        /// <summary>Raised when the player presses start.</summary>
        public event Action OnLevelStarted;

        public List<Level> Levels => levels != null && levels.lvls != null ? levels.lvls : EmptyLevels;

        /// <summary>Level instance currently placed under this manager (null if none).</summary>
        public Level ActiveLevel => GetComponentInChildren<Level>();

        private static readonly List<Level> EmptyLevels = new List<Level>();

        private void Awake()
        {
            Default = this;
        }

        private void OnDestroy()
        {
            LastLevelIndex = CurrentLevelIndex;
            if (Default == this) Default = null;
        }

        private void OnApplicationQuit()
        {
            LastLevelIndex = CurrentLevelIndex;
        }

        /// <summary>Places the saved level. Called by GameManager in Awake, before any Start.</summary>
        public void Init()
        {
            // Init may run before this component's own Awake (GameManager.Awake order is not guaranteed)
            Default = this;

#if !UNITY_EDITOR
            editorMode = false;
#endif
            if (!editorMode) SelectLevel(LastLevelIndex, true);

            if (LastLevelIndex != CurrentLevel)
                CurrentAttempt = 0;
        }

        public void StartLevel()
        {
            OnLevelStarted?.Invoke();
        }

        public void RestartLevel()
        {
            SelectLevel(CurrentLevelIndex, false);
        }

        public void NextLevel()
        {
            if (!editorMode) CurrentLevel++;
            SelectLevel(CurrentLevelIndex + 1);
        }

        public void PrevLevel() => SelectLevel(CurrentLevelIndex - 1);

        public void SelectLevel(int levelIndex, bool indexCheck = true)
        {
            if (Levels.Count == 0)
            {
                Debug.LogError("[LevelManager] The levels list is empty or not assigned.", this);
                return;
            }

            if (indexCheck)
                levelIndex = GetCorrectedIndex(levelIndex);

            if (levelIndex < 0 || levelIndex >= Levels.Count || Levels[levelIndex] == null)
            {
                Debug.LogError("[LevelManager] There is no level prefab at index " + levelIndex + ".", this);
                return;
            }

            SpawnLevel(Levels[levelIndex]);
            CurrentLevelIndex = levelIndex;
        }

        private int GetCorrectedIndex(int levelIndex)
        {
            if (editorMode)
                return levelIndex > Levels.Count - 1 || levelIndex <= 0 ? 0 : levelIndex;

            int levelId = CurrentLevel;
            if (levelId <= Levels.Count - 1)
                return levelId;

            // All levels are passed: loop them, randomly or in order
            if (levels.randomizedLvls && Levels.Count > 1)
            {
                List<int> candidates = Enumerable.Range(0, Levels.Count).ToList();
                candidates.Remove(CurrentLevelIndex);
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            return Mathf.Abs(levelIndex) % Levels.Count;
        }

        private void SpawnLevel(Level level)
        {
            ClearLevel();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                PrefabUtility.InstantiatePrefab(level, transform);
                return;
            }
#endif
            Instantiate(level, transform);
        }

        private void ClearLevel()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
}
