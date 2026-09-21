using System;
using System.Collections.Generic;
using ButchersGames.Core;
using UnityEngine;

namespace ButchersGames.Gameplay.Checkpoints
{
    /// <summary>
    /// Checkpoint zone (the yellow strip across the road). Fires once when the player enters it:
    /// raises its flag, switches the linked placeholder materials and reports itself so the
    /// CheckpointManager saves the progress. Entering an activated zone again does nothing.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SaveZone : MonoBehaviour
    {
        /// <summary>Raised once per zone when the player activates it (not when a save is restored).</summary>
        public static event Action<SaveZone> Activated;

        [Tooltip("Order of the zone in the level, 0 = first. -1 = assigned by CheckpointManager from the order along the path.")]
        [SerializeField] private int zoneIndex = -1;

        [Tooltip("Where the player respawns. Empty = the zone position (snapped to the road).")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Flags of this zone. Empty = collected from the children (see below).")]
        [SerializeField] private List<FlagRaiser> flags = new List<FlagRaiser>();

        [Tooltip("When the list is empty: use FlagRaisers in the children, or add one to every direct child that has none")]
        [SerializeField] private bool flagsFromChildren = true;

        [Tooltip("Placeholder objects switched by this zone")]
        [SerializeField] private List<MaterialSwapTarget> linkedTargets = new List<MaterialSwapTarget>();

        [Tooltip("When the list above is empty, switch every MaterialSwapTarget in the scene that is not linked to any zone")]
        [SerializeField] private bool useUnlinkedTargetsWhenEmpty = true;

        [Tooltip("The trigger is made at least this tall (world units), so a flat strip still catches the player")]
        [SerializeField, Min(0f)] private float minTriggerHeight = 4f;

        public int ZoneIndex => zoneIndex;
        public bool IsActivated { get; private set; }
        public IReadOnlyList<FlagRaiser> Flags => flags;
        public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

        private void Awake()
        {
            SetupTrigger();
            CollectFlags();
        }

        private void CollectFlags()
        {
            flags.RemoveAll(f => f == null);
            if (flags.Count == 0 && flagsFromChildren)
            {
                flags.AddRange(GetComponentsInChildren<FlagRaiser>(true));

                // Flags are placed as children of the zone without any component
                if (flags.Count == 0)
                {
                    foreach (Transform child in transform)
                        flags.Add(child.gameObject.AddComponent<FlagRaiser>());
                }
            }

            // The strip is scaled non-uniformly, a flag left under it is stretched while it rotates
            foreach (FlagRaiser flag in flags)
                flag.DetachFrom(transform);
        }

        /// <summary>Used by CheckpointManager for zones left at -1.</summary>
        public void AssignIndex(int index)
        {
            if (zoneIndex < 0) zoneIndex = index;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsActivated || !other.IsPlayer()) return;
            Activate(false);
        }

        /// <summary>
        /// Plays the activation. instant = no animation and no save notification
        /// (used to restore the look of zones that were passed before the loaded checkpoint).
        /// </summary>
        public void Activate(bool instant)
        {
            if (IsActivated) return;
            IsActivated = true;

            foreach (FlagRaiser flag in flags)
            {
                if (flag != null) flag.Raise(instant);
            }

            foreach (MaterialSwapTarget target in GetTargets())
                target.Apply();

            if (!instant)
                Activated?.Invoke(this);
        }

        private IEnumerable<MaterialSwapTarget> GetTargets()
        {
            bool hasLinks = false;
            foreach (MaterialSwapTarget target in linkedTargets)
            {
                if (target == null) continue;
                hasLinks = true;
                yield return target;
            }

            if (hasLinks || !useUnlinkedTargetsWhenEmpty) yield break;

            var linkedElsewhere = new HashSet<MaterialSwapTarget>();
            foreach (SaveZone zone in FindObjectsOfType<SaveZone>(true))
                linkedElsewhere.UnionWith(zone.linkedTargets);

            foreach (MaterialSwapTarget target in FindObjectsOfType<MaterialSwapTarget>(true))
            {
                if (!linkedElsewhere.Contains(target))
                    yield return target;
            }
        }

        private void SetupTrigger()
        {
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider is MeshCollider meshCollider)
                meshCollider.convex = true;   // a mesh trigger has to be convex
            zoneCollider.isTrigger = true;

            if (!(zoneCollider is BoxCollider box) || minTriggerHeight <= 0f) return;

            float scaleY = Mathf.Abs(transform.lossyScale.y);
            if (scaleY < 1e-5f || box.size.y * scaleY >= minTriggerHeight) return;

            // Grow upwards from slightly below the strip, so the player's collider always overlaps it
            float height = minTriggerHeight / scaleY;
            float bottom = box.center.y - box.size.y * 0.5f - height * 0.1f;
            box.size = new Vector3(box.size.x, height, box.size.z);
            box.center = new Vector3(box.center.x, bottom + height * 0.5f, box.center.z);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsActivated ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(SpawnPosition, 0.4f);
        }
    }
}
