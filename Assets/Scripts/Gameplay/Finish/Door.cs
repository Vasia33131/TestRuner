using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ButchersGames.Gameplay.Finish
{
    /// <summary>
    /// One door of the level end. By default its leaves swing open around their hinges (the pivots of the leaves),
    /// away from the player. Can also slide the whole door or the given parts.
    /// </summary>
    public class Door : MonoBehaviour
    {
        public enum OpenMode
        {
            Swing,
            Slide
        }

        [SerializeField] private OpenMode mode = OpenMode.Swing;

        [Tooltip("Parts that move (door leaves). Empty = found automatically: children whose pivot sits at the edge " +
                 "of their mesh (a hinge). If nothing is found the door slides as a whole.")]
        [SerializeField] private Transform[] movingParts;

        [Header("Swing")]
        [Tooltip("How far each leaf opens, degrees")]
        [SerializeField, Range(0f, 180f)] private float swingAngle = 100f;

        [Tooltip("Leaves open away from the player (the direction passed by DoorsController)")]
        [SerializeField] private bool openAwayFromPlayer = true;

        [Tooltip("A pivot this far from the mesh center (share of the mesh width) counts as a hinge")]
        [SerializeField, Range(0.1f, 0.5f)] private float hingeDetection = 0.3f;

        [Header("Slide")]
        [Tooltip("World direction of the slide (down = the door sinks under the road)")]
        [SerializeField] private Vector3 slideDirection = Vector3.down;

        [Tooltip("Slide by the size of the renderers along the direction, plus the margin")]
        [SerializeField] private bool slideBySize = true;

        [SerializeField, Min(0f)] private float slideDistance = 4f;
        [SerializeField, Min(0f)] private float slideMargin = 0.3f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float duration = 1f;
        [Tooltip("Ease-out with a small overshoot, like a pushed door")]
        [SerializeField] private AnimationCurve ease = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.5f),
            new Keyframe(0.75f, 1.04f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("Turn off solid colliders of the door once it is open")]
        [SerializeField] private bool disableCollidersWhenOpen = true;

        public bool IsOpen { get; private set; }

        /// <summary>Center of the door renderers (the pivot of an imported model may be off).</summary>
        public Vector3 Center => TryGetBounds(GetComponentsInChildren<Renderer>(), out Bounds bounds) ? bounds.center : transform.position;

        /// <summary>
        /// Starts opening after the delay. pushDirection = where the player is going (world),
        /// the leaves swing that way. Does nothing if the door is already open.
        /// </summary>
        public void Open(float delay = 0f, Vector3 pushDirection = default)
        {
            if (IsOpen) return;
            IsOpen = true;

            if (!isActiveAndEnabled)
            {
                ApplyOpen(1f, CaptureStart(pushDirection));
                return;
            }

            StartCoroutine(Animate(delay, pushDirection));
        }

        private IEnumerator Animate(float delay, Vector3 pushDirection)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            PartState[] start = CaptureStart(pushDirection);
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                ApplyOpen(ease.Evaluate(Mathf.Clamp01(time / duration)), start);
                yield return null;
            }

            ApplyOpen(1f, start);

            if (disableCollidersWhenOpen)
            {
                foreach (Collider c in GetComponentsInChildren<Collider>())
                {
                    if (!c.isTrigger) c.enabled = false;
                }
            }
        }

        private struct PartState
        {
            public Transform Part;
            public bool Swing;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Angle;         // signed swing angle around world up
            public Vector3 SlideOffset; // world
        }

        private PartState[] CaptureStart(Vector3 pushDirection)
        {
            List<Transform> parts = GetParts(out bool swing);
            Vector3 slideOffset = swing ? Vector3.zero : slideDirection.normalized * GetSlideDistance();

            var states = new PartState[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                Transform part = parts[i];
                states[i] = new PartState
                {
                    Part = part,
                    Swing = swing,
                    Position = part.position,
                    Rotation = part.rotation,
                    Angle = swing ? swingAngle * GetSwingSign(part, pushDirection, i) : 0f,
                    SlideOffset = slideOffset,
                };
            }
            return states;
        }

        private List<Transform> GetParts(out bool swing)
        {
            var parts = new List<Transform>();
            if (movingParts != null)
            {
                foreach (Transform part in movingParts)
                {
                    if (part != null) parts.Add(part);
                }
            }

            if (mode == OpenMode.Slide)
            {
                if (parts.Count == 0) parts.Add(transform);
                swing = false;
                return parts;
            }

            if (parts.Count == 0)
                FindHingedParts(transform, parts);

            swing = parts.Count > 0;
            if (!swing)
            {
                Debug.LogWarning("[Door] No leaves with a hinge found, sliding the door instead. Assign Moving Parts.", this);
                parts.Add(transform);
            }
            return parts;
        }

        // A leaf has its pivot on the edge of its mesh, the frame and signs have it in the middle
        private void FindHingedParts(Transform root, List<Transform> result)
        {
            foreach (Transform child in root)
            {
                Renderer r = child.GetComponent<Renderer>();
                if (r != null)
                {
                    Bounds b = r.bounds;
                    Vector3 offset = b.center - child.position;
                    offset.y = 0f;
                    float width = Mathf.Max(b.size.x, b.size.z);
                    if (width > 1e-3f && offset.magnitude >= width * hingeDetection)
                    {
                        result.Add(child);
                        continue;
                    }
                }
                FindHingedParts(child, result);
            }
        }

        // Picks the way the leaf center moves along the push direction
        private float GetSwingSign(Transform part, Vector3 pushDirection, int index)
        {
            pushDirection.y = 0f;
            if (!openAwayFromPlayer || pushDirection.sqrMagnitude < 1e-6f)
                return index % 2 == 0 ? 1f : -1f;   // mirrored leaves open to the same side

            Renderer r = part.GetComponent<Renderer>();
            Vector3 arm = (r != null ? r.bounds.center : part.position) - part.position;
            arm.y = 0f;
            Vector3 moved = Quaternion.AngleAxis(swingAngle, Vector3.up) * arm - arm;
            return Vector3.Dot(moved, pushDirection) >= 0f ? 1f : -1f;
        }

        private static void ApplyOpen(float k, PartState[] states)
        {
            foreach (PartState s in states)
            {
                if (s.Part == null) continue;

                if (s.Swing)
                    s.Part.rotation = Quaternion.AngleAxis(s.Angle * k, Vector3.up) * s.Rotation;
                else
                    s.Part.position = s.Position + s.SlideOffset * k;
            }
        }

        private float GetSlideDistance()
        {
            if (!slideBySize || !TryGetBounds(GetComponentsInChildren<Renderer>(), out Bounds bounds)) return slideDistance;

            Vector3 dir = slideDirection.normalized;
            Vector3 size = bounds.size;
            float extent = Mathf.Abs(dir.x) * size.x + Mathf.Abs(dir.y) * size.y + Mathf.Abs(dir.z) * size.z;
            return extent + slideMargin;
        }

        private static bool TryGetBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer r in renderers)
            {
                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return found;
        }
    }
}
