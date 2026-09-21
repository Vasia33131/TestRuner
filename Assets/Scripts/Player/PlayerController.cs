using System;
using ButchersGames.Core;
using ButchersGames.Levels;
using UnityEngine;

namespace ButchersGames.Player
{
    /// <summary>
    /// Runs the player along the waypoint path of the current level. The player steers sideways
    /// inside the road (relative to the current segment), the model leans into the road turns.
    /// </summary>
    // Runs before CameraFollow so the camera snaps to the final start position
    [DefaultExecutionOrder(-50)]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Forward running speed")]
        [SerializeField] private float forwardSpeed = 10f;

        [Tooltip("Lateral movement speed")]
        [SerializeField] private float horizontalSpeed = 12f;

        [Tooltip("Side limit (half of the road width)")]
        [SerializeField] private float limitX = 3.5f;

        [Tooltip("Character model turn speed")]
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Path Following")]
        [Tooltip("Distance to a waypoint at which the next one becomes the target")]
        [SerializeField] private float waypointReachDistance = 1.5f;

        [Tooltip("Distance to a waypoint at which the heading starts blending towards the next segment")]
        [SerializeField] private float cornerBlendDistance = 3f;

        [Tooltip("How strongly the heading is blended towards the next segment (0..1)")]
        [Range(0f, 1f)]
        [SerializeField] private float cornerBlendStrength = 0.5f;

        [Header("Input Settings")]
        [SerializeField] private float swipeSensitivity = 15f;

        [Tooltip("Multiplier applied to the normalized swipe delta")]
        [SerializeField] private float swipeMultiplier = 3f;

        [Tooltip("Lateral speed (units per second) when steering with the keyboard")]
        [SerializeField] private float keyboardSteerSpeed = 8f;

        [Header("Roll (lean into the turn)")]
        [Tooltip("Maximum lean angle in degrees")]
        [SerializeField] private float maxRollAngle = 15f;

        [Tooltip("How fast the lean follows its target")]
        [SerializeField] private float rollSmoothing = 8f;

        [Tooltip("Lean contribution of the road turning, in degrees per (degree/second) of heading change")]
        [SerializeField] private float turnRollFactor = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool isRunning;
        [SerializeField] private float gizmoPointRadius = 0.2f;

        /// <summary>Raised once when the last waypoint has been passed.</summary>
        public event Action OnFinished;

        /// <summary>Path of the current level (taken from Level.Waypoints, not from the inspector).</summary>
        public Transform[] Waypoints { get; private set; }

        public bool IsRunning => isRunning;

        public float WaypointReachDistance => waypointReachDistance;

        /// <summary>Distance travelled along the path (0 if the path is not bound).</summary>
        public float PathProgress => HasPath ? PathUtility.GetProgress(Waypoints, transform.position) : 0f;

        private bool HasPath => PathUtility.IsValid(Waypoints);

        private readonly SteeringInput steering = new SteeringInput();
        private LevelManager subscribedManager;

        private int currentWaypointIndex;
        private float targetLateral;
        private float currentLateral;
        private float lateralVelocity;   // used by SmoothDamp

        private Quaternion headingRotation = Quaternion.identity;   // yaw only, roll is applied on top
        private float currentRoll;

        private void Start()
        {
            isRunning = false;
            BindToCurrentLevel();

            subscribedManager = LevelManager.Default;
            if (subscribedManager != null)
                subscribedManager.OnLevelStarted += StartRun;
            else
                Debug.LogWarning("[PlayerController] LevelManager not found, call StartRun() manually.", this);
        }

        private void OnDestroy()
        {
            if (subscribedManager != null)
                subscribedManager.OnLevelStarted -= StartRun;
            subscribedManager = null;
        }

        private void Update()
        {
            if (!isRunning) return;

            float delta = steering.ReadLateralDelta(swipeSensitivity * swipeMultiplier, keyboardSteerSpeed, Time.deltaTime);
            targetLateral = Mathf.Clamp(targetLateral + delta, -limitX, limitX);

            MoveAlongPath();
        }

        #region Run control

        public void StartRun()
        {
            // The level may have been created after Start, so bind lazily
            if (!HasPath) BindToCurrentLevel();
            steering.Reset();
            isRunning = true;
        }

        public void StopRun() => isRunning = false;

        private void Finish()
        {
            StopRun();
            OnFinished?.Invoke();
        }

        #endregion

        #region Path binding

        /// <summary>
        /// Takes the waypoints from the level currently placed in the scene and puts the player on the start of the path.
        /// </summary>
        public bool BindToCurrentLevel()
        {
            Level level = FindCurrentLevel();
            if (level == null)
            {
                Debug.LogError("[PlayerController] No Level found in the scene, cannot get waypoints!", this);
                return false;
            }

            return BindToLevel(level);
        }

        public bool BindToLevel(Level level)
        {
            Waypoints = level != null ? level.Waypoints : null;

            if (!HasPath)
            {
                Debug.LogError("[PlayerController] Level has no valid waypoints (need at least 2, none of them missing)!", level);
                Waypoints = null;
                return false;
            }

            PlaceAt(Waypoints[0].position, PathUtility.GetSegmentDirection(Waypoints, 1), 1);
            return true;
        }

