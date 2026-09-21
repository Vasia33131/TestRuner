using System.Collections.Generic;
using ButchersGames.Core;
using ButchersGames.Gameplay.Economy;
using ButchersGames.Levels;
using ButchersGames.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButchersGames.Gameplay.Checkpoints
{
    /// <summary>
    /// Saves the progress when a SaveZone is activated and restores it when the scene loads:
    /// money, the player position on the last checkpoint and the look of the zones already passed.
    /// </summary>
    // After PlayerController (-50) has bound the path, before CameraFollow (0) snaps to the player
    [DefaultExecutionOrder(-40)]
    public class CheckpointManager : MonoBehaviour
    {
        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private PlayerController player;

        [Tooltip("Zones of the level. Empty = every SaveZone in the scene.")]
        [SerializeField] private List<SaveZone> zones = new List<SaveZone>();

        [Tooltip("Zones with index -1 get their index from the order along the player's path")]
        [SerializeField] private bool orderZonesAlongPath = true;

        [SerializeField] private bool restoreOnLoad = true;
        [SerializeField] private bool restoreMoney = true;
        [SerializeField] private bool logToConsole = true;

        /// <summary>Index of the last saved zone, -1 if none.</summary>
        public int LastZoneIndex { get; private set; } = -1;

        private void OnEnable()
        {
            SaveZone.Activated += HandleZoneActivated;
        }

        private void OnDisable()
        {
            SaveZone.Activated -= HandleZoneActivated;
        }

        private void Start()
        {
            if (player == null)
                player = FindObjectOfType<PlayerController>(true);

            CollectZones();

            if (restoreOnLoad)
                Restore();
        }

        private void CollectZones()
        {
            zones.RemoveAll(z => z == null);
            if (zones.Count == 0)
                zones.AddRange(FindObjectsOfType<SaveZone>(true));

            Transform[] path = player != null ? player.Waypoints : null;
            if (orderZonesAlongPath && PathUtility.IsValid(path))
                zones.Sort((a, b) => PathUtility.GetProgress(path, a.transform.position)
                    .CompareTo(PathUtility.GetProgress(path, b.transform.position)));

            for (int i = 0; i < zones.Count; i++)
                zones[i].AssignIndex(i);
        }

        private void Restore()
        {
            if (!ProgressSave.TryLoad(Scope, out CheckpointData data)) return;

            LastZoneIndex = data.ZoneIndex;

            // Zones up to the checkpoint are already passed: show them activated and never save them again
            foreach (SaveZone zone in zones)
            {
                if (zone.ZoneIndex <= data.ZoneIndex)
                    zone.Activate(true);
            }

            if (player != null)
                player.PlaceOnPath(data.Position);

            ScoreManager score = ScoreManager.Instance;
            if (restoreMoney && score != null)
                score.SetMoney(data.Money);

            if (logToConsole)
                Debug.Log("[Checkpoint] Loaded zone " + data.ZoneIndex + ", money " + data.Money);
        }

        private void HandleZoneActivated(SaveZone zone)
        {
            // Never go back to an earlier checkpoint
            if (zone.ZoneIndex <= LastZoneIndex) return;
            LastZoneIndex = zone.ZoneIndex;

            var data = new CheckpointData
            {
                ZoneIndex = zone.ZoneIndex,
                Position = zone.SpawnPosition,
                Money = ScoreManager.Instance != null ? ScoreManager.Instance.Money : 0,
            };
            ProgressSave.Save(Scope, data);

            if (logToConsole)
                Debug.Log("[Checkpoint] Saved zone " + data.ZoneIndex + ", money " + data.Money, zone);
        }

        /// <summary>Deletes the checkpoint, the next load starts the level from the beginning.</summary>
        public void ClearSave()
        {
            ProgressSave.Clear();
            LastZoneIndex = -1;
            if (logToConsole)
                Debug.Log("[Checkpoint] Save cleared");
        }

        // A checkpoint belongs to one scene and one level
        private static string Scope
        {
            get
            {
                int level = LevelManager.Default != null ? LevelManager.Default.CurrentLevelIndex : 0;
                return SceneManager.GetActiveScene().name + "#" + level;
            }
        }
    }
}
