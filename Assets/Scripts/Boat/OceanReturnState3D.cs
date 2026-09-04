using UnityEngine;
using GameJamOcean.Combat;

namespace GameJamOcean.Boat
{
    public static class OceanReturnState3D
    {
        private static bool hasPendingReturn;
        private static Vector3 returnPosition;
        private static Quaternion returnRotation;
        private static float returnHealth;
        private static bool hasReturnHealth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetRuntimeState()
        {
            hasPendingReturn = false;
            returnPosition = default;
            returnRotation = Quaternion.identity;
            returnHealth = 0f;
            hasReturnHealth = false;
        }

        public static void Save(Transform boat)
        {
            if (boat == null)
            {
                return;
            }

            returnPosition = boat.position;
            returnRotation = boat.rotation;
            Health health = boat.GetComponent<Health>();
            hasReturnHealth = health != null && !health.IsDead;
            if (hasReturnHealth) returnHealth = health.CurrentHealth;
            hasPendingReturn = true;
        }

        public static bool TryRestore(Transform boat, Rigidbody boatRigidbody)
        {
            if (!hasPendingReturn || boat == null)
            {
                return false;
            }

            boat.SetPositionAndRotation(returnPosition, returnRotation);

            if (boatRigidbody != null)
            {
                boatRigidbody.position = returnPosition;
                boatRigidbody.rotation = returnRotation;
                if (!boatRigidbody.isKinematic)
                {
                    boatRigidbody.linearVelocity = Vector3.zero;
                    boatRigidbody.angularVelocity = Vector3.zero;
                }
            }

            return true;
        }

        public static bool TryRestoreHealth(Health health)
        {
            if (!hasPendingReturn || !hasReturnHealth || health == null) return false;
            health.SetCurrentHealth(returnHealth);
            hasPendingReturn = false;
            hasReturnHealth = false;
            return true;
        }
    }
}
