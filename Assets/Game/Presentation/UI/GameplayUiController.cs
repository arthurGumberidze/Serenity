using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Control;
using Game.Domain.Dynasties;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Domain.Events;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Presentation.Resources;
using Game.Simulation.Control;
using Game.Simulation.Military;
using Game.Simulation.Research;
using Game.Simulation.Time;
using Game.Simulation.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// One player-facing UI Toolkit projection over the existing canonical services. It owns only panel,
    /// selection and confirmation state; gameplay mutations are routed back through Simulation services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayUiController : MonoBehaviour, IWorldPointerUiBlocker, IWorldPointerUiRegionBlocker
    {
        private const float AutomaticRefreshIntervalSeconds = 1f;
        private static readonly Color PanelColor = SerenityUiDesign.Surface;
        private static readonly Color HeaderColor = SerenityUiDesign.SurfaceRaised;
        private static readonly Color AccentColor = SerenityUiDesign.Accent;
        private static readonly Color TextColor = SerenityUiDesign.Text;
        private static readonly GameSpeed[] Speeds =
        {
            GameSpeed.Normal, GameSpeed.Fast, GameSpeed.Faster, GameSpeed.VeryFast, GameSpeed.Maximum
        };

        private GameplayUiContext context;
        private WorldPointerRaycaster raycaster;
        private LocalGameplayInputSource input;
        private UIDocument document;
        private PanelSettings panelSettings;
        private VisualElement root;
        private VisualElement topBar;
        private VisualElement navigation;
        private ScrollView mainPanel;
        private ScrollView inspector;
        private VisualElement notifications;
        private VisualElement statusBar;
        private VisualElement tpsHud;
        private VisualElement modal;
        private VisualElement onboardingCard;
        private Button onboardingToggleButton;
        private Label resourcesLabel;
        private Label dateLabel;
        private Label weatherLabel;
        private Label modeLabel;
        private Label alertLabel;
        private Label statusLabel;
        private Label tpsLabel;
        private Label gameOverLabel;
        private readonly List<string> notificationFeed = new List<string>();
        private int notificationVersion;
        private int renderedNotificationVersion = -1;
        private bool emptyInspectorRendered;
        private GameplayPanel activePanel;
        private WorkGroupId? selectedGroup;
        private MilitaryUnitId? selectedUnit;
        private WorldLocationId? selectedLocation;
        private EventInstanceId? selectedEvent;
        private WorkPriority workPriority = WorkPriority.High;
        private int pendingLoadSlot;
        private long pendingNewGameSeed;
        private float nextRefresh;
        private ulong lastRefreshHash;
        private bool initialized;
        private bool subscribed;
        private bool notificationsPrimed;
        private int knownResearchCount;
        private int knownCharacterCount;
        private int knownAliveCount;
        private int knownDynastyHistoryCount;
        private int knownConditionCount;
        private int knownBuildingHistoryCount;
        private int knownBattleEventCount;
        private int knownTravelCount;
        private long knownFoodShortage;
        private int knownEventHistoryCount;
        private int knownPendingEventCount;
        private ulong knownCampaignEventSequence;
        private ulong knownOccupationEventSequence;
        private int knownAdultCount;
        private bool suppressNotificationCaptureOnce;
        private readonly FirstHourOnboardingProgress onboarding = new FirstHourOnboardingProgress();

        public bool DeveloperUiVisible { get; private set; }
        public GameplayPanel ActivePanel => activePanel;
        public int SubscriptionCount => subscribed ? 4 : 0;
        public VisualElement RootVisual => root;
        public IReadOnlyList<string> Notifications => notificationFeed.AsReadOnly();
        public string Locale => RussianTextCatalog.Locale;
        public float UiScale => panelSettings?.scale ?? 1f;
        public bool OnboardingVisible => onboarding.Visible;
        public FirstHourStage OnboardingStage => onboarding.CurrentStage;
        public int OnboardingCompletedCount => onboarding.CompletedCount;

        public void SetUiScale(float value)
        {
            if (panelSettings == null) throw new InvalidOperationException("Gameplay UI is not initialized.");
            panelSettings.scale = Mathf.Clamp(value, 1f, 1.5f);
        }

        public void Initialize(GameplayUiContext gameplayContext, WorldPointerRaycaster worldRaycaster,
            LocalGameplayInputSource inputSource)
        {
            if (initialized) throw new InvalidOperationException("Gameplay UI is already initialized.");
            context = gameplayContext ?? throw new ArgumentNullException(nameof(gameplayContext));
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            input = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            CreateDocument();
            BuildLayout();
            Subscribe();
            raycaster.RegisterUiBlocker(this);
            SetDeveloperUiVisible(false);
            RefreshNow();
            initialized = true;
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = default;
            return false;
        }

        public bool IsGuiPointBlocked(Vector2 point)
        {
            if (!isActiveAndEnabled || root == null) return false;
            var screenPoint = new Vector2(point.x, Screen.height - point.y);
            var panelPoint = root.panel == null ? point : RuntimePanelUtils.ScreenToPanel(root.panel, screenPoint);
            if (Blocks(topBar, panelPoint) || Blocks(navigation, panelPoint) || Blocks(mainPanel, panelPoint) ||
                Blocks(inspector, panelPoint) || Blocks(notifications, panelPoint) || Blocks(onboardingCard, panelPoint) ||
                Blocks(statusBar, panelPoint) || Blocks(tpsHud, panelPoint)) return true;
            return Blocks(modal, panelPoint);
        }

        public void SetPanel(GameplayPanel panel)
        {
            activePanel = activePanel == panel ? GameplayPanel.None : panel;
            mainPanel.style.display = activePanel == GameplayPanel.None ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshNavigationSelection();
            RefreshNow();
        }

        public void SetOnboardingVisible(bool visible)
        {
            onboarding.SetVisible(visible);
            RefreshOnboarding();
        }

        public void SetDeveloperUiVisible(bool visible)
        {
            DeveloperUiVisible = visible;
            foreach (var overlay in context.LegacyDebugOverlays)
                if (overlay != null) overlay.enabled = visible;
            context.ThirdPerson.ShowLegacyOverlay = visible;
            context.SetDeveloperUiVisible(visible);
            statusLabel.text = T(visible ? "ui.status.developer" : "ui.status.player");
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (context == null || root == null) return;
            ObserveOnboardingSignals();
            RefreshHud();
            RefreshOnboarding();
            CaptureNotifications();
            RefreshNotifications();
            RefreshInspector();
            RefreshPanel();
            RefreshTps();
            RefreshGameOver();
            lastRefreshHash = ComputeRefreshHash();
        }

        public void SaveSlot(int index) => TryCommand(() =>
        {
            context.Saves.Save(index);
            AddNotification(F("ui.notification.saved", index));
        });

        public void RequestLoadSlot(int index)
        {
            pendingLoadSlot = index;
            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddTitle(modal, F("ui.modal.load.title", index));
            AddLine(modal, T("ui.modal.load.body"));
            var row = Row();
            row.Add(ActionButton(T("common.load"), () => ConfirmLoadSlot(index)));
            row.Add(ActionButton(T("common.cancel"), CloseModal));
            modal.Add(row);
        }

        public void ConfirmLoadSlot(int index) => TryCommand(() =>
        {
            suppressNotificationCaptureOnce = true;
            context.Saves.Load(index);
            context.Selection.ClearSelection();
            pendingLoadSlot = 0;
            CloseModal();
            AddNotification(F("ui.notification.loaded", index));
        });

        public void RequestNewGame(long worldSeed)
        {
            if (context.StartNewGame == null) throw PlayerError("ui.status.command_failed");
            pendingNewGameSeed = worldSeed;
            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddTitle(modal, T("ui.modal.new_game.title"));
            AddLine(modal, F("ui.modal.new_game.body", worldSeed));
            var row = Row();
            row.Add(ActionButton(T("ui.action.new_game"), () => ConfirmNewGame(worldSeed), "confirm-new-game"));
            row.Add(ActionButton(T("common.cancel"), CloseModal));
            modal.Add(row);
        }

        public void ConfirmNewGame(long worldSeed)
        {
            pendingNewGameSeed = 0;
            context.StartNewGame?.Invoke(worldSeed);
        }

        public void RequestReturnToMainMenu()
        {
            if (context.RequestMainMenu == null) throw PlayerError("ui.status.command_failed");
            if (!context.Saves.HasUnsavedChanges)
            {
                context.RequestMainMenu.Invoke();
                return;
            }

            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddTitle(modal, T("ui.modal.main_menu.title"));
            AddLine(modal, T("ui.modal.main_menu.unsaved"));
            var row = Row();
            row.Add(ActionButton(T("ui.modal.main_menu.leave"), ConfirmReturnToMainMenu,
                "confirm-main-menu"));
            row.Add(ActionButton(T("common.cancel"), CloseModal, "cancel-main-menu"));
            modal.Add(row);
        }

        public void ConfirmReturnToMainMenu()
        {
            CloseModal();
            context.RequestMainMenu?.Invoke();
        }

        public void CancelModal() => CloseModal();

        private void Update()
        {
            if (!initialized) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame)
                SetDeveloperUiVisible(!DeveloperUiVisible);
            HandleWorkGroupHotkeys(keyboard);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + AutomaticRefreshIntervalSeconds;
            var hash = ComputeRefreshHash();
            if (hash == lastRefreshHash) return;
            RefreshNow();
        }

        private void OnEnable()
        {
            Subscribe();
            raycaster?.RegisterUiBlocker(this);
        }

        private void OnDisable()
        {
            Unsubscribe();
            raycaster?.UnregisterUiBlocker(this);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            raycaster?.UnregisterUiBlocker(this);
            if (document != null) Destroy(document);
            if (panelSettings != null) Destroy(panelSettings);
        }

        private void Subscribe()
        {
            if (subscribed || input == null) return;
            input.PauseToggled += OnPauseToggle;
            input.TimeSlowerRequested += OnSlower;
            input.TimeFasterRequested += OnFaster;
            context.BuildingPlacement.BuildMenuRequested += OnBuildMenuRequested;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || input == null || context == null) return;
            input.PauseToggled -= OnPauseToggle;
            input.TimeSlowerRequested -= OnSlower;
            input.TimeFasterRequested -= OnFaster;
            context.BuildingPlacement.BuildMenuRequested -= OnBuildMenuRequested;
            subscribed = false;
        }

        private void OnPauseToggle()
        {
            if (!DeveloperUiVisible && !IsTextInputFocused()) context.SetPaused(!context.Clock().IsPaused);
        }

        private void OnSlower()
        {
            if (!DeveloperUiVisible && !IsTextInputFocused()) StepSpeed(-1);
        }

        private void OnFaster()
        {
            if (!DeveloperUiVisible && !IsTextInputFocused()) StepSpeed(1);
        }

        private void OnBuildMenuRequested()
        {
            if (!DeveloperUiVisible && !IsTextInputFocused()) SetPanel(GameplayPanel.Build);
        }

        private void CreateDocument()
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "Serenity Gameplay UI Panel";
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1600, 900);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.themeStyleSheet = UnityEngine.Resources.Load<ThemeStyleSheet>("SerenityRuntimeTheme");
            if (panelSettings.themeStyleSheet == null)
                throw new InvalidOperationException("SerenityRuntimeTheme.tss is required for the gameplay UI.");
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = 100;
            root = document.rootVisualElement;
            root.name = "gameplay-ui-root";
            root.style.flexGrow = 1f;
            SerenityUiDesign.ApplyRoot(root, "serenity-gameplay");
            root.pickingMode = PickingMode.Ignore;
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplyResponsiveLayout());
        }

        private void BuildLayout()
        {
            topBar = Panel("top-hud");
            topBar.style.position = Position.Absolute;
            topBar.style.left = 10; topBar.style.right = 10; topBar.style.top = 10; topBar.style.height = 68;
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems = Align.Center;
            topBar.style.paddingLeft = 10; topBar.style.paddingRight = 10;
            SerenityUiDesign.StyleSurface(topBar, true);
            topBar.pickingMode = PickingMode.Position;
            resourcesLabel = HudLabel(350);
            dateLabel = HudLabel(175);
            weatherLabel = HudLabel(210);
            modeLabel = HudLabel(112);
            alertLabel = HudLabel(105);
            topBar.Add(resourcesLabel);
            topBar.Add(dateLabel);
            topBar.Add(weatherLabel);
            topBar.Add(modeLabel);
            topBar.Add(alertLabel);
            var pause = ActionButton(T("ui.action.pause"), () => context.SetPaused(!context.Clock().IsPaused), "pause-button");
            pause.AddToClassList(SerenityUiDesign.PrimaryButtonClass);
            topBar.Add(pause);
            foreach (var speed in Speeds)
            {
                var captured = speed;
                var speedButton = ActionButton("x" + (int)speed, () => context.SetSpeed(captured), "speed-" + (int)speed);
                speedButton.AddToClassList("serenity-time-button");
                topBar.Add(speedButton);
            }
            root.Add(topBar);

            navigation = Panel("navigation");
            navigation.style.position = Position.Absolute;
            navigation.style.left = 10; navigation.style.top = 88; navigation.style.bottom = 58; navigation.style.width = 166;
            navigation.style.paddingTop = 8; navigation.style.paddingLeft = 7; navigation.style.paddingRight = 7;
            SerenityUiDesign.StyleSurface(navigation);
            navigation.pickingMode = PickingMode.Position;
            onboardingToggleButton = ActionButton(T("ui.onboarding.hide"),
                () => SetOnboardingVisible(!onboarding.Visible), "onboarding-toggle");
            navigation.Add(onboardingToggleButton);
            AddNavigation(T("ui.nav.build"), GameplayPanel.Build);
            AddNavigation(T("ui.nav.settlement"), GameplayPanel.Economy);
            AddNavigation(T("ui.nav.research"), GameplayPanel.Research);
            AddNavigation(T("ui.nav.work"), GameplayPanel.Work);
            AddNavigation(T("ui.nav.dynasty"), GameplayPanel.Dynasty);
            AddNavigation(T("ui.nav.military"), GameplayPanel.Military);
            AddNavigation(T("ui.nav.saves"), GameplayPanel.SaveLoad);
            AddNavigation(T("ui.nav.world_map"), GameplayPanel.Global);
            if (context.Events != null) AddNavigation(T("ui.nav.events"), GameplayPanel.Events);
            root.Add(navigation);

            var onboardingScroll = new ScrollView(ScrollViewMode.Vertical) { name = "onboarding-card" };
            onboardingScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            onboardingCard = onboardingScroll;
            onboardingCard.style.position = Position.Absolute;
            onboardingCard.style.right = 10; onboardingCard.style.top = 226; onboardingCard.style.width = 354;
            onboardingCard.style.maxHeight = 246;
            onboardingCard.style.paddingLeft = 14; onboardingCard.style.paddingRight = 14;
            onboardingCard.style.paddingTop = 12; onboardingCard.style.paddingBottom = 12;
            SerenityUiDesign.StyleCard(onboardingCard);
            onboardingCard.pickingMode = PickingMode.Position;
            root.Add(onboardingCard);

            mainPanel = new ScrollView(ScrollViewMode.Vertical) { name = "main-panel" };
            StylePanel(mainPanel);
            mainPanel.AddToClassList("serenity-main-panel");
            mainPanel.style.position = Position.Absolute;
            mainPanel.style.left = 186; mainPanel.style.top = 88; mainPanel.style.bottom = 58; mainPanel.style.width = 486;
            mainPanel.style.display = DisplayStyle.None;
            mainPanel.pickingMode = PickingMode.Position;
            root.Add(mainPanel);

            inspector = new ScrollView(ScrollViewMode.Vertical) { name = "selection-inspector" };
            StylePanel(inspector);
            inspector.AddToClassList("serenity-inspector");
            inspector.style.position = Position.Absolute;
            inspector.style.right = 10; inspector.style.top = 482; inspector.style.bottom = 58; inspector.style.width = 354;
            inspector.pickingMode = PickingMode.Position;
            root.Add(inspector);

            notifications = Panel("notifications");
            notifications.style.position = Position.Absolute;
            notifications.style.right = 10; notifications.style.top = 88; notifications.style.width = 354;
            notifications.style.maxHeight = 126; notifications.style.paddingLeft = 10; notifications.style.paddingRight = 10;
            notifications.AddToClassList("serenity-notifications");
            notifications.pickingMode = PickingMode.Position;
            root.Add(notifications);

            statusBar = Panel("status-bar");
            statusBar.style.position = Position.Absolute;
            statusBar.style.left = 10; statusBar.style.right = 10; statusBar.style.bottom = 8; statusBar.style.height = 42;
            SerenityUiDesign.StyleSurface(statusBar, true);
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.alignItems = Align.Center;
            statusBar.style.paddingLeft = 12;
            statusLabel = new Label(T("ui.status.player"));
            statusLabel.style.flexGrow = 1;
            statusBar.Add(statusLabel);
            var mainMenuButton = ActionButton(T("ui.action.main_menu"), RequestReturnToMainMenu, "main-menu-button");
            mainMenuButton.style.marginRight = 8;
            statusBar.Add(mainMenuButton);
            root.Add(statusBar);

            tpsHud = Panel("tps-hud");
            tpsHud.style.position = Position.Absolute;
            tpsHud.style.left = new Length(36, LengthUnit.Percent);
            tpsHud.style.right = new Length(31, LengthUnit.Percent);
            tpsHud.style.bottom = 58;
            SerenityUiDesign.StyleSurface(tpsHud, true);
            tpsHud.style.paddingLeft = 12; tpsHud.style.paddingRight = 12;
            tpsLabel = new Label();
            tpsHud.Add(tpsLabel);
            tpsHud.style.display = DisplayStyle.None;
            root.Add(tpsHud);

            gameOverLabel = new Label();
            gameOverLabel.name = "game-over";
            gameOverLabel.style.position = Position.Absolute;
            gameOverLabel.style.left = new Length(28, LengthUnit.Percent);
            gameOverLabel.style.right = new Length(28, LengthUnit.Percent);
            gameOverLabel.style.top = new Length(38, LengthUnit.Percent);
            gameOverLabel.style.paddingTop = 24; gameOverLabel.style.paddingBottom = 24;
            gameOverLabel.style.backgroundColor = new Color(0.24f, 0.075f, 0.045f, 0.97f);
            gameOverLabel.style.borderLeftColor = SerenityUiDesign.Danger;
            gameOverLabel.style.borderLeftWidth = 4;
            gameOverLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            gameOverLabel.style.fontSize = 24;
            gameOverLabel.style.display = DisplayStyle.None;
            root.Add(gameOverLabel);

            modal = Panel("confirmation-modal");
            modal.style.position = Position.Absolute;
            modal.style.left = new Length(31, LengthUnit.Percent);
            modal.style.right = new Length(31, LengthUnit.Percent);
            modal.style.top = new Length(34, LengthUnit.Percent);
            modal.style.paddingLeft = 20; modal.style.paddingRight = 20; modal.style.paddingTop = 16; modal.style.paddingBottom = 16;
            modal.AddToClassList(SerenityUiDesign.ModalClass);
            modal.style.display = DisplayStyle.None;
            modal.pickingMode = PickingMode.Position;
            root.Add(modal);
            ApplyResponsiveLayout();
        }

        private void ObserveOnboardingSignals()
        {
            var berries = new ResourceId("berries");
            var shelter = context.Buildings.All.Any(x => x.DefinitionId == new BuildingDefinitionId("primitive_shelter") &&
                                                          x.IsOperational);
            var storage = context.Buildings.All.Any(x => x.DefinitionId == new BuildingDefinitionId("storage_basket") &&
                                                          x.IsOperational && context.Inventories.TryGet(
                                                              new InventoryOwner(InventoryOwnerKind.BuildingStorage, x.Id), out _));
            var researchStarted = context.Research.ActiveProject != null || context.Research.CompletedTechnologies.Any(x =>
                x.ToString() != "basic_survival" && x.ToString() != "gathering");
            var dangerHandled = (context.Events?.History.Count ?? 0) > 0 ||
                                context.Health.Conditions.Any(x => x.State != HealthConditionState.Recovered) ||
                                context.Battles.Battles.Any(x => x.BattleHistory.Count > 0);
            var scoutingStarted = context.Campaign?.State.RivalDiscovered == true || context.GlobalMap.Travels.Any(x =>
                context.Military.TryGet(x.UnitId, out var unit) && unit.OwnerSideId.Value == "player");
            onboarding.Observe(new FirstHourSignals(
                context.Selection.SelectedCharacterIds.Count > 0,
                context.Work.Jobs.Count > 0,
                activePanel == GameplayPanel.Economy,
                shelter,
                storage,
                TotalResource(berries) > 0,
                context.Production.GetProducedTotal(berries) > 0,
                researchStarted,
                dangerHandled,
                scoutingStarted));
        }

        private void RefreshOnboarding()
        {
            if (onboardingCard == null || onboardingToggleButton == null) return;
            onboardingToggleButton.text = T(onboarding.Visible ? "ui.onboarding.hide" : "ui.onboarding.show");
            onboardingCard.style.display = onboarding.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!onboarding.Visible) return;

            onboardingCard.Clear();
            var stage = onboarding.CurrentStage;
            AddTitle(onboardingCard, F("ui.onboarding.progress", onboarding.CompletedCount, onboarding.StepCount));
            AddMuted(onboardingCard, T("ui.onboarding.route"));
            AddSection(onboardingCard, T(OnboardingKey(stage, "title")));
            AddLine(onboardingCard, T(OnboardingKey(stage, "body")));
            var reason = OnboardingReason(stage);
            if (!string.IsNullOrEmpty(reason)) AddMuted(onboardingCard, F("ui.onboarding.reason", reason));
            var row = Row();
            if (stage != FirstHourStage.Complete)
                row.Add(ActionButton(T(OnboardingKey(stage, "action")), PerformOnboardingAction,
                    "onboarding-primary"));
            row.Add(ActionButton(T("ui.onboarding.hide"), () => SetOnboardingVisible(false), "onboarding-hide"));
            onboardingCard.Add(row);
        }

        private void PerformOnboardingAction()
        {
            switch (onboarding.CurrentStage)
            {
                case FirstHourStage.InspectGroup:
                    SetStatus(T("ui.onboarding.inspect.action_hint"));
                    break;
                case FirstHourStage.AssignWork:
                    OpenPanel(GameplayPanel.Work);
                    break;
                case FirstHourStage.InspectShelterAndStorage:
                case FirstHourStage.SecureFood:
                    OpenPanel(GameplayPanel.Economy);
                    break;
                case FirstHourStage.StartResearch:
                    OpenPanel(GameplayPanel.Research);
                    break;
                case FirstHourStage.ResolveEventOrDanger:
                    OpenPanel(GameplayPanel.Events);
                    break;
                case FirstHourStage.BeginScouting:
                    var playerUnit = context.Military.Units.FirstOrDefault(x => x.OwnerSideId.Value == "player");
                    OpenPanel(playerUnit == null || playerUnit.Representation != MilitaryUnitRepresentationMode.Aggregate
                        ? GameplayPanel.Military
                        : GameplayPanel.Global);
                    break;
            }
        }

        private void OpenPanel(GameplayPanel panel)
        {
            activePanel = panel;
            mainPanel.style.display = DisplayStyle.Flex;
            RefreshNavigationSelection();
            RefreshNow();
        }

        private string OnboardingReason(FirstHourStage stage)
        {
            switch (stage)
            {
                case FirstHourStage.InspectGroup:
                    return T("ui.onboarding.inspect.reason");
                case FirstHourStage.AssignWork:
                    if (context.Selection.SelectedCharacterIds.Count == 0) return T("ui.work.need_selection");
                    if (context.Work.Groups.Count == 0) return T("ui.onboarding.work.create_group");
                    return T("ui.onboarding.work.issue_job");
                case FirstHourStage.InspectShelterAndStorage:
                    return T("ui.onboarding.shelter.reason");
                case FirstHourStage.SecureFood:
                    if (TotalResource(new ResourceId("berries")) == 0) return T("ui.onboarding.food.empty");
                    return T("ui.onboarding.food.produce");
                case FirstHourStage.StartResearch:
                    return context.Selection.SelectedCharacterIds.Count == 0
                        ? T("ui.onboarding.research.select")
                        : T("ui.onboarding.research.start");
                case FirstHourStage.ResolveEventOrDanger:
                    return (context.Events?.PendingPlayerEvents.Count ?? 0) > 0
                        ? T("ui.onboarding.event.pending")
                        : T("ui.onboarding.event.wait");
                case FirstHourStage.BeginScouting:
                    var unit = context.Military.Units.FirstOrDefault(x => x.OwnerSideId.Value == "player");
                    if (unit == null) return T("ui.onboarding.scout.create_unit");
                    if (unit.Representation != MilitaryUnitRepresentationMode.Aggregate)
                        return T("ui.onboarding.scout.aggregate");
                    if (context.GlobalMap.Mode == GameWorldMode.Local) return T("ui.onboarding.scout.open_map");
                    return T("ui.onboarding.scout.travel");
                default:
                    return T("ui.onboarding.complete.reason");
            }
        }

        private static string OnboardingKey(FirstHourStage stage, string suffix)
        {
            string name;
            switch (stage)
            {
                case FirstHourStage.InspectGroup: name = "inspect"; break;
                case FirstHourStage.AssignWork: name = "work"; break;
                case FirstHourStage.InspectShelterAndStorage: name = "shelter"; break;
                case FirstHourStage.SecureFood: name = "food"; break;
                case FirstHourStage.StartResearch: name = "research"; break;
                case FirstHourStage.ResolveEventOrDanger: name = "event"; break;
                case FirstHourStage.BeginScouting: name = "scout"; break;
                default: name = "complete"; break;
            }
            return "ui.onboarding." + name + "." + suffix;
        }

        private void RefreshHud()
        {
            var clock = context.Clock();
            var alive = PlayerAliveCount();
            resourcesLabel.text = F("ui.hud.population", alive) + "   " + string.Join("   ", context.Resources.All
                .Where(x => x.Id.ToString() == "berries" || x.Id.ToString() == "wood_log" ||
                            x.Id.ToString() == "stone" || x.Id.ToString() == "plant_fiber" || x.Id.ToString() == "hide")
                .Select(x => Content("resource", x.Id.ToString()) + " " + TotalResource(x.Id)));
            var ticks = clock.State.CalendarTicks;
            dateLabel.text = F("ui.hud.date", GameCalendar.Year(ticks), GameCalendar.DayOfYear(ticks),
                GameCalendar.HourOfDay(ticks).ToString("00"), GameCalendar.MinuteOfHour(ticks).ToString("00"));
            var weather = context.Weather.GetWeatherAtCalendarTick(ticks);
            weatherLabel.text = E(weather.Season) + " · " + E(weather.Type) + " · " + RussianTextCatalog.Decimal(weather.TemperatureCelsius) + " °C";
            modeLabel.text = T(context.GlobalMap.Mode == GameWorldMode.Local ? "ui.hud.mode.local" : "ui.hud.mode.global");
            var pendingEvents = context.Events?.PendingPlayerEvents.Count ?? 0;
            alertLabel.text = F("ui.hud.alerts", notificationFeed.Count) +
                              (pendingEvents > 0 ? F("ui.hud.pending_events", pendingEvents) : string.Empty);
            var pauseButton = topBar.Q<Button>("pause-button");
            pauseButton.text = T(clock.IsPaused ? "ui.action.resume" : "ui.action.pause");
            SerenityUiDesign.SetSelected(pauseButton, clock.IsPaused);
            foreach (var speed in Speeds)
                SerenityUiDesign.SetSelected(topBar.Q<Button>("speed-" + (int)speed), speed == clock.Speed);
            RefreshNavigationSelection();
        }

        private void RefreshPanel()
        {
            if (mainPanel == null || activePanel == GameplayPanel.None) return;
            mainPanel.Clear();
            switch (activePanel)
            {
                case GameplayPanel.Build: DrawBuildPanel(); break;
                case GameplayPanel.Research: DrawResearchPanel(); break;
                case GameplayPanel.Work: DrawWorkPanel(); break;
                case GameplayPanel.Dynasty: DrawDynastyPanel(); break;
                case GameplayPanel.Military: DrawMilitaryPanel(); break;
                case GameplayPanel.SaveLoad: DrawSavePanel(); break;
                case GameplayPanel.Global: DrawGlobalPanel(); break;
                case GameplayPanel.Economy: DrawEconomyPanel(); break;
                case GameplayPanel.Events: DrawEventsPanel(); break;
            }
        }

        private void DrawEventsPanel()
        {
            AddTitle(mainPanel, T("ui.events.title"));
            if (context.Events == null)
            {
                AddMuted(mainPanel, T("ui.events.unavailable"));
                return;
            }
            var pending = context.Events.PendingPlayerEvents;
            AddSection(mainPanel, F("ui.events.pending", pending.Count));
            if (pending.Count == 0) AddMuted(mainPanel, T("ui.events.none_pending"));
            foreach (var instance in pending)
            {
                var definition = context.Events.Catalog.Get(instance.DefinitionId);
                var node = definition.Nodes.Single(x => x.Id == instance.CurrentNodeId);
                var captured = instance.InstanceId;
                var pendingButton = ActionButton((selectedEvent == instance.InstanceId ? T("common.selected_marker") : string.Empty) +
                    EventTitle(instance.DefinitionId, node.Id),
                    () => { selectedEvent = captured; RefreshPanel(); });
                pendingButton.AddToClassList("serenity-card-action");
                SerenityUiDesign.SetSelected(pendingButton, selectedEvent == instance.InstanceId);
                mainPanel.Add(pendingButton);
                AddMuted(mainPanel, string.Join(", ", instance.Targets.Select(context.Events.GetTargetDisplayName)));
            }
            if (!selectedEvent.HasValue || pending.All(x => x.InstanceId != selectedEvent.Value))
                selectedEvent = pending.Count == 0 ? (EventInstanceId?)null : pending[0].InstanceId;
            if (selectedEvent.HasValue)
            {
                var instance = pending.First(x => x.InstanceId == selectedEvent.Value);
                var definition = context.Events.Catalog.Get(instance.DefinitionId);
                var node = definition.Nodes.Single(x => x.Id == instance.CurrentNodeId);
                AddSection(mainPanel, EventTitle(instance.DefinitionId, node.Id));
                AddLine(mainPanel, EventDescription(instance.DefinitionId, node.Id));
                AddMuted(mainPanel, F("ui.events.involving", string.Join(", ", instance.Targets.Select(context.Events.GetTargetDisplayName))));
                foreach (var choice in node.Choices)
                {
                    var capturedChoice = choice.Id;
                    mainPanel.Add(ActionButton(EventChoice(instance.DefinitionId, node.Id, choice.Id), () => TryCommand(() =>
                    {
                        var result = context.Events.ResolvePlayerChoice(instance.InstanceId, capturedChoice,
                            context.Clock().State.CalendarTicks);
                        if (!result.Applied) throw PlayerError("ui.status.command_failed");
                        selectedEvent = null;
                        SetStatus(T("ui.events.choice_applied"));
                    })));
                }
            }
            AddSection(mainPanel, T("ui.events.history"));
            if (context.Events.History.Count == 0) AddMuted(mainPanel, T("ui.events.no_history"));
            var history = new Foldout { text = T("ui.events.history"), value = false, name = "event-history-foldout" };
            history.AddToClassList("serenity-foldout");
            foreach (var entry in context.Events.History.Reverse().Take(12))
            {
                var definition = context.Events.Catalog.Get(entry.DefinitionId);
                var title = EventTitle(entry.DefinitionId, definition.Nodes[0].Id);
                var card = SerenityUiDesign.Card();
                AddLine(card, F("ui.events.history_line", title, E(entry.Status), EventOutcome(entry.OutcomeCode)));
                AddMuted(card, string.Join(", ", entry.Targets.Select(context.Events.GetTargetDisplayName)));
                history.Add(card);
            }
            if (context.Events.History.Count > 0) mainPanel.Add(history);
        }

        private void DrawEconomyPanel()
        {
            AddTitle(mainPanel, T("ui.economy.title"));
            var shelterReady = context.Buildings.All.Any(x =>
                x.DefinitionId == new BuildingDefinitionId("primitive_shelter") && x.IsOperational);
            var storageReady = context.Buildings.All.Any(x =>
                x.DefinitionId == new BuildingDefinitionId("storage_basket") && x.IsOperational &&
                context.Inventories.TryGet(new InventoryOwner(InventoryOwnerKind.BuildingStorage, x.Id), out _));
            var playerIds = PlayerCharacterIds();
            var population = playerIds.Count(id => context.Characters.TryGet(id, out var person) &&
                                                   person.LifeState == CharacterLifeState.Alive);
            var berryUnits = TotalResource(new ResourceId("berries"));
            var foodDays = population == 0 ? 0d : berryUnits * context.Production.Settings.FoodTicksPerPersonUnit.TotalDays / population;
            AddSection(mainPanel, T("ui.economy.overview"));
            AddLine(mainPanel, F("ui.economy.ruler_population", CharacterName(context.Dynasty.CurrentMonarchId), population));
            AddLine(mainPanel, F("ui.economy.camp_ready", T(shelterReady ? "ui.economy.ready" : "ui.economy.missing"),
                T(storageReady ? "ui.economy.ready" : "ui.economy.missing")));
            AddLine(mainPanel, F("ui.economy.food_days", berryUnits, RussianTextCatalog.Decimal(foodDays, "0.#")));
            if (foodDays < 5d) AddMuted(mainPanel, T("ui.economy.food_warning"));
            AddSection(mainPanel, T("ui.economy.starting_group"));
            foreach (var person in playerIds.Select(id => context.Characters.TryGet(id, out var value) ? value : null)
                         .Where(x => x != null && x.LifeState == CharacterLifeState.Alive).Take(10))
                AddLine(mainPanel, F("ui.economy.person", RussianTextCatalog.PersonName(person.Name.DisplayName),
                    person.AgeInCompletedYears(context.Clock().State.BiologicalTicks), ProfessionName(person.ProfessionId?.ToString())));
            var storage = context.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage).ToArray();
            AddLine(mainPanel, F("ui.economy.storage", storage.Sum(x => x.TotalUnits), storage.Sum(x => x.Capacity)));
            foreach (var definition in context.Resources.All)
            {
                var target = context.Production.GetTarget(definition.Id);
                var total = context.Production.GetCanonicalTotal(definition.Id);
                var row = Row();
                SerenityUiDesign.StyleCard(row);
                row.Add(new Label(F("ui.economy.resource_line", Content("resource", definition.Id.ToString()),
                    context.StoredResources.GetStoredTotal(definition.Id).Units, total, target)));
                row.Add(ActionButton("-5", () => TryCommand(() =>
                    context.Production.SetTarget(definition.Id, Math.Max(0, target - 5)))));
                row.Add(ActionButton("+5", () => TryCommand(() =>
                    context.Production.SetTarget(definition.Id, checked(target + 5)))));
                mainPanel.Add(row);
            }
            AddLine(mainPanel, F("ui.economy.food", context.Production.Food.TotalConsumed, context.Production.Food.TotalShortage));
            var weather = context.Weather.GetWeatherAtCalendarTick(context.Clock().State.CalendarTicks);
            AddMuted(mainPanel, F("ui.economy.modifiers", Percent(weather.Modifiers.CropGrowthPerThousand),
                Percent(weather.Modifiers.OutdoorWorkPerThousand), Percent(weather.Modifiers.WildFoodGatheringPerThousand)));
            AddSection(mainPanel, T("ui.economy.sites"));
            foreach (var site in context.Production.Sites)
            {
                var status = site.Kind == ProductionSiteKind.PrimitiveCropPlot
                    ? F("ui.economy.crop", E(site.CropPhase), site.GrowthRequiredTicks == 0 ? string.Empty :
                        RussianTextCatalog.Percent((double)site.GrowthProgressTicks / site.GrowthRequiredTicks))
                    : F("ui.economy.source", site.RemainingAmount, site.SourceCapacity);
                if (context.Buildings.TryGet(site.Id, out var building) && !building.IsOperational)
                    status += " · " + F("ui.economy.disabled", E(building.StructuralStatus));
                AddLine(mainPanel, Content("recipe", site.RecipeId.ToString()) + " · " + status);
            }
            AddSection(mainPanel, T("ui.economy.active_production"));
            if (context.Production.ActiveTasks.Count == 0) AddMuted(mainPanel, T("ui.economy.no_tasks"));
            foreach (var task in context.Production.ActiveTasks.Take(12))
                AddLine(mainPanel, F("ui.economy.task", E(task.Kind), Content("recipe", task.RecipeId.ToString()),
                    RussianTextCatalog.Percent((double)task.AccumulatedWorkTicks / task.RequiredWorkTicks)));
        }

        private void DrawBuildPanel()
        {
            AddTitle(mainPanel, T("ui.build.title"));
            AddLine(mainPanel, T("ui.build.instructions"));
            foreach (var entry in context.BuildingCatalog.Entries.OrderBy(x => x.DefinitionId, StringComparer.Ordinal))
            {
                var definition = entry.CreateDefinition();
                var available = context.Research.IsBuildingUnlocked(definition.Id);
                var cost = definition.ConstructionCost.Count == 0 ? T("ui.build.no_cost") : F("ui.build.cost", string.Join(", ",
                    definition.ConstructionCost.Select(x => Content("resource", x.ResourceId.ToString()) + " " + x.Quantity.Units)));
                var shortage = BuildingResourceShortage(definition);
                var reason = !available ? BuildingLockReason(definition.Id) : shortage;
                var card = SerenityUiDesign.Card();
                AddSection(card, Content("building", definition.Id.ToString()));
                AddMuted(card, cost);
                var button = ActionButton(Content("building", definition.Id.ToString()) + " · " + cost,
                    () => StartBuilding(definition.Id));
                button.SetEnabled(available && string.IsNullOrEmpty(shortage));
                button.AddToClassList(SerenityUiDesign.PrimaryButtonClass);
                card.Add(button);
                if (!string.IsNullOrEmpty(reason)) AddMuted(card, reason);
                mainPanel.Add(card);
            }
            if (context.BuildingPlacement.IsActive)
                AddLine(mainPanel, F("ui.build.placement", Content("building", context.BuildingPlacement.ActiveDefinitionId.ToString()),
                    context.BuildingPlacement.IsPreviewValid ? T("ui.build.valid") : PlacementFailureText()));
        }

        private void DrawResearchPanel()
        {
            AddTitle(mainPanel, T("ui.research.title"));
            var active = context.Research.ActiveProject;
            if (active == null) AddLine(mainPanel, T("ui.research.active_none"));
            else
            {
                var definition = context.Research.Catalog.Get(active.TechnologyId);
                AddLine(mainPanel, F("ui.research.active", Content("technology", definition.Id.ToString()), RussianTextCatalog.Percent(active.Progress)));
                var status = ResearchWorkerStatusQuery.Capture(context.Research, context.AiAgents);
                AddLine(mainPanel, F("ui.research.workers", status.ActiveCount, status.AssignedCount));
                mainPanel.Add(ActionButton(T("ui.research.cancel"), () => TryCommand(() =>
                {
                    if (!context.Research.Cancel()) throw PlayerError("ui.research.no_active");
                })));
            }
            var workerRow = Row();
            var assign = ActionButton(T("ui.research.assign"), () => TryCommand(() =>
                SetStatus(F("ui.research.assigned_count", context.Research.AssignWorkers(context.Selection.SelectedCharacterIds)))));
            var remove = ActionButton(T("ui.research.remove"), () => TryCommand(() =>
                SetStatus(F("ui.research.removed_count", context.Research.UnassignWorkers(context.Selection.SelectedCharacterIds)))));
            assign.SetEnabled(context.Selection.SelectedCharacterIds.Count > 0);
            remove.SetEnabled(context.Selection.SelectedCharacterIds.Count > 0);
            workerRow.Add(assign);
            workerRow.Add(remove);
            mainPanel.Add(workerRow);
            if (context.Selection.SelectedCharacterIds.Count == 0) AddMuted(mainPanel, T("ui.research.need_selection"));
            var locked = new Foldout { text = T("enum.ResearchAvailability.Locked"), value = false,
                name = "locked-research-foldout" };
            locked.AddToClassList("serenity-foldout");
            foreach (var technology in context.Research.Catalog.All)
            {
                var stateKey = context.Research.IsCompleted(technology.Id) ? "enum.ResearchAvailability.Completed" :
                    active != null && active.TechnologyId == technology.Id ? "enum.ResearchAvailability.Active" :
                    context.Research.IsAvailable(technology.Id) ? "enum.ResearchAvailability.Available" : "enum.ResearchAvailability.Locked";
                var card = SerenityUiDesign.Card();
                AddSection(card, Content("technology", technology.Id.ToString()) + " · " + T(stateKey));
                AddMuted(card, F("ui.research.prerequisites", technology.Prerequisites.Count == 0 ? T("ui.research.no_prerequisites") :
                    string.Join(", ", technology.Prerequisites.Select(x => Content("technology", x.ToString())))));
                AddMuted(card, F("ui.research.work_unlocks", technology.ResearchWorkRequiredTicks,
                    technology.Unlocks.Count == 0 ? T("ui.research.capability") :
                    string.Join(", ", technology.Unlocks.Select(UnlockName))));
                if (stateKey == "enum.ResearchAvailability.Available" && active == null)
                {
                    var start = ActionButton(F("ui.research.start", Content("technology", technology.Id.ToString())),
                        () => TryCommand(() => context.Research.Start(technology.Id)));
                    start.AddToClassList(SerenityUiDesign.PrimaryButtonClass);
                    card.Add(start);
                }
                if (stateKey == "enum.ResearchAvailability.Locked") locked.Add(card);
                else mainPanel.Add(card);
            }
            if (locked.childCount > 1) mainPanel.Add(locked);
        }

        private void DrawWorkPanel()
        {
            AddTitle(mainPanel, T("ui.work.title"));
            var groups = context.Work.Groups.All;
            foreach (var group in groups)
            {
                var captured = group.Id;
                var button = ActionButton((selectedGroup == group.Id ? T("common.selected_marker") : string.Empty) + group.DisplayName +
                                          "  (" + group.MemberCount + ")", () =>
                {
                    selectedGroup = captured;
                    SelectGroupPresenters();
                    RefreshPanel();
                });
                button.AddToClassList("serenity-card-action");
                SerenityUiDesign.SetSelected(button, selectedGroup == group.Id);
                mainPanel.Add(button);
                AddMuted(mainPanel, group.Role + " · " + group.Members.Count(id => !context.Work.HasOpenJob(id)) +
                    "/" + group.MemberCount + " доступно");
            }
            var createGroup = ActionButton(T("ui.work.create"), () => TryCommand(() =>
            {
                if (context.Selection.SelectedCharacterIds.Count == 0) throw PlayerError("ui.work.need_selection");
                foreach (var id in context.Selection.SelectedCharacterIds)
                    if (context.Work.Groups.TryGetForMember(id, out var previous))
                        context.Work.RemoveMember(previous.Id, id);
                selectedGroup = context.Work.CreateGroup(F("ui.work.default_name", context.Work.Groups.Count + 1),
                    context.Selection.SelectedCharacterIds).Id;
            }));
            createGroup.SetEnabled(context.Selection.SelectedCharacterIds.Count > 0);
            mainPanel.Add(createGroup);
            if (context.Selection.SelectedCharacterIds.Count == 0) AddMuted(mainPanel, T("ui.work.need_selection"));
            if (!selectedGroup.HasValue || !context.Work.Groups.TryGet(selectedGroup.Value, out var current))
            {
                AddMuted(mainPanel, T("ui.work.select_group"));
                return;
            }
            AddSection(mainPanel, current.DisplayName);
            var diagnosticType = JobTypeForRole(current.Role);
            if (diagnosticType.HasValue)
                foreach (var candidate in context.DirectWork.GetCandidates(diagnosticType.Value, default))
                    AddMuted(mainPanel, CharacterName(candidate.CharacterId) + " · пригодность " +
                        candidate.Suitability + " · " + candidate.Reason);
            var rename = new TextField(T("ui.work.name")) { value = current.DisplayName, name = "work-group-name" };
            StyleTextField(rename);
            mainPanel.Add(rename);
            mainPanel.Add(ActionButton(T("ui.work.rename"), () => TryCommand(() => context.Work.RenameGroup(current.Id, rename.value))));
            var membership = Row();
            membership.Add(ActionButton(T("ui.work.add_selected"), () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds)
                    if (!current.Contains(id))
                    {
                        if (context.Work.Groups.TryGetForMember(id, out var previous))
                            context.Work.RemoveMember(previous.Id, id);
                        context.Work.AddMember(current.Id, id);
                    }
            })));
            membership.Add(ActionButton(T("ui.work.remove_selected"), () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds.ToArray()) context.Work.RemoveMember(current.Id, id);
            })));
            membership.Add(ActionButton(T("ui.work.commander_selected"), () => TryCommand(() =>
            {
                if (context.Selection.SelectedCharacterIds.Count != 1) throw PlayerError("ui.work.need_one_member");
                context.Work.SetCommander(current.Id, context.Selection.SelectedCharacterIds[0]);
            })));
            mainPanel.Add(membership);
            AddLine(mainPanel, F("ui.work.commander_priority", CharacterName(current.CommanderId), E(workPriority)));
            var priority = Row();
            foreach (var value in new[] { WorkPriority.Normal, WorkPriority.High, WorkPriority.Immediate })
            {
                var captured = value;
                var priorityButton = ActionButton(E(value), () => { workPriority = captured; RefreshPanel(); });
                SerenityUiDesign.SetSelected(priorityButton, workPriority == value);
                priority.Add(priorityButton);
            }
            mainPanel.Add(priority);
            var commands = Row();
            commands.Add(ActionButton(T("ui.work.move_center"), () => TryCommand(() =>
                context.ManualMoveCommands.IssueForGroup(current.Id, new WorldPosition(0f, 0f, 10f), workPriority))));
            commands.Add(ActionButton(T("ui.work.haul"), () => TryCommand(() => IssueGroupHaul(current.Id))));
            commands.Add(ActionButton(T("ui.work.cancel_jobs"), () => TryCommand(() => CancelGroupJobs(current.Id))));
            mainPanel.Add(commands);
            AddMuted(mainPanel, T("ui.work.help"));
            foreach (var job in context.Work.Jobs.Where(x => x.GroupId == current.Id).OrderByDescending(x => x.Id).Take(10))
            {
                AddLine(mainPanel, F("ui.work.job", job.Id, E(job.Type), E(job.Priority), E(job.Status), E(job.ClaimState)));
                if (!job.IsTerminal)
                {
                    var captured = job.Id;
                    var jobActions = Row();
                    jobActions.Add(ActionButton("Прямо сейчас", () => TryCommand(() => context.Work.SetPriority(captured, WorkPriority.Immediate))));
                    jobActions.Add(ActionButton("Отменить", () => TryCommand(() => context.Work.Cancel(captured))));
                    mainPanel.Add(jobActions);
                }
            }
        }

        private void DrawDynastyPanel()
        {
            AddTitle(mainPanel, context.Dynasty.Name);
            AddLine(mainPanel, F("ui.dynasty.status", E(context.Dynasty.Status)));
            AddLine(mainPanel, F("ui.dynasty.monarch", CharacterName(context.Dynasty.CurrentMonarchId)));
            var monarch = context.Dynasty.CurrentMonarchId.HasValue
                ? context.Characters.Get(context.Dynasty.CurrentMonarchId.Value) : null;
            AddLine(mainPanel, F("ui.dynasty.spouse", CharacterName(monarch?.SpouseId)));
            var children = monarch == null ? Array.Empty<Character>() : context.Family.GetChildren(monarch.Id).ToArray();
            var biologicalTick = context.Clock().State.BiologicalTicks;
            AddLine(mainPanel, F("ui.dynasty.children", children.Length == 0 ? T("common.none") : string.Join(", ", children.Select(x =>
            {
                var age = x.AgeInCompletedYears(biologicalTick);
                return F("ui.dynasty.child", RussianTextCatalog.PersonName(x.Name.DisplayName), age,
                    T(age >= Game.Simulation.Health.HealthAndFamilyService.AdultAgeYears
                        ? "ui.dynasty.adult" : "ui.dynasty.minor"));
            }))));
            var pregnancies = context.Health.Pregnancies.Where(x => x.Status == PregnancyStatus.Active &&
                context.Dynasty.Contains(x.MotherId)).OrderBy(x => x.DueCalendarTick).ToArray();
            AddLine(mainPanel, F("ui.dynasty.pregnancies", pregnancies.Length));
            foreach (var pregnancy in pregnancies.Take(4))
                AddMuted(mainPanel, F("ui.dynasty.pregnancy", CharacterName(pregnancy.MotherId),
                    FormatDuration(Math.Max(0, pregnancy.DueCalendarTick - context.Clock().State.CalendarTicks))));
            var heir = context.Dynasty.CurrentMonarchId.HasValue
                ? context.Family.GetOrderedSuccessionCandidates(context.Dynasty, context.Dynasty.CurrentMonarchId.Value).FirstOrDefault()
                : null;
            AddLine(mainPanel, F("ui.dynasty.heir", heir == null ? T("common.none") : RussianTextCatalog.PersonName(heir.Name.DisplayName)));
            AddSection(mainPanel, T("ui.dynasty.candidates"));
            if (context.Dynasty.CurrentMonarchId.HasValue)
                foreach (var candidate in context.Family.GetOrderedSuccessionCandidates(context.Dynasty,
                             context.Dynasty.CurrentMonarchId.Value).Take(8))
                    AddLine(mainPanel, F("ui.dynasty.candidate", RussianTextCatalog.PersonName(candidate.Name.DisplayName),
                        candidate.AgeInCompletedYears(context.Clock().State.BiologicalTicks)));
            AddSection(mainPanel, T("ui.dynasty.history"));
            foreach (var entry in context.Dynasty.History.Reverse().Take(6))
                AddLine(mainPanel, F("ui.dynasty.history_line", CharacterName(entry.PreviousMonarchId),
                    CharacterName(entry.NewMonarchId), E(entry.Reason)));
        }

        private void DrawMilitaryPanel()
        {
            AddTitle(mainPanel, T("ui.military.title"));
            var hasSelectedCharacters = context.Selection.SelectedCharacterIds.Count > 0;
            var hasCaptain = context.Selection.SelectedCharacterIds.Any(id => context.Health.GetCapabilities(id).CanFight);
            var createUnit = ActionButton(T("ui.military.create"), () => TryCommand(CreatePlayerUnit));
            createUnit.SetEnabled(hasSelectedCharacters && hasCaptain);
            mainPanel.Add(createUnit);
            if (!hasSelectedCharacters) AddMuted(mainPanel, T("ui.military.need_characters"));
            else if (!hasCaptain) AddMuted(mainPanel, T("ui.military.no_captain"));
            var index = 1;
            foreach (var unit in context.Military.Units)
            {
                var captured = unit.Id;
                var label = UnitLabel(unit, index++);
                mainPanel.Add(ActionButton((selectedUnit == unit.Id ? T("common.selected_marker") : string.Empty) + label,
                    () => { selectedUnit = captured; RefreshPanel(); }));
                var summary = context.Military.GetRosterSummary(unit.Id);
                AddMuted(mainPanel, F("ui.military.summary", CharacterName(unit.CaptainId), summary.Active,
                    summary.CombatCapable, summary.Injured, E(unit.Representation), E(unit.Order.Kind)));
            }
            if (!selectedUnit.HasValue || !context.Military.TryGet(selectedUnit.Value, out var selected)) return;
            AddSection(mainPanel, T("ui.military.actions"));
            AddLine(mainPanel, F("ui.military.supply", selected.Supply.Initialized
                ? selected.Supply.Get(context.Military.SupplySettings.FoodResourceId) + " " +
                  Content("resource", context.Military.SupplySettings.FoodResourceId.ToString())
                : T("ui.military.not_supplied")));
            mainPanel.Add(ActionButton(T("ui.military.resupply"), () => TryCommand(() =>
                context.Military.LoadInitialSupply(selected.Id, context.Inventories,
                    context.Inventories.All.Select(x => x.Owner)))));
            var roster = Row();
            roster.Add(ActionButton(T("ui.military.add_selected"), () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds) context.Military.AddMember(selected.Id, id);
            })));
            roster.Add(ActionButton(T("ui.military.remove_selected"), () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds) context.Military.RemoveMember(selected.Id, id);
            })));
            roster.Add(ActionButton(T("ui.military.captain_selected"), () => TryCommand(() =>
            {
                if (context.Selection.SelectedCharacterIds.Count != 1) throw PlayerError("ui.military.need_one_member");
                var result = context.Military.AssignCaptain(selected.Id, context.Selection.SelectedCharacterIds[0]);
                if (result != CommanderAssignmentStatus.Assigned) throw PlayerError("ui.military.need_one_member");
            })));
            mainPanel.Add(roster);
            var orders = Row();
            orders.Add(ActionButton(T("ui.military.order.idle"), () => TryCommand(() => context.Military.SetOrder(selected.Id, MilitaryUnitOrderKind.Idle))));
            orders.Add(ActionButton(T("ui.military.order.move"), () => TryCommand(() => context.MilitaryCommands.Move(selected.Id, new WorldPosition(0, 0, 10)))));
            orders.Add(ActionButton(T("ui.military.order.hold"), () => TryCommand(() => context.MilitaryCommands.Hold(selected.Id))));
            orders.Add(ActionButton(T("ui.military.order.engage"), () => TryCommand(() => context.Military.SetOrder(selected.Id, MilitaryUnitOrderKind.Engage))));
            mainPanel.Add(orders);
            var representations = Row();
            representations.Add(ActionButton(T("ui.military.detailed"), () => TryCommand(() => context.MilitaryRepresentations.Transition(selected.Id, MilitaryUnitRepresentationMode.Detailed))));
            representations.Add(ActionButton(T("ui.military.aggregate"), () => TryCommand(() => context.MilitaryRepresentations.Transition(selected.Id, MilitaryUnitRepresentationMode.Aggregate))));
            mainPanel.Add(representations);
            AddSection(mainPanel, T("ui.military.battles"));
            foreach (var battle in context.Battles.Battles.Where(x => x.Status == CombatEncounterStatus.Active))
            {
                AddLine(mainPanel, F("ui.military.battle", E(battle.Mode), BattleCounts(battle),
                    SideMorale(battle, battle.SideA), SideMorale(battle, battle.SideB)));
                if (battle.Mode == BattleMode.Abstract)
                    mainPanel.Add(ActionButton(T("ui.military.enter_battle"), () => context.RequestEnterBattle(battle.BattleId)));
            }
        }

        private void DrawSavePanel()
        {
            AddTitle(mainPanel, T("ui.save.title"));
            AddLine(mainPanel, T("ui.save.explanation"));
            if (context.StartNewGame != null)
            {
                AddSection(mainPanel, T("ui.save.new_game"));
                var seed = new TextField(T("ui.save.world_seed")) { value = context.Campaign?.State.WorldSeed.ToString() ?? "20261006" };
                seed.name = "new-game-seed";
                StyleTextField(seed);
                mainPanel.Add(seed);
                mainPanel.Add(ActionButton(T("ui.save.start"), () =>
                {
                    if (!long.TryParse(seed.value, out var parsed))
                        throw PlayerError("ui.save.seed_integer");
                    RequestNewGame(parsed);
                }, "new-game-button"));
            }
            foreach (var slot in context.Saves.ListSlots())
            {
                var card = SerenityUiDesign.Card("save-slot-" + slot.Index);
                AddSection(card, F("ui.save.slot", slot.Index));
                AddLine(card, slot.Exists ? slot.Detail : T("ui.save.empty_slot"));
                var row = Row();
                row.Add(ActionButton(T("common.save"), () => SaveSlot(slot.Index)));
                var load = ActionButton(T("common.load"), () => RequestLoadSlot(slot.Index));
                load.SetEnabled(slot.Exists && !slot.IsCorrupt);
                row.Add(load);
                card.Add(row);
                if (slot.IsCorrupt) AddMuted(card, T("ui.save.corrupt_slot"));
                mainPanel.Add(card);
            }
        }

        private void DrawGlobalPanel()
        {
            AddTitle(mainPanel, T("ui.map.title"));
            AddLine(mainPanel, F("ui.map.mode_location", E(context.GlobalMap.Mode),
                LocationName(context.GlobalMap.ActiveLocationId)));
            if (context.GlobalMap.Occupations.Count > 0)
            {
                AddSection(mainPanel, T("ui.map.occupations"));
                foreach (var occupation in context.GlobalMap.Occupations)
                {
                    var remaining = Math.Max(0, occupation.CompletionTick - context.Clock().State.CalendarTicks);
                    AddLine(mainPanel, F("ui.map.occupation", LocationName(occupation.LocationId),
                        SideName(occupation.ClaimantSideId),
                        RussianTextCatalog.Percent((double)occupation.ProgressTicks / occupation.RequiredDurationTicks),
                        FormatDuration(remaining)));
                }
                AddMuted(mainPanel, T("ui.map.occupation_help"));
            }
            if (context.GlobalMap.Mode == GameWorldMode.Local)
            {
                mainPanel.Add(ActionButton(T("ui.map.open"), context.RequestEnterGlobal));
                AddMuted(mainPanel, T("ui.map.local_continues"));
                return;
            }
            AddSection(mainPanel, T("ui.map.locations"));
            foreach (var location in context.GlobalMap.Locations)
            {
                var captured = location.Id;
                var settlement = context.GlobalMap.GetSettlementAt(location.Id);
                var hidden = settlement != null && context.Campaign != null &&
                             !context.Campaign.CanRevealSettlement(settlement.Id);
                var owner = settlement == null ? T("ui.map.unclaimed") : SideName(settlement.OwnerSideId);
                var locationButton = ActionButton((selectedLocation == location.Id ? T("common.selected_marker") : string.Empty) +
                    (hidden ? T("ui.map.hidden") :
                        F("ui.map.location", LocationName(location.Id), E(location.Type), owner)),
                    () => { selectedLocation = captured; RefreshPanel(); });
                locationButton.AddToClassList("serenity-card-action");
                SerenityUiDesign.SetSelected(locationButton, selectedLocation == location.Id);
                mainPanel.Add(locationButton);
            }
            AddSection(mainPanel, T("ui.map.player_armies"));
            var index = 1;
            foreach (var unit in context.Military.Units.Where(x => x.OwnerSideId.Value == "player" &&
                         x.Representation == MilitaryUnitRepresentationMode.Aggregate))
            {
                var captured = unit.Id;
                var travel = context.GlobalMap.Travels.FirstOrDefault(x => x.UnitId == unit.Id);
                var detail = travel == null ? LocationName(unit.AbstractLocationId) :
                    F("ui.map.travel_eta", LocationName(travel.Origin), LocationName(travel.Destination),
                        FormatDuration(Math.Max(0, travel.ArrivalTick - context.Clock().State.CalendarTicks)));
                var unitButton = ActionButton((selectedUnit == unit.Id ? T("common.selected_marker") : string.Empty) + UnitLabel(unit, index++) + " · " + detail,
                    () => { selectedUnit = captured; RefreshPanel(); });
                unitButton.AddToClassList("serenity-card-action");
                SerenityUiDesign.SetSelected(unitButton, selectedUnit == unit.Id);
                mainPanel.Add(unitButton);
            }
            var actions = Row();
            var travelButton = ActionButton(T("ui.map.travel"), () => TryCommand(() =>
            {
                if (!selectedUnit.HasValue || !selectedLocation.HasValue)
                    throw PlayerError("ui.map.need_unit_destination");
                var result = context.GlobalMap.IssueTravelOrder(selectedUnit.Value, selectedLocation.Value,
                    context.Clock().State.CalendarTicks);
                if (!result.Issued) throw new PlayerFacingException(TravelReason(result.Status));
                AddNotification(F("ui.map.departed", LocationName(selectedLocation.Value)));
            }));
            travelButton.SetEnabled(selectedUnit.HasValue && selectedLocation.HasValue);
            actions.Add(travelButton);
            actions.Add(ActionButton(T("ui.map.enter_location"), () => TryCommand(() =>
            {
                if (!selectedLocation.HasValue) throw PlayerError("ui.map.need_location");
                context.RequestEnterLocation(selectedLocation.Value);
            })));
            mainPanel.Add(actions);
            if (!selectedUnit.HasValue || !selectedLocation.HasValue)
                AddMuted(mainPanel, T("ui.map.need_unit_destination"));
            AddSection(mainPanel, T("ui.map.battles"));
            foreach (var battle in context.Battles.Battles.Where(x => x.Status == CombatEncounterStatus.Active))
            {
                AddLine(mainPanel, F("ui.map.battle", E(battle.Mode), BattleCounts(battle), E(battle.Outcome)));
                if (battle.Mode == BattleMode.Abstract)
                    mainPanel.Add(ActionButton(T("ui.map.enter_battle"), () => context.RequestEnterBattle(battle.BattleId)));
            }
        }

        private void RefreshInspector()
        {
            var hasSelection = context.Selection.SelectedCharacterIds.Count > 0 ||
                               context.Selection.SelectedBuildingPresenter != null ||
                               context.Selection.SelectedWorldResourcePilePresenter != null ||
                               context.Selection.SelectedProductionSourcePresenter != null;
            if (!hasSelection && emptyInspectorRendered) return;
            emptyInspectorRendered = !hasSelection;
            inspector.Clear();
            AddTitle(inspector, T("ui.selection.title"));
            if (context.Selection.SelectedCharacterIds.Count > 1)
            {
                AddLine(inspector, F("ui.selection.characters", context.Selection.SelectedCharacterIds.Count));
                AddMuted(inspector, T("ui.selection.many_help"));
                return;
            }
            if (context.Selection.SelectedCharacterId.HasValue)
            {
                DrawCharacterInspector(context.Selection.SelectedCharacterId.Value);
                return;
            }
            var buildingPresenter = context.Selection.SelectedBuildingPresenter;
            if (buildingPresenter != null && context.Buildings.TryGet(buildingPresenter.BuildingId, out var building))
            {
                DrawBuildingInspector(building);
                return;
            }
            var pile = context.Selection.SelectedWorldResourcePilePresenter;
            if (pile != null)
            {
                var inventory = context.Inventories.Get(new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId));
                AddLine(inspector, T("ui.selection.resource_pile"));
                AddLine(inspector, Content("resource", pile.ResourceId.ToString()) + ": " +
                    inventory.GetAmount(pile.ResourceId).Units);
                DrawDirectPriority();
                inspector.Add(ActionButton("Перенести", () => TryCommand(() => IssueSelectedPileHaul(pile))));
                return;
            }
            var source = context.Selection.SelectedProductionSourcePresenter;
            if (source != null && source.IsBound)
            {
                var recipe = context.Production.Recipes.Get(source.Site.RecipeId);
                AddLine(inspector, T("ui.selection.natural_source"));
                AddLine(inspector, Content("recipe", recipe.Id.ToString()));
                AddLine(inspector, F("ui.selection.source_remaining", source.RemainingAmount, source.SourceCapacity));
                AddLine(inspector, Content("resource", recipe.Outputs[0].ResourceId.ToString()));
                DrawDirectPriority();
                inspector.Add(ActionButton(source.Site.Kind == ProductionSiteKind.NaturalSource ? "Собирать" : "Обрабатывать",
                    () => TryCommand(() => IssueDirect(source.Site.Kind == ProductionSiteKind.NaturalSource
                        ? WorkJobType.Gather : WorkJobType.Farm,
                        WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position)))));
                inspector.Add(ActionButton(T("ui.selection.open_economy"), () => SetPanel(GameplayPanel.Economy)));
                return;
            }
            AddLine(inspector, T("ui.selection.nothing"));
            AddMuted(inspector, T("ui.selection.help"));
        }

        private void DrawCharacterInspector(StableEntityId id)
        {
            var character = context.Characters.Get(id);
            var capabilities = context.Health.GetCapabilities(id);
            AddSection(inspector, RussianTextCatalog.PersonName(character.Name.DisplayName));
            AddLine(inspector, F("ui.character.age", character.AgeInCompletedYears(context.Clock().State.BiologicalTicks),
                E(character.Sex), E(character.LifeState)));
            AddLine(inspector, F("ui.character.profession", ProfessionName(character.ProfessionId?.ToString())));
            AddLine(inspector, F("ui.character.health", capabilities.IsIncapacitated ? T("ui.character.incapacitated") : T("common.active"),
                Percent(capabilities.WorkPerThousand), Percent(capabilities.ResearchPerThousand)));
            if (context.AiAgents.TryGet(id, out var agent))
                AddLine(inspector, F("ui.character.needs", RussianTextCatalog.Percent(agent.Needs.Hunger),
                    RussianTextCatalog.Percent(agent.Needs.Energy), E(agent.CurrentAction), E(agent.Phase)));
            else AddMuted(inspector, T("ui.character.offcamera"));
            var conditions = context.Health.Conditions.Where(x => x.TargetId == id &&
                x.State != HealthConditionState.Recovered).ToArray();
            AddLine(inspector, F("ui.character.conditions", conditions.Length == 0 ? T("common.none") : string.Join(", ", conditions.Select(x =>
                Content("condition", x.DefinitionId.ToString()) + " · " + E(x.Severity)))));
            var pregnancy = context.Health.Pregnancies.FirstOrDefault(x => x.MotherId == id && x.Status == PregnancyStatus.Active);
            if (pregnancy != null) AddLine(inspector, T("ui.character.pregnancy"));
            var inventoryOwner = new InventoryOwner(InventoryOwnerKind.Character, id);
            AddLine(inspector, F("ui.character.inventory", context.Inventories.TryGet(inventoryOwner, out var inventory)
                ? InventoryText(inventory) : T("common.none")));
            var research = context.Research.AssignedWorkers.Contains(id) ? T("ui.character.research_assigned") : T("ui.character.research_not_assigned");
            AddLine(inspector, F("ui.character.research", research));
            AddLine(inspector, F("ui.character.work_group", context.Work.Groups.TryGetForMember(id, out var group) ? group.DisplayName : T("common.none")));
            AddLine(inspector, F("ui.character.current_job", context.Work.TryGetAssigned(id, out var job)
                ? E(job.Type) + " · " + E(job.Status) : T("common.none")));
            AddLine(inspector, F("ui.character.dynasty_role", DynastyRole(id)));
            AddLine(inspector, F("ui.character.military", context.Military.TryGetForMember(id, out var unit)
                ? T("ui.character.member") + (unit.CaptainId == id ? ", " + T("ui.character.captain") : string.Empty)
                : T("ui.character.civilian")));
            if (context.Events != null)
            {
                var eventHistory = context.Events.History.Where(x => x.Targets.Any(target =>
                    target.Kind == EventTargetKind.Character && target.TryGetStableEntityId(out var targetId) && targetId == id))
                    .Reverse().Take(3).ToArray();
                AddLine(inspector, F("ui.character.events", eventHistory.Length == 0 ? T("common.none") : string.Join(", ",
                    eventHistory.Select(x => EventTitle(x.DefinitionId, context.Events.Catalog.Get(x.DefinitionId).Nodes[0].Id)))));
            }
            var eligibility = context.PlayerControl.Evaluate(id);
            var actions = Row();
            actions.Add(ActionButton(T(context.Research.AssignedWorkers.Contains(id) ? "ui.character.remove_research" : "ui.character.assign_research"), () => TryCommand(() =>
            {
                if (context.Research.AssignedWorkers.Contains(id)) context.Research.UnassignWorkers(new[] { id });
                else context.Research.AssignWorkers(new[] { id });
            })));
            var tps = ActionButton(T("ui.character.enter_tps"), () => TryCommand(() =>
            {
                var result = context.ThirdPerson.TryEnter(id);
                if (!result.IsEligible) throw new PlayerFacingException(EligibilityReason(result.Failure));
            }));
            tps.SetEnabled(eligibility.IsEligible);
            actions.Add(tps);
            inspector.Add(actions);
            DrawDirectPriority();
            var direct = Row();
            var targetPosition = context.AiAgents.TryGet(id, out var selectedAgent) ? selectedAgent.Position : default;
            direct.Add(ActionButton("Уход", () => TryCommand(() => IssueDirect(WorkJobType.Care,
                WorkTarget.ForEntity(WorkTargetKind.Character, id, targetPosition)))));
            direct.Add(ActionButton("Исследовать", () => TryCommand(() => IssueDirect(WorkJobType.Research,
                WorkTarget.ForEntity(WorkTargetKind.Research, id, targetPosition)))));
            direct.Add(ActionButton("Разведать", () => TryCommand(() => IssueDirect(WorkJobType.Scout,
                WorkTarget.ForEntity(WorkTargetKind.Character, id, targetPosition)))));
            direct.Add(ActionButton("Военная задача", () => TryCommand(() => IssueDirect(WorkJobType.Military,
                WorkTarget.ForEntity(WorkTargetKind.Character, id, targetPosition)))));
            inspector.Add(direct);
            if (!eligibility.IsEligible) AddMuted(inspector, F("ui.character.tps_unavailable", EligibilityReason(eligibility.Failure)));
            if (DeveloperUiVisible) AddMuted(inspector, F("ui.character.stable_id", id));
        }

        private void DrawBuildingInspector(Building building)
        {
            var definition = context.BuildingCatalog.ResolveDefinition(building.DefinitionId);
            AddSection(inspector, Content("building", definition.Id.ToString()));
            AddLine(inspector, F("ui.building.owner", SideName(building.OwnerSideId), E(building.ConstructionState)));
            AddLine(inspector, F("ui.building.integrity", building.CurrentIntegrity, building.MaxIntegrity, E(building.StructuralStatus)));
            AddLine(inspector, F("ui.building.fire", E(building.FireStatus)) + (building.FireStatus == BuildingFireStatus.Burning
                ? F("ui.building.fire_intensity", building.FireIntensity) : string.Empty));
            var owner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id);
            if (context.Inventories.TryGet(owner, out var storage))
                AddLine(inspector, F("ui.building.storage", storage.TotalUnits, storage.Capacity, InventoryText(storage)));
            if (context.Production.Sites.FirstOrDefault(x => x.Id == building.Id) is ProductionSite site)
            {
                AddLine(inspector, F("ui.building.production", building.IsOperational ? T("ui.building.operational") : T("common.unavailable")));
                if (site.Kind == ProductionSiteKind.PrimitiveCropPlot)
                    AddLine(inspector, F("ui.building.crop", E(site.CropPhase),
                        site.GrowthRequiredTicks == 0 ? "—" : RussianTextCatalog.Percent((double)site.GrowthProgressTicks / site.GrowthRequiredTicks)));
            }
            DrawDirectPriority();
            var target = WorkTarget.ForEntity(WorkTargetKind.Building, building.Id,
                new WorldPosition(building.Coordinate.X, building.Coordinate.Level, building.Coordinate.Z));
            inspector.Add(ActionButton(building.IsOperational ? "Изготавливать" : "Строить",
                () => TryCommand(() => IssueDirect(building.IsOperational ? WorkJobType.Craft : WorkJobType.Build, target))));
            if (DeveloperUiVisible) AddMuted(inspector, F("ui.character.stable_id", building.Id));
        }

        private void RefreshNotifications()
        {
            if (renderedNotificationVersion == notificationVersion) return;
            renderedNotificationVersion = notificationVersion;
            notifications.Clear();
            AddSection(notifications, T("ui.notifications.title"));
            if (notificationFeed.Count == 0) AddMuted(notifications, T("ui.notifications.none"));
            foreach (var message in notificationFeed.Skip(Math.Max(0, notificationFeed.Count - 5)).Reverse())
                AddLine(notifications, message);
        }

        private void CaptureNotifications()
        {
            var researchCount = context.Research.CompletedTechnologies.Count;
            var characterCount = PlayerCharacterIds().Count;
            var aliveCount = PlayerAliveCount();
            var dynastyCount = context.Dynasty.History.Count;
            var conditionCount = context.Health.Conditions.Count;
            var buildingHistory = context.Buildings.All.Sum(x => x.History.Count);
            var battleEvents = context.Battles.Battles.Sum(x => x.BattleHistory.Count);
            var travelCount = context.GlobalMap.Travels.Count;
            var shortage = context.Production.Food.TotalShortage;
            var eventHistoryCount = context.Events?.History.Count ?? 0;
            var pendingEventCount = context.Events?.PendingPlayerEvents.Count ?? 0;
            var campaignSequence = context.Campaign?.State.EventSequence ?? 0UL;
            var occupationSequence = context.GlobalMap.OccupationEventSequence;
            var adultCount = PlayerCharacterIds().Count(id => context.Characters.Get(id)
                .AgeInCompletedYears(context.Clock().State.BiologicalTicks) >=
                Game.Simulation.Health.HealthAndFamilyService.AdultAgeYears);
            if (notificationsPrimed && !suppressNotificationCaptureOnce)
            {
                if (researchCount > knownResearchCount) AddNotification(T("ui.notification.research"));
                if (characterCount > knownCharacterCount) AddNotification(T("ui.notification.birth"));
                if (aliveCount < knownAliveCount) AddNotification(T("ui.notification.death"));
                if (dynastyCount > knownDynastyHistoryCount) AddNotification(T("ui.notification.succession"));
                if (conditionCount > knownConditionCount) AddNotification(T("ui.notification.condition"));
                if (buildingHistory > knownBuildingHistoryCount)
                {
                    var change = context.Buildings.All.SelectMany(x => x.History).OrderByDescending(x => x.CalendarTick)
                        .ThenByDescending(x => x.Sequence).First();
                    AddNotification(F("ui.notification.building", E(change.Kind)));
                }
                if (battleEvents > knownBattleEventCount)
                {
                    var change = context.Battles.Battles.SelectMany(x => x.BattleHistory)
                        .OrderByDescending(x => x.CalendarTick).ThenByDescending(x => x.Sequence).First();
                    AddNotification(T(change.Kind == BattleEventKind.Started ? "ui.notification.battle_started" :
                        change.Kind == BattleEventKind.Finished ? "ui.notification.battle_finished" : "ui.notification.battle_changed"));
                }
                if (travelCount < knownTravelCount) AddNotification(T("ui.notification.arrival"));
                if (shortage > knownFoodShortage) AddNotification(T("ui.notification.shortage"));
                if (pendingEventCount > knownPendingEventCount) AddNotification(T("ui.notification.event"));
                if (eventHistoryCount > knownEventHistoryCount)
                {
                    var entry = context.Events.History.Last();
                    AddNotification(F("ui.notification.event_resolved", EventTitle(entry.DefinitionId,
                        context.Events.Catalog.Get(entry.DefinitionId).Nodes[0].Id)));
                }
                if (campaignSequence > knownCampaignEventSequence && context.Campaign != null)
                {
                    if (context.Campaign.State.MilitaryVictory)
                        AddNotification(T("ui.notification.victory"));
                    else if (context.Campaign.State.RivalDiscovered)
                        AddNotification(T("ui.notification.discovery"));
                }
                if (occupationSequence > knownOccupationEventSequence)
                {
                    var change = context.GlobalMap.OccupationEvents.Last();
                    var key = change.Kind == SettlementOccupationEventKind.Started
                        ? "ui.notification.occupation_started"
                        : change.Kind == SettlementOccupationEventKind.Completed
                            ? "ui.notification.occupation_completed"
                            : "ui.notification.occupation_interrupted";
                    AddNotification(F(key, SideName(change.ClaimantSideId)));
                }
                if (adultCount > knownAdultCount) AddNotification(T("ui.notification.adulthood"));
            }
            suppressNotificationCaptureOnce = false;
            notificationsPrimed = true;
            knownResearchCount = researchCount;
            knownCharacterCount = characterCount;
            knownAliveCount = aliveCount;
            knownDynastyHistoryCount = dynastyCount;
            knownConditionCount = conditionCount;
            knownBuildingHistoryCount = buildingHistory;
            knownBattleEventCount = battleEvents;
            knownTravelCount = travelCount;
            knownFoodShortage = shortage;
            knownEventHistoryCount = eventHistoryCount;
            knownPendingEventCount = pendingEventCount;
            knownCampaignEventSequence = campaignSequence;
            knownOccupationEventSequence = occupationSequence;
            knownAdultCount = adultCount;
        }

        private void RefreshTps()
        {
            if (context.PlayerControl.State.Mode != PlayerControlMode.ThirdPerson)
            {
                tpsHud.style.display = DisplayStyle.None;
                return;
            }
            tpsHud.style.display = DisplayStyle.Flex;
            var id = context.PlayerControl.State.ControlledCharacterId.Value;
            var character = context.Characters.Get(id);
            var capabilities = context.Health.GetCapabilities(id);
            var combat = context.Combat.Encounters.Any(x => x.Status == CombatEncounterStatus.Active &&
                x.Combatants.Any(c => c.CharacterId == id));
            tpsLabel.text = F("ui.tps.hud", RussianTextCatalog.PersonName(character.Name.DisplayName),
                capabilities.IsIncapacitated ? T("ui.character.incapacitated") : T("ui.tps.health_active"),
                combat ? T("ui.tps.combat_engaged") : T("ui.tps.combat_clear"));
        }

        private void RefreshGameOver()
        {
            if (context.GameRun.Status == GameRunStatus.GameOver)
            {
                gameOverLabel.text = F("ui.game_over.defeat", E(context.GameRun.GameOverReason));
                gameOverLabel.style.display = DisplayStyle.Flex;
                return;
            }
            if (context.Campaign?.State.MilitaryVictory == true)
            {
                gameOverLabel.text = T("ui.game_over.victory");
                gameOverLabel.style.display = DisplayStyle.Flex;
                return;
            }
            gameOverLabel.style.display = DisplayStyle.None;
        }

        private void HandleWorkGroupHotkeys(Keyboard keyboard)
        {
            if (DeveloperUiVisible || keyboard == null || context.Work.Groups.Count == 0 || IsTextInputFocused()) return;
            var keys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key,
                keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key };
            for (var i = 0; i < keys.Length; i++)
            {
                if (!keys[i].wasPressedThisFrame) continue;
                var group = context.Work.Groups.All.ElementAtOrDefault(i);
                if (group == null) return;
                selectedGroup = group.Id;
                SelectGroupPresenters();
                SetStatus(F("ui.work.selected_shortcut", group.DisplayName, i + 1));
            }
        }

        private void StartBuilding(BuildingDefinitionId id)
        {
            TryCommand(() =>
            {
                if (!context.BuildingPlacement.TryStartPlacement(id))
                    throw new InvalidOperationException(PlacementFailureText());
                activePanel = GameplayPanel.None;
                mainPanel.style.display = DisplayStyle.None;
                SetStatus(T("ui.build.started"));
            });
        }

        private void SelectGroupPresenters()
        {
            if (!selectedGroup.HasValue || !context.Work.Groups.TryGet(selectedGroup.Value, out var group)) return;
            context.Selection.SelectCharacters(group.Members.Select(id => context.Presentations.TryGet(id, out var presenter)
                ? presenter : null).Where(x => x != null));
        }

        private void IssueGroupHaul(WorkGroupId groupId)
        {
            var source = context.WorldPiles.All.OrderBy(x => x.Id).FirstOrDefault(x =>
                context.Inventories.Get(x.InventoryOwner).GetAmount(x.ResourceId).Units > 0);
            var destination = context.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage &&
                    x.Capacity > x.TotalUnits).OrderBy(x => x.Owner.Id).FirstOrDefault();
            if (source == null || destination == null) throw PlayerError("ui.work.no_haul_route");
            context.Work.CreateGroupHaulJobs(groupId, source.InventoryOwner, destination.Owner, source.ResourceId,
                new ResourceQuantity(1), workPriority);
        }

        private void DrawDirectPriority()
        {
            AddMuted(inspector, "Приоритет: " + (workPriority == WorkPriority.Immediate ? "Прямо сейчас" : E(workPriority)));
            var row = Row();
            foreach (var value in new[] { WorkPriority.Normal, WorkPriority.High, WorkPriority.Immediate })
            {
                var captured = value;
                var button = ActionButton(value == WorkPriority.Immediate ? "Прямо сейчас" : E(value),
                    () => { workPriority = captured; RefreshInspector(); });
                SerenityUiDesign.SetSelected(button, workPriority == value);
                row.Add(button);
            }
            inspector.Add(row);
        }

        private static WorkJobType? JobTypeForRole(WorkGroupRole role)
        {
            switch (role)
            {
                case WorkGroupRole.Gatherers: return WorkJobType.Gather;
                case WorkGroupRole.Builders: return WorkJobType.Build;
                case WorkGroupRole.Haulers: return WorkJobType.Haul;
                case WorkGroupRole.Crafters: return WorkJobType.Craft;
                case WorkGroupRole.FarmersGatherers: return WorkJobType.Farm;
                case WorkGroupRole.Scientists: return WorkJobType.Research;
                case WorkGroupRole.Scouts: return WorkJobType.Scout;
                case WorkGroupRole.Warriors: return WorkJobType.Military;
                case WorkGroupRole.Caregivers: return WorkJobType.Care;
                default: return null;
            }
        }

        private void IssueDirect(WorkJobType type, WorkTarget target)
        {
            var job = context.DirectWork.Issue(type, target, workPriority);
            SetStatus("Задание #" + job.Id + ": " + E(type));
        }

        private void IssueSelectedPileHaul(WorldResourcePilePresenter pile)
        {
            var destination = context.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage &&
                    x.Capacity > x.TotalUnits).OrderBy(x => x.Owner.Id).FirstOrDefault();
            if (destination == null) throw PlayerError("ui.work.no_haul_route");
            var position = pile.transform.position;
            var job = context.DirectWork.IssueHaul(new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId),
                destination.Owner, pile.ResourceId, new ResourceQuantity(1),
                new WorldPosition(position.x, position.y, position.z), workPriority);
            SetStatus("Задание #" + job.Id + ": переноска");
        }

        private void CancelGroupJobs(WorkGroupId groupId)
        {
            foreach (var job in context.Work.Jobs.Where(x => x.GroupId == groupId && !x.IsTerminal).ToArray())
                context.Work.Cancel(job.Id);
        }

        private void CreatePlayerUnit()
        {
            var roster = context.Selection.SelectedCharacterIds.ToArray();
            if (roster.Length == 0) throw PlayerError("ui.military.need_characters");
            var captain = roster.FirstOrDefault(id => context.Health.GetCapabilities(id).CanFight);
            if (!captain.IsValid) throw PlayerError("ui.military.no_captain");
            var unit = context.Military.Create(new MilitarySideId("player"), roster, captain);
            context.Military.SetAbstractLocation(unit.Id, context.GlobalMap.ActiveLocationId.Value);
            context.Military.LoadInitialSupply(unit.Id, context.Inventories,
                context.Inventories.All.Select(x => x.Owner));
            selectedUnit = unit.Id;
        }

        private void StepSpeed(int direction)
        {
            var current = context.Clock().Speed;
            var index = Array.IndexOf(Speeds, current);
            context.SetSpeed(Speeds[Mathf.Clamp(index + direction, 0, Speeds.Length - 1)]);
        }

        private long TotalResource(ResourceId id)
        {
            long total = 0;
            foreach (var inventory in context.Inventories.All) total = checked(total + inventory.GetAmount(id).Units);
            return total;
        }

        private IReadOnlyList<StableEntityId> PlayerCharacterIds()
        {
            var configured = context.PlayerCharacterIds();
            return configured.Count > 0 ? configured : context.Characters.GetAll().Select(x => x.Id).ToArray();
        }

        private int PlayerAliveCount() => PlayerCharacterIds().Count(id =>
            context.Characters.TryGet(id, out var character) && character.LifeState == CharacterLifeState.Alive);

        private string BuildingLockReason(BuildingDefinitionId id)
        {
            var technologies = context.Research.Catalog.All.Where(x => x.Unlocks.Any(unlock =>
                    unlock.Kind == TechnologyUnlockKind.Building && unlock.ContentId == id.ToString()))
                .Select(x => x.Id).ToArray();
            return technologies.Length == 0 ? T("ui.build.locked_era") : F("ui.build.requires", string.Join(" или ",
                technologies.Select(x => Content("technology", x.ToString()))));
        }

        private string BuildingResourceShortage(BuildingDefinition definition)
        {
            var missing = new List<string>();
            foreach (var cost in definition.ConstructionCost)
            {
                var available = context.WorldPiles.All.Sum(x => x.ResourceId == cost.ResourceId
                    ? context.Inventories.Get(x.InventoryOwner).GetAmount(cost.ResourceId).Units : 0L);
                available += context.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage)
                    .Sum(x => x.GetAmount(cost.ResourceId).Units);
                var shortage = Math.Max(0, cost.Quantity.Units - available);
                if (shortage > 0) missing.Add(Content("resource", cost.ResourceId.ToString()) + " — " + shortage);
            }
            return missing.Count == 0 ? string.Empty : F("ui.build.insufficient_resources", string.Join(", ", missing));
        }

        private string PlacementFailureText()
        {
            switch (context.BuildingPlacement.LastConstructionFailure)
            {
                case Game.Simulation.Resources.ConstructionFailureReason.LockedByResearch: return T("ui.build.locked_research");
                case Game.Simulation.Resources.ConstructionFailureReason.InsufficientResources:
                    return context.BuildingPlacement.IsActive
                        ? BuildingResourceShortage(context.BuildingCatalog.ResolveDefinition(context.BuildingPlacement.ActiveDefinitionId.Value))
                        : T("ui.build.insufficient_resources_generic");
                default: return T("ui.build.invalid_place");
            }
        }

        private string CharacterName(StableEntityId? id) => id.HasValue && context.Characters.TryGet(id.Value, out var character)
            ? RussianTextCatalog.PersonName(character.Name.DisplayName) : T("common.none");

        private string DynastyRole(StableEntityId id)
        {
            if (context.Dynasty.CurrentMonarchId == id) return T("ui.dynasty.role.monarch");
            if (context.Dynasty.Contains(id)) return T("ui.dynasty.role.member");
            return T("common.none");
        }

        private string InventoryText(ResourceInventory inventory)
        {
            var amounts = context.Resources.All.Select(x => new { DisplayName = Content("resource", x.Id.ToString()), Units = inventory.GetAmount(x.Id).Units })
                .Where(x => x.Units > 0).ToArray();
            return amounts.Length == 0 ? T("common.empty") : string.Join(", ", amounts.Select(x => x.DisplayName + " " + x.Units));
        }

        private string UnitLabel(MilitaryUnitState unit, int index) =>
            F(unit.OwnerSideId.Value == "player" ? "ui.military.player_unit" : "ui.military.known_enemy_unit", index) +
            (DeveloperUiVisible ? " [" + unit.Id + "]" : string.Empty);

        private static string BattleCounts(CombatEncounterState battle)
        {
            var active = battle.Combatants.Count(x => x.State != CombatantState.Dead &&
                x.State != CombatantState.Incapacitated && x.State != CombatantState.Disengaged);
            var dead = battle.Combatants.Count(x => x.State == CombatantState.Dead);
            var retreated = battle.Combatants.Count(x => x.State == CombatantState.Retreating ||
                x.State == CombatantState.Disengaged);
            return F("ui.military.battle_counts", active, dead, retreated);
        }

        private static int SideMorale(CombatEncounterState battle, CombatSideId side)
        {
            var members = battle.Combatants.Where(x => x.SideId == side).ToArray();
            return members.Length == 0 ? 0 : (int)members.Average(x => x.Morale);
        }

        private static string EligibilityReason(DirectControlEligibilityFailure reason)
        {
            switch (reason)
            {
                case DirectControlEligibilityFailure.None: return string.Empty;
                case DirectControlEligibilityFailure.Dead: return T("ui.tps.dead");
                case DirectControlEligibilityFailure.Child: return T("ui.tps.child");
                case DirectControlEligibilityFailure.Incapacitated: return T("ui.tps.incapacitated");
                case DirectControlEligibilityFailure.AggregateMilitaryUnit: return T("ui.tps.aggregate");
                case DirectControlEligibilityFailure.NotPlayerControlledRole: return T("ui.tps.role");
                default: return T("ui.status.command_failed");
            }
        }

        private static string TravelReason(Game.Simulation.World.TravelOrderStatus status)
        {
            switch (status)
            {
                case Game.Simulation.World.TravelOrderStatus.NoDirectRoute: return T("ui.map.no_route");
                case Game.Simulation.World.TravelOrderStatus.UnitInBattle: return T("ui.map.unit_in_battle");
                case Game.Simulation.World.TravelOrderStatus.AlreadyTravelling: return T("ui.map.already_travelling");
                case Game.Simulation.World.TravelOrderStatus.UnitNotAggregate: return T("ui.map.need_aggregate");
                default: return T("ui.status.command_failed");
            }
        }

        private static string Percent(int perThousand) => RussianTextCatalog.Format("common.percent", (perThousand / 10d).ToString("0"));
        private static string FormatDuration(long ticks) => TimeSpan.FromTicks(ticks).TotalDays >= 1
            ? F("common.days", RussianTextCatalog.Decimal(TimeSpan.FromTicks(ticks).TotalDays))
            : F("common.hours", RussianTextCatalog.Decimal(TimeSpan.FromTicks(ticks).TotalHours));

        private bool IsTextInputFocused() => root?.panel?.focusController?.focusedElement is TextField;

        private void TryCommand(Action action)
        {
            try
            {
                action();
                SetStatus(T("ui.status.command_accepted"));
                RefreshNow();
            }
            catch (PlayerFacingException exception)
            {
                SetStatus(exception.Message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus(T("ui.status.internal_error"));
            }
        }

        private void SetStatus(string message) => statusLabel.text = message ?? string.Empty;

        private void AddNotification(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            notificationFeed.Add(message.Trim());
            if (notificationFeed.Count > 20) notificationFeed.RemoveAt(0);
            notificationVersion++;
        }

        private void CloseModal()
        {
            pendingLoadSlot = 0;
            pendingNewGameSeed = 0;
            modal.style.display = DisplayStyle.None;
            modal.Clear();
        }

        private ulong ComputeRefreshHash()
        {
            var clock = context.Clock();
            var hash = unchecked((ulong)clock.State.CalendarTicks) ^ ((ulong)clock.Speed << 4) ^
                       (clock.IsPaused ? 0x8000000000000000UL : 0UL) ^ context.GlobalMap.ComputeStateHash() ^
                       context.Military.ComputeStateHash() ^ context.Combat.ComputeStateHash() ^
                       context.Research.ComputeStateHash() ^ context.Health.ComputeStateHash() ^
                       (context.Events?.ComputeStateHash() ?? 0UL) ^ (context.Campaign?.ComputeStateHash() ?? 0UL) ^
                       unchecked((ulong)context.Selection.SelectedCharacterIds.Count << 20) ^
                       unchecked((ulong)context.Buildings.All.Sum(x => x.History.Count) << 24) ^
                       unchecked((ulong)context.Work.JobCount << 32);
            if (context.Selection.SelectedCharacterId.HasValue) hash ^= unchecked((ulong)context.Selection.SelectedCharacterId.Value.GetHashCode());
            if (context.Selection.SelectedBuildingPresenter != null) hash ^= unchecked((ulong)context.Selection.SelectedBuildingPresenter.BuildingId.GetHashCode());
            if (context.Selection.SelectedProductionSourcePresenter != null)
                hash ^= unchecked((ulong)context.Selection.SelectedProductionSourcePresenter.Site.Id.GetHashCode());
            return hash;
        }

        private void AddNavigation(string label, GameplayPanel panel)
        {
            var button = ActionButton(label, () => SetPanel(panel), "nav-" + panel.ToString().ToLowerInvariant());
            SerenityUiDesign.StyleButton(button, navigation: true);
            navigation.Add(button);
        }

        private void RefreshNavigationSelection()
        {
            if (navigation == null) return;
            foreach (GameplayPanel panel in Enum.GetValues(typeof(GameplayPanel)))
            {
                if (panel == GameplayPanel.None) continue;
                SerenityUiDesign.SetSelected(navigation.Q<Button>("nav-" + panel.ToString().ToLowerInvariant()),
                    activePanel == panel);
            }
        }

        private void ApplyResponsiveLayout()
        {
            if (root == null) return;
            SerenityUiDesign.ApplySafeArea(root, Screen.safeArea, Screen.width, Screen.height);
            var width = root.resolvedStyle.width;
            var compact = !float.IsNaN(width) && width > 0f && width < 1380f;
            root.EnableInClassList("serenity-compact", compact);
            if (topBar == null) return;
            resourcesLabel.style.width = compact ? 286 : 350;
            dateLabel.style.width = compact ? 145 : 175;
            weatherLabel.style.width = compact ? 172 : 210;
            modeLabel.style.width = compact ? 92 : 112;
            alertLabel.style.width = compact ? 86 : 105;
            navigation.style.width = compact ? 150 : 166;
            mainPanel.style.left = compact ? 170 : 186;
            mainPanel.style.width = compact ? 430 : 486;
            inspector.style.width = compact ? 322 : 354;
            notifications.style.width = compact ? 322 : 354;
            onboardingCard.style.width = compact ? 322 : 354;
            inspector.style.top = onboarding.Visible ? 482 : 226;
        }

        private static bool Blocks(VisualElement element, Vector2 point) => element != null &&
            element.resolvedStyle.display != DisplayStyle.None && element.worldBound.Contains(point);

        private static string T(string key) => RussianTextCatalog.Get(key);
        private static string F(string key, params object[] arguments) => RussianTextCatalog.Format(key, arguments);
        private static string E(Enum value) => RussianTextCatalog.Enum(value);
        private static string Content(string category, string id) => RussianTextCatalog.Content(category, id);

        private static PlayerFacingException PlayerError(string key, params object[] arguments) =>
            new PlayerFacingException(F(key, arguments));

        private static string EventTitle(EventDefinitionId definitionId, EventNodeId nodeId) =>
            RussianTextCatalog.EventTitle(definitionId.ToString(), nodeId.ToString());

        private static string EventDescription(EventDefinitionId definitionId, EventNodeId nodeId) =>
            RussianTextCatalog.EventDescription(definitionId.ToString(), nodeId.ToString());

        private static string EventChoice(EventDefinitionId definitionId, EventNodeId nodeId, EventChoiceId choiceId) =>
            RussianTextCatalog.EventChoice(definitionId.ToString(), nodeId.ToString(), choiceId.ToString());

        private static string EventOutcome(string outcomeCode)
        {
            if (string.IsNullOrWhiteSpace(outcomeCode)) return T("common.none");
            return RussianTextCatalog.TryGet("event.outcome." + outcomeCode, out var value) ? value : T("ui.event.completed");
        }

        private string UnlockName(TechnologyUnlock unlock)
        {
            switch (unlock.Kind)
            {
                case TechnologyUnlockKind.Building: return Content("building", unlock.ContentId);
                case TechnologyUnlockKind.ProductionRecipe: return Content("recipe", unlock.ContentId);
                default: return T("ui.research.capability");
            }
        }

        private string LocationName(WorldLocationId id)
        {
            var key = "content.location." + id.Value;
            return RussianTextCatalog.TryGet(key, out var value) ? value : T("ui.location.unknown");
        }

        private string LocationName(string id) => string.IsNullOrWhiteSpace(id) ? T("common.none") :
            LocationName(new WorldLocationId(id));

        private static string SideName(CombatSideId side) => T(side.Value == "player" ? "ui.side.player" :
            side.Value == "enemy" ? "ui.side.enemy" : "ui.side.neutral");

        private static string ProfessionName(string professionId)
        {
            if (string.IsNullOrWhiteSpace(professionId)) return T("ui.character.unassigned");
            return RussianTextCatalog.TryGet("content.profession." + professionId, out var value) ? value : T("ui.profession.other");
        }

        private static VisualElement Panel(string name)
        {
            var value = new VisualElement { name = name };
            StylePanel(value);
            return value;
        }

        private static void StylePanel(VisualElement value)
        {
            SerenityUiDesign.StyleSurface(value);
            value.style.paddingLeft = 12; value.style.paddingRight = 12;
            value.style.paddingTop = 10; value.style.paddingBottom = 10;
        }

        private static void StyleTextField(TextField field)
        {
            SerenityUiDesign.StyleField(field);
        }

        private static Label HudLabel(float width)
        {
            var label = new Label();
            label.style.width = width;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.AddToClassList(SerenityUiDesign.ChipClass);
            return label;
        }

        private static Button ActionButton(string label, Action action, string name = null)
        {
            var button = new Button(action) { text = label, name = name ?? string.Empty };
            button.style.marginTop = 3; button.style.marginBottom = 3;
            button.style.marginLeft = 2; button.style.marginRight = 2;
            SerenityUiDesign.StyleButton(button);
            return button;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.AddToClassList(SerenityUiDesign.RowClass);
            return row;
        }

        private static void AddTitle(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList(SerenityUiDesign.TitleClass);
            parent.Add(label);
        }

        private static void AddSection(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList(SerenityUiDesign.SectionClass);
            parent.Add(label);
        }

        private static void AddLine(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList(SerenityUiDesign.BodyClass);
            parent.Add(label);
        }

        private static void AddMuted(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList(SerenityUiDesign.MutedClass);
            parent.Add(label);
        }
    }

    internal sealed class PlayerFacingException : Exception
    {
        public PlayerFacingException(string message) : base(message) { }
    }

    internal static class GameplayUiContextExtensions
    {
        public static bool StarterEnemyDefeated(this GameplayUiContext context) =>
            context.GlobalMap.Settlements.Any(x => x.OwnerSideId.Value == "enemy") == false;
    }
}
