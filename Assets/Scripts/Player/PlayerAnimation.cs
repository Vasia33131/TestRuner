using UnityEngine;

namespace ButchersGames.Player
{
    /// <summary>Plays the walk cycle while the player runs and holds its first frame while the player stands.</summary>
    public class PlayerAnimation : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [Tooltip("Animator on the character model. Found in children when empty.")]
        [SerializeField] private Animator animator;

        [Tooltip("Playback speed of the walk cycle while running")]
        [SerializeField] private float animationSpeed = 1f;

        private bool wasRunning;

        private void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (animator == null)
                Debug.LogWarning("[PlayerAnimation] Animator not found, run Tools/Runner/Setup Player Visuals.", this);
        }

        private void OnEnable()
        {
            wasRunning = false;
            HoldFirstFrame();
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            bool running = player != null && player.IsRunning;
            if (running == wasRunning) return;

            wasRunning = running;
            if (running)
                animator.speed = animationSpeed;
            else
                HoldFirstFrame();
        }

        // A stopped player keeps a neutral pose instead of freezing mid-step
        private void HoldFirstFrame()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            animator.speed = 0f;
            if (animator.isActiveAndEnabled)
                animator.Play(0, 0, 0f);
        }
    }
}
