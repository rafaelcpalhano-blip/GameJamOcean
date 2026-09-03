using UnityEngine;
using UnityEngine.Rendering;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    public sealed class DivePointBeacon3D : MonoBehaviour
    {
        [Header("Beam")]
        [SerializeField] private Color beamColor = new(0.48f, 0.9f, 1f, 0.09f);
        [SerializeField, Min(0.5f)] private float beamHeight = 3f;
        [SerializeField, Range(10f, 80f)] private float beamAngle = 40f;
        [SerializeField, Range(6, 48)] private int beamSegments = 24;

        [Header("Soft Light")]
        [SerializeField] private bool useSpotLight = true;
        [SerializeField, Min(0f)] private float lightIntensity = 1.1f;
        [SerializeField, Min(0.5f)] private float lightRange = 7f;
        [SerializeField, Range(0f, 1f)] private float innerAngleRatio = 0.55f;

        [Header("Attention Pulse")]
        [SerializeField, Min(0f)] private float pulseAmount = 0.12f;
        [SerializeField, Min(0.05f)] private float pulseSpeed = 0.65f;

        private GameObject effectRoot;
        private Light spotLight;
        private Mesh beamMesh;
        private Material beamMaterial;

        private void Awake()
        {
            CreateEffect();
        }

        private void OnEnable()
        {
            if (effectRoot == null)
            {
                CreateEffect();
            }

            effectRoot.SetActive(true);
        }

        private void OnDisable()
        {
            if (effectRoot != null)
            {
                effectRoot.SetActive(false);
            }
        }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * pulseAmount;

            if (spotLight != null)
            {
                spotLight.intensity = lightIntensity * pulse;
            }

            if (beamMaterial != null)
            {
                Color currentColor = beamColor;
                currentColor.a = Mathf.Clamp01(beamColor.a * pulse);
                beamMaterial.SetColor("_BaseColor", currentColor);
                beamMaterial.color = currentColor;
            }
        }

        private void OnDestroy()
        {
            if (beamMaterial != null)
            {
                Destroy(beamMaterial);
            }

            if (beamMesh != null)
            {
                Destroy(beamMesh);
            }
        }

        private void CreateEffect()
        {
            effectRoot = new GameObject("InteractionBeaconEffect");
            effectRoot.transform.SetParent(transform, false);
            effectRoot.transform.localPosition = Vector3.up * beamHeight;
            effectRoot.layer = gameObject.layer;

            CreateVisibleBeam();
            if (useSpotLight)
            {
                CreateSpotLight();
            }
        }

        private void CreateVisibleBeam()
        {
            GameObject beamObject = new("SoftLightCone");
            beamObject.transform.SetParent(effectRoot.transform, false);
            beamObject.layer = gameObject.layer;

            MeshFilter meshFilter = beamObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = beamObject.AddComponent<MeshRenderer>();

            beamMesh = BuildConeMesh();
            beamMesh.name = "Dive Point Soft Light Cone";
            meshFilter.sharedMesh = beamMesh;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            beamMaterial = new Material(shader)
            {
                name = "Dive Point Beacon (Runtime)",
                renderQueue = (int)RenderQueue.Transparent
            };
            ConfigureTransparentMaterial(beamMaterial);
            meshRenderer.sharedMaterial = beamMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void CreateSpotLight()
        {
            GameObject lightObject = new("SoftSpotLight");
            lightObject.transform.SetParent(effectRoot.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lightObject.layer = gameObject.layer;

            spotLight = lightObject.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.color = new Color(beamColor.r, beamColor.g, beamColor.b, 1f);
            spotLight.intensity = lightIntensity;
            spotLight.range = lightRange;
            spotLight.spotAngle = beamAngle;
            spotLight.innerSpotAngle = beamAngle * innerAngleRatio;
            spotLight.shadows = LightShadows.None;
            spotLight.renderMode = LightRenderMode.Auto;
        }

        private Mesh BuildConeMesh()
        {
            int segments = Mathf.Clamp(beamSegments, 6, 48);
            float radius = Mathf.Tan(beamAngle * 0.5f * Mathf.Deg2Rad) * beamHeight;
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                vertices[index + 1] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    -beamHeight,
                    Mathf.Sin(angle) * radius);

                int triangleIndex = index * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = index + 1;
                triangles[triangleIndex + 2] = (index + 1) % segments + 1;
            }

            Mesh mesh = new();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ConfigureTransparentMaterial(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetColor("_BaseColor", beamColor);
            material.color = beamColor;
        }

        private void OnValidate()
        {
            beamHeight = Mathf.Max(0.5f, beamHeight);
            beamSegments = Mathf.Clamp(beamSegments, 6, 48);
            lightIntensity = Mathf.Max(0f, lightIntensity);
            lightRange = Mathf.Max(0.5f, lightRange);
            pulseAmount = Mathf.Max(0f, pulseAmount);
            pulseSpeed = Mathf.Max(0.05f, pulseSpeed);
        }
    }
}
