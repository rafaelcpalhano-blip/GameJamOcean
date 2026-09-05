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
        private readonly int[] offeredLevels = new int[5];
        private bool isOpen;
        private bool wasMoving;
        private bool wasInteracting;
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private float nextPurchaseTime;
        private GameProgress progress;

        public void Configure(GameObject first, GameObject second, GameObject third, GameObject fourth, BoatController3D targetBoat)
        {
            villageLevel1 = first; villageLevel2 = second; villageLevel3 = third;
            villageLevel4 = fourth;
            boat = targetBoat;
            interactor = boat != null ? boat.GetComponent<PlayerInteractor3D>() : null;
        }

        private void Start()
        {
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
            GameObject chosen = level == 1 ? villageLevel1 : level == 2 ? villageLevel2 : level == 3 ? villageLevel3 : villageLevel4;
            if (chosen != null)
            {
                // Enable the new village FIRST, then remove the previous visuals.
                chosen.SetActive(true);
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
            progress.TryPurchase(kind, offeredLevels[index], out string message);
            statusLabel.text = progress.IsGameCompleted ? "Aldeia N4 — jogo concluído!" : message;
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
            for (int i = 0; i < VisibleUpgrades.Length; i++)
            {
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
                labels[i].text = $"{definition.displayName}  |  N{level}" +
                    (maximum ? "\nNível máximo" : $"\nN{level + 1}: {Describe(definition.kind, next, level + 1)}");
                prices[i].text = maximum ? "MÁXIMO" : $"{next.goldCost} OURO\nCOMPRAR";
                buttons[i].interactable = !maximum && progress.TotalGold >= next.goldCost;
                if (kind == UpgradeKind.Island && (villageLevel1 == null || villageLevel2 == null || villageLevel3 == null || villageLevel4 == null)) buttons[i].interactable = false;
            }
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
            modal.SetActive(false);
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
    }
}
