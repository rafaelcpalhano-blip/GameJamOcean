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

        private Vector3 restPosition;
        private Quaternion restRotation;
        private float elapsed;
        private bool hasRestPose;

        public void Configure(Transform model)
        {
            visualModel = model;
        }

        private void OnEnable()
        {
            if (visualModel == null || visualModel == transform
                || !visualModel.IsChildOf(transform)
                || visualModel.GetComponentInChildren<Collider>(true) != null
                || visualModel.GetComponentInChildren<Rigidbody>(true) != null)
            {
                Debug.LogWarning("Boat water motion needs a visual child without physics components.", this);
                enabled = false;
                return;
            }

            restPosition = visualModel.localPosition;
            restRotation = visualModel.localRotation;
            hasRestPose = true;
            elapsed = 0f;
        }

        private void LateUpdate()
        {
            if (!hasRestPose || visualModel == null) return;
            elapsed += Time.deltaTime;
            float phase = elapsed * frequency * Mathf.PI * 2f;
            float strength = intensity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed));
            visualModel.localPosition = restPosition
                + Vector3.up * (Mathf.Sin(phase) * bobHeight * strength);
            visualModel.localRotation = restRotation * Quaternion.Euler(
                Mathf.Sin(phase * 0.83f) * pitchDegrees * strength,
                0f,
                Mathf.Sin(phase * 0.67f + 0.8f) * rollDegrees * strength);
        }

        private void OnDisable()
        {
            if (hasRestPose && visualModel != null)
            {
                visualModel.SetLocalPositionAndRotation(restPosition, restRotation);
            }
            hasRestPose = false;
        }
    }
}
