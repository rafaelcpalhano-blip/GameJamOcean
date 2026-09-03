using GameJamOcean.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GameJamOcean.EditorTools
{
    public static class PortUpgradePointSetup
    {
        [MenuItem("Tools/GameJamOcean/Create Port Upgrade Point")]
        public static void CreatePoint()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "OceanScene_3D")
            {
                EditorUtility.DisplayDialog("Port Upgrade Point", "Abra OceanScene_3D fora do Play Mode.", "OK");
                return;
            }
            GameObject existing = GameObject.Find("PortUpgradePoint3D");
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                return;
            }
            int layer = LayerMask.NameToLayer("Interactable");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Materials/SoftInteractionCircle.shader");
            GameObject water = GameObject.Find("Water");
            if (layer < 0 || shader == null || water == null)
            {
                EditorUtility.DisplayDialog("Port Upgrade Point", "Verifique a Layer Interactable, Water e a importação do shader SoftInteractionCircle.", "OK");
                return;
            }
            Vector3 position = Selection.activeTransform != null && Selection.activeGameObject.scene == scene
                ? Selection.activeTransform.position
                : water.transform.position;
            position.y = water.transform.position.y + 0.12f;

            const string materialPath = "Assets/Materials/PortUpgradeGlow.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "PortUpgradeGlow" };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            const string prefabPath = "Assets/Prefabs/Interacao/PortUpgradePoint3D.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject template = new("PortUpgradePoint3D");
                try
                {
                    template.layer = layer;
                    SphereCollider collider = template.AddComponent<SphereCollider>();
                    collider.isTrigger = true;
                    collider.radius = 1.25f;
                    template.AddComponent<InteractionEvent3D>();
                    GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    glow.name = "Soft Circle Glow";
                    glow.transform.SetParent(template.transform, false);
                    glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    glow.transform.localScale = new Vector3(3f, 3f, 1f);
                    Object.DestroyImmediate(glow.GetComponent<Collider>());
                    MeshRenderer renderer = glow.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    prefab = PrefabUtility.SaveAsPrefabAsset(template, prefabPath);
                }
                finally { Object.DestroyImmediate(template); }
            }
            if (prefab == null) return;
            GameObject point = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(point, "Create port upgrade interaction");
            point.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(point.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = point;
            EditorUtility.DisplayDialog("Port Upgrade Point",
                "Ponto criado. Posicione-o na água ao lado do píer e salve a cena. F/clique acionam On Interacted; conecte o painel quando estiver pronto.", "OK");
        }
    }
}
