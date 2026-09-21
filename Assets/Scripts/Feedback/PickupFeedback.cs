using ButchersGames.Core;
using ButchersGames.Gameplay.Collectibles;
using ButchersGames.Gameplay.Economy;
using ButchersGames.Player;
using UnityEngine;

namespace ButchersGames.Feedback
{
    /// <summary>
    /// Pickup feedback: floating "+10 $" / "-20 $" text over the player, a particle burst at the item
    /// and a sound. Listens to Collectible.OnCollected and WealthTracker.OnStatusChanged,
    /// gameplay classes know nothing about it. All effects come from pools created in Awake.
    /// </summary>
    public class PickupFeedback : MonoBehaviour
    {
        [Header("Floating text")]
        [SerializeField] private FloatingText floatingTextPrefab;
        [SerializeField] private int textPoolSize = 8;
        [Tooltip("TMP SetText format, {0:0} is the absolute amount")]
        [SerializeField] private string gainFormat = "+{0:0} $";
        [SerializeField] private string lossFormat = "-{0:0} $";
        [SerializeField] private Color gainColor = new Color(0.25f, 1f, 0.35f, 1f);
        [SerializeField] private Color lossColor = new Color(1f, 0.25f, 0.25f, 1f);
        [Tooltip("Start height above the top of the player collider")]
        [SerializeField] private float textHeightMargin = 0.9f;
        [Tooltip("Used when the player has no collider")]
        [SerializeField] private float fallbackPlayerHeight = 2f;
        [SerializeField] private float textHorizontalJitter = 0.15f;

        [Header("Particles")]
        [SerializeField] private ParticleSystem coinBurstPrefab;
        [SerializeField] private ParticleSystem bottleBurstPrefab;
        [SerializeField] private int particlePoolSize = 8;

        [Header("Audio")]
        [SerializeField] private AudioClip coinClip;
        [SerializeField] private AudioClip bottleClip;
        [SerializeField] private AudioClip statusUpClip;
        [Range(0f, 1f)] [SerializeField] private float coinVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float bottleVolume = 0.9f;
        [Range(0f, 1f)] [SerializeField] private float statusUpVolume = 1f;
        [Tooltip("Random pitch offset (+/-) for pickup sounds, keeps fast streaks from sounding identical")]
        [SerializeField] private float pitchJitter = 0.05f;

        private RoundRobinPool<FloatingText> textPool;
        private RoundRobinPool<ParticleSystem> coinPool;
        private RoundRobinPool<ParticleSystem> bottlePool;

        private WealthTracker tracker;
        private WealthStatus lastStatus;
        private Transform player;
        private Collider playerCollider;

        private void Awake()
        {
            if (floatingTextPrefab != null)
                textPool = new RoundRobinPool<FloatingText>(textPoolSize, CreateText, IsActive);
            else
                Debug.LogWarning("[PickupFeedback] FloatingText prefab is not assigned, run Tools/Runner/Setup Feedback.", this);

            coinPool = CreateParticlePool(coinBurstPrefab);
            bottlePool = CreateParticlePool(bottleBurstPrefab);
        }

        private void OnEnable()
        {
            Collectible.OnCollected += HandleCollected;
            if (tracker != null)
                tracker.OnStatusChanged += HandleStatusChanged;
        }

        private void Start()
        {
            if (tracker != null) return;

            tracker = FindObjectOfType<WealthTracker>(true);
            if (tracker == null)
            {
                Debug.LogWarning("[PickupFeedback] WealthTracker not found, the status-up sound is disabled.", this);
                return;
            }

            // The tracker raises the initial status at its own Start, that must not count as a promotion
            lastStatus = tracker.CurrentStatus;
            tracker.OnStatusChanged += HandleStatusChanged;
        }

        private void OnDisable()
        {
            Collectible.OnCollected -= HandleCollected;
            if (tracker != null)
                tracker.OnStatusChanged -= HandleStatusChanged;
        }

        private void HandleCollected(Collectible item, int delta)
        {
            if (delta == 0) return;

            bool gain = delta > 0;
            SpawnText(delta, gain);
            SpawnBurst(gain ? coinPool : bottlePool, GetItemPosition(item));
            PlaySound(gain ? coinClip : bottleClip, gain ? coinVolume : bottleVolume, pitchJitter);
        }

        private void HandleStatusChanged(WealthStatus status)
        {
            bool promoted = status > lastStatus;
            lastStatus = status;

            if (promoted)
                PlaySound(statusUpClip, statusUpVolume, 0f);
        }

        #region Effects

        private void SpawnText(int delta, bool gain)
        {
            if (textPool == null) return;

            Transform target = ResolvePlayer();
            if (target == null) return;

            float top = playerCollider != null ? playerCollider.bounds.max.y : target.position.y + fallbackPlayerHeight;
            Vector3 start = new Vector3(
                target.position.x + Random.Range(-textHorizontalJitter, textHorizontalJitter),
                top + textHeightMargin,
                target.position.z);

            textPool.Get().Play(gain ? gainFormat : lossFormat, Mathf.Abs(delta), gain ? gainColor : lossColor, start, target);
        }

        private static void SpawnBurst(RoundRobinPool<ParticleSystem> pool, Vector3 position)
        {
            if (pool == null) return;

            ParticleSystem burst = pool.Get();
            burst.transform.position = position;
            burst.Clear(true);
            burst.Play(true);
        }

        private void PlaySound(AudioClip clip, float volume, float jitter)
        {
            AudioManager manager = AudioManager.Instance;
            if (manager == null || clip == null) return;

            manager.Play(clip, volume, 1f + Random.Range(-jitter, jitter));
        }

        #endregion

        #region Helpers

        private RoundRobinPool<ParticleSystem> CreateParticlePool(ParticleSystem prefab)
        {
            if (prefab == null) return null;
            return new RoundRobinPool<ParticleSystem>(particlePoolSize, () => Instantiate(prefab, transform), IsAlive);
        }

        private FloatingText CreateText()
        {
            FloatingText text = Instantiate(floatingTextPrefab, transform);
            text.gameObject.SetActive(false);
            return text;
        }

        private Transform ResolvePlayer()
        {
            if (player != null) return player;

            PlayerController controller = FindObjectOfType<PlayerController>();
            if (controller == null) return null;

            player = controller.transform;
            playerCollider = player.GetComponent<Collider>();
            return player;
        }

        // The collider centre is used because the model pivot of a banknote or bottle may sit at its corner
        private static Vector3 GetItemPosition(Collectible item)
        {
            return item.TryGetComponent(out Collider itemCollider) ? itemCollider.bounds.center : item.transform.position;
        }

        private static bool IsActive(FloatingText text)
        {
            return text.gameObject.activeSelf;
        }

        private static bool IsAlive(ParticleSystem system)
        {
            return system.IsAlive(true);
        }

        #endregion
    }
}
