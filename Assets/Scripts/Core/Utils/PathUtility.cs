using UnityEngine;

namespace ButchersGames.Core
{
    /// <summary>Helpers for the waypoint path the player runs along. Distances are measured on the XZ plane.</summary>
    public static class PathUtility
    {
        public static bool IsValid(Transform[] waypoints)
        {
            if (waypoints == null || waypoints.Length < 2) return false;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) return false;
            }
            return true;
        }

        /// <summary>Total length of the path.</summary>
        public static float GetLength(Transform[] waypoints)
        {
            if (!IsValid(waypoints)) return 0f;

            float length = 0f;
            for (int i = 1; i < waypoints.Length; i++)
                length += Horizontal(waypoints[i].position - waypoints[i - 1].position).magnitude;
            return length;
        }

        /// <summary>Distance along the path to the point of the path closest to the given position.</summary>
        public static float GetProgress(Transform[] waypoints, Vector3 position)
        {
            return Project(waypoints, position, out _, out _);
        }

        /// <summary>
        /// Finds the point of the path closest to the given position.
        /// segmentEnd is the index of the waypoint that ends the found segment (1..Length-1),
        /// point is the projected point (its height is interpolated between the waypoints).
        /// Returns the distance along the path to that point.
        /// </summary>
        public static float Project(Transform[] waypoints, Vector3 position, out int segmentEnd, out Vector3 point)
        {
            segmentEnd = 1;
            point = position;
            if (!IsValid(waypoints)) return 0f;

            float bestSqrDistance = float.MaxValue;
            float bestProgress = 0f;
            float travelled = 0f;

            for (int i = 1; i < waypoints.Length; i++)
            {
                Vector3 a = waypoints[i - 1].position;
                Vector3 b = waypoints[i].position;
                Vector3 ab = Horizontal(b - a);
                float sqrLength = ab.sqrMagnitude;

                float t = sqrLength > 1e-6f ? Mathf.Clamp01(Vector3.Dot(Horizontal(position - a), ab) / sqrLength) : 0f;
                Vector3 candidate = Vector3.Lerp(a, b, t);
                float sqrDistance = Horizontal(position - candidate).sqrMagnitude;

                // "<" keeps the earlier segment on ties, so a corner point belongs to the segment that ends there
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestProgress = travelled + Mathf.Sqrt(sqrLength) * t;
                    segmentEnd = i;
                    point = candidate;
                }

                travelled += Mathf.Sqrt(sqrLength);
            }

            return bestProgress;
        }

        /// <summary>Horizontal direction of travel at the path point closest to the given position.</summary>
        public static Vector3 GetDirection(Transform[] waypoints, Vector3 position)
        {
            if (!IsValid(waypoints)) return Vector3.zero;

            Project(waypoints, position, out int segmentEnd, out _);
            return GetSegmentDirection(waypoints, segmentEnd);
        }

        /// <summary>Horizontal direction of the segment that ends at the given waypoint (zero if degenerate or out of range).</summary>
        public static Vector3 GetSegmentDirection(Transform[] waypoints, int endIndex)
        {
            if (waypoints == null || endIndex <= 0 || endIndex >= waypoints.Length) return Vector3.zero;

            Vector3 ab = Horizontal(waypoints[endIndex].position - waypoints[endIndex - 1].position);
            return ab.sqrMagnitude > 1e-6f ? ab.normalized : Vector3.zero;
        }

        /// <summary>Point of the segment [endIndex - 1, endIndex] closest to the given position (on the XZ plane).</summary>
        public static Vector3 GetSegmentPoint(Transform[] waypoints, int endIndex, Vector3 position)
        {
            if (waypoints == null || endIndex <= 0 || endIndex >= waypoints.Length) return position;

            Vector3 a = waypoints[endIndex - 1].position;
            Vector3 ab = Horizontal(waypoints[endIndex].position - a);
            float sqrLength = ab.sqrMagnitude;
            if (sqrLength < 1e-6f)
                return new Vector3(a.x, position.y, a.z);

            float t = Mathf.Clamp01(Vector3.Dot(position - a, ab) / sqrLength);
            return a + ab * t;
        }

        private static Vector3 Horizontal(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
