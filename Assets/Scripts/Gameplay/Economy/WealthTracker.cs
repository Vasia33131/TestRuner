using System;
using ButchersGames.Player;
using UnityEngine;

namespace ButchersGames.Gameplay.Economy
{
    /// <summary>Turns the money amount into a wealth status and progress, and updates the player look.</summary>
    // Starts after PlayerAppearance so its default look does not overwrite the current status
    [DefaultExecutionOrder(10)]
    public class WealthTracker : MonoBehaviour
    {
        [SerializeField] private WealthConfig config;
        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private PlayerAppearance appearance;
        [SerializeField] private bool logStatusChanges = true;

        /// <summary>Raised when the status changes (and once at start with the initial status).</summary>
        public event Action<WealthStatus> OnStatusChanged;

        /// <summary>Raised on every money change and once at start: progress inside the status and the status itself.</summary>
        public event Action<float, WealthStatus> OnProgressChanged;

        public WealthConfig Config => config;
        public WealthStatus CurrentStatus { get; private set; }
        public float CurrentProgress { get; private set; }

        private ScoreManager subscribedScore;
        private bool initialized;

        private void Start()
        {
            if (config == null)
            {
                Debug.LogError("[WealthTracker] WealthConfig is not assigned, run Tools/Runner/Setup Collectibles.", this);
                enabled = false;
                return;
            }

            if (appearance == null)
                appearance = FindObjectOfType<PlayerAppearance>(true);

            subscribedScore = ScoreManager.Instance;
            if (subscribedScore == null)
            {
                Debug.LogError("[WealthTracker] ScoreManager not found in the scene.", this);
                enabled = false;
                return;
            }

            subscribedScore.OnMoneyChanged += Refresh;
            Refresh(subscribedScore.Money);
        }

        private void OnDestroy()
        {
            if (subscribedScore != null)
                subscribedScore.OnMoneyChanged -= Refresh;
            subscribedScore = null;
        }

        private void Refresh(int money)
        {
            config.Evaluate(money, out WealthStatus status, out float progress);
            CurrentProgress = progress;

            bool statusChanged = !initialized || status != CurrentStatus;
            CurrentStatus = status;
            initialized = true;

            if (statusChanged)
            {
                if (appearance != null)
                    appearance.SetAppearance(status);

                if (logStatusChanges)
                    Debug.Log("[Wealth] Status: " + status + " (" + config.GetDisplayName(status) + "), money " + money);
                OnStatusChanged?.Invoke(status);
            }

            OnProgressChanged?.Invoke(progress, status);
        }
    }
}
