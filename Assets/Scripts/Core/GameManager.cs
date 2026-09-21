using System;
using ButchersGames.Gameplay.Economy;
using ButchersGames.Levels;
using ButchersGames.Player;
using ButchersGames.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButchersGames.Core
{
    /// <summary>
    /// Top-level game flow: Ready -> Playing -> Won, then the next level or a restart (both reload the scene).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;

        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private UIManager uiManager;

        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private PlayerController player;

        /// <summary>Raised whenever the state changes.</summary>
        public event Action<GameState> OnStateChanged;

        public GameState State { get; private set; } = GameState.Ready;

        /// <summary>True after the finish was reached. Reset by the scene reload.</summary>
        public bool IsWon => State == GameState.Won;

        /// <summary>Amount claimed on the win or finish screen (with the multiplier applied).</summary>
        public int ClaimedReward { get; private set; }

        public LevelManager LevelManager
        {
            get => levelManager;
            set => levelManager = value;
        }

        private LevelManager subscribedLevels;
        private WinPanelView subscribedPanel;

        private void Awake()
        {
            if (levelManager == null)
                levelManager = GetComponent<LevelManager>();

            if (levelManager != null)
                levelManager.Init();
            else
                Debug.LogWarning("[GameManager] LevelManager is not assigned, the level will not be loaded.", this);
        }

        private void Start()
        {
            if (uiManager == null)
                uiManager = FindObjectOfType<UIManager>(true);
            if (player == null)
                player = FindObjectOfType<PlayerController>(true);

            if (player != null)
                player.OnFinished += Win;
            else
                Debug.LogWarning("[GameManager] Player not found, the finish will not be detected. Call Win() manually.", this);

            if (levelManager != null)
            {
                subscribedLevels = levelManager;
                subscribedLevels.OnLevelStarted += HandleLevelStarted;
            }

            if (uiManager != null && uiManager.WinPanelView != null)
            {
                subscribedPanel = uiManager.WinPanelView;
                subscribedPanel.OnClaimed += HandleClaimed;
            }
        }

        private void OnDestroy()
        {
            if (player != null)
                player.OnFinished -= Win;
            if (subscribedLevels != null)
                subscribedLevels.OnLevelStarted -= HandleLevelStarted;
            if (subscribedPanel != null)
                subscribedPanel.OnClaimed -= HandleClaimed;
        }

        /// <summary>Stops the player and shows the win screen. Safe to call more than once.</summary>
        public void Win()
        {
            if (IsWon) return;
            SetState(GameState.Won);

            if (player != null)
                player.StopRun();

            if (uiManager != null)
                uiManager.ShowWin();
            else
                Debug.LogWarning("[GameManager] UIManager not found, the win screen was not shown.", this);
        }

        /// <summary>
        /// Marks the level as won with the reward already chosen (the finish screen did it),
        /// so the win screen is not shown.
        /// </summary>
        public void ClaimReward(int total)
        {
            ClaimedReward = Mathf.Max(0, total);
            SetState(GameState.Won);

            if (player != null)
                player.StopRun();
        }

        /// <summary>Resets the score, moves on to the next level and reloads the scene.</summary>
        public void NextLevel()
        {
            ResetScore();

            if (levelManager != null)
            {
                // The level counts as completed only when the player moves on
                LevelManager.CompleteLevelCount++;
                levelManager.NextLevel();
            }

            ReloadScene();
        }

        /// <summary>Resets the score and reloads the scene with the same level.</summary>
        public void Restart()
        {
            ResetScore();
            ReloadScene();
        }

        private void HandleLevelStarted()
        {
            if (State == GameState.Ready)
                SetState(GameState.Playing);
        }

        private void HandleClaimed(int multiplier, int total)
        {
            ClaimedReward = total;
        }

        private void SetState(GameState state)
        {
            if (State == state) return;
            State = state;
            OnStateChanged?.Invoke(state);
        }

        private static void ResetScore()
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ResetScore();
        }

        private static void ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
