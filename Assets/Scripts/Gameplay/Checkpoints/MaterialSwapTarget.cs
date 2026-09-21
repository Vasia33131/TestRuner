using UnityEngine;

namespace ButchersGames.Gameplay.Checkpoints
{
    /// <summary>
    /// Object with a placeholder (purple) material that gets its real material when a save zone is activated.
    /// Link it to a zone in SaveZone.linkedTargets, or leave it unlinked to be switched by any zone.
    /// </summary>
    public class MaterialSwapTarget : MonoBehaviour
    {
        [Tooltip("Material assigned on activation")]
        [SerializeField] private Material targetMaterial;

        [Tooltip("Only slots with this material are replaced. Empty = every slot.")]
        [SerializeField] private Material placeholderMaterial;

        [Tooltip("Renderers to change. Empty = all renderers on this object and its children.")]
        [SerializeField] private Renderer[] renderers;

        public bool IsApplied { get; private set; }
        public Material TargetMaterial => targetMaterial;

        public void Apply()
        {
            if (IsApplied) return;
            IsApplied = true;

            if (targetMaterial == null)
            {
                Debug.LogWarning("[MaterialSwapTarget] Target Material is not assigned.", this);
                return;
            }

            Renderer[] list = renderers != null && renderers.Length > 0 ? renderers : GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in list)
            {
                if (r == null) continue;

                // sharedMaterials: no per-object material copies are created
                Material[] materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (placeholderMaterial == null || materials[i] == placeholderMaterial)
                        materials[i] = targetMaterial;
                }
                r.sharedMaterials = materials;
            }
        }
    }
}
