using System;
using System.Collections.Generic;
using ButchersGames.Core;
using ButchersGames.Gameplay.Economy;
using ButchersGames.Player;
using UnityEngine;

namespace ButchersGames.Gameplay.Finish
{
    /// <summary>
    /// Doors at the level end. When the player gets close, measures the wealth and opens
    /// as many doors in a row as the thresholds allow. Then stops the player in front of the
    /// first closed door, or a bit after the last one if all of them are open.
    /// </summary>
    public class DoorsController : MonoBehaviour
    {
        public enum WealthSource
        {
            /// <summary>Current money from ScoreManager.</summary>
            Money,
            /// <summary>Status index from WealthTracker: 0 Poor, 1 Middle, 2 Rich, 3 Millionaire.</summary>
            WealthStatus
        }

        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private PlayerController player;

        [Tooltip("Doors in the order the player meets them")]
        [SerializeField] private List<Door> doors = new List<Door>();

        [Tooltip("Sort the doors by their position along the player's path at start")]
        [SerializeField] private bool sortDoorsAlongPath = true;

        [Header("Wealth")]
        [SerializeField] private WealthSource wealthSource = WealthSource.Money;

        [Tooltip("Door i opens when the wealth is at least thresholds[i], and only if all doors before it opened. " +
                 "Money: amount of money. WealthStatus: status index (1 = Middle, 2 = Rich, 3 = Millionaire).")]
        [SerializeField] private int[] thresholds = { 1, 100, 500, 1000 };

        [Tooltip("Only for WealthStatus. Found in the scene automatically if left empty.")]
        [SerializeField] private WealthTracker wealthTracker;

        [Header("Distances (along the path)")]
        [Tooltip("The doors are evaluated when the player is this close to the first door")]
        [SerializeField, Min(0f)] private float approachDistance = 15f;

        [Tooltip("The player stops this far before the first closed door")]
        [SerializeField, Min(0f)] private float stopDistanceBeforeDoor = 1.5f;

        [Tooltip("When every door is open, the finish is this far after the last door")]
        [SerializeField, Min(0f)] private float finishDistanceAfterLastDoor = 3f;

        [Header("Opening")]
        [Tooltip("Delay between the doors starting to open")]
        [SerializeField, Min(0f)] private float openInterval = 0.15f;

        [Header("Finish")]
        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private FinishController finish;

        [SerializeField] private bool logToConsole = true;

        /// <summary>Raised once when the doors are evaluated: opened doors and total doors.</summary>
        public event Action<int, int> OnDoorsEvaluated;

        public int OpenedCount { get; private set; }
        public bool IsEvaluated { get; private set; }

        private float[] doorProgress;
        private Vector3[] doorDirections;
        private float finishProgress;
        private bool pathReady;
        private bool finished;

        private void Start()
        {
            if (player == null)
                player = FindObjectOfType<PlayerController>(true);
            if (finish == null)
                finish = FindObjectOfType<FinishController>(true);
            if (wealthTracker == null)
                wealthTracker = FindObjectOfType<WealthTracker>(true);

            doors.RemoveAll(d => d == null);
            if (doors.Count == 0)
                Debug.LogWarning("[DoorsController] No doors assigned.", this);
        }

        private void Update()
        {
            if (finished || player == null || !player.IsRunning || doors.Count == 0) return;
            if (!pathReady && !PreparePath()) return;

            float progress = player.PathProgress;

            if (!IsEvaluated && progress >= doorProgress[0] - approachDistance)
                Evaluate();

            if (IsEvaluated && progress >= finishProgress)
                Finish();
        }

        // Done lazily: the path is bound by the player and may appear after Start
        private bool PreparePath()
        {
            Transform[] path = player.Waypoints;
            if (!PathUtility.IsValid(path)) return false;

            if (sortDoorsAlongPath)
                doors.Sort((a, b) => PathUtility.GetProgress(path, a.Center).CompareTo(PathUtility.GetProgress(path, b.Center)));

            // Measured before any door moves
            doorProgress = new float[doors.Count];
            doorDirections = new Vector3[doors.Count];
            for (int i = 0; i < doors.Count; i++)
            {
                doorProgress[i] = PathUtility.GetProgress(path, doors[i].Center);
                doorDirections[i] = PathUtility.GetDirection(path, doors[i].Center);
            }
            pathReady = true;
            return true;
        }

        /// <summary>Measures the wealth and opens the doors. Called automatically on approach.</summary>
        public void Evaluate()
        {
            if (IsEvaluated || doors.Count == 0 || player == null) return;
            if (!pathReady && !PreparePath()) return;
            IsEvaluated = true;

            int wealth = GetWealth();
            OpenedCount = CountDoorsToOpen(wealth);

            for (int i = 0; i < OpenedCount; i++)
                doors[i].Open(i * openInterval, doorDirections[i]);

            finishProgress = GetFinishProgress();

            if (logToConsole)
                Debug.Log("[Doors] " + wealthSource + " = " + wealth + ", open " + OpenedCount + " of " + doors.Count, this);
            OnDoorsEvaluated?.Invoke(OpenedCount, doors.Count);
        }

        private int GetWealth()
        {
            if (wealthSource == WealthSource.WealthStatus && wealthTracker != null)
                return (int)wealthTracker.CurrentStatus;

            if (wealthSource == WealthSource.WealthStatus)
                Debug.LogWarning("[DoorsController] WealthTracker not found, falling back to money.", this);
            return ScoreManager.Instance != null ? ScoreManager.Instance.Money : 0;
        }

        private int CountDoorsToOpen(int wealth)
        {
            int count = 0;
            int limit = Mathf.Min(doors.Count, thresholds != null ? thresholds.Length : 0);
            while (count < limit && wealth >= thresholds[count])
                count++;
            return count;
        }

        private float GetFinishProgress()
        {
            float result = OpenedCount < doors.Count
                ? doorProgress[OpenedCount] - stopDistanceBeforeDoor
                : doorProgress[doors.Count - 1] + finishDistanceAfterLastDoor;

            // Must come before the player's own end of path, otherwise the old finish would fire first
            float pathEnd = PathUtility.GetLength(player.Waypoints) - player.WaypointReachDistance - 0.1f;
            return Mathf.Min(result, pathEnd);
        }

        private void Finish()
        {
            finished = true;
            if (finish != null)
                finish.Finish(OpenedCount, doors.Count);
            else
            {
                Debug.LogWarning("[DoorsController] FinishController not found, only stopping the player.", this);
                player.StopRun();
            }
        }

        private void OnValidate()
        {
            if (thresholds == null) return;
            for (int i = 1; i < thresholds.Length; i++)
            {
                if (thresholds[i] < thresholds[i - 1])
                {
                    Debug.LogWarning("[DoorsController] Thresholds should go up, door " + i + " is cheaper than door " + (i - 1) + ".", this);
                    break;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i] != null)
                    Gizmos.DrawWireSphere(doors[i].Center, 0.5f + 0.1f * i);
            }
        }
    }
}
