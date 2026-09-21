using UnityEngine;

namespace ButchersGames.Gameplay.Checkpoints
{
    /// <summary>Progress stored at a checkpoint.</summary>
    public struct CheckpointData
    {
        public int ZoneIndex;
        public Vector3 Position;
        public int Money;
    }
}
