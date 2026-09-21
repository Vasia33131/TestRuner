using UnityEngine;

namespace ButchersGames.Core
{
    public static class ColliderExtensions
    {
        /// <summary>
        /// True if the collider belongs to the player. The collider may sit on a child
        /// while the tag is on the object with the Rigidbody.
        /// </summary>
        public static bool IsPlayer(this Collider other)
        {
            if (other == null) return false;
            if (other.CompareTag(GameTags.Player)) return true;

            Rigidbody body = other.attachedRigidbody;
            return body != null && body.CompareTag(GameTags.Player);
        }
    }
}
