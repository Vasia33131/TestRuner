using TMPro;
using UnityEngine;

namespace ButchersGames.Feedback
{
    /// <summary>
    /// World-space text that rises and fades out, then deactivates itself so the pool can reuse it.
    /// Lives on a TextMeshPro (3D) object.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] private float duration = 0.8f;
        [SerializeField] private float riseDistance = 1.5f;
        [Tooltip("Normalized time at which the fade-out starts")]
        [Range(0f, 1f)]
        [SerializeField] private float fadeStart = 0.5f;

        private TMP_Text label;
        private Transform follow;
        private Vector3 anchor;
        private float elapsed;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        /// <summary>
        /// Starts the animation. The format is a TMP SetText format such as "+{0:0} $" (no string allocation).
        /// With a follow target the text keeps its start offset from it while rising.
        /// </summary>
        public void Play(string format, float value, Color color, Vector3 startPosition, Transform followTarget)
        {
            if (label == null) label = GetComponent<TMP_Text>();

            follow = followTarget;
            anchor = follow != null ? startPosition - follow.position : startPosition;
            elapsed = 0f;

            label.color = color;
            label.SetText(format, value);
            transform.position = startPosition;

            gameObject.SetActive(true);
            FaceCamera();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            float eased = 1f - (1f - t) * (1f - t);
            Vector3 basePosition = follow != null ? follow.position + anchor : anchor;
            transform.position = basePosition + Vector3.up * (riseDistance * eased);

            label.alpha = 1f - Mathf.InverseLerp(fadeStart, 1f, t);
            FaceCamera();

            if (t >= 1f)
                gameObject.SetActive(false);
        }

        private void FaceCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;
        }
    }
}
