using GameJamOcean.Boat;
using UnityEditor;
using UnityEngine;

namespace GameJamOcean.EditorTools
{
    [InitializeOnLoad]
    public static class BoatVisualUpgradeSettingsBuilder
    {
        private const string SettingsPath = "Assets/Resources/BoatVisualUpgradeSettings.asset";

        static BoatVisualUpgradeSettingsBuilder()
        {
            EditorApplication.delayCall += EnsureSettings;
        }

        [MenuItem("Tools/GameJamOcean/Refresh Boat Prefab References")]
        public static void EnsureSettings()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureSettings;
                return;
            }

            BoatVisualUpgradeSettings settings = AssetDatabase.LoadAssetAtPath<BoatVisualUpgradeSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BoatVisualUpgradeSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.boat1Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Barcos/boat1.prefab");
            settings.boat2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Barcos/boat2.prefab");
            settings.boat3Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Barcos/boat3.prefab");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            if (!settings.IsValid)
                Debug.LogError("Boat prefabs not found. Keep boat1, boat2 and boat3 in Assets/Prefabs/Barcos.");
        }
    }
}
