using UnityEngine;

namespace ButchersGames.Gameplay.Collectibles
{
    /// <summary>Banknote: adds money.</summary>
    public class MoneyPickup : Collectible
    {
        // Applies to newly added components only, serialized values win afterwards
        public MoneyPickup()
        {
            value = 10;
        }

        protected override int GetMoneyDelta()
        {
            return Mathf.Abs(value);
        }
    }
}
