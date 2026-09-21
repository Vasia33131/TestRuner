using System;

namespace ButchersGames.Gameplay.Economy
{
    /// <summary>Reward math shared by the win and finish screens.</summary>
    public static class RewardCalculator
    {
        /// <summary>Money multiplied by the multiplier (at least 1), clamped to int.MaxValue instead of overflowing.</summary>
        public static int Multiply(int money, int multiplier)
        {
            long result = (long)Math.Max(0, money) * Math.Max(1, multiplier);
            return (int)Math.Min(result, int.MaxValue);
        }
    }
}
