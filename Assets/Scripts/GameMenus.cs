using System;
using System.Collections;
using System.Collections.Generic;
using GameJamOcean.Boat;
using GameJamOcean.Flow;
using GameJamOcean.Interaction;
using GameJamOcean.Localization;
using GameJamOcean.Progression;
using GameJamOcean.Spawning;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
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
        private bool showingCampaignIntro;
        private Coroutine typewriter;
        private Coroutine continuePromptBlink;
        private TMP_Text continuePrompt;
        private static readonly string[] RescueMessageKeys =
        {
            "rescue.random.1", "rescue.random.2", "rescue.random.3", "rescue.random.4",
            "rescue.random.5", "rescue.random.6", "rescue.random.7", "rescue.random.8"
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
        private RectTransform languageTooltipRect;
        private TMP_Text languageTooltipText;

        private static string L(string key, params object[] args) => LocalizationManager.Get(key, args);

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
            if (scene.name == Ocean) GameJamOcean.World.OceanEnvironment3D.ConfigureScene(scene);
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
            UpdateLanguageTooltipPosition();
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
                    if (showingCampaignIntro)
                    {
                        showingCampaignIntro = false;
                        GameProgress.Instance?.MarkIntroCompleted();
                    }
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

        private void Freeze(bool isMain, bool keepWorldFlowing = false)
        {
            main = isMain;
            savedTimeScale = Time.timeScale;
            savedAudioPause = AudioListener.pause;
            savedCursorVisible = Cursor.visible;
            savedCursorLock = Cursor.lockState;
            open = true;
            if (!isMain && !keepWorldFlowing)
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
            if (GameProgress.Instance == null || !GameProgress.Instance.HasValidCampaign)
            {
                Debug.LogWarning("Gameplay bloqueada porque não existe uma campanha válida.", this);
                ShowHome();
                return;
            }
            transitioning = true;
            canvas.gameObject.SetActive(false);
            StartCoroutine(EnterGameplay());
        }

        private void ShowLetter()
        {
            if (GameProgress.Instance == null || !GameProgress.Instance.HasValidCampaign)
            {
                ShowHome();
                return;
            }
            showingLetter = true;
            showingCampaignIntro = true;
            letterFrame = Time.frameCount;
            ClearPanel(L("story.new_life.title"), EditableUIPanelKind.TutorialPanel);
            panel.sizeDelta = new Vector2(610f, 690f);
            TMP_Text body = Label(L("story.new_life.body"), -85, 20, 520);
            body.rectTransform.sizeDelta = new Vector2(550f, body.rectTransform.sizeDelta.y);
            body.alignment = TextAlignmentOptions.Justified;
            gameAudio.StartLetter();
            BeginTypewriter(body);
            PrepareContinuePrompt(-625);
        }

        public static void ShowRescueLetter(bool firstDeathFree, int chargedGold, int configuredCost)
        {
            BoatRecoveryActive = false;
            if (instance == null) return;
            // Keep ocean animation and scene music running while gameplay input and
            // the repaired boat remain blocked for the rescue message/transition.
            instance.Freeze(false, true);
            instance.showingLetter = true;
            instance.showingCampaignIntro = false;
            instance.letterFrame = Time.frameCount;
            instance.ClearPanel(L("rescue.title"), EditableUIPanelKind.TutorialPanel);
            bool receivedDiscount = !firstDeathFree && chargedGold < configuredCost;
            string message = firstDeathFree
                ? L("rescue.first_free")
                : receivedDiscount
                    ? L("rescue.discounted", configuredCost, chargedGold)
                    : L("rescue.charged", L(RescueMessageKeys[UnityEngine.Random.Range(0, RescueMessageKeys.Length)]), configuredCost, chargedGold);
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
            continuePrompt = Label(L("tutorial.continue_prompt"), y, 18);
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
            ClearPanel(L(ocean ? "tutorial.boat.title" : "tutorial.dive.title"),
                EditableUIPanelKind.TutorialPanel);
            // Tutorial panels share the same runtime template. Always restore a
            // content-sized layout here so the larger story panel cannot leak
            // its dimensions into the controls screen.
            panel.sizeDelta = ocean ? new Vector2(570f, 490f) : new Vector2(630f, 440f);
            string controls = L(ocean ? "tutorial.boat.body" : "tutorial.dive.body");
            Label(controls, -105, ocean ? 24 : 21, ocean ? 230 : 240);
            Button(L("common.understood"), ocean ? -405 : -350, () => CloseControlsTutorial(ocean));
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
            ClearPanel(main ? string.Empty : L("menu.paused"),
                main ? EditableUIPanelKind.MainMenu : EditableUIPanelKind.PauseMenu);
            if (main)
            {
                var progress = GameProgress.Instance;
                bool hasCampaign = progress != null && progress.HasValidCampaign;
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, hasCampaign ? 420f : 360f);
                MainMenuLogo();
                Button(L(hasCampaign ? "menu.continue" : "menu.new_game"), -145,
                    hasCampaign ? ContinueCampaign : ConfirmNewGame, 360f);
                if (hasCampaign) Button(L("menu.new_game"), -210, ConfirmNewGame, 360f);
                Button(L("menu.settings"), hasCampaign ? -275 : -215, ShowSettings, 360f);
                Button(L("menu.quit"), hasCampaign ? -340 : -280, ConfirmExitGame, 360f);
            }
            else
            {
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, 420f);
                Button(L("menu.continue"), -100, Resume);
                Button(L("menu.return_island"), -170, () => ConfirmTravel(false));
                Button(L("menu.settings"), -240, ShowSettings);
                Button(L("menu.return_menu"), -310, () => ConfirmTravel(true));
            }
        }

        private void ContinueCampaign()
        {
            GameProgress progress = GameProgress.Instance;
            if (progress == null || !progress.HasValidCampaign)
            {
                ShowHome();
                return;
            }
            if (progress.IsIntroPending)
            {
                // Also restores the tutorial route if preferences were removed while
                // the campaign introduction was still pending.
                PrepareNewGameTutorials();
                ShowLetter();
                return;
            }
            StartGameplay();
        }

        private void ConfirmNewGame()
        {
            ClearPanel(L("new_game.title"));
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 560f);
            Label(L("new_game.warning"), -130, 22, 120);
            Button(L("new_game.start"), -285, () =>
            {
                if (!CanLoadOcean()) return;
                ShowDifficultySelection();
            });
            Button(L("common.cancel"), -355, ShowHome);
        }

        private void ShowDifficultySelection()
        {
            ClearPanel(L("difficulty.title"));
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 430f);
            Button(L("difficulty.easy"), -145,
                () => StartNewCampaign(GameDifficulty.Easy), 360f);
            Button(L("difficulty.normal"), -215,
                () => StartNewCampaign(GameDifficulty.Normal), 360f);
            Button(L("difficulty.hard"), -285,
                () => StartNewCampaign(GameDifficulty.Hard), 360f);
        }

        private void StartNewCampaign(GameDifficulty difficulty)
        {
            if (!CanLoadOcean() || GameProgress.Instance == null) return;
            GameProgress.Instance.BeginNewCampaign(difficulty);
            PrepareNewGameTutorials();
            InteractionDiscoveryStore.ResetAll();
            DivePointSpawnManager3D.ResetRuntimeState();
            requestIntro = true;
            Travel(false);
        }

        private void ConfirmTravel(bool toMain)
        {
            ClearPanel(L(toMain ? "travel.menu_title" : "travel.island_title"));
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 520f);
            Label(L(SceneManager.GetActiveScene().name == "DiveScene"
                ? "travel.dive_warning" : "travel.ocean_warning"), -130, 22, 120);
            Button(L("common.confirm"), -285, () => Travel(toMain));
            Button(L("common.cancel"), -355, ShowHome);
        }

        private void ConfirmExitGame()
        {
            ClearPanel(L("quit.title"), EditableUIPanelKind.MainMenu);
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 350f);
            Label(L("quit.prompt"), -105, 22);
            Button(L("quit.yes"), -175, ExitGame, 360f);
            Button(L("quit.no"), -245, ShowHome, 360f);
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
            FindFirstObjectByType<BoatStats3D>()?.SaveCurrentBoatHealth();
            GameProgress.Instance.SaveProgress();
            OceanReturnState3D.ResetRuntimeState();
            requestMain = toMain;
            loading = true;
            Resume();
            SceneManager.LoadScene(Ocean);
        }

        private void ShowSettings()
        {
            HideLanguageTooltip();
            ClearPanel(L("settings.title"));
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 680f);
            SettingSlider(L("settings.master_volume"), -85, 0f, 1f, AudioListener.volume, value =>
            {
                AudioListener.volume = value;
                PlayerPrefs.SetFloat(SettingsKey + "Volume", value);
            });
            SettingSlider(L("settings.music"), -175, 0f, 1f, gameAudio.BackgroundVolume, gameAudio.SetBackground);
            SettingSlider(L("settings.effects"), -265, 0f, 1f, gameAudio.EffectsVolume, gameAudio.SetEffects);
            SettingSlider(L("settings.steering"), -355, .85f, 1.15f, SteeringMultiplier, value =>
            {
                SteeringMultiplier = value;
                PlayerPrefs.SetFloat(SettingsKey + "Steering", value);
            });
            Label(L("settings.language"), -440, 21);
            CreateLanguageFlagButton(GameLanguage.PortugueseBrazil,
                new Rect(.448f, .2361f, .098f, .1012f),
                new Vector2(-75f, -480f), "Português");
            CreateLanguageFlagButton(GameLanguage.English,
                new Rect(.136f, .344f, .098f, .1012f),
                new Vector2(75f, -480f), "English");
            Button(L("common.back"), -585, SaveSettingsAndReturnHome);
        }

        private void SaveSettingsAndReturnHome()
        {
            HideLanguageTooltip();
            // Preferences have their own persistence lifecycle and never touch GameProgress.
            PlayerPrefs.Save();
            ShowHome();
        }

        private void CreateLanguageFlagButton(GameLanguage language, Rect atlasUv,
            Vector2 position, string tooltip)
        {
            Button button = EditableUIFactory.CreateButton($"Language {language}", panelContent,
                new Vector2(105f, 66f), position, out TMP_Text label, new Color(.1f, .37f, .43f));
            Texture2D atlas = EditableUIFactory.Settings != null
                ? EditableUIFactory.Settings.languageFlagsAtlas : null;
            label.text = atlas == null
                ? (language == GameLanguage.English ? "US" : "BR") : string.Empty;
            if (atlas != null)
            {
                RectTransform flagRect = Rect("Flag", button.transform,
                    new Vector2(96f, 58f), new Vector2(0f, -4f));
                flagRect.anchorMin = flagRect.anchorMax = flagRect.pivot = new Vector2(.5f, .5f);
                flagRect.anchoredPosition = Vector2.zero;
                RawImage flagImage = flagRect.gameObject.AddComponent<RawImage>();
                flagImage.texture = atlas;
                flagImage.uvRect = atlasUv;
                flagImage.raycastTarget = false;
            }
            Outline glow = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(.35f, .95f, 1f, .95f);
            glow.effectDistance = new Vector2(4f, -4f);
            glow.useGraphicAlpha = false;
            glow.enabled = LocalizationManager.CurrentLanguage == language;
            button.onClick.AddListener(() =>
            {
                HideLanguageTooltip();
                LocalizationManager.SetLanguage(language);
                ShowSettings();
            });
            EventTrigger trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();
            AddPointerTrigger(trigger, EventTriggerType.PointerEnter, _ => ShowLanguageTooltip(tooltip));
            AddPointerTrigger(trigger, EventTriggerType.PointerExit, _ => HideLanguageTooltip());
        }

        private static void AddPointerTrigger(EventTrigger trigger, EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private void ShowLanguageTooltip(string content)
        {
            if (canvas == null) return;
            HideLanguageTooltip();
            languageTooltipRect = new GameObject("Language Tooltip", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            languageTooltipRect.SetParent(canvas.transform, false);
            languageTooltipRect.anchorMin = languageTooltipRect.anchorMax =
                languageTooltipRect.pivot = Vector2.zero;
            languageTooltipRect.sizeDelta = new Vector2(145f, 38f);
            languageTooltipRect.gameObject.GetComponent<Image>().color = new Color(.015f, .07f, .1f, .94f);
            RectTransform labelRect = new GameObject("Label", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            labelRect.SetParent(languageTooltipRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 3f);
            labelRect.offsetMax = new Vector2(-8f, -3f);
            languageTooltipText = labelRect.GetComponent<TextMeshProUGUI>();
            GameFontStyles.Apply(languageTooltipText, GameFontRole.General);
            languageTooltipText.text = content;
            languageTooltipText.fontSize = 18f;
            languageTooltipText.alignment = TextAlignmentOptions.Center;
            languageTooltipText.color = Color.white;
            languageTooltipText.raycastTarget = false;
            languageTooltipRect.SetAsLastSibling();
            UpdateLanguageTooltipPosition();
        }

        private void UpdateLanguageTooltipPosition()
        {
            if (languageTooltipRect == null || Mouse.current == null) return;
            Vector2 pointer = Mouse.current.position.ReadValue() + new Vector2(18f, -48f);
            Vector2 half = languageTooltipRect.sizeDelta * .5f;
            pointer.x = Mathf.Clamp(pointer.x, 8f, Screen.width - languageTooltipRect.sizeDelta.x - 8f);
            pointer.y = Mathf.Clamp(pointer.y, 8f, Screen.height - languageTooltipRect.sizeDelta.y - 8f);
            languageTooltipRect.anchoredPosition = pointer;
        }

        private void HideLanguageTooltip()
        {
            if (languageTooltipRect != null) Destroy(languageTooltipRect.gameObject);
            languageTooltipRect = null;
            languageTooltipText = null;
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
            text.extraPadding = true;
            text.isTextObjectScaleStatic = true;
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
            // A denser SDF source keeps small UI labels clean after CanvasScaler reduction.
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, 120, 12,
                GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
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

namespace GameJamOcean.Localization
{
    public enum GameLanguage
    {
        PortugueseBrazil,
        English
    }

    public static class LocalizationManager
    {
        private const string LanguageKey = "GameJamOcean.Settings.Language";
        private static readonly Dictionary<string, string> Portuguese = new()
        {
            ["menu.paused"] = "PAUSADO",
            ["menu.continue"] = "Continuar",
            ["menu.new_game"] = "Novo jogo",
            ["menu.settings"] = "Ajustes",
            ["menu.quit"] = "Sair",
            ["menu.return_island"] = "Voltar à ilha",
            ["menu.return_menu"] = "Voltar ao menu",
            ["common.back"] = "Voltar",
            ["common.cancel"] = "Cancelar",
            ["common.confirm"] = "Confirmar",
            ["common.understood"] = "ENTENDI",
            ["common.close"] = "FECHAR",
            ["new_game.title"] = "NOVO JOGO",
            ["new_game.warning"] = "Iniciar uma nova aventura?\nO ouro e os upgrades salvos serão substituídos.\nEsta ação não pode ser desfeita.",
            ["new_game.start"] = "Iniciar novo jogo",
            ["travel.menu_title"] = "VOLTAR AO MENU?",
            ["travel.island_title"] = "VOLTAR À ILHA?",
            ["travel.dive_warning"] = "O mergulho será encerrado.\nVocê mantém o ouro já coletado,\nmas não recebe o baú final ainda fechado.",
            ["travel.ocean_warning"] = "Seu ouro e seus upgrades serão mantidos.\nO barco retornará ao ponto inicial junto à ilha.",
            ["quit.title"] = "DESEJA SAIR?",
            ["quit.prompt"] = "Tem certeza que deseja sair?",
            ["quit.yes"] = "Sim, desejo sair",
            ["quit.no"] = "Vou continuar jogando",
            ["settings.title"] = "AJUSTES",
            ["settings.master_volume"] = "Volume geral",
            ["settings.music"] = "Som de fundo",
            ["settings.effects"] = "Efeitos sonoros",
            ["settings.steering"] = "Sensibilidade da curva",
            ["settings.language"] = "Idioma",
            ["hud.hull"] = "CASCO {0}%",
            ["hud.turbo"] = "TURBO {0}%",
            ["interaction.press_f_or_click"] = "Pressione F ou clique",
            ["interaction.open_chest"] = "Abrir baú",
            ["interaction.enter_dive"] = "Entrar na fase de mergulho",
            ["interaction.open_upgrades"] = "Abrir upgrades do porto",
            ["interaction.interact"] = "Interagir",
            ["dive_results.completed"] = "MERGULHO CONCLUÍDO!",
            ["dive_results.ended"] = "FIM DO MERGULHO",
            ["dive_results.enemies"] = "Inimigos derrotados: {0}/{1}",
            ["dive_results.small_chest"] = "Baú Pequeno ×{0}",
            ["dive_results.final_chest"] = "Baú Grande ×{0}",
            ["dive_results.loose_coins"] = "Moedas soltas",
            ["dive_results.reward"] = "+{0} ouro",
            ["dive_results.total"] = "TOTAL RECEBIDO: {0} OURO",
            ["dive_results.return_ocean"] = "VOLTAR AO OCEANO",
            ["ending.title"] = "PARABÉNS, CAPITÃO!",
            ["ending.body"] = "Você ajudou a transformar a Ilha do Campeche em um lugar cheio de vida, esperança e novos começos.\nO farol agora brilha por todos que chamam esta ilha de lar.",
            ["upgrade.title"] = "UPGRADES DO PORTO",
            ["upgrade.gold"] = "OURO",
            ["upgrade.gold_available"] = "OURO DISPONÍVEL: {0}",
            ["upgrade.repair.insufficient"] = "Ouro insuficiente para o conserto.",
            ["upgrade.repair.success"] = "Barco consertado! Casco 100%. Custo: {0} ouro.",
            ["upgrade.repair.button"] = "CONSERTAR BARCO — {0} OURO",
            ["upgrade.repair.full"] = "CASCO 100% — SEM REPAROS",
            ["upgrade.status.choose"] = "Escolha um upgrade. Todos os preços são em ouro.",
            ["upgrade.status.completed"] = "Ilha N4 — jogo concluído!",
            ["upgrade.status.completed_thanks"] = "Ilha N4 — jogo concluído! Obrigado por construir sua ilha.",
            ["upgrade.error.village_versions"] = "Configure as quatro versões da ilha antes de comprar.",
            ["upgrade.error.maximum"] = "Nível máximo atingido.",
            ["upgrade.error.offer_changed"] = "A oferta mudou; atualize o painel.",
            ["upgrade.error.invalid_price"] = "Preço inválido.",
            ["upgrade.error.insufficient_gold"] = "Ouro insuficiente.",
            ["upgrade.error.in_progress"] = "Compra em andamento.",
            ["upgrade.error.catalog_missing"] = "Catálogo de upgrades não configurado.",
            ["upgrade.error.unknown"] = "Upgrade desconhecido.",
            ["upgrade.purchase.success"] = "Upgrade adquirido!",
            ["upgrade.unavailable"] = "Indisponível",
            ["upgrade.pier"] = "PÍER",
            ["upgrade.pier.description"] = "O primeiro passo para melhorar o comércio marítimo; depois vem nosso farol!",
            ["upgrade.maximum_level"] = "Nível máximo",
            ["upgrade.next_level"] = "Nível {0}: {1}",
            ["upgrade.maximum"] = "MÁXIMO",
            ["upgrade.buy"] = "{0} OURO\nCOMPRAR",
            ["upgrade.island.2"] = "Mais moradores chegando, as condições da ilha estão melhorando.",
            ["upgrade.island.3"] = "A construção do farol será um grande avanço para a ilha.",
            ["upgrade.island.4"] = "Acenda a luz do farol e traga segurança para nossa pequena ilha.",
            ["upgrade.island.appearance"] = "Uma nova aparência para a ilha.",
            ["upgrade.boat.2"] = "Casco +15% / Turbo +20% sobre o valor inicial",
            ["upgrade.boat.3"] = "Casco +30% / Turbo +40% sobre o valor inicial",
            ["upgrade.diver_health"] = "+2 pontos de vida.",
            ["upgrade.diver_speed.2"] = "15% sobre o valor inicial",
            ["upgrade.diver_speed.3"] = "30% sobre o valor inicial",
            ["upgrade.harpoon.2"] = "Velocidade +2 / Distância +6",
            ["upgrade.harpoon.3"] = "Velocidade +4 / Distância +10 / Dano +1",
            ["upgrade.harpoon.4"] = "Projétil duplo",
            ["upgrade.percent_initial"] = "+{0}% sobre o valor inicial",
            ["upgrade.name.island"] = "Ilha",
            ["upgrade.name.vessel"] = "Embarcação",
            ["upgrade.name.diver_health"] = "Mergulhador - Vida",
            ["upgrade.name.diver_speed"] = "Mergulhador - Velocidade",
            ["upgrade.name.harpoon"] = "Mergulhador - Arpão",
            ["upgrade.current_level"] = "{0} | Atual Nível {1}",
            ["upgrade.boat_label"] = "BARCO {0}",
            ["village.thanks.1"] = "Os moradores agradecem pelo novo píer! Para tornar este lugar seguro diante das grandes embarcações, ainda precisamos evoluir bastante. Nosso objetivo é construir um farol e fazê-lo brilhar. Será um caminho árduo, mas recompensador.",
            ["village.thanks.2"] = "A vila está crescendo com sua ajuda. Os moradores agradecem por mais esta melhoria!",
            ["village.thanks.3"] = "Cada melhoria torna nossa comunidade mais forte. Obrigado por continuar ao nosso lado!",
            ["village.thanks.4"] = "O farol voltou a brilhar! Toda a vila agradece por tornar este lugar mais seguro.",
            ["difficulty.title"] = "Escolha a dificuldade",
            ["difficulty.easy"] = "Fácil",
            ["difficulty.normal"] = "Normal",
            ["difficulty.hard"] = "Difícil",
            ["story.new_life.title"] = "UMA NOVA VIDA",
            ["story.new_life.body"] = "Um bom lugar para começar uma nova vida como mergulhador profissional, não acha?\n\nDizem que estas águas escondem tesouros e riquezas há muito esquecidos. No fundo do oceano há muito a encontrar, mas chegar até lá nem sempre será fácil.\n\nO que você trouxer do mar pode ajudar este pequeno vilarejo a crescer. Aos poucos, novas pessoas podem chamar a ilha de lar, enquanto você melhora seus equipamentos e ganha experiência como mergulhador.\n\nA vida por aqui pode ser simples, mas talvez seja justamente esse o encanto. Um barco, o oceano à sua frente e a chance de construir uma nova vida na Ilha do Campeche.\n\nSó não esqueça que, conforme você avança, as recompensas aumentam, mas os perigos também.",
            ["tutorial.continue_prompt"] = "Clique ou pressione WASD / setas para continuar",
            ["tutorial.boat.title"] = "CONTROLES DO BARCO",
            ["tutorial.boat.body"] = "WASD / Setas — mover e virar\nShift — turbo\nF — interagir\nP — pausar\nBotão direito do mouse — movimentar a câmera",
            ["tutorial.dive.title"] = "CONTROLES DO MERGULHO",
            ["tutorial.dive.body"] = "WASD / Setas — nadar\nShift — dash\nClique esquerdo / segurar — atacar\nF — interagir\nP — pausar\n\nAs profundezas do mar guardam muitos desafios. Alguns bichos são mais hostis, outros mais astutos, mas cada mergulho também traz novas descobertas. Mantenha-se atento, explore com coragem e aproveite tudo o que o oceano tem a oferecer.",
            ["rescue.title"] = "DE VOLTA AO ESTALEIRO",
            ["rescue.first_free"] = "Destruir o barco custa mais do que mantê-lo em boas condições, então procure deixar a manutenção em dia. Desta vez, como sua missão é nobre e ajuda o vilarejo a crescer, o primeiro conserto fica por conta da vila.",
            ["rescue.discounted"] = "Conserto após o naufrágio: {0} ouro.\nValor debitado: {1} ouro.\n\nA vila concedeu um desconto porque sabe que suas finanças não andam boas. A vida no mar não é fácil, mas pode ser muito recompensadora conforme você adquire experiência.",
            ["rescue.charged"] = "{0}\n\nConserto após o naufrágio: {1} ouro.\nValor debitado: {2} ouro.",
            ["rescue.random.1"] = "O mar não está pra peixe… e hoje também não estava pra barco! Respire fundo: amanhã a pescaria de tesouros continua.",
            ["rescue.random.2"] = "Você foi conferir o fundo do mar levando o barco inteiro? Entusiasmo não falta! Já está tudo pronto para outra aventura.",
            ["rescue.random.3"] = "Até o melhor pescador já voltou ao píer com mais água no barco do que peixe no balde. Bora tentar de novo!",
            ["rescue.random.4"] = "Seu barco confundiu a profissão e quis virar submarino. Conversamos com ele: agora é navegar e deixar o mergulho com você!",
            ["rescue.random.5"] = "O mar cobrou um banho, mas não levou sua coragem. Sacuda a água das botas: ainda tem muito tesouro esperando!",
            ["rescue.random.6"] = "Dizem que quem procura acha. Você achou uma pedra! Na próxima, vamos tentar achar um baú, combinado?",
            ["rescue.random.7"] = "Calma, capitão! Isso não foi naufrágio, foi uma visita técnica ao fundo do mar. O estaleiro já liberou sua próxima tentativa!",
            ["rescue.random.8"] = "Se afundar desse experiência, você já era almirante! Levante a cabeça: o fundo do mar ainda reserva grandes aventuras."
        };
        private static readonly Dictionary<string, string> English = new()
        {
            ["menu.paused"] = "PAUSED",
            ["menu.continue"] = "Continue",
            ["menu.new_game"] = "New Game",
            ["menu.settings"] = "Settings",
            ["menu.quit"] = "Quit",
            ["menu.return_island"] = "Return to Island",
            ["menu.return_menu"] = "Return to Main Menu",
            ["common.back"] = "Back",
            ["common.cancel"] = "Cancel",
            ["common.confirm"] = "Confirm",
            ["common.understood"] = "GOT IT",
            ["common.close"] = "CLOSE",
            ["new_game.title"] = "NEW GAME",
            ["new_game.warning"] = "Start a new adventure?\nYour saved gold and upgrades will be replaced.\nThis action cannot be undone.",
            ["new_game.start"] = "Start New Game",
            ["travel.menu_title"] = "RETURN TO MAIN MENU?",
            ["travel.island_title"] = "RETURN TO THE ISLAND?",
            ["travel.dive_warning"] = "The dive will end.\nYou will keep the gold you have collected,\nbut not the unopened final chest.",
            ["travel.ocean_warning"] = "Your gold and upgrades will be kept.\nThe boat will return to its starting point by the island.",
            ["quit.title"] = "QUIT GAME?",
            ["quit.prompt"] = "Are you sure you want to quit?",
            ["quit.yes"] = "Yes, quit the game",
            ["quit.no"] = "Keep playing",
            ["settings.title"] = "SETTINGS",
            ["settings.master_volume"] = "Master Volume",
            ["settings.music"] = "Music",
            ["settings.effects"] = "Sound Effects",
            ["settings.steering"] = "Steering Sensitivity",
            ["settings.language"] = "Language",
            ["hud.hull"] = "HULL {0}%",
            ["hud.turbo"] = "TURBO {0}%",
            ["interaction.press_f_or_click"] = "Press F or Click",
            ["interaction.open_chest"] = "Open Chest",
            ["interaction.enter_dive"] = "Enter Dive",
            ["interaction.open_upgrades"] = "Open Port Upgrades",
            ["interaction.interact"] = "Interact",
            ["dive_results.completed"] = "DIVE COMPLETE!",
            ["dive_results.ended"] = "DIVE OVER",
            ["dive_results.enemies"] = "Enemies defeated: {0}/{1}",
            ["dive_results.small_chest"] = "Small Chest ×{0}",
            ["dive_results.final_chest"] = "Large Chest ×{0}",
            ["dive_results.loose_coins"] = "Loose Coins",
            ["dive_results.reward"] = "+{0} Gold",
            ["dive_results.total"] = "TOTAL RECEIVED: {0} GOLD",
            ["dive_results.return_ocean"] = "RETURN TO THE OCEAN",
            ["ending.title"] = "CONGRATULATIONS, CAPTAIN!",
            ["ending.body"] = "You helped transform Campeche Island into a place filled with life, hope, and new beginnings.\nThe lighthouse now shines for everyone who calls this island home.",
            ["upgrade.title"] = "PORT UPGRADES",
            ["upgrade.gold"] = "GOLD",
            ["upgrade.gold_available"] = "GOLD AVAILABLE: {0}",
            ["upgrade.repair.insufficient"] = "Not enough Gold for repairs.",
            ["upgrade.repair.success"] = "Boat repaired! Hull 100%. Cost: {0} Gold.",
            ["upgrade.repair.button"] = "REPAIR BOAT — {0} GOLD",
            ["upgrade.repair.full"] = "HULL 100% — NO REPAIRS NEEDED",
            ["upgrade.status.choose"] = "Choose an upgrade. All prices are in Gold.",
            ["upgrade.status.completed"] = "Island N4 — game complete!",
            ["upgrade.status.completed_thanks"] = "Island N4 — game complete! Thank you for rebuilding your island.",
            ["upgrade.error.village_versions"] = "Configure all four island versions before purchasing.",
            ["upgrade.error.maximum"] = "Maximum level reached.",
            ["upgrade.error.offer_changed"] = "The offer changed; refresh the panel.",
            ["upgrade.error.invalid_price"] = "Invalid price.",
            ["upgrade.error.insufficient_gold"] = "Not enough Gold.",
            ["upgrade.error.in_progress"] = "Purchase in progress.",
            ["upgrade.error.catalog_missing"] = "Upgrade catalog is not configured.",
            ["upgrade.error.unknown"] = "Unknown upgrade.",
            ["upgrade.purchase.success"] = "Upgrade purchased!",
            ["upgrade.unavailable"] = "Unavailable",
            ["upgrade.pier"] = "PIER",
            ["upgrade.pier.description"] = "The first step toward improving maritime trade; next comes our lighthouse!",
            ["upgrade.maximum_level"] = "Maximum level",
            ["upgrade.next_level"] = "Level {0}: {1}",
            ["upgrade.maximum"] = "MAX",
            ["upgrade.buy"] = "{0} GOLD\nBUY",
            ["upgrade.island.2"] = "More residents are arriving, and conditions on the island are improving.",
            ["upgrade.island.3"] = "Building the lighthouse will be a major step forward for the island.",
            ["upgrade.island.4"] = "Light the lighthouse and bring safety to our small island.",
            ["upgrade.island.appearance"] = "A new look for the island.",
            ["upgrade.boat.2"] = "Hull +15% / Turbo +20% from base values",
            ["upgrade.boat.3"] = "Hull +30% / Turbo +40% from base values",
            ["upgrade.diver_health"] = "+2 health points.",
            ["upgrade.diver_speed.2"] = "15% above base value",
            ["upgrade.diver_speed.3"] = "30% above base value",
            ["upgrade.harpoon.2"] = "Speed +2 / Range +6",
            ["upgrade.harpoon.3"] = "Speed +4 / Range +10 / Damage +1",
            ["upgrade.harpoon.4"] = "Double projectile",
            ["upgrade.percent_initial"] = "+{0}% above base value",
            ["upgrade.name.island"] = "Island",
            ["upgrade.name.vessel"] = "Vessel",
            ["upgrade.name.diver_health"] = "Diver - Health",
            ["upgrade.name.diver_speed"] = "Diver - Speed",
            ["upgrade.name.harpoon"] = "Diver - Harpoon",
            ["upgrade.current_level"] = "{0} | Current Level {1}",
            ["upgrade.boat_label"] = "BOAT {0}",
            ["village.thanks.1"] = "The villagers are grateful for the new pier! To make this place safe for larger vessels, we still have a long way to go. Our goal is to build a lighthouse and make it shine. It will be a difficult journey, but a rewarding one.",
            ["village.thanks.2"] = "The village is growing thanks to your help. The residents are grateful for this latest improvement!",
            ["village.thanks.3"] = "Every improvement makes our community stronger. Thank you for continuing to stand with us!",
            ["village.thanks.4"] = "The lighthouse shines once more! The entire village thanks you for making this place safer.",
            ["difficulty.title"] = "Choose Difficulty",
            ["difficulty.easy"] = "Easy",
            ["difficulty.normal"] = "Normal",
            ["difficulty.hard"] = "Hard",
            ["story.new_life.title"] = "A NEW LIFE",
            ["story.new_life.body"] = "A good place to begin a new life as a professional diver, don't you think?\n\nThey say these waters hide long-forgotten treasures and riches. There is plenty to find beneath the ocean, but reaching it will not always be easy.\n\nWhat you bring back from the sea can help this small village grow. In time, more people may come to call the island home while you improve your equipment and gain experience as a diver.\n\nLife here may be simple, but perhaps that is part of its charm. A boat, the open ocean, and the chance to build a new life on Campeche Island.\n\nJust remember: as you progress, the rewards grow—but so do the dangers.",
            ["tutorial.continue_prompt"] = "Click or press WASD / Arrow Keys to continue",
            ["tutorial.boat.title"] = "BOAT CONTROLS",
            ["tutorial.boat.body"] = "WASD / Arrow Keys — move and steer\nShift — turbo\nF — interact\nP — pause\nRight mouse button — move the camera",
            ["tutorial.dive.title"] = "DIVING CONTROLS",
            ["tutorial.dive.body"] = "WASD / Arrow Keys — swim\nShift — dash\nLeft-click / hold — attack\nF — interact\nP — pause\n\nThe depths hold many challenges. Some creatures are more hostile, others more cunning, but every dive also brings new discoveries. Stay alert, explore bravely, and enjoy everything the ocean has to offer.",
            ["rescue.title"] = "BACK AT THE SHIPYARD",
            ["rescue.first_free"] = "Replacing a boat costs far more than keeping it in good condition, so remember to stay on top of maintenance. This time, because your mission is helping the village grow, the villagers will cover the repairs.",
            ["rescue.discounted"] = "Shipwreck repair: {0} Gold.\nAmount charged: {1} Gold.\n\nThe village offered you a discount because times are tough. Life at sea is not easy, but experience can make it very rewarding.",
            ["rescue.charged"] = "{0}\n\nShipwreck repair: {1} Gold.\nAmount charged: {2} Gold.",
            ["rescue.random.1"] = "The sea was not feeling generous today—and neither was your boat. Take a breath: the treasure hunt continues tomorrow.",
            ["rescue.random.2"] = "Did you decide to inspect the seabed with the entire boat? No shortage of enthusiasm! Everything is ready for another adventure.",
            ["rescue.random.3"] = "Even the finest sailor has returned to the pier with more water in the boat than fish in the hold. Ready to try again?",
            ["rescue.random.4"] = "Your boat mistook itself for a submarine. We had a word with it: you do the diving, it does the sailing.",
            ["rescue.random.5"] = "The sea claimed a soaking, but not your courage. Shake the water from your boots—there is still plenty of treasure waiting.",
            ["rescue.random.6"] = "They say those who seek shall find. You found a rock! Let us aim for a treasure chest next time.",
            ["rescue.random.7"] = "Easy, Captain! That was not a shipwreck, just a technical inspection of the seabed. The shipyard has cleared you for another attempt.",
            ["rescue.random.8"] = "If sinking earned experience, you would already be an admiral. Keep your head up—the ocean floor still holds great adventures."
        };

        private static GameLanguage? currentLanguage;
        public static event Action LanguageChanged;

        public static GameLanguage CurrentLanguage
        {
            get
            {
                if (currentLanguage.HasValue) return currentLanguage.Value;
                string saved = PlayerPrefs.GetString(LanguageKey, "en");
                currentLanguage = saved.Equals("en", StringComparison.OrdinalIgnoreCase)
                    || saved.StartsWith("en-", StringComparison.OrdinalIgnoreCase)
                    ? GameLanguage.English
                    : GameLanguage.PortugueseBrazil;
                return currentLanguage.Value;
            }
        }

        public static string Get(string key, params object[] args)
        {
            Dictionary<string, string> table = CurrentLanguage == GameLanguage.English ? English : Portuguese;
            string value = table.TryGetValue(key, out string translated) ? translated : key;
            return args != null && args.Length > 0 ? string.Format(value, args) : value;
        }

        public static void SetLanguage(GameLanguage language)
        {
            if (CurrentLanguage == language) return;
            currentLanguage = language;
            PlayerPrefs.SetString(LanguageKey,
                language == GameLanguage.English ? "en" : "pt-BR");
            PlayerPrefs.Save();
            LanguageChanged?.Invoke();
        }
    }
}
