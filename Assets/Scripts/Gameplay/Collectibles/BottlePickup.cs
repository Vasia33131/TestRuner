using UnityEngine;

namespace ButchersGames.Gameplay.Collectibles
{
    /// <summary>Bottle: takes money away.</summary>
    public class BottlePickup : Collectible
    {
        // Applies to newly added components only, serialized values win afterwards
        public BottlePickup()
        {
            value = 20;
        }

        protected override int GetMoneyDelta()
        {
            return -Mathf.Abs(value);
        }
    }
}
