using System.Collections.Generic;
using System.Linq;
using GameJamOcean.Boat;
using GameJamOcean.CameraSystem;
using GameJamOcean.Combat;
using GameJamOcean.Interaction;
using GameJamOcean.Spawning;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJamOcean.EditorTools
{
    public static class OceanScene3DSetup
    {
        private const string TargetSceneName = "OceanScene_3D";
        private const string TargetScenePath = "Assets/Scenes/OceanScene_3D.unity";
        private const string DiveScenePath = "Assets/Scenes/DiveScene.unity";
        private const string DivePointPrefabPath = "Assets/Prefabs/Interacao/LifeguardDivePoint3D.prefab";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Tools/GameJamOcean/Configure OceanScene 3D")]
        public static void ConfigureActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != TargetSceneName)
            {
                EditorUtility.DisplayDialog(
                    "OceanScene 3D",
                    $"Abra a cena '{TargetSceneName}' antes de executar este configurador.",
                    "OK");
                return;
            }

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0)
            {
                EditorUtility.DisplayDialog(
                    "Missing layer",
                    "Crie a Layer 'Interactable' antes de configurar a cena.",
                    "OK");
                return;
            }

            GameObject boatRoot = ConfigureBoat();
            Camera mainCamera = ConfigureCamera(boatRoot.transform);
            PlayerInteractor3D interactor = boatRoot.GetComponent<PlayerInteractor3D>();
            interactor.Configure(mainCamera, boatRoot.transform, 1 << interactableLayer);

            DisableWaterCollision();
            ConfigureBoatBounds(boatRoot);
            ConfigureDiveSpawnSystem(boatRoot.transform, false);
            ConfigureInteractionUI(interactor, mainCamera);
            ConfigureBoatStatusUI(boatRoot);
            ConfigureEventSystem();
            ConfigureBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScenePath);
            Selection.activeGameObject = boatRoot;
            EditorUtility.DisplayDialog(
                "OceanScene 3D",
                "Configuração concluída. Teste o barco com WASD e aproxime-se da boia.",
                "OK");
        }

        [MenuItem("Tools/GameJamOcean/Configure Water Bounds and Dive Spawns")]
        public static void ConfigureWaterBoundsAndDiveSpawns()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != TargetSceneName)
            {
                EditorUtility.DisplayDialog(
                    "OceanScene 3D",
                    $"Abra a cena '{TargetSceneName}' antes de executar este configurador.",
                    "OK");
                return;
            }

            GameObject boatRoot = GameObject.Find("BoatPlayer3D");
            if (boatRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "OceanScene 3D",
                    "O objeto BoatPlayer3D não foi encontrado. Execute primeiro Configure OceanScene 3D.",
                    "OK");
                return;
            }

            ConfigureBoatBounds(boatRoot);
            ConfigureDiveSpawnSystem(boatRoot.transform, true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScenePath);
            GameObject exclusion = GameObject.Find("IslandSpawnExclusion");
            if (exclusion != null)
            {
                Selection.activeGameObject = exclusion;
            }
            EditorUtility.DisplayDialog(
                "OceanScene 3D",
                "Limite do barco e 30 pontos de mergulho configurados. O círculo amarelo selecionado impede boias sobre a ilha.",
                "OK");
        }

        [MenuItem("Tools/GameJamOcean/Configure Boat Status and HUD")]
        public static void ConfigureBoatStatusAndHud()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != TargetSceneName)
            {
                EditorUtility.DisplayDialog(
                    "OceanScene 3D",
                    $"Abra a cena '{TargetSceneName}' antes de executar este configurador.",
                    "OK");
                return;
            }

            GameObject boatRoot = GameObject.Find("BoatPlayer3D");
            if (boatRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "OceanScene 3D",
                    "O objeto BoatPlayer3D não foi encontrado. Execute primeiro Configure OceanScene 3D.",
                    "OK");
                return;
            }

            ConfigureBoatStats(boatRoot);
            ConfigureBoatStatusUI(boatRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScenePath);
            Selection.activeGameObject = boatRoot;
            EditorUtility.DisplayDialog(
                "Boat Status",
                "Vida, atributos escaláveis e HUD do barco foram configurados.",
                "OK");
        }

        [MenuItem("Tools/GameJamOcean/Configure Boat Foam")]
        public static void ConfigureBoatFoam()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject boat = GameObject.Find("BoatPlayer3D");
            GameObject water = GameObject.Find("Water");
            if (EditorApplication.isPlaying || scene.name != TargetSceneName
                || boat == null || water == null)
            {
                EditorUtility.DisplayDialog("Boat Foam",
                    "Abra OceanScene_3D fora do Play Mode, com BoatPlayer3D e Water presentes.", "OK");
                return;
            }
            const string materialPath = "Assets/Materials/BoatFoam.mat";
            const string contactMaterialPath = "Assets/Materials/BoatContactFoam.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Material contactMaterial = AssetDatabase.LoadAssetAtPath<Material>(contactMaterialPath);
            if (material == null)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Materials/BoatFoam.shader");
                if (shader == null)
                {
                    EditorUtility.DisplayDialog("Boat Foam", "Aguarde a importação de BoatFoam.shader.", "OK");
                    return;
                }
                material = new Material(shader) { name = "BoatFoam" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            BoatFoam3D foam = GetOrAddComponent<BoatFoam3D>(boat);
            Undo.RecordObject(foam, "Configure boat foam");
            foam.Configure(material, contactMaterial, water.transform, boat.GetComponent<BoxCollider>());
            EditorUtility.SetDirty(foam);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScenePath);
            Selection.activeGameObject = boat;
            EditorUtility.DisplayDialog("Boat Foam",
                "Espuma configurada. Entre no Play Mode e navegue para visualizar. Controles e câmera preservados.", "OK");
        }

        [MenuItem("Tools/GameJamOcean/Configure Relaxed Boat Handling")]
        public static void ConfigureRelaxedBoatHandling()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject boat = GameObject.Find("BoatPlayer3D");
            if (EditorApplication.isPlaying || scene.name != TargetSceneName
                || boat == null || Camera.main == null)
            {
                EditorUtility.DisplayDialog("Boat Handling",
                    "Abra OceanScene_3D fora do Play Mode, com BoatPlayer3D e Main Camera presentes.", "OK");
                return;
            }

            BoatController3D controller = boat.GetComponent<BoatController3D>();
            CameraFollow3D follow = Camera.main.GetComponent<CameraFollow3D>();
            if (controller == null || follow == null) return;
            Undo.RecordObject(controller, "Relaxed boat handling");
            controller.ConfigureRelaxedHandling();
            BoatStats3D stats = boat.GetComponent<BoatStats3D>();
            if (stats != null)
            {
                Undo.RecordObject(stats, "Relaxed base acceleration");
                stats.ConfigureRelaxedAcceleration();
            }
            Undo.RecordObject(follow, "Relaxed camera follow");
            follow.ConfigureRelaxedFollow();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScenePath);
            Selection.activeGameObject = boat;
            EditorUtility.DisplayDialog("Boat Handling",
                "Controles de leme, inércia, freio/ré e câmera suave configurados. Offset preservado.", "OK");
        }

        [MenuItem("Tools/GameJamOcean/Configure Boat Camera and Water Motion")]
        public static void ConfigureBoatCameraAndWaterMotion()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject boat = GameObject.Find("BoatPlayer3D");
            if (EditorApplication.isPlaying || scene.name != TargetSceneName
                || boat == null || Camera.main == null)
            {
                EditorUtility.DisplayDialog("Boat Camera",
                    "Abra OceanScene_3D fora do Play Mode, com BoatPlayer3D e Main Camera presentes.", "OK");
                return;
            }

            Transform model = boat.transform.Find("boat-speed-b");
            if (model == null)
            {
                EditorUtility.DisplayDialog("Boat Camera",
                    "Não encontrei o filho visual boat-speed-b dentro de BoatPlayer3D.", "OK");
                return;
            }

            CameraFollow3D follow = GetOrAddComponent<CameraFollow3D>(Camera.main.gameObject);
            Undo.RecordObject(follow, "Follow boat heading");
            follow.ConfigureHeadingFollow(boat.transform);
            BoatWaterMotion3D motion = GetOrAddComponent<BoatWaterMotion3D>(boat);
            Undo.RecordObject(motion, "Configure visual boat motion");
            motion.Configure(model);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScenePath);
            Selection.activeGameObject = Camera.main.gameObject;
            EditorUtility.DisplayDialog("Boat Camera",
                "Câmera e balanço visual configurados. O Offset da câmera foi preservado.", "OK");
        }

        private static GameObject ConfigureBoat()
        {
            GameObject boatRoot = GameObject.Find("BoatPlayer3D");
            GameObject boatModel = GameObject.Find("boat-speed-b");

            if (boatRoot == null)
            {
                if (boatModel == null)
                {
                    throw new MissingReferenceException("The scene needs an object named 'boat-speed-b'.");
                }

                boatRoot = new GameObject("BoatPlayer3D");
                Undo.RegisterCreatedObjectUndo(boatRoot, "Create 3D boat player");
                boatRoot.transform.SetPositionAndRotation(
                    boatModel.transform.position,
                    boatModel.transform.rotation);
                Undo.SetTransformParent(boatModel.transform, boatRoot.transform, "Parent boat model");
                boatModel.transform.localPosition = Vector3.zero;
                boatModel.transform.localRotation = Quaternion.identity;
            }

            Rigidbody body = GetOrAddComponent<Rigidbody>(boatRoot);
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezePositionY
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ;

            BoxCollider collider = GetOrAddComponent<BoxCollider>(boatRoot);
            FitBoxCollider(boatRoot.transform, collider, new Vector3(0.72f, 0.45f, 0.78f));

            BoatController3D controller = GetOrAddComponent<BoatController3D>(boatRoot);
            controller.ConfigureInput(
                FindActionReference("Player", "Move"),
                FindActionReference("Player", "Sprint"));
            GetOrAddComponent<PlayerInteractor3D>(boatRoot);
            ConfigureBoatStats(boatRoot);
            return boatRoot;
        }

        private static void ConfigureBoatStats(GameObject boatRoot)
        {
            GetOrAddComponent<Health>(boatRoot);
            BoatStats3D stats = GetOrAddComponent<BoatStats3D>(boatRoot);
            stats.ConfigureBaseStats(100f, 6f, 2.5f);
            stats.ConfigureBaseTurbo(3f, 1.75f, 1.5f, 0.5f, 1.25f);
        }

        private static Camera ConfigureCamera(Transform boat)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new("Main Camera");
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create main camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
            }

            mainCamera.orthographic = false;
            mainCamera.fieldOfView = 50f;
            CameraFollow3D follow = GetOrAddComponent<CameraFollow3D>(mainCamera.gameObject);
            follow.Configure(boat, new Vector3(0f, 12f, -10f));
            mainCamera.transform.position = boat.position + new Vector3(0f, 12f, -10f);
            mainCamera.transform.LookAt(boat.position + Vector3.up * 0.5f);
            return mainCamera;
        }

        private static void DisableWaterCollision()
        {
            GameObject water = GameObject.Find("Water");
            if (water == null)
            {
                return;
            }

            foreach (Collider collider in water.GetComponentsInChildren<Collider>(true))
            {
                Undo.RecordObject(collider, "Disable visual water collision");
                collider.enabled = false;
            }
        }

        private static void ConfigureBoatBounds(GameObject boatRoot)
        {
            GameObject water = GameObject.Find("Water");
            Renderer waterRenderer = water != null
                ? water.GetComponentInChildren<Renderer>()
                : null;
            if (waterRenderer == null)
            {
                throw new MissingReferenceException("The Water object needs a Renderer.");
            }

            BoatWaterBounds3D bounds = GetOrAddComponent<BoatWaterBounds3D>(boatRoot);
            bounds.Configure(waterRenderer, 5f);
        }

        private static void ConfigureDiveSpawnSystem(Transform boat, bool forceRegeneratePoints)
        {
            GameObject water = GameObject.Find("Water");
            Renderer waterRenderer = water != null
                ? water.GetComponentInChildren<Renderer>()
                : null;
            GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(DivePointPrefabPath);
            DivePointInteractable3D divePointPrefab = prefabObject != null
                ? prefabObject.GetComponent<DivePointInteractable3D>()
                : null;
            if (waterRenderer == null || divePointPrefab == null)
            {
                throw new MissingReferenceException(
                    "Water Renderer or LifeguardDivePoint3D prefab was not found.");
            }

            GameObject oldDivePoint = GameObject.Find("DivePoint_3D");
            if (oldDivePoint != null)
            {
                Undo.DestroyObjectImmediate(oldDivePoint);
            }

            GameObject systemObject = GameObject.Find("DivePointSpawnSystem");
            if (systemObject == null)
            {
                systemObject = new GameObject("DivePointSpawnSystem");
                Undo.RegisterCreatedObjectUndo(systemObject, "Create dive point spawn system");
            }

            Transform pointsParent = FindOrCreateChild(systemObject.transform, "DiveSpawnPoints");
            Transform activeParent = FindOrCreateChild(systemObject.transform, "ActiveDivePoints");
            DiveSpawnExclusionCircle3D islandExclusion = GetOrCreateIslandExclusion(
                systemObject.transform,
                water.transform.position.y);
            List<Transform> points = GetOrGenerateSpawnPoints(
                pointsParent,
                waterRenderer.bounds,
                water.transform.position.y,
                boat.position,
                islandExclusion,
                forceRegeneratePoints);

            DivePointSpawnManager3D manager = GetOrAddComponent<DivePointSpawnManager3D>(systemObject);
            manager.Configure(divePointPrefab, points, activeParent, 10);
        }

        private static List<Transform> GetOrGenerateSpawnPoints(
            Transform pointsParent,
            Bounds waterBounds,
            float waterHeight,
            Vector3 boatPosition,
            DiveSpawnExclusionCircle3D islandExclusion,
            bool forceRegeneratePoints)
        {
            const int desiredCount = 30;
            List<Transform> existingPoints = new();
            foreach (Transform child in pointsParent)
            {
                existingPoints.Add(child);
            }

            if (!forceRegeneratePoints && existingPoints.Count == desiredCount)
            {
                return existingPoints;
            }

            for (int index = pointsParent.childCount - 1; index >= 0; index--)
            {
                Undo.DestroyObjectImmediate(pointsParent.GetChild(index).gameObject);
            }

            float mapPadding = 8f;
            float minimumSeparation = 8f;

            List<Vector3> positions = new();
            Random.State previousRandomState = Random.state;
            Random.InitState(60309);

            for (int attempt = 0; attempt < 6000 && positions.Count < desiredCount; attempt++)
            {
                Vector3 candidate = new(
                    Random.Range(waterBounds.min.x + mapPadding, waterBounds.max.x - mapPadding),
                    waterHeight,
                    Random.Range(waterBounds.min.z + mapPadding, waterBounds.max.z - mapPadding));

                if (islandExclusion.ContainsXZ(candidate)
                    || HorizontalDistance(candidate, boatPosition) < minimumSeparation
                    || IsNearExistingPoint(candidate, positions, minimumSeparation))
                {
                    continue;
                }

                positions.Add(candidate);
            }

            Random.state = previousRandomState;
            List<Transform> generatedPoints = new();
            for (int index = 0; index < positions.Count; index++)
            {
                GameObject point = new($"DiveSpawnPoint_{index + 1:00}");
                Undo.RegisterCreatedObjectUndo(point, "Create dive spawn point");
                point.transform.SetParent(pointsParent, false);
                point.transform.position = positions[index];
                generatedPoints.Add(point.transform);
            }

            if (generatedPoints.Count < desiredCount)
            {
                Debug.LogWarning($"Only {generatedPoints.Count} valid dive spawn points were generated.");
            }

            return generatedPoints;
        }

        private static DiveSpawnExclusionCircle3D GetOrCreateIslandExclusion(
            Transform system,
            float waterHeight)
        {
            const string exclusionName = "IslandSpawnExclusion";
            Transform exclusionTransform = system.Find(exclusionName);
            bool wasCreated = exclusionTransform == null;
            if (wasCreated)
            {
                GameObject exclusionObject = new(exclusionName);
                Undo.RegisterCreatedObjectUndo(exclusionObject, "Create island spawn exclusion");
                exclusionObject.transform.SetParent(system, false);
                exclusionTransform = exclusionObject.transform;
            }

            DiveSpawnExclusionCircle3D exclusion =
                GetOrAddComponent<DiveSpawnExclusionCircle3D>(exclusionTransform.gameObject);
            if (wasCreated)
            {
                GameObject ground = GameObject.Find("Ground");
                Vector3 center = ground != null
                    ? ground.transform.position
                    : Vector3.zero;
                center.y = waterHeight + 0.1f;
                exclusion.Configure(center, 36f);
            }

            return exclusion;
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new(childName);
            Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static bool IsNearExistingPoint(
            Vector3 candidate,
            List<Vector3> existingPoints,
            float minimumDistance)
        {
            foreach (Vector3 existingPoint in existingPoints)
            {
                if (HorizontalDistance(candidate, existingPoint) < minimumDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            Vector2 firstXZ = new(first.x, first.z);
            Vector2 secondXZ = new(second.x, second.z);
            return Vector2.Distance(firstXZ, secondXZ);
        }

        private static void ConfigureInteractionUI(PlayerInteractor3D interactor, Camera mainCamera)
        {
            GameObject canvasObject = GetOrCreateOceanCanvas();

            Transform existingText = canvasObject.transform.Find("InteractionPromptText");
            GameObject textObject;
            if (existingText == null)
            {
                textObject = new GameObject(
                    "InteractionPromptText",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(textObject, "Create interaction prompt");
                textObject.transform.SetParent(canvasObject.transform, false);
            }
            else
            {
                textObject = existingText.gameObject;
            }

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = InteractionPromptUI2D.DefaultFirstTimeMessage;
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(320f, 60f);

            InteractionPromptUI2D prompt = GetOrAddComponent<InteractionPromptUI2D>(textObject);
            prompt.Configure3D(interactor, mainCamera, text);
        }

        private static void ConfigureBoatStatusUI(GameObject boatRoot)
        {
            GameObject canvasObject = GetOrCreateOceanCanvas();
            Transform existingHud = canvasObject.transform.Find("BoatStatusHUD");
            GameObject hudObject;
            if (existingHud == null)
            {
                hudObject = new GameObject("BoatStatusHUD", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(hudObject, "Create boat status HUD");
                hudObject.transform.SetParent(canvasObject.transform, false);
            }
            else
            {
                hudObject = existingHud.gameObject;
            }

            RectTransform hudRect = hudObject.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0f, 1f);
            hudRect.anchorMax = new Vector2(0f, 1f);
            hudRect.pivot = new Vector2(0f, 1f);
            hudRect.anchoredPosition = new Vector2(24f, -24f);
            hudRect.sizeDelta = new Vector2(360f, 84f);

            CreateHudBar(
                hudRect,
                "HealthBar",
                0f,
                new Color(0.2f, 0.85f, 0.35f, 1f),
                out Image healthFill,
                out TextMeshProUGUI healthText);
            Transform legacyAccelerationBar = hudRect.Find("AccelerationBar");
            if (hudRect.Find("TurboBar") == null && legacyAccelerationBar != null)
            {
                legacyAccelerationBar.name = "TurboBar";
            }

            CreateHudBar(
                hudRect,
                "TurboBar",
                -44f,
                new Color(0.15f, 0.75f, 1f, 1f),
                out Image turboFill,
                out TextMeshProUGUI turboText);

            BoatStatusHUD3D hud = GetOrAddComponent<BoatStatusHUD3D>(hudObject);
            hud.Configure(
                boatRoot.GetComponent<Health>(),
                boatRoot.GetComponent<BoatController3D>(),
                healthFill,
                healthText,
                turboFill,
                turboText);
        }

        private static GameObject GetOrCreateOceanCanvas()
        {
            GameObject canvasObject = GameObject.Find("OceanInteractionCanvas");
            if (canvasObject == null)
            {
                canvasObject = new GameObject(
                    "OceanInteractionCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create ocean canvas");
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasObject;
        }

        private static void CreateHudBar(
            RectTransform parent,
            string barName,
            float verticalPosition,
            Color fillColor,
            out Image fill,
            out TextMeshProUGUI label)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Transform existingBar = parent.Find(barName);
            GameObject barObject;
            if (existingBar == null)
            {
                barObject = new GameObject(barName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(barObject, $"Create {barName}");
                barObject.transform.SetParent(parent, false);
            }
            else
            {
                barObject = existingBar.gameObject;
            }

            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, verticalPosition);
            barRect.sizeDelta = new Vector2(0f, 36f);
            Image background = barObject.GetComponent<Image>();
            background.sprite = uiSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.025f, 0.06f, 0.09f, 0.88f);
            background.raycastTarget = false;

            Transform existingFill = barObject.transform.Find("Fill");
            GameObject fillObject;
            if (existingFill == null)
            {
                fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(fillObject, $"Create {barName} fill");
                fillObject.transform.SetParent(barObject.transform, false);
            }
            else
            {
                fillObject = existingFill.gameObject;
            }

            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            fill = fillObject.GetComponent<Image>();
            fill.sprite = uiSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            fill.color = fillColor;
            fill.raycastTarget = false;

            Transform existingLabel = barObject.transform.Find("Label");
            GameObject labelObject;
            if (existingLabel == null)
            {
                labelObject = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(labelObject, $"Create {barName} label");
                labelObject.transform.SetParent(barObject.transform, false);
            }
            else
            {
                labelObject = existingLabel.gameObject;
            }

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        private static void ConfigureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create event system");
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(item => item.path != TargetScenePath
                    && item.path != "Assets/Scenes/OceanScene.unity"
                    && item.path != "Assets/Scenes/OceanScene_backup.unity"
                    && item.path != DiveScenePath)
                .ToList();

            scenes.Insert(0, new EditorBuildSettingsScene(TargetScenePath, true));
            scenes.Insert(1, new EditorBuildSettingsScene(DiveScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static InputActionReference FindActionReference(string mapName, string actionName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(reference => reference.action != null
                    && reference.action.actionMap?.name == mapName
                    && reference.action.name == actionName);
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static void FitBoxCollider(
            Transform root,
            BoxCollider targetCollider,
            Vector3 sizeMultiplier)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            Undo.RecordObject(targetCollider, "Fit box collider");
            targetCollider.center = root.InverseTransformPoint(bounds.center);
            Vector3 scale = root.lossyScale;
            Vector3 localSize = new(
                bounds.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                bounds.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                bounds.size.z / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
            targetCollider.size = Vector3.Scale(localSize, sizeMultiplier);
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
