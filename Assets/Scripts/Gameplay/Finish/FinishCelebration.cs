using System.Collections;
using ButchersGames.Player;
using UnityEngine;

namespace ButchersGames.Gameplay.Finish
{
    /// <summary>
    /// Victory animation at the spot where the player stopped: the character turns to the camera,
    /// jumps with a little wobble and confetti flies around. Purely procedural (the models have no animations).
    /// </summary>
    public class FinishCelebration : MonoBehaviour
    {
        [Tooltip("Found in the scene automatically if left empty")]
        [SerializeField] private PlayerController player;

        [Tooltip("The model that dances. Empty = the visible model of PlayerAppearance, or the player itself")]
        [SerializeField] private Transform model;

        [Header("Turn")]
        [SerializeField] private bool faceCamera = true;
        [SerializeField, Min(0f)] private float turnDuration = 0.35f;

        [Header("Jump")]
        [SerializeField, Min(0f)] private float jumpHeight = 0.6f;
        [SerializeField, Min(0.1f)] private float jumpsPerSecond = 2.2f;
        [SerializeField, Range(0f, 30f)] private float wobbleAngle = 10f;
        [SerializeField, Range(0f, 0.3f)] private float squash = 0.08f;

        [Header("Confetti")]
        [SerializeField] private bool confetti = true;
        [SerializeField, Min(1)] private int confettiBurst = 60;

        public bool IsPlaying { get; private set; }

        private Coroutine routine;
        private ParticleSystem confettiSystem;
        private Transform target;
        private Vector3 startLocalPosition;
        private Quaternion startLocalRotation;
        private Vector3 startLocalScale;

        /// <summary>Starts the dance. It goes on until Stop() or the scene reload.</summary>
        public void Play()
        {
            if (IsPlaying) return;

            if (player == null)
                player = FindObjectOfType<PlayerController>(true);
            target = model != null ? model : FindModel();
            if (target == null) return;

            IsPlaying = true;
            startLocalPosition = target.localPosition;
            startLocalRotation = target.localRotation;
            startLocalScale = target.localScale;

            if (confetti)
                SpawnConfetti();

            routine = StartCoroutine(Dance());
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            if (routine != null) StopCoroutine(routine);
            routine = null;

            if (target != null)
            {
                target.localPosition = startLocalPosition;
                target.localRotation = startLocalRotation;
                target.localScale = startLocalScale;
            }
        }

        private Transform FindModel()
        {
            if (player == null) return null;

            var appearance = player.GetComponentInChildren<PlayerAppearance>(true);
            GameObject active = appearance != null ? appearance.ActiveModel : null;
            return active != null && active.activeInHierarchy ? active.transform : player.transform;
        }

        private IEnumerator Dance()
        {
            Quaternion fromRotation = target.rotation;
            Quaternion toRotation = fromRotation;
            if (faceCamera && Camera.main != null)
            {
                Vector3 toCamera = Camera.main.transform.position - target.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 1e-4f)
                    toRotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }

            float time = 0f;
            while (true)
            {
                time += Time.deltaTime;

                float turn = turnDuration > 0f ? Mathf.SmoothStep(0f, 1f, time / turnDuration) : 1f;
                Quaternion facing = Quaternion.Slerp(fromRotation, toRotation, turn);

                float phase = time * jumpsPerSecond * Mathf.PI;
                float hop = Mathf.Abs(Mathf.Sin(phase));
                float wobble = Mathf.Sin(phase) * wobbleAngle;

                // Parent's rotation is taken into account so the model turns in world space
                Transform parent = target.parent;
                target.rotation = facing * Quaternion.Euler(0f, 0f, wobble);
                Vector3 up = parent != null ? parent.InverseTransformDirection(Vector3.up) : Vector3.up;
                target.localPosition = startLocalPosition + up * (hop * jumpHeight);

                // Stretches in the air and squashes at the landing
                float stretch = 1f + (hop - 0.5f) * 2f * squash;
                target.localScale = new Vector3(startLocalScale.x / Mathf.Sqrt(stretch), startLocalScale.y * stretch, startLocalScale.z / Mathf.Sqrt(stretch));

                yield return null;
            }
        }

        private void SpawnConfetti()
        {
            var go = new GameObject("Confetti");
            go.transform.position = player.transform.position + Vector3.up * 3f;

            confettiSystem = go.AddComponent<ParticleSystem>();
            // A new system starts playing right away, and the duration can only be changed while it is stopped
            confettiSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) renderer.material = new Material(shader);

            var main = confettiSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 500;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.9f, 0.4f));

            var emission = confettiSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)confettiBurst),
                new ParticleSystem.Burst(0.35f, (short)(confettiBurst / 2))
            });

            var shape = confettiSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4f, 0.5f, 2f);

            var rotation = confettiSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

            confettiSystem.Play();
        }

        private void OnDestroy()
        {
            if (confettiSystem != null)
                Destroy(confettiSystem.gameObject);
        }
    }
}
