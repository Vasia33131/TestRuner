using System;
using System.Collections;
using ButchersGames.Core;
using ButchersGames.Gameplay.Checkpoints;
using ButchersGames.Gameplay.Economy;
using ButchersGames.Player;
using ButchersGames.UI;
using UnityEngine;

namespace ButchersGames.Gameplay.Finish
{
    /// <summary>
    /// Level end in front of the doors: stops the player, plays the victory dance and shows the "Финиш" screen,
    /// where the player takes the earned money or doubles it. "Далее" then moves on to the next level.
    /// Without a finish panel the usual win flow is used instead (GameManager.Win).
    /// </summary>
    public class FinishController : MonoBehaviour
    {
        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private PlayerController player;

        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("Found in the scene automatically if left empty. Without it the win screen opens right away.")]
        [SerializeField] private FinishPanelView finishPanel;

        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private CheckpointManager checkpoints;

        [Tooltip("The level is completed, so the next run starts from the beginning")]
        [SerializeField] private bool clearCheckpointOnFinish = true;

        [Tooltip("Victory dance at the stop point. Added automatically if the scene has none.")]
        [SerializeField] private FinishCelebration celebration;

        [Tooltip("How long the dance plays before the result and the reward choice appear")]
        [SerializeField, Min(0f)] private float celebrationDelay = 1.8f;

        /// <summary>Raised once on finish: doors passed, total doors, money.</summary>
        public event Action<int, int, int> OnFinished;

        public bool IsFinished { get; private set; }

        private void Start()
        {
            if (player == null)
                player = FindObjectOfType<PlayerController>(true);
            if (gameManager == null)
                gameManager = FindObjectOfType<GameManager>(true);
            if (finishPanel == null)
                finishPanel = FindObjectOfType<FinishPanelView>(true);
            if (checkpoints == null)
                checkpoints = FindObjectOfType<CheckpointManager>(true);

            if (celebration == null)
                celebration = FindObjectOfType<FinishCelebration>(true);
            if (celebration == null)
                celebration = gameObject.AddComponent<FinishCelebration>();

            if (finishPanel != null)
                finishPanel.Hide();
        }

        public void Finish(int doorsPassed, int totalDoors)
        {
            if (IsFinished) return;
            IsFinished = true;

            if (player != null)
                player.StopRun();

            if (clearCheckpointOnFinish)
            {
                if (checkpoints != null) checkpoints.ClearSave();
                else ProgressSave.Clear();
            }

            int money = ScoreManager.Instance != null ? ScoreManager.Instance.Money : 0;
            Debug.Log("[Finish] Doors passed " + doorsPassed + " of " + totalDoors + ", money " + money, this);
            OnFinished?.Invoke(doorsPassed, totalDoors, money);

            if (celebration != null)
                celebration.Play();

            StartCoroutine(ShowResultAfterCelebration(doorsPassed, totalDoors, money));
        }

        private IEnumerator ShowResultAfterCelebration(int doorsPassed, int totalDoors, int money)
        {
            if (celebration != null && celebration.IsPlaying && celebrationDelay > 0f)
                yield return new WaitForSeconds(celebrationDelay);

            if (finishPanel != null)
                finishPanel.Show(doorsPassed, totalDoors, money, ClaimReward, GoToNextLevel);
            else
                ShowWinScreen();
        }

        private void ClaimReward(int multiplier, int total)
        {
            Debug.Log("[Finish] Reward claimed x" + multiplier + ": " + total, this);

            // The HUD counter shows the bonus too
            if (ScoreManager.Instance != null && total > ScoreManager.Instance.Money)
                ScoreManager.Instance.SetMoney(total);

            if (gameManager != null)
                gameManager.ClaimReward(total);
        }

        private void GoToNextLevel()
        {
            if (gameManager != null)
                gameManager.NextLevel();
        }

        private void ShowWinScreen()
        {
            if (gameManager != null)
                gameManager.Win();
        }
    }
}
