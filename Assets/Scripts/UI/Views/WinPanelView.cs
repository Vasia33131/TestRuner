using System;
using ButchersGames.Gameplay.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ButchersGames.UI
{
    /// <summary>
    /// Win screen: level title, wealth status, reward total and a multiplier scale with a swinging arrow.
    /// The arrow moves back and forth until the player claims the reward.
    /// Uses unscaled time, so it also works while Time.timeScale is 0.
    /// </summary>
    public class WinPanelView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text claimMultiplierLabel;

        [Header("Multiplier scale")]
        [Tooltip("Bar the arrow travels along. The arrow must be its child, anchored to the horizontal center.")]
        [SerializeField] private RectTransform scaleArea;
        [SerializeField] private RectTransform arrow;
        [Tooltip("Equal-width segments from left to right. Must match the segments built in the scene.")]
        [SerializeField] private int[] multipliers = { 2, 3, 4, 5 };
        [Tooltip("Seconds for the arrow to go from one end to the other and back")]
        [SerializeField] private float swingPeriod = 1.6f;

        [Header("Buttons")]
        [SerializeField] private Button claimMultiplierButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button nextButton;

        [Header("Formats")]
        [SerializeField] private string titleFormat = "Уровень {0} ЗАВЕРШЕНО";
        [SerializeField] private string claimMultiplierFormat = "Получить x{0}";

        [Header("Animation")]
        [SerializeField] private float countUpDuration = 0.6f;

        /// <summary>Raised once when the reward is claimed: applied multiplier and the final amount.</summary>
        public event Action<int, int> OnClaimed;

        public bool IsClaimed { get; private set; }
        public int FinalAmount { get; private set; }

        private int baseMoney;
        private float swingPhase;
        private int currentMultiplier = 1;
        private readonly CountUpTween countUp = new CountUpTween();
        private Action onNext;
        private bool nextPressed;

        private void Awake()
        {
            if (claimMultiplierButton != null) claimMultiplierButton.onClick.AddListener(ClaimWithMultiplier);
            if (claimButton != null) claimButton.onClick.AddListener(ClaimWithoutMultiplier);
            if (nextButton != null) nextButton.onClick.AddListener(HandleNext);
        }

        /// <summary>Opens the panel and starts the arrow. onNext is invoked by the "Далее" button.</summary>
        public void Show(int level, int money, string statusName, Action onNextAction)
        {
            baseMoney = Mathf.Max(0, money);
            FinalAmount = baseMoney;
            IsClaimed = false;
            nextPressed = false;
            countUp.Stop();
            onNext = onNextAction;
            swingPhase = 0f;

            if (titleText != null) titleText.text = string.Format(titleFormat, level);
            if (statusText != null) statusText.text = statusName;
            SetMoneyText(baseMoney);

            if (claimMultiplierButton != null) claimMultiplierButton.gameObject.SetActive(true);
            if (claimButton != null) claimButton.gameObject.SetActive(true);
            if (nextButton != null) nextButton.gameObject.SetActive(false);

            gameObject.SetActive(true);
            UpdateArrow();
        }

        private void Update()
        {
            if (!IsClaimed)
                UpdateArrow();
            else if (countUp.IsPlaying)
                SetMoneyText(countUp.Tick(Time.unscaledDeltaTime));
        }

        private void UpdateArrow()
        {
            if (swingPeriod > 0f)
                swingPhase += Time.unscaledDeltaTime * 2f / swingPeriod;

            float t = Mathf.PingPong(swingPhase, 1f);

            if (scaleArea != null && arrow != null)
            {
                float halfWidth = scaleArea.rect.width * 0.5f;
                Vector2 position = arrow.anchoredPosition;
                position.x = Mathf.Lerp(-halfWidth, halfWidth, t);
                arrow.anchoredPosition = position;
            }

            currentMultiplier = MultiplierAt(t);
            if (claimMultiplierLabel != null)
                claimMultiplierLabel.text = string.Format(claimMultiplierFormat, currentMultiplier);
        }

        private int MultiplierAt(float t)
        {
            if (multipliers == null || multipliers.Length == 0) return 1;
            int index = Mathf.Min(Mathf.FloorToInt(t * multipliers.Length), multipliers.Length - 1);
            return Mathf.Max(1, multipliers[index]);
        }

        private void ClaimWithMultiplier() => Claim(currentMultiplier);

        private void ClaimWithoutMultiplier() => Claim(1);

        private void Claim(int multiplier)
        {
            if (IsClaimed) return;
            IsClaimed = true;

            FinalAmount = RewardCalculator.Multiply(baseMoney, multiplier);

            if (claimMultiplierButton != null) claimMultiplierButton.gameObject.SetActive(false);
            if (claimButton != null) claimButton.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(true);

            if (!countUp.Play(baseMoney, FinalAmount, countUpDuration))
                SetMoneyText(FinalAmount);

            OnClaimed?.Invoke(multiplier, FinalAmount);
        }

        private void SetMoneyText(int value)
        {
            if (moneyText != null) moneyText.text = value.ToString();
        }

        private void HandleNext()
        {
            // Guards against a double tap while the scene is loading
            if (nextPressed) return;
            nextPressed = true;
            onNext?.Invoke();
        }
    }
}
