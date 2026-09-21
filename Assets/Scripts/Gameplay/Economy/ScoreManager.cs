using System;
using ButchersGames.Core;
using ButchersGames.Gameplay.Collectibles;
using UnityEngine;

namespace ButchersGames.Gameplay.Economy
{
    /// <summary>Scene singleton that keeps the money counter. Money never goes below zero.</summary>
    [DefaultExecutionOrder(-100)]
    public class ScoreManager : SceneSingleton<ScoreManager>
    {
        [SerializeField] private bool logToConsole = true;

        /// <summary>Raised with the new amount of money whenever it changes or is reset.</summary>
        public event Action<int> OnMoneyChanged;

        public int Money { get; private set; }

        private void OnEnable()
        {
            Collectible.OnCollected += HandleCollected;
        }

        private void OnDisable()
        {
            Collectible.OnCollected -= HandleCollected;
        }

        private void HandleCollected(Collectible item, int delta)
        {
            // A duplicate that is about to be destroyed must not count pickups twice
            if (!IsInstance) return;
            Add(delta);
        }

        public void Add(int delta)
        {
            SetMoney(Money + delta);
        }

        /// <summary>Sets the exact amount (used by a checkpoint restore and the finish bonus).</summary>
        public void SetMoney(int amount)
        {
            int newMoney = Mathf.Max(0, amount);
            if (newMoney == Money) return;

            int delta = newMoney - Money;
            Money = newMoney;
            if (logToConsole)
                Debug.Log("[Score] " + (delta >= 0 ? "+" : "") + delta + " -> " + Money);
            OnMoneyChanged?.Invoke(Money);
        }

        public void ResetScore()
        {
            Money = 0;
            if (logToConsole)
                Debug.Log("[Score] Reset -> 0");
            OnMoneyChanged?.Invoke(Money);
        }
    }
}