        /// <summary>
        /// Puts the player on the path at the point closest to the given position, facing along the road.
        /// Used to respawn on a checkpoint. Does not start the run.
        /// </summary>
        public bool PlaceOnPath(Vector3 worldPosition)
        {
            if (!HasPath && !BindToCurrentLevel()) return false;

            PathUtility.Project(Waypoints, worldPosition, out int segmentEnd, out Vector3 point);
            PlaceAt(point, PathUtility.GetSegmentDirection(Waypoints, segmentEnd), segmentEnd);
            return true;
        }

        private void PlaceAt(Vector3 position, Vector3 direction, int nextWaypoint)
        {
            transform.position = position;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            headingRotation = transform.rotation;
            currentRoll = 0f;
            currentWaypointIndex = nextWaypoint;

            targetLateral = 0f;
            currentLateral = 0f;
            lateralVelocity = 0f;
        }

        private static Level FindCurrentLevel()
        {
            LevelManager manager = LevelManager.Default;
            Level level = manager != null ? manager.ActiveLevel : null;
            return level != null ? level : FindObjectOfType<Level>();
        }

        #endregion

        #region Movement

        private void MoveAlongPath()
        {
            if (!HasPath || currentWaypointIndex >= Waypoints.Length) return;

            // === 1. Road direction at the current point ===
            Vector3 toTarget = HorizontalOffsetTo(currentWaypointIndex);

            // Switch to the next waypoint slightly early so the heading
            // changes smoothly instead of in a jerk
            if (toTarget.magnitude < waypointReachDistance)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= Waypoints.Length)
                {
                    Finish();
                    return;
                }
                toTarget = HorizontalOffsetTo(currentWaypointIndex);
            }

            Vector3 segmentDir = PathUtility.GetSegmentDirection(Waypoints, currentWaypointIndex);
            Vector3 moveDir = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : segmentDir;
            if (segmentDir == Vector3.zero) segmentDir = moveDir;
            if (moveDir == Vector3.zero) moveDir = HorizontalForward();

            // The model faces along the road only, so swiping sideways never turns it
            Vector3 headingDir = segmentDir != Vector3.zero ? segmentDir : moveDir;

            // === 2. Blend in the next segment direction (corner smoothing) ===
            Vector3 nextDir = PathUtility.GetSegmentDirection(Waypoints, currentWaypointIndex + 1);
            if (nextDir != Vector3.zero)
            {
                // The closer to the waypoint, the stronger the influence of nextDir
                float blend = 1f - Mathf.Clamp01(toTarget.magnitude / Mathf.Max(cornerBlendDistance, 0.01f));
                moveDir = Vector3.Slerp(moveDir, nextDir, blend * cornerBlendStrength).normalized;
                headingDir = Vector3.Slerp(headingDir, nextDir, blend * cornerBlendStrength).normalized;
            }

            // === 3. Forward step + lateral offset (relative to the current road segment, not to the world) ===
            transform.position += moveDir * (forwardSpeed * Time.deltaTime) + GetLateralCorrection(segmentDir);

            // === 4. Smooth model rotation + roll into the turn ===
            UpdateRotation(headingDir);
        }

        private Vector3 GetLateralCorrection(Vector3 segmentDir)
        {
            Vector3 lateralAxis = Vector3.Cross(Vector3.up, segmentDir).normalized;

            currentLateral = Mathf.SmoothDamp(
                currentLateral,
                targetLateral,
                ref lateralVelocity,
                1f / Mathf.Max(horizontalSpeed, 0.01f));

            Vector3 roadCenter = PathUtility.GetSegmentPoint(Waypoints, currentWaypointIndex, transform.position);
            float lateralNow = Vector3.Dot(transform.position - roadCenter, lateralAxis);
            return lateralAxis * (currentLateral - lateralNow);
        }

        private void UpdateRotation(Vector3 headingDir)
        {
            Quaternion previousHeading = headingRotation;
            headingRotation = Quaternion.Slerp(
                headingRotation,
                Quaternion.LookRotation(headingDir, Vector3.up),
                Time.deltaTime * rotationSpeed);

            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            float yawRate = Mathf.DeltaAngle(previousHeading.eulerAngles.y, headingRotation.eulerAngles.y) / dt;

            // Lean only when the road turns, not when the player steers sideways
            float targetRoll = Mathf.Clamp(yawRate * turnRollFactor, -maxRollAngle, maxRollAngle);
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, 1f - Mathf.Exp(-rollSmoothing * Time.deltaTime));

            // Positive roll = lean to the right, which is a negative rotation around local Z
            transform.rotation = headingRotation * Quaternion.Euler(0f, 0f, -currentRoll);
        }

        private Vector3 HorizontalOffsetTo(int waypointIndex)
        {
            Vector3 offset = Waypoints[waypointIndex].position - transform.position;
            offset.y = 0f;
            return offset;
        }

        private Vector3 HorizontalForward()
        {
            Vector3 f = headingRotation * Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        #endregion

        private void OnDrawGizmos()
        {
            if (Waypoints == null || Waypoints.Length < 2) return;

            Gizmos.color = Color.yellow;
            for (int i = 0; i < Waypoints.Length - 1; i++)
            {
                if (Waypoints[i] == null || Waypoints[i + 1] == null) continue;
                Gizmos.DrawLine(Waypoints[i].position, Waypoints[i + 1].position);
                Gizmos.DrawSphere(Waypoints[i].position, gizmoPointRadius);
            }

            Transform last = Waypoints[Waypoints.Length - 1];
            if (last != null) Gizmos.DrawSphere(last.position, gizmoPointRadius);
        }
    }
}
