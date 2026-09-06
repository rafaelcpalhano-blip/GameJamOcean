using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.World
{
    [DisallowMultipleComponent]
    public sealed class OceanHorizonBackdrop3D : MonoBehaviour
    {
        public static void ConfigureScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<OceanHorizonBackdrop3D>(true) != null) return;
            new GameObject("Fake Sky Horizon").AddComponent<OceanHorizonBackdrop3D>();
        }

        private void Awake()
        {
            transform.position = new Vector3(0f, 35f, 0f);
            CreateHorizonCylinder();
            CreateClouds();
        }

        private void CreateHorizonCylinder()
        {
            const int sides = 48;
            const float radius = 360f;
            const float height = 150f;
            var vertices = new Vector3[sides * 2];
            var triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i * 2] = new Vector3(Mathf.Cos(angle) * radius, -height * .5f, Mathf.Sin(angle) * radius);
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(angle) * radius, height * .5f, Mathf.Sin(angle) * radius);
                int next = (i + 1) % sides;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2 + 1; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
            }
            GameObject sky = new("Blue Sky", typeof(MeshFilter), typeof(MeshRenderer));
            sky.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Fake Sky Horizon Mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds(); mesh.RecalculateNormals();
            sky.GetComponent<MeshFilter>().sharedMesh = mesh;
            sky.GetComponent<MeshRenderer>().material = Unlit(new Color(.34f, .68f, .9f, 1f));
        }

        private void CreateClouds()
        {
            for (int group = 0; group < 10; group++)
            {
                float angle = group * Mathf.PI * 2f / 10f + .17f;
                Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 center = direction * 300f + Vector3.up * Random.Range(-1f, 24f);
                for (int puff = 0; puff < 3; puff++)
                {
                    GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cloud.name = "Cloud Puff";
                    cloud.transform.SetParent(transform, false);
                    cloud.transform.localPosition = center + new Vector3((puff - 1) * 8f, puff == 1 ? 3f : 0f, 0f);
                    cloud.transform.localScale = new Vector3(14f, 4.5f, 5f);
                    Destroy(cloud.GetComponent<Collider>());
                    cloud.GetComponent<Renderer>().material = Unlit(new Color(1f, 1f, 1f, .78f));
                }
            }
        }

        private static Material Unlit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            material.SetColor("_BaseColor", color);
            return material;
        }
    }
}
