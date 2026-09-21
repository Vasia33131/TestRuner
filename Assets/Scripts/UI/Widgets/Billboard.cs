using UnityEngine;

namespace ButchersGames.UI
{
    /// <summary>Keeps a world-space object facing the camera (aligned with the camera rotation).</summary>
    public class Billboard : MonoBehaviour
    {
        [Tooltip("Main Camera is used if left empty")]
        [SerializeField] private Camera targetCamera;

        private void LateUpdate()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            transform.rotation = cam.transform.rotation;
        }
    }
}
