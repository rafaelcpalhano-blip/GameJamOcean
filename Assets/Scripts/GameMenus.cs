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
        private readonly List<Canvas> hiddenCanvases = new();
        private Rigidbody menuBoat;
        private bool boatWasKinematic;
        private Vector3 boatVelocity, boatAngularVelocity;

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
            if (!open || !main) return;
            // Also catches HUDs constructed by other components in Start.
            foreach (var other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (other == canvas || !other.enabled || other.renderMode == RenderMode.WorldSpace) continue;
                hiddenCanvases.Add(other);
                other.enabled = false;
            }
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
            ClearPanel("UMA NOVA VIDA");
            panel.GetComponent<Image>().color = new Color(.02f, .09f, .14f, .9f);
            TMP_Text body = Label("<b>Um bom lugar para começar uma vida de mergulhador profissional, não acha?</b>\n\n"
                + "Dizem que tesouros e riquezas esquecidas aguardam nas profundezas.\n"
                + "Quanto mais você conquistar, mais poderá melhorar suas instalações, seu transporte e seus equipamentos. "
                + "Mas não se engane: quanto mais forte você ficar, mais profundo poderá ir… e maiores serão os perigos que encontrará.",
                -95, 23, 335);
            Label("Clique ou pressione WASD / setas para continuar", -450, 18);
            gameAudio.StartLetter();
            BeginTypewriter(body);
        }

        public static void ShowRescueLetter(bool firstDeathFree, int chargedGold, int configuredCost)
        {
            BoatRecoveryActive = false;
            if (instance == null) return;
            instance.Freeze(false);
            instance.showingLetter = true;
            instance.letterFrame = Time.frameCount;
            instance.ClearPanel("DE VOLTA AO ESTALEIRO");
            instance.panel.GetComponent<Image>().color = new Color(.02f, .09f, .14f, .9f);
            string message = firstDeathFree
                ? "Destruir o barco custa mais do que mantê-lo em boas condições, então procure deixar a manutenção em dia. Desta vez, como sua missão é nobre e ajuda o vilarejo a crescer, o primeiro conserto fica por conta da vila."
                : $"{RescueMessages[UnityEngine.Random.Range(0, RescueMessages.Length)]}\n\nConserto após o naufrágio: {configuredCost} Gold. Valor debitado: {chargedGold} Gold.";
            TMP_Text body = instance.Label(message, -130, 26, 260);
            instance.Label("Clique ou pressione WASD / setas para continuar", -450, 18);
            instance.gameAudio.StartLetter();
            instance.BeginTypewriter(body);
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
        }

        private void StopTypewriter()
        {
            if (typewriter != null) StopCoroutine(typewriter);
            typewriter = null;
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
            ClearPanel(ocean ? "CONTROLES DO BARCO" : "CONTROLES DO MERGULHO");
            panel.GetComponent<Image>().color = new Color(.02f, .09f, .14f, .88f);
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
            ClearPanel(main ? "GAME JAM OCEAN" : "PAUSADO");
            if (main)
            {
                var progress = GameProgress.Instance;
                bool hasSave = progress != null && progress.HasSavedGame;
                Label(hasSave ? $"Sua aldeia • nível {progress.GetLevel(UpgradeKind.Island)}" : "Uma nova aventura espera por você", -85, 20);
                Button(hasSave ? "Continuar" : "Iniciar", -145, () =>
                {
                    if (hasSave) StartGameplay();
                    else { PrepareNewGameTutorials(); ShowLetter(); }
                });
                if (hasSave) Button("Novo jogo", -210, ConfirmNewGame);
                Button("Ajustes", -285, ShowSettings);
            }
            else
            {
                Button("Continuar partida  [ P ]", -100, Resume);
                Button("Voltar à ilha", -170, () => ConfirmTravel(false));
                Button("Voltar ao menu", -240, () => ConfirmTravel(true));
                Button("Ajustes", -310, ShowSettings);
            }
        }

        private void ConfirmNewGame()
        {
            ClearPanel("NOVO JOGO");
            Label("Iniciar uma nova aventura?\nO ouro e os upgrades salvos serão substituídos.\nEsta ação não pode ser desfeita.", -130, 22, 120);
            Button("Sim, iniciar novo jogo", -285, () =>
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
            Label(SceneManager.GetActiveScene().name == "DiveScene"
                ? "O mergulho será encerrado.\nVocê mantém o ouro já coletado,\nmas não recebe o baú final ainda fechado."
                : "Seu ouro e seus upgrades serão mantidos.\nO barco retornará ao ponto inicial junto à ilha.", -130, 22, 120);
            Button("Confirmar", -285, () => Travel(toMain));
            Button("Cancelar", -355, ShowHome);
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
            panel = Rect("Menu", root.transform, new Vector2(570, 520), Vector2.zero);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
            panel.gameObject.AddComponent<Image>().color = new Color(.02f, .09f, .14f, .24f);
        }

        private void ClearPanel(string title)
        {
            panel.GetComponent<Image>().color = new Color(.02f, .09f, .14f, .24f);
            foreach (Transform child in panel) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            Label(title, -35, 30);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private TMP_Text Label(string text, float y, int size, float height = 45)
        {
            var label = Rect("Label", panel, new Vector2(520, height), new Vector2(0, y)).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white; label.raycastTarget = false;
            return label;
        }

        private void Button(string title, float y, Action action)
        {
            var rect = Rect(title, panel, new Vector2(440, 52), new Vector2(0, y));
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.1f, .37f, .43f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => { if (!loading) action(); });
            var label = Label(title, y, 23, 52);
            label.transform.SetParent(rect, true);
        }

        private void SettingSlider(string title, float y, float min, float max, float value, Action<float> apply)
        {
            var label = Label($"{title}: {value * 100f:0}%", y, 23);
            var root = Rect(title, panel, new Vector2(400, 32), new Vector2(0, y - 50));
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

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Resume();
            instance = null;
        }
    }
}
