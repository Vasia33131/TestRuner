using UnityEngine;

namespace ButchersGames.Core
{
    /// <summary>
    /// Base class for a component that exists once per scene. A second instance removes itself.
    /// Derived classes put their Awake logic into OnInitialize, it runs only for the kept instance.
    /// </summary>
    public abstract class SceneSingleton<T> : MonoBehaviour where T : SceneSingleton<T>
    {
        public static T Instance { get; private set; }

        /// <summary>False for a duplicate that is about to be destroyed.</summary>
        protected bool IsInstance => Instance == this;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[" + typeof(T).Name + "] Duplicate instance removed.", this);
                Destroy(this);
                return;
            }

            Instance = (T)this;
            OnInitialize();
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected virtual void OnInitialize() { }
    }
}
