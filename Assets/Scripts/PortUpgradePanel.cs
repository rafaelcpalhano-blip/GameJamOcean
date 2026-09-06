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
        private readonly Button[] boatButtons = new Button[3];
        private Button colorButton;
        private GameObject colorPanel;
        private readonly Image[] colorSamples = new Image[3];
        private readonly Color[] pendingColors = new Color[3];
        private readonly Color[] initialColors = new Color[3];
        private readonly bool[] pendingCustomized = new bool[3];
        private TMP_Text colorCostLabel;
        private int selectedColorSlot;
        private const int ColorPrice = 50;
        private static readonly string[] VillageThanks =
        {
            "",
            "Os moradores agradecem pelo novo Pier! Para tornar este lugar seguro diante das grandes embarcações, ainda precisaremos evoluir bastante. Nosso objetivo é fazer o farol voltar a brilhar. Será um caminho árduo, mas recompensador.",
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
            int villageLevel = progress.GetLevel(UpgradeKind.Island);
            statusLabel.text = purchased && kind == UpgradeKind.Island
                ? VillageThanks[Mathf.Clamp(villageLevel, 1, 4)]
                : progress.IsGameCompleted ? "Aldeia N4 — jogo concluído!" : message;
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
                    labels[i].text = kind.ToString();
                    prices[i].text = "Indisponível";
                    buttons[i].interactable = false;
                    continue;
                }
                bool maximum = level >= definition.MaximumLevel;
                var next = definition.Tier(Mathf.Min(definition.MaximumLevel, level + 1));
                labels[i].text = firstPierUpgrade && kind == UpgradeKind.Island
                    ? "PIER\nO primeiro passo para melhorar o comércio marítimo; o próximo será nosso farol!"
                    : $"{definition.displayName}  |  N{level}" +
                    (maximum ? "\nNível máximo" : $"\nN{level + 1}: {Describe(definition.kind, next, level + 1)}");
                prices[i].text = maximum ? "MÁXIMO" : $"{next.goldCost} OURO\nCOMPRAR";
                buttons[i].interactable = !maximum && progress.TotalGold >= next.goldCost;
                if (kind == UpgradeKind.Island && (villageLevel1 == null || villageLevel2 == null || villageLevel3 == null || villageLevel4 == null)) buttons[i].interactable = false;
            }
            if (repairButton != null) repairButton.gameObject.SetActive(!firstPierUpgrade);
            RefreshBoatSelection(firstPierUpgrade);
            if (!valid) statusLabel.text = error;
        }

        private static string Describe(UpgradeKind kind, UpgradeTier tier, int level)
        {
            if (kind == UpgradeKind.Island) return level == 4 ? "Aldeia final — conclusão do jogo" : "Nova aparência da aldeia";
            if (kind == UpgradeKind.Harpoon) return level == 4 ? "Disparo duplo permanente" : tier.harpoonPrefab != null ? tier.harpoonPrefab.name : "Prefab ausente";
            if (kind == UpgradeKind.DiverHealth) return $"{tier.value:0} pontos de vida";
            if (kind == UpgradeKind.BoatHull)
            {
                var turbo = GameProgress.HasInstance ? GameProgress.Instance.Catalog?.Find(UpgradeKind.BoatTurbo) : null;
                float turboValue = turbo != null ? turbo.Tier(level).value : 0f;
                return $"Casco +{tier.value:0.#}% / Turbo +{turboValue:0.#}%";
            }
            return $"+{tier.value:0.#}% sobre o valor inicial";
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
            RectTransform panel = Rect("Port Upgrades", modal.transform, new Vector2(900, 800), Vector2.zero);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.10f, 0.14f, .72f);
            Label("Title", panel, new Vector2(700, 40), new Vector2(-45, -22), "UPGRADES DO PORTO", 30);
            goldLabel = Label("Gold", panel, new Vector2(800, 35), new Vector2(0, -75), "OURO", 24);
            Button close = ButtonUI("Close", panel, new Vector2(90, 40), new Vector2(380, -22), out TMP_Text closeText);
            closeText.text = "FECHAR";
            close.onClick.AddListener(Close);
            for (int i = 0; i < VisibleUpgrades.Length; i++)
            {
                RectTransform row = Rect($"Upgrade {i}", panel, new Vector2(830, 65), new Vector2(0, -130 - i * 75));
                rows[i] = row.gameObject;
                row.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.19f, 0.23f, 1);
                labels[i] = Label("Description", row, new Vector2(600, 60), new Vector2(-105, -2), "", 20);
                labels[i].alignment = TextAlignmentOptions.MidlineLeft;
                buttons[i] = ButtonUI("Buy", row, new Vector2(180, 55), new Vector2(310, -5), out prices[i]);
                int index = i;
                buttons[i].onClick.AddListener(() => Buy(index));
            }
            repairButton = ButtonUI("Repair Boat", panel, new Vector2(650, 55), new Vector2(0, -520), out repairPrice);
            repairButton.onClick.AddListener(RepairBoat);
            statusLabel = Label("Status", panel, new Vector2(820, 90), new Vector2(0, -600), "", 21);
            for (int level = 1; level <= 3; level++)
            {
                int capturedLevel = level;
                boatButtons[level - 1] = ButtonUI($"Boat {level}", panel, new Vector2(130, 72),
                    new Vector2(-245 + (level - 1) * 145, -700), out TMP_Text boatText);
                boatText.text = $"BOAT {level}";
                boatText.rectTransform.sizeDelta = new Vector2(125, 24);
                boatText.rectTransform.anchoredPosition = new Vector2(0, -21);
                Image boatIcon = Rect("Boat Icon", boatButtons[level - 1].transform,
                    new Vector2(72, 38), new Vector2(0, -5)).gameObject.AddComponent<Image>();
                boatIcon.sprite = CreateBoatIconSprite(level);
                boatIcon.preserveAspect = true;
                boatIcon.raycastTarget = false;
                boatButtons[level - 1].onClick.AddListener(() => SelectBoat(capturedLevel));
            }
            colorButton = ButtonUI("Boat Colors", panel, new Vector2(210, 72), new Vector2(275, -700), out TMP_Text colorsText);
            colorsText.text = "PERSONALIZAR\nCORES";
            colorButton.onClick.AddListener(OpenColorPanel);
            BuildColorPanel();
            modal.SetActive(false);
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
                if (available) boatButtons[i].GetComponent<Image>().color = i + 1 == selected
                    ? new Color(.9f, .68f, .12f) : new Color(.15f, .48f, .5f);
            }
            if (colorButton != null) colorButton.gameObject.SetActive(!hide);
        }

        private void SelectBoat(int level)
        {
            BoatStats3D stats = boat != null ? boat.GetComponent<BoatStats3D>() : null;
            if (stats != null) stats.SelectBoat(level);
            Refresh();
        }

        private void BuildColorPanel()
        {
            RectTransform root = Rect("Boat Color Panel", modal.transform, new Vector2(680, 500), Vector2.zero);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f;
            root.gameObject.AddComponent<Image>().color = new Color(.02f, .09f, .14f, .98f);
            colorPanel = root.gameObject;
            Label("CORES DO BARCO", root, new Vector2(600, 45), new Vector2(0, -25), "CORES DO BARCO", 28);
            for (int slot = 0; slot < 3; slot++)
            {
                int captured = slot;
                Button button = ButtonUI($"Cor {slot + 1}", root, new Vector2(150, 70),
                    new Vector2(-170 + slot * 170, -95), out TMP_Text label);
                label.text = $"COR {slot + 1}";
                colorSamples[slot] = button.GetComponent<Image>();
                button.onClick.AddListener(() => selectedColorSlot = captured);
            }
            Color[] palette = { Color.red, new Color(.1f, .45f, 1f), new Color(.15f, .8f, .3f),
                Color.yellow, Color.white, new Color(.25f, .25f, .25f), Color.black };
            for (int i = 0; i < palette.Length; i++)
            {
                Color choice = palette[i];
                Button swatch = ButtonUI($"Paleta {i + 1}", root, new Vector2(64, 64),
                    new Vector2(-225 + i * 75, -205), out TMP_Text swatchText);
                swatchText.text = "";
                swatch.GetComponent<Image>().color = choice;
                swatch.onClick.AddListener(() => ChooseColor(choice));
            }
            colorCostLabel = Label("Color Cost", root, new Vector2(500, 40), new Vector2(0, -290),
                "Custo total: 0 Gold", 23);
            Button apply = ButtonUI("Apply Colors", root, new Vector2(180, 55), new Vector2(-205, -370), out TMP_Text applyText);
            applyText.text = "APLICAR"; apply.onClick.AddListener(ApplyColors);
            Button cancel = ButtonUI("Cancel Colors", root, new Vector2(180, 55), new Vector2(0, -370), out TMP_Text cancelText);
            cancelText.text = "CANCELAR"; cancel.onClick.AddListener(() => colorPanel.SetActive(false));
            Button reset = ButtonUI("Reset Colors", root, new Vector2(180, 55), new Vector2(205, -370), out TMP_Text resetText);
            resetText.text = "RESET GRÁTIS"; reset.onClick.AddListener(ResetColors);
            colorPanel.SetActive(false);
        }

        private void OpenColorPanel()
        {
            BoatVisualUpgrade3D visual = boat != null ? boat.GetComponent<BoatVisualUpgrade3D>() : null;
            if (visual == null) return;
            int level = progress.SelectedBoatLevel;
            for (int slot = 0; slot < 3; slot++)
            {
                initialColors[slot] = pendingColors[slot] = visual.GetDisplayedColor(slot);
                pendingCustomized[slot] = progress.HasCustomizedBoatColor(level, slot);
                colorSamples[slot].color = pendingColors[slot];
            }
            selectedColorSlot = 0;
            UpdateColorCost();
            colorPanel.SetActive(true);
            colorPanel.transform.SetAsLastSibling();
        }

        private void ChooseColor(Color color)
        {
            pendingColors[selectedColorSlot] = color;
            pendingCustomized[selectedColorSlot] = true;
            colorSamples[selectedColorSlot].color = color;
            UpdateColorCost();
        }

        private int ChangedColorCount()
        {
            int count = 0;
            for (int slot = 0; slot < 3; slot++)
                if (Vector4.SqrMagnitude((Vector4)(pendingColors[slot] - initialColors[slot])) > .0001f) count++;
            return count;
        }

        private void UpdateColorCost()
        {
            if (colorCostLabel != null) colorCostLabel.text = $"Custo total: {ChangedColorCount() * ColorPrice} Gold";
        }

        private void ApplyColors()
        {
            if (!progress.TryApplyBoatColors(progress.SelectedBoatLevel, pendingColors, pendingCustomized,
                    ChangedColorCount(), ColorPrice, out int cost))
            {
                colorCostLabel.text = $"Gold insuficiente — custo total: {cost} Gold";
                return;
            }
            colorPanel.SetActive(false);
            statusLabel.text = $"Cores aplicadas por {cost} Gold.";
            Refresh();
        }

        private void ResetColors()
        {
            progress.ResetBoatColors(progress.SelectedBoatLevel);
            colorPanel.SetActive(false);
            statusLabel.text = "Cores originais restauradas gratuitamente.";
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
            label.text = text; label.fontSize = fontSize; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button ButtonUI(string name, Transform parent, Vector2 size, Vector2 position, out TMP_Text text)
        {
            RectTransform rect = Rect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.15f, 0.48f, 0.50f, 1);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            text = Label("Label", rect, size, Vector2.zero, "", 18);
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
