using UnityEngine;

namespace ButchersGames.UI
{
    /// <summary>
    /// Animates an integer from one value to another over a fixed time (the "counting up" money on the result screens).
    /// Plain class: the owner calls Tick from its Update with the delta time it wants (usually unscaled).
    /// </summary>
    public sealed class CountUpTween
    {
        private int from;
        private int to;
        private float duration;
        private float time;

        public bool IsPlaying { get; private set; }

        /// <summary>Starts the animation. Returns false if there is nothing to animate (same values or no duration).</summary>
        public bool Play(int fromValue, int toValue, float seconds)
        {
            from = fromValue;
            to = toValue;
            duration = seconds;
            time = 0f;
            IsPlaying = fromValue != toValue && seconds > 0f;
            return IsPlaying;
        }

        public void Stop()
        {
            IsPlaying = false;
        }

        /// <summary>Advances the animation and returns the value to show.</summary>
        public int Tick(float deltaTime)
        {
            if (!IsPlaying) return to;

            time += deltaTime;
            float k = Mathf.Clamp01(time / duration);
            if (k >= 1f) IsPlaying = false;
            return Mathf.RoundToInt(Mathf.Lerp(from, to, k));
        }
    }
}
