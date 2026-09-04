using UnityEngine;

namespace GameJamOcean.Boat
{
    public static class OceanReturnState3D
    {
        private static bool hasPendingReturn;
        private static Vector3 returnPosition;
        private static Quaternion returnRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetRuntimeState()
        {
            hasPendingReturn = false;
            returnPosition = default;
            returnRotation = Quaternion.identity;
        }

        public static void Save(Transform boat)
        {
            if (boat == null)
            {
                return;
            }

            returnPosition = boat.position;
            returnRotation = boat.rotation;
            hasPendingReturn = true;
        }

        public static bool TryRestore(Transform boat, Rigidbody boatRigidbody)
        {
            if (!hasPendingReturn || boat == null)
            {
                return false;
            }

            hasPendingReturn = false;
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
    }
}
