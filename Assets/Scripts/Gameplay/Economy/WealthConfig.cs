using UnityEngine;

namespace ButchersGames.Gameplay.Economy
{
    [CreateAssetMenu(fileName = "WealthConfig", menuName = "Runner/Wealth Config")]
    public class WealthConfig : ScriptableObject
    {
        private const int StatusCount = 4;

        [Tooltip("Money needed to reach each status (Poor, Middle, Rich, Millionaire), ascending")]
        [SerializeField] private int[] thresholds = { 0, 100, 300, 600 };

        [Tooltip("Displayed names, same order as the thresholds")]
        [SerializeField] private string[] displayNames = { "Бедный", "Средний", "Богатый", "Миллионер" };

        public int GetThreshold(WealthStatus status)
        {
            int index = Mathf.Clamp((int)status, 0, StatusCount - 1);
            return thresholds != null && index < thresholds.Length ? thresholds[index] : 0;
        }

        public string GetDisplayName(WealthStatus status)
        {
            int index = (int)status;
            if (displayNames != null && index >= 0 && index < displayNames.Length && !string.IsNullOrEmpty(displayNames[index]))
                return displayNames[index];
            return status.ToString();
        }

        /// <summary>
        /// Finds the status for the given money and the progress (0..1) inside it
        /// towards the next status. The last status always reports 1.
        /// </summary>
        public void Evaluate(int money, out WealthStatus status, out float progress01)
        {
            int index = 0;
            for (int i = 1; i < StatusCount; i++)
            {
                if (money >= GetThreshold((WealthStatus)i)) index = i;
            }

            status = (WealthStatus)index;
            if (index >= StatusCount - 1)
            {
                progress01 = 1f;
                return;
            }

            int from = GetThreshold(status);
            int to = GetThreshold((WealthStatus)(index + 1));
            progress01 = to > from ? Mathf.Clamp01((float)(money - from) / (to - from)) : 1f;
        }

        private void OnValidate()
        {
            if (thresholds == null || thresholds.Length != StatusCount)
                System.Array.Resize(ref thresholds, StatusCount);
            if (displayNames == null || displayNames.Length != StatusCount)
                System.Array.Resize(ref displayNames, StatusCount);

            thresholds[0] = Mathf.Max(0, thresholds[0]);
            for (int i = 1; i < StatusCount; i++)
                thresholds[i] = Mathf.Max(thresholds[i], thresholds[i - 1] + 1);
        }
    }
}
