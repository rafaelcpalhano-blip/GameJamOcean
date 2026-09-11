using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.World
{
    [DisallowMultipleComponent]
    public sealed class LighthouseEndGameLight3D : MonoBehaviour
    {
        private static readonly Vector3 FixedLocalOrigin = new(.003f, 5.335f, .006f);
        [Header("Beacon")]
        [SerializeField] private Color lightColor = new(1f, .78f, .24f, 1f);
        [SerializeField, Range(2000f, 6500f)] private float colorTemperature = 3600f;
        [SerializeField, Min(.1f)] private float pointIntensity = 7f;
        [SerializeField, Min(1f)] private float pointRange = 16f;
        [SerializeField, Range(0f, .2f)] private float beaconPulseAmount = .06f;
        [SerializeField, Min(.05f)] private float beaconPulseFrequency = .65f;
        [Header("Rotating beam")]
        [SerializeField, Min(1f)] private float rotationDegreesPerSecond = 12f;
        [SerializeField, Range(5f, 45f)] private float spotAngle = 17f;
        [SerializeField, Min(1f)] private float beamRange = 45f;
        [SerializeField, Min(.1f)] private float beamIntensity = 13f;
        private Transform pivot, halo;
        private Light pointLight;
        private Vector3 haloBaseScale;

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
            source.name = "Beacon Glow"; source.transform.SetParent(transform, false); source.transform.localScale = Vector3.one * .52f;
            Destroy(source.GetComponent<Collider>());
            Color hotCore = Color.Lerp(lightColor, Color.white, .58f);
            source.GetComponent<Renderer>().material = GlowMaterial(hotCore, 1f, 5f);

            Vector3 inverseScale = new(1f / Mathf.Max(.001f, transform.lossyScale.x),
                1f / Mathf.Max(.001f, transform.lossyScale.y), 1f / Mathf.Max(.001f, transform.lossyScale.z));
            halo = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            halo.name = "Spherical Soft Halo"; halo.SetParent(transform, false);
            haloBaseScale = Vector3.Scale(Vector3.one * 1.55f, inverseScale);
            halo.localScale = haloBaseScale;
            Destroy(halo.GetComponent<Collider>());
            halo.GetComponent<Renderer>().material = GlowMaterial(
                new Color(lightColor.r, lightColor.g * .92f, lightColor.b * .55f, .3f), .3f, 3.2f);

            pointLight = gameObject.AddComponent<Light>();
            pointLight.type = LightType.Point; pointLight.color = lightColor;
            pointLight.useColorTemperature = true; pointLight.colorTemperature = colorTemperature;
            pointLight.intensity = pointIntensity; pointLight.range = pointRange;
            pointLight.shadows = LightShadows.None;

            pivot = new GameObject("Rotating Lighthouse Beam").transform; pivot.SetParent(transform, false);
            var spotObject = new GameObject("Spot Light"); spotObject.transform.SetParent(pivot, false);
            // Local zero starts exactly at LuzFarol: (0.003, 5.335, 0.006) in AldeiaNV4.
            spotObject.transform.localPosition = Vector3.zero;
            spotObject.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
            Light spot = spotObject.AddComponent<Light>(); spot.type = LightType.Spot; spot.color = lightColor;
            spot.useColorTemperature = true; spot.colorTemperature = colorTemperature;
            spot.range = beamRange; spot.spotAngle = spotAngle; spot.innerSpotAngle = spotAngle * .35f;
            spot.intensity = beamIntensity; spot.shadows = LightShadows.Soft;

            var cone = new GameObject("Visible Light Cone", typeof(MeshFilter), typeof(MeshRenderer));
            cone.transform.SetParent(spotObject.transform, false);
            cone.transform.localScale = inverseScale;
            cone.GetComponent<MeshFilter>().sharedMesh = CreateCone(beamRange * .75f,
                Mathf.Tan(spotAngle * .5f * Mathf.Deg2Rad) * beamRange * .75f);
            cone.GetComponent<MeshRenderer>().material = GlowMaterial(
                new Color(lightColor.r, lightColor.g, lightColor.b, .1f), .1f, 3f);
        }

        private void Update()
        {
            if (pivot != null) pivot.Rotate(0f, rotationDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            float pulse = (Mathf.Sin(Time.time * beaconPulseFrequency * Mathf.PI * 2f) + 1f) * .5f;
            if (pointLight != null)
                pointLight.intensity = pointIntensity * (1f + pulse * beaconPulseAmount);
            if (halo != null)
                halo.localScale = haloBaseScale * (1f + pulse * beaconPulseAmount * .5f);
        }

        private static Material GlowMaterial(Color color, float alpha, float emissionStrength)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = new Color(color.r, color.g, color.b, alpha) };
            material.SetColor("_BaseColor", material.color);
            material.SetColor("_EmissionColor", color * Mathf.Max(1f, emissionStrength));
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
