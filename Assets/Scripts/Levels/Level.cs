using UnityEngine;

namespace ButchersGames.Levels
{
    public class Level : MonoBehaviour
    {
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform[] waypoints;

        // Only used by editor gizmos, so builds would report them as unused
#pragma warning disable 0414
        [Header("Gizmos")]
        [SerializeField] private Color waypointsGizmoColor = Color.yellow;
        [SerializeField] private float waypointGizmoRadius = 0.3f;
#pragma warning restore 0414

        // Exposed so a PlayerSpawner can place the player
        public Transform PlayerSpawnPoint => playerSpawnPoint;

        // Ordered path the player follows along the road
        public Transform[] Waypoints => waypoints;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (playerSpawnPoint != null)
            {
                Gizmos.color = Color.magenta;
                var m = Gizmos.matrix;
                Gizmos.matrix = playerSpawnPoint.localToWorldMatrix;
                Gizmos.DrawSphere(Vector3.up * 0.5f + Vector3.forward, 0.5f);
                Gizmos.DrawCube(Vector3.up * 0.5f, Vector3.one);
                Gizmos.matrix = m;
            }

            DrawWaypointsGizmo();
        }

        private void DrawWaypointsGizmo()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Gizmos.color = waypointsGizmoColor;
            Transform previous = null;
            for (int i = 0; i < waypoints.Length; i++)
            {
                Transform point = waypoints[i];
                if (point == null) continue;

                Gizmos.DrawSphere(point.position, waypointGizmoRadius);
                if (previous != null)
                    Gizmos.DrawLine(previous.position, point.position);
                previous = point;
            }
        }
#endif
    }
}
