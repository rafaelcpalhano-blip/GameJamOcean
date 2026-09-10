using GameJamOcean.Boat;
using GameJamOcean.Combat;
using GameJamOcean.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameJamOcean.Progression
{
    public sealed class PortUpgradePanel : MonoBehaviour
    {
        [Header("Village versions — never parent these under one another")]
        [SerializeField] private GameObject villageLevel0;
        [SerializeField] private GameObject villageLevel1;
        [SerializeField] private GameObject villageLevel2;
        [SerializeField] private GameObject villageLevel3;
        [SerializeField] private GameObject villageLevel4;
        [Header("Boat")]
        [SerializeField] private BoatController3D boat;
        [SerializeField] private PlayerInteractor3D interactor;
        [Header("Boat Repair")]
        [Tooltip("Price for repairing a completely damaged hull. Partial damage is charged proportionally.")]
        [SerializeField, Min(0)] private int fullRepairCost = 150;
        private Button repairButton;
        private TMP_Text repairPrice;
        private bool repairing;

        private int RepairCost(Health health)
        {
            double missing = 1.0 - (double)health.CurrentHealth / health.MaximumHealth;
            return (int)System.Math.Min(int.MaxValue, System.Math.Max(0,
                System.Math.Ceiling(System.Math.Max(0, fullRepairCost) * missing - .00001)));
        }

        private void RepairBoat()
        {
            if (!isOpen || repairing || boat == null || !boat.TryGetComponent<Health>(out var health)
                || health.IsDead || health.CurrentHealth >= health.MaximumHealth) return;
            int cost = RepairCost(health);
            repairing = true;
            try
            {
                if (cost > 0 && !progress.TrySpendGold(cost)) { statusLabel.text = "Ouro insuficiente para o conserto."; return; }
                health.Heal(health.MaximumHealth - health.CurrentHealth);
                statusLabel.text = $"Barco consertado! Casco 100%. Custo: {cost} ouro.";
                ShowUpgradeAcquiredColor();
                Refresh();
            }
            finally { repairing = false; }
        }

        private GameObject modal;
        private TMP_Text goldLabel;
        private TMP_Text statusLabel;
        private static readonly UpgradeKind[] VisibleUpgrades =
        {
            UpgradeKind.Island, UpgradeKind.BoatHull, UpgradeKind.DiverHealth,
            UpgradeKind.DiverSpeed, UpgradeKind.Harpoon
        };
        private readonly Button[] buttons = new Button[5];
        private readonly TMP_Text[] labels = new TMP_Text[5];
        private readonly TMP_Text[] upgradeNames = new TMP_Text[5];
        private readonly TMP_Text[] prices = new TMP_Text[5];
        private readonly GameObject[] rows = new GameObject[5];
        private readonly int[] offeredLevels = new int[5];
        private bool isOpen;
        private bool wasMoving;
        private bool wasInteracting;
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private float nextPurchaseTime;
        private GameProgress progress;
        private bool firstPierGoldHintShown;
        private Coroutine statusColorRoutine;
        private readonly Button[] boatButtons = new Button[3];
        private static readonly string[] VillageThanks =
        {
            "",
            "Os moradores agradecem pelo novo píer! Para tornar este lugar seguro diante das grandes embarcações, ainda precisamos evoluir bastante. Nosso objetivo é construir um farol e fazê-lo brilhar. Será um caminho árduo, mas recompensador.",
            "A vila está crescendo graças à sua ajuda. Os moradores agradecem por mais este avanço!",
            "Cada melhoria torna nossa comunidade mais forte. Muito obrigado por continuar ao nosso lado!",
            "O farol voltou a brilhar! Todo o vilarejo agradece por você ter tornado este lugar mais seguro."
        };

        public void Configure(GameObject first, GameObject second, GameObject third, GameObject fourth, BoatController3D targetBoat)
        {
            villageLevel1 = first; villageLevel2 = second; villageLevel3 = third;
            villageLevel4 = fourth;
            boat = targetBoat;
            interactor = boat != null ? boat.GetComponent<PlayerInteractor3D>() : null;
        }

        private void Start()
        {
            if (villageLevel0 == null)
                foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (candidate.name == "AldeiaNV0") { villageLevel0 = candidate.gameObject; break; }
            progress = GameProgress.Instance;
            if (progress == null) return;
            BuildPanel();
            progress.UpgradesChanged += OnProgressChanged;
            progress.TotalGoldChanged += OnGoldChanged;
            OnProgressChanged();
        }

        private void Update()
        {
            if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open()
        {
            if (GameJamOcean.UI.GameMenus.BlocksGameplay) return;
            if (isOpen || modal == null || progress == null || Time.timeScale == 0f) return;
            wasMoving = boat != null && boat.enabled;
            wasInteracting = interactor != null && interactor.enabled;
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            isOpen = true;
            GameJamOcean.Audio.GameAudio.Instance?.SetBoatEngineTemporarilyMuted(true);
            if (boat != null)
            {
                Rigidbody body = boat.GetComponent<Rigidbody>();
                boat.transform.SetPositionAndRotation(boat.DockPosition, boat.DockRotation);
                if (body != null)
                {
                    body.position = boat.DockPosition; body.rotation = boat.DockRotation;
                    body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                }
            }
            FindFirstObjectByType<GameJamOcean.CameraSystem.CameraFollow3D>()?.ShowMenuView();
            if (boat != null) boat.enabled = false;
            if (interactor != null) interactor.enabled = false;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            modal.SetActive(true);
            statusLabel.text = progress.IsGameCompleted ? "Aldeia N4 — jogo concluído!" : "Escolha um upgrade. Todos os preços são em ouro.";
            statusLabel.color = Color.white;
            Refresh();
            if (!firstPierGoldHintShown && progress.GetLevel(UpgradeKind.Island) == 0)
            {
                firstPierGoldHintShown = true;
                GameJamOcean.UI.OceanGoldHUD.FlashAvailableGold();
            }
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            GameJamOcean.Audio.GameAudio.Instance?.SetBoatEngineTemporarilyMuted(false);
            if (modal != null) modal.SetActive(false);
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            if (boat != null && wasMoving && (!boat.TryGetComponent(out Health health) || !health.IsDead)) boat.enabled = true;
            if (interactor != null && wasInteracting) interactor.enabled = true;
            FindFirstObjectByType<GameJamOcean.CameraSystem.CameraFollow3D>()?.ReturnFromMenuView(.5f);
        }

        private void OnDisable() { Close(); }
        private void OnDestroy()
        {
            Close();
            if (progress != null)
            {
                progress.UpgradesChanged -= OnProgressChanged;
                progress.TotalGoldChanged -= OnGoldChanged;
            }
        }

        private void OnGoldChanged(int gold) => Refresh();
        private void OnProgressChanged()
        {
            int level = progress.GetLevel(UpgradeKind.Island);
            GameObject chosen = level == 0 ? villageLevel0 : level == 1 ? villageLevel1
                : level == 2 ? villageLevel2 : level == 3 ? villageLevel3 : villageLevel4;
            if (chosen != null)
            {
                // Enable the new village FIRST, then remove the previous visuals.
                chosen.SetActive(true);
                if (villageLevel0 != null && villageLevel0 != chosen) villageLevel0.SetActive(false);
                if (villageLevel1 != null && villageLevel1 != chosen) villageLevel1.SetActive(false);
                if (villageLevel2 != null && villageLevel2 != chosen) villageLevel2.SetActive(false);
                if (villageLevel3 != null && villageLevel3 != chosen) villageLevel3.SetActive(false);
                if (villageLevel4 != null && villageLevel4 != chosen) villageLevel4.SetActive(false);
            }
            if (progress.IsGameCompleted && statusLabel != null) statusLabel.text = "Aldeia N4 — jogo concluído! Obrigado por construir sua aldeia.";
            Refresh();
        }

        private void Buy(int index)
        {
            if (!isOpen || Time.unscaledTime < nextPurchaseTime) return;
            nextPurchaseTime = Time.unscaledTime + 0.35f;
            if (index == 0 && (villageLevel1 == null || villageLevel2 == null || villageLevel3 == null || villageLevel4 == null))
            { statusLabel.text = "Configure as quatro versões da aldeia antes de comprar."; return; }
            UpgradeKind kind = VisibleUpgrades[index];
            bool completedBefore = progress.IsGameCompleted;
            bool purchased = progress.TryPurchase(kind, offeredLevels[index], out string message);
            if (purchased && kind == UpgradeKind.Island)
                GameJamOcean.Audio.GameAudio.Instance?.PlayVillageConstruction();
            if (purchased && kind == UpgradeKind.BoatHull && boat != null
                && boat.TryGetComponent(out BoatStats3D boatStats))
                boatStats.SelectBoat(progress.GetLevel(UpgradeKind.BoatHull));
            int villageLevel = progress.GetLevel(UpgradeKind.Island);
            statusLabel.text = purchased && kind == UpgradeKind.Island
                ? VillageThanks[Mathf.Clamp(villageLevel, 1, 4)]
                : progress.IsGameCompleted ? "Aldeia N4 — jogo concluído!" : message;
            if (purchased && message == "Upgrade adquirido!")
                ShowUpgradeAcquiredColor();
            else
                statusLabel.color = Color.white;
            Refresh();
            if (!completedBefore && progress.IsGameCompleted)
            {
                Close();
                EndGameCelebration3D.Show(villageLevel4);
            }
        }

        private void Refresh()
        {
            if (goldLabel == null || progress == null) return;
            goldLabel.text = $"OURO DISPONÍVEL: {progress.TotalGold}";
            if (repairButton != null)
            {
                var hull = boat != null ? boat.GetComponent<Health>() : null;
                bool damaged = hull != null && !hull.IsDead && hull.CurrentHealth < hull.MaximumHealth;
                int cost = damaged ? RepairCost(hull) : 0;
                repairPrice.text = damaged ? $"CONSERTAR BARCO — {cost} OURO" : "CASCO 100% — SEM REPAROS";
                repairButton.interactable = damaged && progress.TotalGold >= cost;
                SetPurchaseButtonVisual(repairButton);
            }
            string error = "Catálogo ausente. Execute Configure Upgrade System.";
            bool valid = progress.Catalog != null && progress.Catalog.Validate(out error);
            bool firstPierUpgrade = progress.GetLevel(UpgradeKind.Island) == 0;
            for (int i = 0; i < VisibleUpgrades.Length; i++)
            {
                if (rows[i] != null) rows[i].SetActive(!firstPierUpgrade || i == 0);
                if (firstPierUpgrade && i != 0) continue;
                UpgradeKind kind = VisibleUpgrades[i];
                int level = progress.GetLevel(kind);
                offeredLevels[i] = level;
                var definition = progress.Catalog != null ? progress.Catalog.Find(kind) : null;
                if (!valid || definition == null)
                {
                    upgradeNames[i].text = kind.ToString();
                    labels[i].text = "";
                    prices[i].text = "Indisponível";
                    buttons[i].interactable = false;
                    SetPurchaseButtonVisual(buttons[i]);
                    continue;
                }
                bool maximum = level >= definition.MaximumLevel;
                var next = definition.Tier(Mathf.Min(definition.MaximumLevel, level + 1));
                upgradeNames[i].text = firstPierUpgrade && kind == UpgradeKind.Island
                    ? "PIER" : UpgradeTitle(kind, level);
                labels[i].text = firstPierUpgrade && kind == UpgradeKind.Island
                    ? "O primeiro passo para melhorar o comércio marítimo; o próximo será nosso farol!"
                    : maximum ? "Nível máximo" : $"Nível {level + 1}: {Describe(definition.kind, next, level + 1)}";
                prices[i].text = maximum ? "MÁXIMO" : $"{next.goldCost} OURO\nCOMPRAR";
                buttons[i].interactable = !maximum && progress.TotalGold >= next.goldCost;
                if (kind == UpgradeKind.Island && (villageLevel1 == null || villageLevel2 == null || villageLevel3 == null || villageLevel4 == null)) buttons[i].interactable = false;
                SetPurchaseButtonVisual(buttons[i]);
            }
            if (repairButton != null) repairButton.gameObject.SetActive(!firstPierUpgrade);
            RefreshBoatSelection(firstPierUpgrade);
            if (!valid) statusLabel.text = error;
        }

        private static string Describe(UpgradeKind kind, UpgradeTier tier, int level)
        {
            if (kind == UpgradeKind.Island) return level switch
            {
                2 => "Mais moradores chegando, as condições da ilha estão melhorando.",
                3 => "A construção do farol será um grande avanço para ilha.",
                4 => "Acenda a luz do farol, e traga segurança para a nossa pequena ilha.",
                _ => "Nova aparência da ilha."
            };
            if (kind == UpgradeKind.BoatHull) return level switch
            {
                2 => "Casco +15% / Turbo +20% sobre o valor inicial",
                3 => "Casco +30% / Turbo +40% sobre o valor inicial",
                _ => "Nível máximo"
            };
            if (kind == UpgradeKind.DiverHealth) return "+2 pontos de vida.";
            if (kind == UpgradeKind.DiverSpeed) return level switch
            {
                2 => "15% sobre o valor inicial",
                3 => "30% sobre o valor inicial",
                _ => "Nível máximo"
            };
            if (kind == UpgradeKind.Harpoon) return level switch
            {
                2 => "Velocidade +2 / Distância +6",
                3 => "Velocidade +4 / Distância +10 / Dano +1",
                4 => "Projétil duplo",
                _ => "Nível máximo"
            };
            return $"+{tier.value:0.#}% sobre o valor inicial";
        }

        private static string UpgradeTitle(UpgradeKind kind, int level)
        {
            string name = kind switch
            {
                UpgradeKind.Island => "Ilha",
                UpgradeKind.BoatHull => "Embarcação",
                UpgradeKind.DiverHealth => "Mergulhador - Vida",
                UpgradeKind.DiverSpeed => "Mergulhador - Velocidade",
                UpgradeKind.Harpoon => "Mergulhador - Arpão",
                _ => kind.ToString()
            };
            return $"{name} | Atual Nível {level}";
        }

        private void BuildPanel()
        {
            modal = new GameObject("Upgrade Panel (Runtime)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            modal.transform.SetParent(transform, false);
            Canvas canvas = modal.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = modal.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform shade = Rect("Dim Background", modal.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.16f);
            RectTransform panel = GameJamOcean.UI.EditableUIFactory.CreatePanel(
                GameJamOcean.UI.EditableUIPanelKind.UpgradePanel, modal.transform,
                new Vector2(960, 900), new Color(0.025f, 0.10f, 0.14f, .72f),
                "Port Upgrades", out RectTransform content);
            panel.sizeDelta = new Vector2(960, 900);
            TMP_Text title = Label("Title", content, new Vector2(650, 40), new Vector2(0, -30), "UPGRADES DO PORTO", 30);
            GameJamOcean.UI.GameFontStyles.Apply(title, GameJamOcean.UI.GameFontRole.Display);
            goldLabel = Label("Gold", content, new Vector2(800, 35), new Vector2(0, -75), "OURO", 24);
            GameJamOcean.UI.GameFontStyles.Apply(goldLabel, GameJamOcean.UI.GameFontRole.Display);
            Button close = ButtonUI("Close", content, new Vector2(90, 40), new Vector2(400, -30), out TMP_Text closeText);
            closeText.text = "FECHAR";
            close.onClick.AddListener(Close);
            Image panelBackground = panel.GetComponent<Image>();
            for (int i = 0; i < VisibleUpgrades.Length; i++)
            {
                RectTransform row = Rect($"Upgrade {i}", content, new Vector2(880, 76), new Vector2(0, -130 - i * 82));
                rows[i] = row.gameObject;
                Image rowBackground = row.gameObject.AddComponent<Image>();
                rowBackground.sprite = panelBackground != null ? panelBackground.sprite : null;
                rowBackground.type = panelBackground != null ? panelBackground.type : Image.Type.Simple;
                rowBackground.color = Color.white;
                upgradeNames[i] = Label("Upgrade Name", row, new Vector2(630, 25), new Vector2(-100, -8), "", 18);
                upgradeNames[i].alignment = TextAlignmentOptions.MidlineLeft;
                GameJamOcean.UI.GameFontStyles.Apply(upgradeNames[i], GameJamOcean.UI.GameFontRole.Display);
                labels[i] = Label("Description", row, new Vector2(630, 35), new Vector2(-100, -35), "", 16);
                labels[i].alignment = TextAlignmentOptions.MidlineLeft;
                buttons[i] = ButtonUI("Buy", row, new Vector2(180, 64), new Vector2(335, -6), out prices[i]);
                prices[i].fontSize = 16.5f;
                prices[i].fontSizeMin = 11f;
                prices[i].fontSizeMax = 16.5f;
                prices[i].lineSpacing = -10f;
                int index = i;
                buttons[i].onClick.AddListener(() => Buy(index));
                if (i >= 2)
                    ConfigureCashbackClick(buttons[i]);
            }
            repairButton = ButtonUI("Repair Boat", content, new Vector2(540, 55), new Vector2(0, -545), out repairPrice);
            repairButton.onClick.AddListener(RepairBoat);
            ConfigureCashbackClick(repairButton);
            statusLabel = Label("Status", content, new Vector2(820, 90), new Vector2(0, -615), "", 21);
            for (int level = 1; level <= 3; level++)
            {
                int capturedLevel = level;
                boatButtons[level - 1] = ButtonUI($"Boat {level}", content, new Vector2(135, 100),
                    new Vector2(-245 + (level - 1) * 150, -740), out TMP_Text boatText);
                boatText.text = $"BOAT {level}";
                boatText.rectTransform.anchorMin = boatText.rectTransform.anchorMax =
                    boatText.rectTransform.pivot = new Vector2(.5f, 1f);
                boatText.rectTransform.sizeDelta = new Vector2(130, 26);
                boatText.rectTransform.anchoredPosition = new Vector2(0, -51);
                Image boatIcon = Rect("Boat Icon", boatButtons[level - 1].transform,
                    new Vector2(82, 45), new Vector2(0, -8)).gameObject.AddComponent<Image>();
                boatIcon.sprite = CreateBoatIconSprite(level);
                boatIcon.preserveAspect = true;
                boatIcon.raycastTarget = false;
                boatButtons[level - 1].onClick.AddListener(() => SelectBoat(capturedLevel));
            }
            modal.SetActive(false);
        }

        private void ConfigureCashbackClick(Button button)
        {
            GameJamOcean.UI.EditableUISettings settings = GameJamOcean.UI.EditableUIFactory.Settings;
            if (button != null && settings != null
                && button.TryGetComponent(out GameJamOcean.UI.UIButtonAudioFeedback feedback))
                feedback.ConfigureClickSound(
                    settings.cashbackButtonClickSound,
                    settings.cashbackButtonClickVolume);
        }

        private void ShowUpgradeAcquiredColor()
        {
            if (statusColorRoutine != null) StopCoroutine(statusColorRoutine);
            statusColorRoutine = StartCoroutine(ReturnStatusColorToWhite());
        }

        private System.Collections.IEnumerator ReturnStatusColorToWhite()
        {
            statusLabel.color = new Color(.2f, 1f, .35f, 1f);
            yield return new WaitForSecondsRealtime(1f);
            if (statusLabel != null) statusLabel.color = Color.white;
            statusColorRoutine = null;
        }

        private void RefreshBoatSelection(bool hide)
        {
            int unlocked = progress.GetLevel(UpgradeKind.BoatHull);
            int selected = progress.SelectedBoatLevel;
            for (int i = 0; i < boatButtons.Length; i++)
            {
                if (boatButtons[i] == null) continue;
                bool available = !hide && i + 1 <= unlocked;
                boatButtons[i].gameObject.SetActive(available);
                if (!available) continue;
                boatButtons[i].interactable = i + 1 != selected;
                Image image = boatButtons[i].GetComponent<Image>();
                if (image != null) image.color = i + 1 == selected
                    ? new Color(.65f, .65f, .65f, 1f) : Color.white;
            }
        }

        private static void SetPurchaseButtonVisual(Button button)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = button.interactable ? Color.white : new Color(.65f, .65f, .65f, 1f);
        }

        private void SelectBoat(int level)
        {
            BoatStats3D stats = boat != null ? boat.GetComponent<BoatStats3D>() : null;
            if (stats != null) stats.SelectBoat(level);
            Refresh();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private static TMP_Text Label(string name, Transform parent, Vector2 size, Vector2 position, string text, float fontSize)
        {
            var label = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            GameJamOcean.UI.GameFontStyles.Apply(label, GameJamOcean.UI.GameFontRole.General);
            label.text = text; label.fontSize = fontSize; label.color = new Color(1f, 1f, 1f, 1f);
            label.alpha = 1f;
            label.enableVertexGradient = false;
            label.enableAutoSizing = true; label.fontSizeMin = Mathf.Max(10f, fontSize * .72f); label.fontSizeMax = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button ButtonUI(string name, Transform parent, Vector2 size, Vector2 position, out TMP_Text text)
        {
            Button button = GameJamOcean.UI.EditableUIFactory.CreateButton(name, parent, size,
                position, out text, new Color(0.15f, 0.48f, 0.50f, 1));
            GameJamOcean.UI.GameFontStyles.Apply(text, GameJamOcean.UI.GameFontRole.General);
            text.fontSize = 18;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12;
            text.fontSizeMax = 18;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 1f, 1f, 1f);
            text.alpha = 1f;
            text.enableVertexGradient = false;
            text.raycastTarget = false;
            return button;
        }

        private static Sprite CreateBoatIconSprite(int level)
        {
            const int width = 64, height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = $"Boat {level} menu icon", filterMode = FilterMode.Point };
            Color32 clear = new(0, 0, 0, 0);
            Color32[] pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            Color32 hull = level == 1 ? new Color32(245, 178, 45, 255)
                : level == 2 ? new Color32(80, 165, 205, 255) : new Color32(214, 89, 55, 255);
            Color32 cabin = new(235, 240, 244, 255);
            int left = 8 - level * 2, right = 55 + level * 2;
            for (int y = 7; y <= 14; y++)
            for (int x = left + (14 - y); x <= right - (14 - y); x++) pixels[y * width + x] = hull;
            int cabinWidth = 13 + level * 4;
            for (int y = 15; y <= 24; y++)
            for (int x = 32 - cabinWidth / 2; x <= 32 + cabinWidth / 2; x++) pixels[y * width + x] = cabin;
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 32f);
        }
    }
}
