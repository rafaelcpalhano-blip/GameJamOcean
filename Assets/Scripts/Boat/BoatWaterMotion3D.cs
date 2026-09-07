using System.Collections;
using UnityEngine;

namespace GameJamOcean.Boat
{
    [DisallowMultipleComponent]
    public sealed class BoatWaterMotion3D : MonoBehaviour
    {
        [SerializeField] private Transform visualModel;
        [SerializeField, Range(0f, 1f)] private float intensity = 1f;
        [SerializeField, Range(0f, 0.2f)] private float bobHeight = 0.035f;
        [SerializeField, Range(0f, 3f)] private float pitchDegrees = 0.6f;
        [SerializeField, Range(0f, 3f)] private float rollDegrees = 0.9f;
        [SerializeField, Range(0.05f, 1f)] private float frequency = 0.22f;
        [Header("Turbo pitch")]
        [SerializeField, Range(0f, 8f)] private float turboPitchDegrees = 6f;
        [SerializeField, Min(.1f)] private float turboPitchResponse = 2.5f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private float elapsed;
        private bool hasRestPose;
        [Header("Collision Impact")]
        [SerializeField, Min(.05f)] private float impactDuration = 1.05f;
        [SerializeField, Range(0f, 10f)] private float impactPitchDegrees = 4.2f;
        [SerializeField, Range(0f, 10f)] private float impactRollDegrees = 6f;
        [SerializeField, Range(0f, .3f)] private float impactLift = .12f;
        private float impactStartedAt = float.NegativeInfinity;
        private float impactSide = 1f;
        private BoatController3D controller;
        private float turboPitchBlend;
        private Coroutine initialization;

        public void Configure(Transform model)
        {
            visualModel = model;
            if (isActiveAndEnabled) ScheduleInitialization();
        }

        private void OnEnable()
        {
            // BoatVisualUpgrade3D creates and assigns the visual holder after scene load.
            // Until then there is nothing to animate, but this is a valid initialization state.
            hasRestPose = false;
            if (visualModel != null) ScheduleInitialization();
        }

        private void ScheduleInitialization()
        {
            if (initialization != null) StopCoroutine(initialization);
            hasRestPose = false;
            initialization = StartCoroutine(InitializeAfterVisualSetup());
        }

        private IEnumerator InitializeAfterVisualSetup()
        {
            // Instantiated prefab colliders are removed with Destroy, which completes at frame end.
            yield return null;
            initialization = null;

            if (visualModel == null) yield break;

            if (visualModel == transform || !visualModel.IsChildOf(transform)
                || visualModel.GetComponentInChildren<Collider>(true) != null
                || visualModel.GetComponentInChildren<Rigidbody>(true) != null)
            {
                Debug.LogWarning("Boat water motion needs a visual child without physics components.", this);
                enabled = false;
                yield break;
            }

            restPosition = visualModel.localPosition;
            restRotation = visualModel.localRotation;
            controller = GetComponent<BoatController3D>();
            hasRestPose = true;
            elapsed = 0f;
        }

        private void LateUpdate()
        {
            if (!hasRestPose || visualModel == null) return;
            elapsed += Time.deltaTime;
            float phase = elapsed * frequency * Mathf.PI * 2f;
            float strength = intensity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed));
            float impactProgress = Mathf.Clamp01((Time.time - impactStartedAt) / impactDuration);
            float impactEnvelope = (1f - impactProgress) * (1f - impactProgress);
            float impactWave = Mathf.Sin(impactProgress * Mathf.PI * 3f) * impactEnvelope;
            turboPitchBlend = Mathf.MoveTowards(turboPitchBlend,
                controller != null && controller.IsTurboActive ? 1f : 0f,
                turboPitchResponse * Time.deltaTime);
            visualModel.localPosition = restPosition
                + Vector3.up * (Mathf.Sin(phase) * bobHeight * strength
                    + Mathf.Abs(impactWave) * impactLift);
            visualModel.localRotation = restRotation * Quaternion.Euler(
                Mathf.Sin(phase * 0.83f) * pitchDegrees * strength + impactWave * impactPitchDegrees
                    - turboPitchBlend * turboPitchDegrees,
                0f,
                Mathf.Sin(phase * 0.67f + 0.8f) * rollDegrees * strength
                    + impactWave * impactRollDegrees * impactSide);
        }

        public void PlayCollisionImpact(Vector3 worldPushDirection)
        {
            impactSide = Vector3.Dot(transform.right, worldPushDirection) >= 0f ? -1f : 1f;
            impactStartedAt = Time.time;
        }

        private void OnDisable()
        {
            if (initialization != null)
            {
                StopCoroutine(initialization);
                initialization = null;
            }
            if (hasRestPose && visualModel != null)
            {
                visualModel.SetLocalPositionAndRotation(restPosition, restRotation);
            }
            hasRestPose = false;
        }
    }
}
