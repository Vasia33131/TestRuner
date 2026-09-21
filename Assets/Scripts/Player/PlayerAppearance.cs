using ButchersGames.Gameplay.Economy;
using UnityEngine;

namespace ButchersGames.Player
{
    /// <summary>
    /// Switches the player model. Exactly one model is visible at a time,
    /// the index matches WealthStatus (0 Poor, 1 Middle, 2 Rich, 3 Millionaire), extra models are optional.
    /// </summary>
    public class PlayerAppearance : MonoBehaviour
    {
        [Header("Models in the order of the wealth status")]
        [SerializeField] private GameObject poorModel;
        [SerializeField] private GameObject middleModel;
        [SerializeField] private GameObject blingModel;
        [SerializeField] private GameObject businessModel;
        [SerializeField] private GameObject casualModel;
        [SerializeField] private GameObject cocktailModel;

        /// <summary>Index of the visible model, -1 before the first SetAppearance.</summary>
        public int CurrentIndex { get; private set; } = -1;

        /// <summary>The model that is visible right now (null if it is not assigned).</summary>
        public GameObject ActiveModel => GetModel(CurrentIndex);

        public int ModelCount => Models.Length;

        private GameObject[] models;

        private GameObject[] Models => models ??= new[]
        {
            poorModel, middleModel, blingModel, businessModel, casualModel, cocktailModel
        };

        private void Start()
        {
            // The game starts with the poor look, WealthTracker then applies the real status
            if (CurrentIndex < 0)
                SetAppearance(0);
        }

        public void SetAppearance(WealthStatus status) => SetAppearance((int)status);

        /// <summary>Shows the model with the given index and hides the others. Out-of-range indices are clamped.</summary>
        public void SetAppearance(int index)
        {
            GameObject[] all = Models;
            CurrentIndex = Mathf.Clamp(index, 0, all.Length - 1);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                    all[i].SetActive(i == CurrentIndex);
            }
        }

        public GameObject GetModel(int index)
        {
            GameObject[] all = Models;
            return index >= 0 && index < all.Length ? all[index] : null;
        }

        private void OnValidate()
        {
            // Rebuilt from the fields on the next access
            models = null;
        }
    }
}
