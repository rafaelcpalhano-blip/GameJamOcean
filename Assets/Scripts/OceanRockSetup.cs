using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameJamOcean.Boat;

namespace GameJamOcean.World
{
    public static class OceanRockSetup
    {
        private static bool IsRock(Transform t) => t.name.Equals("Rock", StringComparison.OrdinalIgnoreCase)
            || t.name.Equals("Rocks", StringComparison.OrdinalIgnoreCase)
            || t.name.StartsWith("Rock (", StringComparison.OrdinalIgnoreCase)
            || t.name.StartsWith("Rock(Clone)", StringComparison.OrdinalIgnoreCase);

        public static void Configure(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var rock in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsRock(rock)) continue;
                bool nested = false;
                for (var parent = rock.parent; parent != null; parent = parent.parent)
                    if (IsRock(parent)) { nested = true; break; }
                if (nested) continue;
                var colliders = rock.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0)
                {
                    foreach (var mesh in rock.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mesh.sharedMesh == null) continue;
                        var collider = mesh.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = mesh.sharedMesh;
                    }
                    colliders = rock.GetComponentsInChildren<Collider>(true);
                }
                if (colliders.Length == 0) { Debug.LogWarning($"Rocha sem malha/colisor: {rock.name}", rock); continue; }
                var clock = rock.GetComponent<BoatDamageObstacle3D>() ?? rock.gameObject.AddComponent<BoatDamageObstacle3D>();
                clock.ConfigureRock(clock);
                foreach (var collider in colliders)
                {
                    var damage = collider.GetComponent<BoatDamageObstacle3D>() ?? collider.gameObject.AddComponent<BoatDamageObstacle3D>();
                    damage.ConfigureRock(clock);
                }
            }
        }
    }
}
