using System;
using ButchersGames.Gameplay.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ButchersGames.UI
{
    /// <summary>
    /// "Финиш" screen: how many doors were passed and the reward choice.
    /// The player either takes the earned money or takes it multiplied (x2 by default).
    /// After the choice the amount counts up and the "Далее" button appears.
    /// Uses unscaled time, so it also works while Time.timeScale is 0.
    /// </summary>
    public class FinishPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text resultText;

        [Header("Buttons")]
        [Tooltip("Takes the reward multiplied by rewardMultiplier")]
        [SerializeField] private Button multiplyButton;
        [Tooltip("Takes the reward as is")]
        [SerializeField] private Button claimButton;
        [Tooltip("Shown after the choice, moves on")]
        [SerializeField] private Button continueButton;

        [Header("Reward")]
        [SerializeField, Min(1)] private int rewardMultiplier = 2;

        [Header("Formats")]
        [SerializeField] private string title = "Финиш";
        [Tooltip("{0} doors passed, {1} total doors, {2} money")]
        [SerializeField, TextArea] private string resultFormat = "Пройдено дверей: {0} из {1}\nДеньги: {2}";
        [Tooltip("{0} multiplier, {1} multiplied money")]
        [SerializeField] private string multiplyFormat = "x{0}  ({1})";
        [Tooltip("{0} money")]
        [SerializeField] private string claimFormat = "Забрать {0}";
        [SerializeField] private string continueLabel = "Далее";

        [Header("Animation")]
        [SerializeField, Min(0f)] private float countUpDuration = 0.6f;

        /// <summary>Raised once when the reward is chosen: applied multiplier and the final amount.</summary>
        public event Action<int, int> OnClaimed;

        public bool IsClaimed { get; private set; }
        public int FinalAmount { get; private set; }

        private Action<int, int> onClaim;
        private Action onContinue;
        private bool listening;

        private int doorsPassed;
        private int totalDoors;
        private int baseMoney;
        private readonly CountUpTween countUp = new CountUpTween();

        /// <summary>
        /// Opens the panel with the reward choice. onClaimAction gets the multiplier and the final amount,
        /// onContinueAction is invoked by the "Далее" button after the choice.
        /// </summary>
        public void Show(int doorsPassedCount, int totalDoorsCount, int money,
            Action<int, int> onClaimAction, Action onContinueAction)
        {
            doorsPassed = doorsPassedCount;
            totalDoors = totalDoorsCount;
            baseMoney = Mathf.Max(0, money);
            FinalAmount = baseMoney;
            IsClaimed = false;
            countUp.Stop();
            onClaim = onClaimAction;
            onContinue = onContinueAction;

            Subscribe();

            if (titleText != null) titleText.text = title;
            SetResultText(baseMoney);

            SetLabel(multiplyButton, string.Format(multiplyFormat, rewardMultiplier, RewardCalculator.Multiply(baseMoney, rewardMultiplier)));
            SetLabel(claimButton, string.Format(claimFormat, baseMoney));
            SetLabel(continueButton, continueLabel);

            SetButtonActive(multiplyButton, true);
            SetButtonActive(claimButton, true);
            // Without choice buttons there is nothing to choose, the reward is taken as is
            bool hasChoice = multiplyButton != null || claimButton != null;
            SetButtonActive(continueButton, !hasChoice);

            gameObject.SetActive(true);

            if (!hasChoice)
                Claim(1);
        }

        public void Hide()
        {
            countUp.Stop();
            gameObject.SetActive(false);
        }

        private void Subscribe()
        {
            if (listening) return;
            listening = true;
            if (multiplyButton != null) multiplyButton.onClick.AddListener(ClaimMultiplied);
            if (claimButton != null) claimButton.onClick.AddListener(ClaimPlain);
            if (continueButton != null) continueButton.onClick.AddListener(HandleContinue);
        }

        private void Update()
        {
            if (countUp.IsPlaying)
                SetResultText(countUp.Tick(Time.unscaledDeltaTime));
        }

        private void ClaimMultiplied() => Claim(rewardMultiplier);

        private void ClaimPlain() => Claim(1);

        private void Claim(int multiplier)
        {
            // Guards against a double tap and a tap on both buttons
            if (IsClaimed) return;
            IsClaimed = true;

            FinalAmount = RewardCalculator.Multiply(baseMoney, multiplier);

            SetButtonActive(multiplyButton, false);
            SetButtonActive(claimButton, false);
            SetButtonActive(continueButton, true);

            if (!countUp.Play(baseMoney, FinalAmount, countUpDuration))
                SetResultText(FinalAmount);

            Action<int, int> action = onClaim;
            onClaim = null;
            action?.Invoke(multiplier, FinalAmount);
            OnClaimed?.Invoke(multiplier, FinalAmount);

            // No "Далее" button in the scene: move on right away
            if (continueButton == null)
                HandleContinue();
        }

        private void HandleContinue()
        {
            // Cleared first, so a double tap does not continue twice
            Action action = onContinue;
            onContinue = null;
            action?.Invoke();
        }

        private void SetResultText(int money)
        {
            if (resultText != null) resultText.text = string.Format(resultFormat, doorsPassed, totalDoors, money);
        }

        private static void SetButtonActive(Button button, bool active)
        {
            if (button != null) button.gameObject.SetActive(active);
        }

        private static void SetLabel(Button button, string value)
        {
            if (button == null) return;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = value;
        }
    }
}
