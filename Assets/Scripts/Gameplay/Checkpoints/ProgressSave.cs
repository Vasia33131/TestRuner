using UnityEngine;

namespace ButchersGames.Gameplay.Checkpoints
{
    /// <summary>
    /// Single checkpoint slot in PlayerPrefs. The slot is bound to a scope (scene + level),
    /// so a checkpoint of one level is never loaded into another.
    /// </summary>
    public static class ProgressSave
    {
        private const string Prefix = "Checkpoint.";
        private const string ScopeKey = Prefix + "Scope";
        private const string ZoneKey = Prefix + "Zone";
        private const string PosXKey = Prefix + "PosX";
        private const string PosYKey = Prefix + "PosY";
        private const string PosZKey = Prefix + "PosZ";
        private const string MoneyKey = Prefix + "Money";

        public static void Save(string scope, CheckpointData data)
        {
            PlayerPrefs.SetString(ScopeKey, scope);
            PlayerPrefs.SetInt(ZoneKey, data.ZoneIndex);
            PlayerPrefs.SetFloat(PosXKey, data.Position.x);
            PlayerPrefs.SetFloat(PosYKey, data.Position.y);
            PlayerPrefs.SetFloat(PosZKey, data.Position.z);
            PlayerPrefs.SetInt(MoneyKey, data.Money);
            PlayerPrefs.Save();
        }

        public static bool TryLoad(string scope, out CheckpointData data)
        {
            data = default;
            if (!PlayerPrefs.HasKey(ScopeKey) || PlayerPrefs.GetString(ScopeKey) != scope) return false;

            data.ZoneIndex = PlayerPrefs.GetInt(ZoneKey, -1);
            data.Position = new Vector3(
                PlayerPrefs.GetFloat(PosXKey),
                PlayerPrefs.GetFloat(PosYKey),
                PlayerPrefs.GetFloat(PosZKey));
            data.Money = PlayerPrefs.GetInt(MoneyKey);
            return data.ZoneIndex >= 0;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(ScopeKey);
            PlayerPrefs.DeleteKey(ZoneKey);
            PlayerPrefs.DeleteKey(PosXKey);
            PlayerPrefs.DeleteKey(PosYKey);
            PlayerPrefs.DeleteKey(PosZKey);
            PlayerPrefs.DeleteKey(MoneyKey);
            PlayerPrefs.Save();
        }
    }
}
