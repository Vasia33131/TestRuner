using System.Collections;
using UnityEngine;

namespace ButchersGames.Gameplay.Checkpoints
{
    /// <summary>
    /// Raises a flag once: moves its Z (in the coordinates of Space) to the target value, 0 by default,
    /// and at the same time rotates it to the target rotation in the same coordinates.
    /// The motion is applied in world space, so the flag does not have to be a child of Space:
    /// a flag under a non-uniformly scaled zone would be stretched while it rotates.
    /// </summary>
    public class FlagRaiser : MonoBehaviour
    {
        [Tooltip("Object that moves. Defaults to this object.")]
        [SerializeField] private Transform target;

        [Tooltip("Coordinates the target values are given in (the zone the flag belongs to). Empty = the flag's parent.")]
        [SerializeField] private Transform space;

        [Tooltip("On: Z in Space becomes Target Local Z. Off: the flag moves by Rise Height along Rise Axis of Space.")]
        [SerializeField] private bool useTargetLocalZ = true;

        [Tooltip("Z of the raised flag in Space")]
        [SerializeField] private float targetLocalZ = 0f;

        [Tooltip("Only when Use Target Local Z is off: how far the flag moves, in the units of Space")]
        [SerializeField] private float riseHeight = 1.5f;

        [Tooltip("Only when Use Target Local Z is off: direction of the rise in Space")]
        [SerializeField] private Vector3 riseAxis = Vector3.forward;

        [Tooltip("Rotation the flag ends up with, relative to Space")]
        [SerializeField] private Vector3 targetLocalEuler = Vector3.zero;

        [SerializeField, Min(0f)] private float duration = 0.8f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public bool IsRaised { get; private set; }

        private Transform Target => target != null ? target : transform;

        /// <summary>
        /// Takes the flag out of the zone's hierarchy (keeping its world pose) and keeps the zone as Space,
        /// so the target values still mean the same.
        /// </summary>
        public void DetachFrom(Transform zone)
        {
            if (zone == null) return;
            if (space == null) space = zone;

            Transform t = Target;
            if (t.IsChildOf(zone) && t != zone)
                t.SetParent(zone.parent, true);
        }

        /// <summary>Starts the rise. instant = jump straight to the end (used when a save is loaded).</summary>
        public void Raise(bool instant)
        {
            if (IsRaised) return;
            IsRaised = true;

            Transform t = Target;
            Vector3 fromPosition = t.position;
            Quaternion fromRotation = t.rotation;
            GetTargetPose(t, out Vector3 toPosition, out Quaternion toRotation);

            // A coroutine cannot run on an inactive object, so the flag just snaps there
            if (instant || duration <= 0f || !isActiveAndEnabled)
            {
                t.SetPositionAndRotation(toPosition, toRotation);
                return;
            }

            StartCoroutine(Animate(t, fromPosition, toPosition, fromRotation, toRotation));
        }

        private void GetTargetPose(Transform t, out Vector3 position, out Quaternion rotation)
        {
            Transform s = space != null ? space : t.parent;
            Vector3 local = s != null ? s.InverseTransformPoint(t.position) : t.position;

            if (useTargetLocalZ)
                local.z = targetLocalZ;
            else
                local += riseAxis.normalized * riseHeight;

            position = s != null ? s.TransformPoint(local) : local;
            rotation = (s != null ? s.rotation : Quaternion.identity) * Quaternion.Euler(targetLocalEuler);
        }

        private IEnumerator Animate(Transform t, Vector3 fromPosition, Vector3 toPosition, Quaternion fromRotation, Quaternion toRotation)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(time / duration));
                t.SetPositionAndRotation(
                    Vector3.LerpUnclamped(fromPosition, toPosition, k),
                    Quaternion.SlerpUnclamped(fromRotation, toRotation, k));
                yield return null;
            }

            t.SetPositionAndRotation(toPosition, toRotation);
        }
    }
}
