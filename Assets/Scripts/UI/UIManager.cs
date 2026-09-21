using ButchersGames.Core;
using ButchersGames.Gameplay.Economy;
using ButchersGames.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ButchersGames.UI
{
    /// <summary>
    /// Screen-level UI: the start panel, the money HUD and the win/lose panels.
    /// Listens to ScoreManager on its own, GameManager only asks it to show the result.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        [Tooltip("Shows the money number with an animation. Preferred over moneyText.")]
        [SerializeField] private MoneyCounterView moneyCounter;
        [Tooltip("Plain fallback text, used only when moneyCounter is not assigned")]
        [SerializeField] private TextMeshProUGUI moneyText;

        [Header("Panels")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject winPanel;
        [Tooltip("Fills the win panel with the level result. Optional: without it the panel is only shown.")]
        [SerializeField] private WinPanelView winPanelView;
        [SerializeField] private GameObject losePanel;

        [Header("Buttons")]
        [Tooltip("Starts the level. May live outside the start panel.")]
        [SerializeField] private Button startButton;

        [Header("Scene references")]
        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private GameManager gameManager;
        [Tooltip("Gives the status name for the win screen. Found in the scene automatically if left empty.")]
        [SerializeField] private WealthTracker wealthTracker;

        public MoneyCounterView MoneyCounter => moneyCounter;
        public TextMeshProUGUI MoneyText => moneyText;
        public WinPanelView WinPanelView => winPanelView;

        private ScoreManager subscribedScore;

        private void Start()
        {
            if (gameManager == null)
                gameManager = FindObjectOfType<GameManager>(true);
            if (wealthTracker == null)
                wealthTracker = FindObjectOfType<WealthTracker>(true);

            SetPanelActive(startPanel, true);
            SetPanelActive(winPanel, false);
            SetPanelActive(losePanel, false);

            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);

            subscribedScore = ScoreManager.Instance;
            if (subscribedScore != null)
            {
                subscribedScore.OnMoneyChanged += UpdateMoney;
                UpdateMoney(subscribedScore.Money);
            }
            else
            {
                Debug.LogWarning("[UIManager] ScoreManager not found, the money counter will not update.", this);
            }
        }

        private void OnDestroy()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(OnStartClicked);
            if (subscribedScore != null)
                subscribedScore.OnMoneyChanged -= UpdateMoney;
            subscribedScore = null;
        }

        public void UpdateMoney(int amount)
        {
            if (moneyCounter != null)
                moneyCounter.SetMoney(amount);
            else if (moneyText != null)
                moneyText.SetText("{0}", amount);
        }

        public void ShowWin()
        {
            SetPanelActive(winPanel, true);

            if (winPanelView == null) return;

            int money = ScoreManager.Instance != null ? ScoreManager.Instance.Money : 0;
            int level = LevelManager.Default != null ? LevelManager.CurrentLevel : 1;
            winPanelView.Show(level, money, GetStatusName(), OnWinNextClicked);
        }

        public void ShowLose()
        {
            SetPanelActive(losePanel, true);
        }

        private void OnStartClicked()
        {
            SetPanelActive(startPanel, false);
            // The button is not always inside the start panel, so hide it explicitly
            if (startButton != null) startButton.gameObject.SetActive(false);

            if (LevelManager.Default != null)
                LevelManager.Default.StartLevel();
        }

        private void OnWinNextClicked()
        {
            if (gameManager != null)
                gameManager.NextLevel();
        }

        private string GetStatusName()
        {
            if (wealthTracker == null || wealthTracker.Config == null) return string.Empty;
            return wealthTracker.Config.GetDisplayName(wealthTracker.CurrentStatus);
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }
    }
}
