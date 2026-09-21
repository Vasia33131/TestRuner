using ButchersGames.Gameplay.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ButchersGames.UI
{
    /// <summary>Status name and progress bar above the player. Listens to WealthTracker on its own.</summary>
    public class StatusBarView : MonoBehaviour
    {
        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private WealthTracker tracker;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("Image with Type = Filled")]
        [SerializeField] private Image fillImage;

        [Header("Colors")]
        [SerializeField] private Color poorColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color middleColor = new Color(1f, 0.94f, 0.35f);
        [SerializeField] private Color richColor = new Color(0.3f, 0.85f, 0.3f);
        [SerializeField] private Color millionaireColor = new Color(1f, 0.72f, 0.05f);

        [Header("Animation")]
        [Tooltip("How fast the bar and its color follow the target, larger is faster")]
        [SerializeField] private float smoothSpeed = 6f;
        [Tooltip("On a status change the bar restarts from the opposite edge: from 0 when the status grows, from 1 when it drops")]
        [SerializeField] private bool restartOnStatusChange = true;

        private float targetFill;
        private Color targetColor;
        private WealthStatus currentStatus;
        private bool subscribed;

        private void Start()
        {
            if (tracker == null)
                tracker = FindObjectOfType<WealthTracker>(true);

            if (tracker == null)
            {
                Debug.LogError("[StatusBarView] WealthTracker not found in the scene.", this);
                enabled = false;
                return;
            }

            // Start runs before WealthTracker.Start (execution order 10), so the initial event is received too
            tracker.OnStatusChanged += HandleStatusChanged;
            tracker.OnProgressChanged += HandleProgressChanged;
            subscribed = true;

            currentStatus = tracker.CurrentStatus;
            SetName(currentStatus);
            targetColor = GetColor(currentStatus);
            targetFill = tracker.CurrentProgress;
            ApplyInstant();
        }

        private void OnDestroy()
        {
            if (!subscribed || tracker == null) return;
            tracker.OnStatusChanged -= HandleStatusChanged;
            tracker.OnProgressChanged -= HandleProgressChanged;
        }

        private void Update()
        {
            if (fillImage == null) return;

            // Exponential smoothing, independent of the frame rate
            float k = 1f - Mathf.Exp(-smoothSpeed * Time.unscaledDeltaTime);
            fillImage.fillAmount = Mathf.Lerp(fillImage.fillAmount, targetFill, k);
            fillImage.color = Color.Lerp(fillImage.color, targetColor, k);
        }

        private void HandleStatusChanged(WealthStatus status)
        {
            if (restartOnStatusChange && fillImage != null && status != currentStatus)
                fillImage.fillAmount = status > currentStatus ? 0f : 1f;

            currentStatus = status;
            SetName(status);
            targetColor = GetColor(status);
        }

        private void HandleProgressChanged(float progress, WealthStatus status)
        {
            targetFill = Mathf.Clamp01(progress);
        }

        private void SetName(WealthStatus status)
        {
            if (statusText != null && tracker != null && tracker.Config != null)
                statusText.text = tracker.Config.GetDisplayName(status);
        }

        private void ApplyInstant()
        {
            if (fillImage == null) return;
            fillImage.fillAmount = targetFill;
            fillImage.color = targetColor;
        }

        private Color GetColor(WealthStatus status)
        {
            switch (status)
            {
                case WealthStatus.Middle: return middleColor;
                case WealthStatus.Rich: return richColor;
                case WealthStatus.Millionaire: return millionaireColor;
                default: return poorColor;
            }
        }
    }
}
