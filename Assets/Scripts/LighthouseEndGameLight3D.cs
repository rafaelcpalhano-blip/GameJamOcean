using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.World
{
    [DisallowMultipleComponent]
    public sealed class LighthouseEndGameLight3D : MonoBehaviour
    {
        private static readonly Vector3 FixedLocalOrigin = new(.003f, 5.335f, .006f);
        [Header("Beacon")]
        [SerializeField] private Color lightColor = new(1f, .86f, .48f, 1f);
        [SerializeField, Min(.1f)] private float pointIntensity = 5f;
        [SerializeField, Min(1f)] private float pointRange = 14f;
        [Header("Rotating beam")]
        [SerializeField, Min(1f)] private float rotationDegreesPerSecond = 12f;
        [SerializeField, Range(5f, 45f)] private float spotAngle = 17f;
        [SerializeField, Min(1f)] private float beamRange = 45f;
        [SerializeField, Min(.1f)] private float beamIntensity = 11f;
        private Transform pivot, halo;

        public static void ConfigureScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.Equals("LuzFarol", System.StringComparison.OrdinalIgnoreCase))
                {
                    child.localPosition = FixedLocalOrigin;
                    if (child.GetComponent<LighthouseEndGameLight3D>() == null)
                        child.gameObject.AddComponent<LighthouseEndGameLight3D>();
                }
        }

        private void Awake()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            source.name = "Beacon Glow"; source.transform.SetParent(transform, false); source.transform.localScale = Vector3.one * .45f;
            Destroy(source.GetComponent<Collider>());
            source.GetComponent<Renderer>().material = GlowMaterial(lightColor, 1f);

            Vector3 inverseScale = new(1f / Mathf.Max(.001f, transform.lossyScale.x),
                1f / Mathf.Max(.001f, transform.lossyScale.y), 1f / Mathf.Max(.001f, transform.lossyScale.z));
            halo = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            halo.name = "Soft Halo"; halo.SetParent(transform, false); halo.localScale = Vector3.Scale(Vector3.one * 2.4f, inverseScale);
            Destroy(halo.GetComponent<Collider>());
            halo.GetComponent<Renderer>().material = GlowMaterial(new Color(lightColor.r, lightColor.g, lightColor.b, .22f), .22f);

            Light point = gameObject.AddComponent<Light>(); point.type = LightType.Point; point.color = lightColor;
            point.intensity = pointIntensity; point.range = pointRange; point.shadows = LightShadows.None;

            pivot = new GameObject("Rotating Lighthouse Beam").transform; pivot.SetParent(transform, false);
            var spotObject = new GameObject("Spot Light"); spotObject.transform.SetParent(pivot, false);
            // Local zero starts exactly at LuzFarol: (0.003, 5.335, 0.006) in AldeiaNV4.
            spotObject.transform.localPosition = Vector3.zero;
            spotObject.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
            Light spot = spotObject.AddComponent<Light>(); spot.type = LightType.Spot; spot.color = lightColor;
            spot.range = beamRange; spot.spotAngle = spotAngle; spot.innerSpotAngle = spotAngle * .35f;
            spot.intensity = beamIntensity; spot.shadows = LightShadows.Soft;

            var cone = new GameObject("Visible Light Cone", typeof(MeshFilter), typeof(MeshRenderer));
            cone.transform.SetParent(spotObject.transform, false);
            cone.transform.localScale = inverseScale;
            cone.GetComponent<MeshFilter>().sharedMesh = CreateCone(beamRange * .75f,
                Mathf.Tan(spotAngle * .5f * Mathf.Deg2Rad) * beamRange * .75f);
            cone.GetComponent<MeshRenderer>().material = GlowMaterial(new Color(lightColor.r, lightColor.g, lightColor.b, .075f), .075f);
        }

        private void Update()
        {
            if (pivot != null) pivot.Rotate(0f, rotationDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            Camera camera = Camera.main;
            if (halo != null && camera != null) halo.rotation = Quaternion.LookRotation(halo.position - camera.transform.position, camera.transform.up);
        }

        private static Material GlowMaterial(Color color, float alpha)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = new Color(color.r, color.g, color.b, alpha) };
            material.SetColor("_BaseColor", material.color); material.SetColor("_EmissionColor", color * 2f);
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 1f); material.SetFloat("_Cull", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_ZWrite", 0); material.renderQueue = 3000;
            return material;
        }

        private static Mesh CreateCone(float length, float radius)
        {
            const int sides = 24; var vertices = new Vector3[sides + 1]; var triangles = new int[sides * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, length);
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % sides + 1;
            }
            var mesh = new Mesh { name = "Runtime Lighthouse Cone", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
