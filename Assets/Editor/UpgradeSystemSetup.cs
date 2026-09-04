using System.Linq;
using GameJamOcean.Boat;
using GameJamOcean.Interaction;
using GameJamOcean.Progression;
using GameJamOcean.Weapons;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GameJamOcean.EditorTools
{
    public static class UpgradeSystemSetup
    {
        [MenuItem("Tools/GameJamOcean/Configure Upgrade System")]
        public static void Configure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "OceanScene_3D")
            { EditorUtility.DisplayDialog("Upgrades", "Abra OceanScene_3D fora do Play Mode.", "OK"); return; }
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            GameObject Find(string name) => all.FirstOrDefault(t => t.name == name)?.gameObject;
            GameObject[] villages = Enumerable.Range(1, 4).Select(i => Find($"AldeiaNV{i}")).ToArray();
            GameObject port = Find("PortUpgradePoint3D");
            BoatController3D boat = Find("BoatPlayer3D")?.GetComponent<BoatController3D>();
            InteractionEvent3D interaction = port != null ? port.GetComponent<InteractionEvent3D>() : null;
            if (villages.Any(v => v == null) || interaction == null || boat == null)
            { EditorUtility.DisplayDialog("Upgrades", "Preciso de AldeiaNV1 até AldeiaNV4, BoatPlayer3D e PortUpgradePoint3D com InteractionEvent3D.", "OK"); return; }
            foreach (var village in villages)
            {
                if (villages.Any(other => other != village && village.transform.IsChildOf(other.transform))
                    || boat.transform.IsChildOf(village.transform) || port.transform.IsChildOf(village.transform))
                { EditorUtility.DisplayDialog("Upgrades", "Deixe as quatro aldeias como versões independentes. Barco e ponto do porto não devem ser filhos de uma aldeia que será desativada.", "OK"); return; }
            }
            if (boat.GetComponent<BoatStats3D>() == null)
            { EditorUtility.DisplayDialog("Upgrades", "Configure antes Boat Status and HUD para adicionar BoatStats3D.", "OK"); return; }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            const string catalogPath = "Assets/Resources/UpgradeCatalog.asset";
            UpgradeCatalog catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            var harpoons = catalog.Find(UpgradeKind.Harpoon);
            if (harpoons != null)
            {
                string[] paths = { "Assets/Prefabs/Combat/harpoonNV1.prefab", "Assets/Prefabs/Combat/harpoonNV2.prefab", "Assets/Prefabs/Combat/harpoon NV3.prefab" };
                for (int i = 0; i < 3; i++)
                {
                    var tier = harpoons.Tier(i + 1);
                    if (tier != null && tier.harpoonPrefab == null)
                        tier.harpoonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i])?.GetComponent<HarpoonProjectile2D>();
                }
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            if (!catalog.Validate(out string error))
            { Selection.activeObject = catalog; EditorUtility.DisplayDialog("Upgrades", error, "OK"); return; }

            GameObject system = Find("UpgradeSystem");
            if (system == null)
            { system = new GameObject("UpgradeSystem"); Undo.RegisterCreatedObjectUndo(system, "Create upgrade system"); }
            PortUpgradePanel panel = system.GetComponent<PortUpgradePanel>();
            if (panel == null) panel = Undo.AddComponent<PortUpgradePanel>(system);
            Undo.RecordObject(panel, "Configure upgrade panel");
            panel.Configure(villages[0], villages[1], villages[2], villages[3], boat);
            Undo.RecordObject(interaction, "Connect port upgrade panel");
            for (int i = interaction.OnInteracted.GetPersistentEventCount() - 1; i >= 0; i--)
                if (interaction.OnInteracted.GetPersistentTarget(i) == panel && interaction.OnInteracted.GetPersistentMethodName(i) == nameof(PortUpgradePanel.Open))
                    UnityEventTools.RemovePersistentListener(interaction.OnInteracted, i);
            UnityEventTools.AddPersistentListener(interaction.OnInteracted, new UnityAction(panel.Open));
            PrefabUtility.RecordPrefabInstancePropertyModifications(interaction);
            // Scene starts at N1; saved progress is applied when playing.
            foreach (var village in villages) Undo.RecordObject(village, "Set initial village version");
            villages[0].SetActive(true);
            for (int i = 1; i < 4; i++) villages[i].SetActive(false);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(events, "Create UI input");
            }
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(interaction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = catalog;
            EditorUtility.DisplayDialog("Upgrades",
                "Sistema configurado! Balanceie custos e valores no UpgradeCatalog. A aldeia termina no N4; os demais no N3. Interaja com o porto para abrir o painel.", "OK");
        }

        [MenuItem("Tools/GameJamOcean/Reset Saved Gold and Upgrades")]
        public static void ResetSavedProgress()
        {
            if (EditorUtility.DisplayDialog("Apagar progresso de teste?",
                "Isso zera o ouro salvo e retorna TODOS os upgrades ao N1. Não apaga seus assets nem o catálogo.", "Apagar progresso", "Cancelar"))
                GameProgress.ResetSavedProgress();
        }
    }
}
