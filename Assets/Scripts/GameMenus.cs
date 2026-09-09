using System;
using System.Collections;
using System.Collections.Generic;
using GameJamOcean.Boat;
using GameJamOcean.Flow;
using GameJamOcean.Interaction;
using GameJamOcean.Progression;
using GameJamOcean.Spawning;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJamOcean.UI
{
    // Runtime bootstrap: no duplicated ocean scene or manual canvas wiring required.
    [DefaultExecutionOrder(-2000)]
    public sealed class GameMenus : MonoBehaviour
    {
        private const string Ocean = "OceanScene_3D";
        private const string SettingsKey = "GameJamOcean.Settings.";
        private const string TutorialsArmedKey = "GameJamOcean.Tutorials.Armed";
        private const string OceanTutorialKey = "GameJamOcean.Tutorials.OceanShown";
        private const string DiveTutorialKey = "GameJamOcean.Tutorials.DiveShown";
        private static GameMenus instance;
        private static int resumeFrame = -1;
        public static bool BoatRecoveryActive { get; set; }
        public static bool EndGameActive { get; set; }
        public static bool BlocksGameplay => BoatRecoveryActive || EndGameActive || Time.timeScale <= 0f || (instance != null && instance.open) || Time.frameCount == resumeFrame;
        public static float SteeringMultiplier { get; private set; } = 1f;
        private bool firstScene = true, requestMain, main, open, loading;
        private bool requestIntro, transitioning;
        private bool showingLetter;
        private Coroutine typewriter;
        private Coroutine continuePromptBlink;
        private TMP_Text continuePrompt;
        private static readonly string[] RescueMessages =
        {
            "O mar não está pra peixe… e hoje também não estava pra barco! Respire fundo: amanhã a pescaria de tesouros continua.",
            "Você foi conferir o fundo do mar levando o barco inteiro? Entusiasmo não falta! Já está tudo pronto para outra aventura.",
            "Até o melhor pescador já voltou ao píer com mais água no barco do que peixe no balde. Bora tentar de novo!",
            "Seu barco confundiu a profissão e quis virar submarino. Conversamos com ele: agora é navegar e deixar o mergulho com você!",
            "O mar cobrou um banho, mas não levou sua coragem. Sacuda a água das botas: ainda tem muito tesouro esperando!",
            "Dizem que quem procura acha. Você achou uma pedra! Na próxima, vamos tentar achar um baú, combinado?",
            "Calma, capitão! Isso não foi naufrágio, foi uma visita técnica ao fundo do mar. O estaleiro já liberou sua próxima tentativa!",
            "Se afundar desse experiência, você já era almirante! Levante a cabeça: o fundo do mar ainda reserva grandes aventuras."
        };
        private int letterFrame;
        private GameJamOcean.Audio.GameAudio gameAudio;
        private float savedTimeScale;
        private bool savedAudioPause, savedCursorVisible;
        private CursorLockMode savedCursorLock;
        private Canvas canvas;
        private RectTransform panel;
        private RectTransform panelContent;
        private EditableUIPanelKind? activePanelKind;
        private readonly List<Canvas> hiddenCanvases = new();
        private Rigidbody menuBoat;
        private bool boatWasKinematic;
        private Vector3 boatVelocity, boatAngularVelocity;
        private bool customCursorApplied;
        private Texture2D scaledCursorTexture;
        private Texture2D cursorSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; resumeFrame = -1; SteeringMultiplier = 1f; BoatRecoveryActive = false; EndGameActive = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance == null) new GameObject("GameMenus").AddComponent<GameMenus>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInitialMenu()
        {
            // Fallback for Editor startup without a normal Single sceneLoaded notification.
            Bootstrap();
            if (instance.firstScene)
                instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            gameAudio = gameObject.AddComponent<GameJamOcean.Audio.GameAudio>();
            SteeringMultiplier = Mathf.Clamp(PlayerPrefs.GetFloat(SettingsKey + "Steering", 1f), .85f, 1.15f);
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsKey + "Volume", .5f));
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The initial Editor scene can arrive as Additive. Do not skip its menu.
            if (mode != LoadSceneMode.Single && !firstScene) return;
            if (scene.name != Ocean && scene.name != "DiveScene") return;
            gameAudio.SetScene(scene.name);
            if (scene.name == Ocean) GameJamOcean.World.OceanRockSetup.Configure(scene);
            if (scene.name == Ocean) GameJamOcean.World.SharkFinPatrol3D.ConfigureScene(scene);
            if (scene.name == Ocean) GameJamOcean.World.WindVfxEnhancer3D.ConfigureScene(scene);
            if (scene.name == Ocean) OceanGoldHUD.Ensure();
            if (scene.name == Ocean) GameJamOcean.Boat.BoatVisualUpgrade3D.ConfigureScene(scene);
            if (scene.name == Ocean) GameJamOcean.World.LighthouseEndGameLight3D.ConfigureScene(scene);
            if (scene.name == Ocean) GameJamOcean.World.OceanHorizonBackdrop3D.ConfigureScene(scene);
            if (scene.name == Ocean) GameJamOcean.World.OceanVfxScene3D.ConfigureScene(scene);
            loading = false;
            bool showMain = scene.name == Ocean && (firstScene || requestMain);
            firstScene = false;
            requestMain = false;
            if (showMain || requestIntro)
            {
                Freeze(true);
                FindFirstObjectByType<GameJamOcean.CameraSystem.CameraFollow3D>()?.ShowMenuView();
                if (requestIntro) { requestIntro = false; ShowLetter(); }
                else ShowHome();
            }
            else if (scene.name == "DiveScene" && PlayerPrefs.GetInt(TutorialsArmedKey, 0) == 1
                     && PlayerPrefs.GetInt(DiveTutorialKey, 0) == 0)
            {
                Freeze(false);
                ShowControlsTutorial(false);
            }
            StartCoroutine(ApplyFontsAfterUiCreation());
        }

        private static IEnumerator ApplyFontsAfterUiCreation()
        {
            // Scene UI and runtime panels finish their Start methods on the next frame.
            yield return null;
            yield return null;
            GameFontStyles.ApplyLoadedTexts();
        }

        private void Update()
        {
            if (showingLetter)
            {
                var keys = Keyboard.current;
                bool direction = keys != null && (keys.wKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame
                    || keys.sKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame
                    || keys.downArrowKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame);
                bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                if (typewriter == null && Time.frameCount > letterFrame && (direction || click))
                {
                    showingLetter = false;
                    StopTypewriter();
                    gameAudio.StopLetter();
                    StartGameplay();
                }
                return;
            }
            if (BoatRecoveryActive || loading || transitioning || Keyboard.current == null || !Keyboard.current.pKey.wasPressedThisFrame) return;
            if (open) { if (!main) Resume(); return; }
            string scene = SceneManager.GetActiveScene().name;
            if (scene != Ocean && scene != "DiveScene") return;
            // Existing upgrade/results windows own their own pause. Never steal that ownership.
            if (Time.timeScale <= 0f) return;
            var flow = FindFirstObjectByType<DiveSessionFlowController>();
            if (flow != null && flow.IsTransitioning) return;
            Freeze(false);
            ShowHome();
        }

        private void LateUpdate()
        {
            UpdateCursorAppearance();
            if (!open || !main) return;
            // Also catches HUDs constructed by other components in Start.
            foreach (var other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (other == canvas || !other.enabled || other.renderMode == RenderMode.WorldSpace) continue;
                hiddenCanvases.Add(other);
                other.enabled = false;
            }
        }

        private void UpdateCursorAppearance()
        {
            Texture2D source = EditableUIFactory.Settings != null
                ? EditableUIFactory.Settings.gameCursor : null;
            if (source != cursorSource)
            {
                if (scaledCursorTexture != null) Destroy(scaledCursorTexture);
                cursorSource = source;
                scaledCursorTexture = source != null ? CreateScaledCursor(source, 2) : null;
                customCursorApplied = false;
            }
            Texture2D cursor = scaledCursorTexture;
            if (Cursor.visible && cursor != null)
            {
                if (customCursorApplied) return;
                Cursor.SetCursor(cursor, EditableUIFactory.Settings.cursorHotspot * 2f,
                    CursorMode.ForceSoftware);
                customCursorApplied = true;
            }
            else if (customCursorApplied)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                customCursorApplied = false;
            }
        }

        private static Texture2D CreateScaledCursor(Texture2D source, int scale)
        {
            Color32[] original = source.GetPixels32();
            int width = source.width * scale;
            int height = source.height * scale;
            Color32[] enlarged = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                enlarged[y * width + x] = original[(y / scale) * source.width + x / scale];
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = source.name + " 2x Cursor", filterMode = FilterMode.Point };
            texture.SetPixels32(enlarged);
            // Cursor.SetCursor requires CPU-readable pixels even after upload.
            texture.Apply(false, false);
            return texture;
        }

        private void Freeze(bool isMain)
        {
            main = isMain;
            savedTimeScale = Time.timeScale;
            savedAudioPause = AudioListener.pause;
            savedCursorVisible = Cursor.visible;
            savedCursorLock = Cursor.lockState;
            open = true;
            if (!isMain)
            {
                Time.timeScale = 0f;
                AudioListener.pause = true;
                gameAudio.SetDivePauseAmbience(SceneManager.GetActiveScene().name == "DiveScene");
            }
            else
            {
                // Keep waves, ambient sounds and scenery alive; only anchor the player.
                var boat = FindFirstObjectByType<BoatController3D>();
                menuBoat = boat != null ? boat.GetComponent<Rigidbody>() : null;
                if (menuBoat != null)
                {
                    boatWasKinematic = menuBoat.isKinematic;
                    boatVelocity = menuBoat.linearVelocity;
                    boatAngularVelocity = menuBoat.angularVelocity;
                    menuBoat.isKinematic = true;
                }
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EnsureCanvas();
            canvas.gameObject.SetActive(true);
        }

        private void Resume()
        {
            if (!open) return;
            if (menuBoat != null)
            {
                menuBoat.isKinematic = boatWasKinematic;
                if (!boatWasKinematic)
                {
                    menuBoat.linearVelocity = boatVelocity;
                    menuBoat.angularVelocity = boatAngularVelocity;
                }
                menuBoat = null;
            }
            open = false;
            gameAudio.SetDivePauseAmbience(false);
            resumeFrame = Time.frameCount;
            Time.timeScale = savedTimeScale;
            AudioListener.pause = savedAudioPause;
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
            if (canvas != null) canvas.gameObject.SetActive(false);
            foreach (var other in hiddenCanvases) if (other != null) other.enabled = true;
            hiddenCanvases.Clear();
            PlayerPrefs.Save();
        }

        private void StartGameplay()
        {
            if (transitioning) return;
            GameProgress.Instance.SaveProgress();
            transitioning = true;
            canvas.gameObject.SetActive(false);
            StartCoroutine(EnterGameplay());
        }

        private void ShowLetter()
        {
            GameProgress.Instance.SaveProgress();
            showingLetter = true;
            letterFrame = Time.frameCount;
            ClearPanel("UMA NOVA VIDA", EditableUIPanelKind.TutorialPanel);
            TMP_Text body = Label("<b>Um bom lugar para começar uma vida de mergulhador profissional, não acha?</b>\n\n"
                + "Dizem que tesouros e riquezas esquecidas aguardam nas profundezas.\n"
                + "Quanto mais você conquistar, mais poderá melhorar suas instalações, seu transporte e seus equipamentos. "
                + "Mas não se engane: quanto mais forte você ficar, mais profundo poderá ir… e maiores serão os perigos que encontrará.",
                -95, 23, 335);
            gameAudio.StartLetter();
            BeginTypewriter(body);
            PrepareContinuePrompt(-450);
        }

        public static void ShowRescueLetter(bool firstDeathFree, int chargedGold, int configuredCost)
        {
            BoatRecoveryActive = false;
            if (instance == null) return;
            instance.Freeze(false);
            instance.showingLetter = true;
            instance.letterFrame = Time.frameCount;
            instance.ClearPanel("DE VOLTA AO ESTALEIRO", EditableUIPanelKind.TutorialPanel);
            bool receivedDiscount = !firstDeathFree && chargedGold < configuredCost;
            string message = firstDeathFree
                ? "Destruir o barco custa mais do que mantê-lo em boas condições, então procure deixar a manutenção em dia. Desta vez, como sua missão é nobre e ajuda o vilarejo a crescer, o primeiro conserto fica por conta da vila."
                : receivedDiscount
                    ? $"Conserto após o naufrágio: {configuredCost} Gold.\nValor debitado: {chargedGold} Gold.\n\nA vila concedeu um desconto porque sabe que suas finanças não andam boas. A vida no mar não é fácil, mas pode ser muito recompensadora conforme você adquire experiência."
                    : $"{RescueMessages[UnityEngine.Random.Range(0, RescueMessages.Length)]}\n\nConserto após o naufrágio: {configuredCost} Gold.\nValor debitado: {chargedGold} Gold.";
            TMP_Text body = instance.Label(message, -130, 26, 260);
            body.rectTransform.sizeDelta = new Vector2(480f, body.rectTransform.sizeDelta.y);
            instance.gameAudio.StartLetter();
            instance.BeginTypewriter(body);
            instance.PrepareContinuePrompt(-450);
        }

        private void BeginTypewriter(TMP_Text text)
        {
            StopTypewriter();
            typewriter = StartCoroutine(TypeLetter(text));
        }

        private IEnumerator TypeLetter(TMP_Text text)
        {
            text.maxVisibleCharacters = 0;
            text.ForceMeshUpdate();
            int total = text.textInfo.characterCount;
            if (gameAudio.PanelOpenDuration > 0f)
                yield return new WaitForSecondsRealtime(gameAudio.PanelOpenDuration);
            const float charactersPerSecond = 70f;
            float visible = 0f;
            while (text != null && text.maxVisibleCharacters < total)
            {
                visible += charactersPerSecond * Time.unscaledDeltaTime;
                text.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(visible));
                yield return null;
            }
            typewriter = null;
            ShowContinuePrompt();
        }

        private void PrepareContinuePrompt(float y)
        {
            continuePrompt = Label("Clique ou pressione WASD / setas para continuar", y, 18);
            continuePrompt.gameObject.SetActive(false);
        }

        private void ShowContinuePrompt()
        {
            if (!showingLetter || continuePrompt == null) return;
            continuePrompt.gameObject.SetActive(true);
            if (continuePromptBlink != null) StopCoroutine(continuePromptBlink);
            continuePromptBlink = StartCoroutine(BlinkContinuePrompt());
        }

        private IEnumerator BlinkContinuePrompt()
        {
            while (continuePrompt != null)
            {
                Color color = continuePrompt.color;
                color.a = Mathf.Lerp(.48f, .76f,
                    (Mathf.Sin(Time.unscaledTime * 2.1f) + 1f) * .5f);
                continuePrompt.color = color;
                yield return null;
            }
            continuePromptBlink = null;
        }

        private void StopTypewriter()
        {
            if (typewriter != null) StopCoroutine(typewriter);
            typewriter = null;
            if (continuePromptBlink != null) StopCoroutine(continuePromptBlink);
            continuePromptBlink = null;
            continuePrompt = null;
        }

        private IEnumerator EnterGameplay()
        {
            var follow = FindFirstObjectByType<GameJamOcean.CameraSystem.CameraFollow3D>();
            if (follow != null)
            {
                float elapsed = 0f;
                float duration = Mathf.Max(.1f, follow.MenuTransitionSeconds);
                while (elapsed < duration && follow != null)
                {
                    follow.EvaluateMenuTransition(elapsed / duration);
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                }
                if (follow != null) follow.EvaluateMenuTransition(1f);
            }
            transitioning = false;
            if (PlayerPrefs.GetInt(TutorialsArmedKey, 0) == 1 && PlayerPrefs.GetInt(OceanTutorialKey, 0) == 0)
            {
                ShowControlsTutorial(true);
                yield break;
            }
            Resume();
        }

        private void PrepareNewGameTutorials()
        {
            PlayerPrefs.SetInt(TutorialsArmedKey, 1);
            PlayerPrefs.SetInt(OceanTutorialKey, 0);
            PlayerPrefs.SetInt(DiveTutorialKey, 0);
            PlayerPrefs.Save();
        }

        private void ShowControlsTutorial(bool ocean)
        {
            EnsureCanvas();
            canvas.gameObject.SetActive(true);
            ClearPanel(ocean ? "CONTROLES DO BARCO" : "CONTROLES DO MERGULHO",
                EditableUIPanelKind.TutorialPanel);
            string controls = ocean
                ? "WASD / Setas — mover e virar\nShift — turbo\nF — interagir\nP — pausar\nBotão direito do mouse — movimentar a câmera"
                : "WASD / Setas — nadar\nShift — dash\nClique esquerdo / segurar — atacar\nF — interagir\nP — pausar\n\nMuito cuidado com as profundezas do mar. Alguns bichos são mais hostis, outros são mais astutos, mas uma coisa é certa: estamos em perigo o tempo todo.";
            Label(controls, -105, ocean ? 24 : 21, ocean ? 230 : 285);
            Button("ENTENDI", -430, () => CloseControlsTutorial(ocean));
        }

        private void CloseControlsTutorial(bool ocean)
        {
            PlayerPrefs.SetInt(ocean ? OceanTutorialKey : DiveTutorialKey, 1);
            if (!ocean) PlayerPrefs.SetInt(TutorialsArmedKey, 0);
            PlayerPrefs.Save();
            Resume();
        }

        private void ShowHome()
        {
            if (main) gameAudio.EnsureAmbiencePlaying();
            ClearPanel(main ? string.Empty : "PAUSADO",
                main ? EditableUIPanelKind.MainMenu : EditableUIPanelKind.PauseMenu);
            if (main)
            {
                var progress = GameProgress.Instance;
                bool hasSave = progress != null && progress.HasSavedGame;
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, hasSave ? 420f : 360f);
                MainMenuLogo();
                Button(hasSave ? "Continuar" : "Iniciar", -145, () =>
                {
                    if (hasSave) StartGameplay();
                    else { PrepareNewGameTutorials(); ShowLetter(); }
                }, 360f);
                if (hasSave) Button("Novo jogo", -210, ConfirmNewGame, 360f);
                Button("Ajustes", hasSave ? -275 : -215, ShowSettings, 360f);
                Button("Sair", hasSave ? -340 : -280, ConfirmExitGame, 360f);
            }
            else
            {
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, 420f);
                Button("Continuar", -100, Resume);
                Button("Voltar à ilha", -170, () => ConfirmTravel(false));
                Button("Ajustes", -240, ShowSettings);
                Button("Voltar ao menu", -310, () => ConfirmTravel(true));
            }
        }

        private void ConfirmNewGame()
        {
            ClearPanel("NOVO JOGO");
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 560f);
            Label("Iniciar uma nova aventura?\nO ouro e os upgrades salvos serão substituídos.\nEsta ação não pode ser desfeita.", -130, 22, 120);
            Button("Iniciar novo jogo", -285, () =>
            {
                if (!CanLoadOcean()) return;
                GameProgress.Instance.ResetProgress();
                PrepareNewGameTutorials();
                InteractionDiscoveryStore.ResetAll();
                DivePointSpawnManager3D.ResetRuntimeState();
                requestIntro = true;
                Travel(false);
            });
            Button("Cancelar", -355, ShowHome);
        }

        private void ConfirmTravel(bool toMain)
        {
            ClearPanel(toMain ? "VOLTAR AO MENU?" : "VOLTAR À ILHA?");
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 520f);
            Label(SceneManager.GetActiveScene().name == "DiveScene"
                ? "O mergulho será encerrado.\nVocê mantém o ouro já coletado,\nmas não recebe o baú final ainda fechado."
                : "Seu ouro e seus upgrades serão mantidos.\nO barco retornará ao ponto inicial junto à ilha.", -130, 22, 120);
            Button("Confirmar", -285, () => Travel(toMain));
            Button("Cancelar", -355, ShowHome);
        }

        private void ConfirmExitGame()
        {
            ClearPanel("DESEJA SAIR?", EditableUIPanelKind.MainMenu);
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 350f);
            Label("Tem certeza que deseja sair?", -105, 22);
            Button("Sim, desejo sair", -175, ExitGame, 360f);
            Button("Vou continuar jogando", -245, ShowHome, 360f);
        }

        private bool CanLoadOcean()
        {
            if (Application.CanStreamedLevelBeLoaded(Ocean)) return true;
            Debug.LogError("Adicione OceanScene_3D à Scene List do Build Profiles.", this);
            return false;
        }

        private void Travel(bool toMain)
        {
            if (loading || !CanLoadOcean()) return;
            FindFirstObjectByType<DiveSessionFlowController>()?.CommitAbandonedSession();
            GameProgress.Instance.SaveProgress();
            OceanReturnState3D.ResetRuntimeState();
            requestMain = toMain;
            loading = true;
            Resume();
            SceneManager.LoadScene(Ocean);
        }

        private void ShowSettings()
        {
            ClearPanel("AJUSTES");
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 580f);
            SettingSlider("Volume geral", -85, 0f, 1f, AudioListener.volume, value =>
            {
                AudioListener.volume = value;
                PlayerPrefs.SetFloat(SettingsKey + "Volume", value);
            });
            SettingSlider("Som de fundo", -175, 0f, 1f, gameAudio.BackgroundVolume, gameAudio.SetBackground);
            SettingSlider("Efeitos sonoros", -265, 0f, 1f, gameAudio.EffectsVolume, gameAudio.SetEffects);
            SettingSlider("Sensibilidade da curva", -355, .85f, 1.15f, SteeringMultiplier, value =>
            {
                SteeringMultiplier = value;
                PlayerPrefs.SetFloat(SettingsKey + "Steering", value);
            });
            Button("Voltar", -450, ShowHome);
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void EnsureCanvas()
        {
            if (EventSystem.current == null)
                new GameObject("Menu EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (canvas != null) return;
            var root = new GameObject("Game Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var dim = Rect("Dim", root.transform, Vector2.zero, Vector2.zero);
            dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            dim.gameObject.AddComponent<Image>().color = new Color(0, .03f, .07f, .06f);
            SetPanelTemplate(main ? EditableUIPanelKind.MainMenu : EditableUIPanelKind.PauseMenu);
        }

        private void ClearPanel(string title, EditableUIPanelKind? requestedKind = null)
        {
            SetPanelTemplate(requestedKind ?? (main
                ? EditableUIPanelKind.MainMenu : EditableUIPanelKind.PauseMenu));
            foreach (Transform child in panelContent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            if (!string.IsNullOrWhiteSpace(title))
            {
                TMP_Text titleLabel = Label(title, -35, 30);
                GameFontStyles.Apply(titleLabel, GameFontRole.Display);
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void MainMenuLogo()
        {
            Sprite logo = EditableUIFactory.Settings != null
                ? EditableUIFactory.Settings.mainMenuLogo : null;
            if (logo == null)
            {
                Debug.LogWarning("Main menu logo is not assigned in EditableUISettings.", this);
                return;
            }

            RectTransform logoRect = Rect("Logo Game Campeche", panelContent,
                new Vector2(340f, 190f), new Vector2(0f, 40f));
            Image logoImage = logoRect.gameObject.AddComponent<Image>();
            logoImage.sprite = logo;
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;
        }

        private TMP_Text Label(string text, float y, int size, float height = 45)
        {
            var label = Rect("Label", panelContent, new Vector2(520, height), new Vector2(0, y)).gameObject.AddComponent<TextMeshProUGUI>();
            GameFontStyles.Apply(label, GameFontRole.General);
            label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true; label.fontSizeMin = Mathf.Max(10f, size * .72f); label.fontSizeMax = size;
            MakeTextSolid(label);
            return label;
        }

        private void Button(string title, float y, Action action, float width = 440f)
        {
            var button = EditableUIFactory.CreateButton(title, panelContent, new Vector2(width, 52),
                new Vector2(0, y), out TMP_Text label, new Color(.1f, .37f, .43f));
            button.onClick.AddListener(() => { if (!loading) action(); });
            GameFontStyles.Apply(label, GameFontRole.General);
            label.text = title;
            label.fontSize = 23;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16;
            label.fontSizeMax = 23;
            label.alignment = TextAlignmentOptions.Center;
            MakeTextSolid(label);
            label.raycastTarget = false;
        }

        private static void MakeTextSolid(TMP_Text label)
        {
            if (label == null) return;
            label.color = new Color(1f, 1f, 1f, 1f);
            label.alpha = 1f;
            label.enableVertexGradient = false;
        }

        private void SettingSlider(string title, float y, float min, float max, float value, Action<float> apply)
        {
            var label = Label($"{title}: {value * 100f:0}%", y, 23);
            var root = Rect(title, panelContent, new Vector2(400, 32), new Vector2(0, y - 50));
            root.gameObject.AddComponent<Image>().color = new Color(.04f, .2f, .25f);
            var slider = root.gameObject.AddComponent<Slider>();
            var handle = Rect("Handle", root, new Vector2(24, 0), Vector2.zero);
            var image = handle.gameObject.AddComponent<Image>(); image.color = new Color(.5f, .9f, .9f);
            slider.targetGraphic = image; slider.handleRect = handle;
            slider.minValue = min; slider.maxValue = max; slider.value = value;
            slider.onValueChanged.AddListener(v => { apply(v); label.text = $"{title}: {v * 100f:0}%"; });
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = size; rect.anchoredPosition = pos;
            return rect;
        }

        private void SetPanelTemplate(EditableUIPanelKind kind)
        {
            if (panel != null && activePanelKind == kind && panelContent != null) return;
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
                Destroy(panel.gameObject);
            }
            panel = EditableUIFactory.CreatePanel(kind, canvas.transform, new Vector2(570, 520),
                new Color(.02f, .09f, .14f, .24f), kind.ToString(), out panelContent);
            activePanelKind = kind;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (scaledCursorTexture != null) Destroy(scaledCursorTexture);
            Resume();
            instance = null;
        }
    }

    public enum GameFontRole { General, Display }

    public static class GameFontStyles
    {
        private static TMP_FontAsset kanit;
        private static TMP_FontAsset russoOne;

        public static void Apply(TMP_Text text, GameFontRole role)
        {
            if (text == null) return;
            TMP_FontAsset font = role == GameFontRole.Display ? RussoOne : Kanit;
            if (font != null) text.font = font;
        }

        public static void ApplyLoadedTexts()
        {
            foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text == null) continue;
                Apply(text, IsDisplayText(text) ? GameFontRole.Display : GameFontRole.General);
            }
        }

        private static TMP_FontAsset Kanit => kanit ??= Create("Fonts/Kanit-SemiBold", "Kanit SemiBold");
        private static TMP_FontAsset RussoOne => russoOne ??= Create("Fonts/RussoOne-Regular", "Russo One");

        private static TMP_FontAsset Create(string resourcePath, string assetName)
        {
            Font source = Resources.Load<Font>(resourcePath);
            if (source == null) return null;
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source);
            asset.name = assetName;
            return asset;
        }

        private static bool IsDisplayText(TMP_Text text)
        {
            if (text == null) return false;

            string key = (text.name ?? string.Empty).ToUpperInvariant();
            string content = (text.text ?? string.Empty).ToUpperInvariant();
            return key.Contains("TITLE") || key.Contains("GOLD") || key.Contains("OURO")
                || key.Contains("TOTAL") || key.Contains("PROGRESS") || key.Contains("REWARD")
                || key.Contains("UPGRADE NAME") || content.Contains("MERGULHO CONCLUÍDO")
                || content.Trim() == "100%";
        }
    }
}
