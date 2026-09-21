using System;
using ButchersGames.Core;
using UnityEngine;

namespace ButchersGames.Gameplay.Collectibles
{
    /// <summary>
    /// Base class of everything the player picks up by running through it.
    /// The object needs a trigger collider and the player needs a (kinematic) Rigidbody.
    /// </summary>
    public abstract class Collectible : MonoBehaviour
    {
        /// <summary>Raised on pickup: the item and the money delta (positive or negative).</summary>
        public static event Action<Collectible, int> OnCollected;

        [SerializeField] protected int value = 10;

        public int Value => value;

        private bool collected;

        /// <summary>Money delta this item applies to the score.</summary>
        protected abstract int GetMoneyDelta();

        private void OnEnable()
        {
            collected = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || !other.IsPlayer()) return;
            Collect();
        }

        public void Collect()
        {
            if (collected) return;
            collected = true;

            OnCollected?.Invoke(this, GetMoneyDelta());
            gameObject.SetActive(false);
        }
    }
}
