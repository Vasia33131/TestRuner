using ButchersGames.Core;
using UnityEngine;

namespace ButchersGames.Cameras
{
    /// <summary>
    /// Third-person camera behind the runner. Follows the target's heading (yaw only),
    /// so the lean of the model never shakes the view.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Found by the Player tag if left empty")]
        [SerializeField] private Transform target;

        [Header("Offset Settings")]
        [Tooltip("Offset in the target's heading space (yaw only): X=0, Y=3.8, Z=-5.2 means behind and above the player")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 3.8f, -5.2f);

        [Tooltip("Rotate the offset together with the target's heading (Y axis only)")]
        [SerializeField] private bool rotateWithTarget = true;

        [Header("Smoothing")]
        [SerializeField] private float smoothSpeed = 5f;

        [Header("Look At")]
        [Tooltip("Look-at point offset in the same heading space as the offset")]
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1f, 4f);

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                SnapToTarget();
            }
        }

        public Vector3 Offset
        {
            get => offset;
            set => offset = value;
        }

        public bool RotateWithTarget
        {
            get => rotateWithTarget;
            set => rotateWithTarget = value;
        }

        public Vector3 LookAtOffset
        {
            get => lookAtOffset;
            set => lookAtOffset = value;
        }

        private void Start()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(GameTags.Player);
                if (player == null)
                {
                    Debug.LogError("[CameraFollow] No object tagged '" + GameTags.Player + "' found in the scene!", this);
                    return;
                }
                target = player.transform;
            }

            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Quaternion yaw = GetHeading();
            Vector3 desiredPosition = target.position + yaw * offset;
            float t = Mathf.Clamp01(smoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, t);
            transform.LookAt(target.position + yaw * lookAtOffset);
        }

        /// <summary>Moves the camera to its final position right away, without smoothing.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            Quaternion yaw = GetHeading();
            transform.position = target.position + yaw * offset;
            transform.LookAt(target.position + yaw * lookAtOffset);
        }

        // Only the Y rotation is used, so the roll/pitch of the model does not shake the camera
        private Quaternion GetHeading()
        {
            return rotateWithTarget ? Quaternion.Euler(0f, target.eulerAngles.y, 0f) : Quaternion.identity;
        }
    }
}
