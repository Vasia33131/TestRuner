using ButchersGames.Core;
using UnityEngine;

namespace ButchersGames.Feedback
{
    /// <summary>Scene singleton that plays 2D sound effects through a pool of AudioSources.</summary>
    public class AudioManager : SceneSingleton<AudioManager>
    {
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 1f;
        [Tooltip("Max number of sounds playing at the same time")]
        [SerializeField] private int poolSize = 8;

        private RoundRobinPool<AudioSource> pool;

        /// <summary>Applied to sounds that start after the change.</summary>
        public float MasterVolume
        {
            get => masterVolume;
            set => masterVolume = Mathf.Clamp01(value);
        }

        protected override void OnInitialize()
        {
            pool = new RoundRobinPool<AudioSource>(poolSize, CreateSource, IsPlaying);
        }

        /// <summary>Plays a clip on a free pooled source. Volume is multiplied by the master volume.</summary>
        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || pool == null) return;

            AudioSource source = pool.Get();
            source.clip = clip;
            source.volume = masterVolume * volume;
            source.pitch = pitch;
            source.Play();
        }

        private AudioSource CreateSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            return source;
        }

        private static bool IsPlaying(AudioSource source)
        {
            return source.isPlaying;
        }
    }
}
