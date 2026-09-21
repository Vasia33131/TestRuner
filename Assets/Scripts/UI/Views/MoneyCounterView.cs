using TMPro;
using UnityEngine;

namespace ButchersGames.UI
{
    /// <summary>Shows the money number and plays a short "punch scale" animation whenever the number changes.</summary>
    public class MoneyCounterView : MonoBehaviour
    {
        [SerializeField] private TMP_Text valueText;
        [Tooltip("Object that gets scaled by the punch. Defaults to this object.")]
        [SerializeField] private RectTransform punchTarget;

        [Header("Punch")]
        [SerializeField] private float punchScale = 1.25f;
        [SerializeField] private float punchDuration = 0.25f;

        private const int NotShown = int.MinValue;

        private int shownValue = NotShown;
        private Vector3 baseScale = Vector3.one;
        private float punchTime;
        private bool punching;

        private void Awake()
        {
            if (punchTarget == null)
                punchTarget = transform as RectTransform;
            if (punchTarget != null)
                baseScale = punchTarget.localScale;
        }

        private void OnDisable()
        {
            StopPunch();
        }

        /// <summary>Sets the number. The first call only sets the text, later changes also play the punch.</summary>
        public void SetMoney(int amount)
        {
            if (amount == shownValue) return;

            bool animate = shownValue != NotShown;
            shownValue = amount;

            if (valueText != null)
                valueText.text = amount.ToString();

            if (animate && isActiveAndEnabled)
                StartPunch();
        }

        private void StartPunch()
        {
            if (punchTarget == null || punchDuration <= 0f) return;
            punchTime = 0f;
            punching = true;
        }

        private void StopPunch()
        {
            punching = false;
            if (punchTarget != null)
                punchTarget.localScale = baseScale;
        }

        private void Update()
        {
            if (!punching) return;

            // Unscaled time, so the HUD still reacts if the game is paused
            punchTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(punchTime / punchDuration);

            if (t >= 1f)
            {
                StopPunch();
                return;
            }

            // Grows and returns to the base size, the peak is at the middle of the animation
            float pulse = Mathf.Sin(t * Mathf.PI);
            punchTarget.localScale = baseScale * (1f + (punchScale - 1f) * pulse);
        }
    }
}
