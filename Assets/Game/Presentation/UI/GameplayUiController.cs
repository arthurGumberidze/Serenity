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
        private static readonly Color PanelColor = new Color(0.075f, 0.085f, 0.07f, 0.96f);
        private static readonly Color HeaderColor = new Color(0.15f, 0.18f, 0.12f, 0.98f);
        private static readonly Color AccentColor = new Color(0.66f, 0.52f, 0.28f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.90f, 0.82f, 1f);
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
        private bool suppressNotificationCaptureOnce;

        public bool DeveloperUiVisible { get; private set; }
        public GameplayPanel ActivePanel => activePanel;
        public int SubscriptionCount => subscribed ? 4 : 0;
        public VisualElement RootVisual => root;
        public IReadOnlyList<string> Notifications => notificationFeed.AsReadOnly();

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
            if (point.y <= 72f) return true;
            if (point.x >= 12f && point.x <= 150f && point.y >= 84f && point.y <= Screen.height - 12f) return true;
            if (mainPanel.resolvedStyle.display != DisplayStyle.None && point.x >= 158f && point.x <= 638f &&
                point.y >= 84f && point.y <= Screen.height - 54f) return true;
            if (inspector.resolvedStyle.display != DisplayStyle.None && point.x >= Screen.width - 432f &&
                point.y >= Screen.height - 372f) return true;
            if (notifications.resolvedStyle.display != DisplayStyle.None && point.x >= Screen.width - 392f &&
                point.y >= 80f && point.y <= 260f) return true;
            return modal.resolvedStyle.display != DisplayStyle.None;
        }

        public void SetPanel(GameplayPanel panel)
        {
            activePanel = activePanel == panel ? GameplayPanel.None : panel;
            mainPanel.style.display = activePanel == GameplayPanel.None ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshPanel();
        }

        public void SetDeveloperUiVisible(bool visible)
        {
            DeveloperUiVisible = visible;
            foreach (var overlay in context.LegacyDebugOverlays)
                if (overlay != null) overlay.enabled = visible;
            context.ThirdPerson.ShowLegacyOverlay = visible;
            context.SetDeveloperUiVisible(visible);
            statusLabel.text = visible
                ? "Developer UI enabled - F10 returns to player interface"
                : "Player interface active - F10 developer tools";
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (context == null || root == null) return;
            RefreshHud();
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
            AddNotification("Game saved to Slot " + index + ".");
        });

        public void RequestLoadSlot(int index)
        {
            pendingLoadSlot = index;
            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddTitle(modal, "Load Slot " + index);
            AddLine(modal, "Loading replaces the current canonical session state. Continue?");
            var row = Row();
            row.Add(ActionButton("Load", () => ConfirmLoadSlot(index)));
            row.Add(ActionButton("Cancel", CloseModal));
            modal.Add(row);
        }

        public void ConfirmLoadSlot(int index) => TryCommand(() =>
        {
            suppressNotificationCaptureOnce = true;
            context.Saves.Load(index);
            context.Selection.ClearSelection();
            pendingLoadSlot = 0;
            CloseModal();
            AddNotification("Slot " + index + " loaded. UI rebound to restored state.");
        });

        public void RequestNewGame(long worldSeed)
        {
            if (context.StartNewGame == null) throw new InvalidOperationException("New Game is unavailable.");
            pendingNewGameSeed = worldSeed;
            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddTitle(modal, "Start new game");
            AddLine(modal, "This replaces the running session with deterministic seed " + worldSeed + ". Continue?");
            var row = Row();
            row.Add(ActionButton("New Game", () => ConfirmNewGame(worldSeed), "confirm-new-game"));
            row.Add(ActionButton("Cancel", CloseModal));
            modal.Add(row);
        }

        public void ConfirmNewGame(long worldSeed)
        {
            pendingNewGameSeed = 0;
            context.StartNewGame?.Invoke(worldSeed);
        }

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
            root.style.color = TextColor;
            root.pickingMode = PickingMode.Ignore;
        }

        private void BuildLayout()
        {
            topBar = Panel("top-hud");
            topBar.style.position = Position.Absolute;
            topBar.style.left = 0; topBar.style.right = 0; topBar.style.top = 0; topBar.style.height = 72;
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems = Align.Center;
            topBar.style.paddingLeft = 12; topBar.style.paddingRight = 12;
            topBar.style.backgroundColor = HeaderColor;
            topBar.pickingMode = PickingMode.Position;
            resourcesLabel = HudLabel(440);
            dateLabel = HudLabel(210);
            weatherLabel = HudLabel(250);
            modeLabel = HudLabel(110);
            alertLabel = HudLabel(85);
            topBar.Add(resourcesLabel);
            topBar.Add(dateLabel);
            topBar.Add(weatherLabel);
            topBar.Add(modeLabel);
            topBar.Add(alertLabel);
            topBar.Add(ActionButton("Pause", () => context.SetPaused(!context.Clock().IsPaused), "pause-button"));
            foreach (var speed in Speeds)
            {
                var captured = speed;
                topBar.Add(ActionButton("x" + (int)speed, () => context.SetSpeed(captured), "speed-" + (int)speed));
            }
            root.Add(topBar);

            navigation = Panel("navigation");
            navigation.style.position = Position.Absolute;
            navigation.style.left = 12; navigation.style.top = 84; navigation.style.bottom = 54; navigation.style.width = 138;
            navigation.style.backgroundColor = PanelColor;
            navigation.style.paddingTop = 8; navigation.style.paddingLeft = 8; navigation.style.paddingRight = 8;
            navigation.pickingMode = PickingMode.Position;
            AddNavigation("Build  B", GameplayPanel.Build);
            AddNavigation("Settlement", GameplayPanel.Economy);
            AddNavigation("Research", GameplayPanel.Research);
            AddNavigation("Work groups", GameplayPanel.Work);
            AddNavigation("Dynasty", GameplayPanel.Dynasty);
            AddNavigation("Military", GameplayPanel.Military);
            AddNavigation("Save / Load", GameplayPanel.SaveLoad);
            AddNavigation("Global map", GameplayPanel.Global);
            if (context.Events != null) AddNavigation("Events", GameplayPanel.Events);
            root.Add(navigation);

            mainPanel = new ScrollView(ScrollViewMode.Vertical) { name = "main-panel" };
            StylePanel(mainPanel);
            mainPanel.style.position = Position.Absolute;
            mainPanel.style.left = 158; mainPanel.style.top = 84; mainPanel.style.bottom = 54; mainPanel.style.width = 480;
            mainPanel.style.display = DisplayStyle.None;
            mainPanel.pickingMode = PickingMode.Position;
            root.Add(mainPanel);

            inspector = new ScrollView(ScrollViewMode.Vertical) { name = "selection-inspector" };
            StylePanel(inspector);
            inspector.style.position = Position.Absolute;
            inspector.style.right = 12; inspector.style.bottom = 54; inspector.style.width = 420; inspector.style.maxHeight = 360;
            inspector.pickingMode = PickingMode.Position;
            root.Add(inspector);

            notifications = Panel("notifications");
            notifications.style.position = Position.Absolute;
            notifications.style.right = 12; notifications.style.top = 80; notifications.style.width = 380;
            notifications.style.maxHeight = 176; notifications.style.paddingLeft = 10; notifications.style.paddingRight = 10;
            notifications.style.backgroundColor = new Color(0.05f, 0.055f, 0.045f, 0.88f);
            notifications.pickingMode = PickingMode.Position;
            root.Add(notifications);

            statusBar = Panel("status-bar");
            statusBar.style.position = Position.Absolute;
            statusBar.style.left = 0; statusBar.style.right = 0; statusBar.style.bottom = 0; statusBar.style.height = 42;
            statusBar.style.backgroundColor = HeaderColor;
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.alignItems = Align.Center;
            statusBar.style.paddingLeft = 12;
            statusLabel = new Label("Player interface active - F10 developer tools");
            statusLabel.style.flexGrow = 1;
            statusBar.Add(statusLabel);
            root.Add(statusBar);

            tpsHud = Panel("tps-hud");
            tpsHud.style.position = Position.Absolute;
            tpsHud.style.left = 650; tpsHud.style.right = 450; tpsHud.style.bottom = 58;
            tpsHud.style.backgroundColor = PanelColor;
            tpsHud.style.paddingLeft = 12; tpsHud.style.paddingRight = 12;
            tpsLabel = new Label();
            tpsHud.Add(tpsLabel);
            tpsHud.style.display = DisplayStyle.None;
            root.Add(tpsHud);

            gameOverLabel = new Label();
            gameOverLabel.name = "game-over";
            gameOverLabel.style.position = Position.Absolute;
            gameOverLabel.style.left = 500; gameOverLabel.style.right = 500; gameOverLabel.style.top = 350;
            gameOverLabel.style.paddingTop = 24; gameOverLabel.style.paddingBottom = 24;
            gameOverLabel.style.backgroundColor = new Color(0.25f, 0.03f, 0.02f, 0.96f);
            gameOverLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            gameOverLabel.style.fontSize = 24;
            gameOverLabel.style.display = DisplayStyle.None;
            root.Add(gameOverLabel);

            modal = Panel("confirmation-modal");
            modal.style.position = Position.Absolute;
            modal.style.left = 560; modal.style.right = 560; modal.style.top = 310;
            modal.style.paddingLeft = 20; modal.style.paddingRight = 20; modal.style.paddingTop = 16; modal.style.paddingBottom = 16;
            modal.style.backgroundColor = new Color(0.09f, 0.08f, 0.055f, 0.99f);
            modal.style.display = DisplayStyle.None;
            modal.pickingMode = PickingMode.Position;
            root.Add(modal);
        }

        private void RefreshHud()
        {
            var clock = context.Clock();
            var alive = context.Characters.CountAlive();
            resourcesLabel.text = "Population " + alive + "   " + string.Join("   ", context.Resources.All
                .Where(x => x.Id.ToString() == "berries" || x.Id.ToString() == "wood_log" ||
                            x.Id.ToString() == "stone" || x.Id.ToString() == "plant_fiber" || x.Id.ToString() == "hide")
                .Select(x => x.DisplayName + " " + TotalResource(x.Id)));
            var ticks = clock.State.CalendarTicks;
            dateLabel.text = "Year " + GameCalendar.Year(ticks) + "  Day " + GameCalendar.DayOfYear(ticks) +
                             "  " + GameCalendar.HourOfDay(ticks).ToString("00") + ":" + GameCalendar.MinuteOfHour(ticks).ToString("00");
            var weather = context.Weather.GetWeatherAtCalendarTick(ticks);
            weatherLabel.text = weather.Season + "   " + weather.Type + "   " + weather.TemperatureCelsius.ToString("0.0") + " C";
            modeLabel.text = context.GlobalMap.Mode == GameWorldMode.Local ? "LOCAL" : "GLOBAL";
            var pendingEvents = context.Events?.PendingPlayerEvents.Count ?? 0;
            alertLabel.text = "Alerts " + notificationFeed.Count + (pendingEvents > 0 ? "  Events " + pendingEvents : string.Empty);
            topBar.Q<Button>("pause-button").text = clock.IsPaused ? "Resume" : "Pause";
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
            AddTitle(mainPanel, "Events");
            if (context.Events == null)
            {
                AddMuted(mainPanel, "Event service is unavailable in this session.");
                return;
            }
            var pending = context.Events.PendingPlayerEvents;
            AddSection(mainPanel, "Pending " + pending.Count);
            if (pending.Count == 0) AddMuted(mainPanel, "No decisions are waiting.");
            foreach (var instance in pending)
            {
                var definition = context.Events.Catalog.Get(instance.DefinitionId);
                var node = definition.Nodes.Single(x => x.Id == instance.CurrentNodeId);
                var captured = instance.InstanceId;
                mainPanel.Add(ActionButton((selectedEvent == instance.InstanceId ? "> " : string.Empty) + node.Title,
                    () => { selectedEvent = captured; RefreshPanel(); }));
                AddMuted(mainPanel, string.Join(", ", instance.Targets.Select(context.Events.GetTargetDisplayName)));
            }
            if (!selectedEvent.HasValue || pending.All(x => x.InstanceId != selectedEvent.Value))
                selectedEvent = pending.Count == 0 ? (EventInstanceId?)null : pending[0].InstanceId;
            if (selectedEvent.HasValue)
            {
                var instance = pending.First(x => x.InstanceId == selectedEvent.Value);
                var definition = context.Events.Catalog.Get(instance.DefinitionId);
                var node = definition.Nodes.Single(x => x.Id == instance.CurrentNodeId);
                AddSection(mainPanel, node.Title);
                AddLine(mainPanel, node.Description);
                AddMuted(mainPanel, "Involving " + string.Join(", ", instance.Targets.Select(context.Events.GetTargetDisplayName)) + ".");
                foreach (var choice in node.Choices)
                {
                    var capturedChoice = choice.Id;
                    mainPanel.Add(ActionButton(choice.Label, () => TryCommand(() =>
                    {
                        var result = context.Events.ResolvePlayerChoice(instance.InstanceId, capturedChoice,
                            context.Clock().State.CalendarTicks);
                        if (!result.Applied) throw new InvalidOperationException("Event choice was not applied: " + result.Status);
                        selectedEvent = null;
                        SetStatus("Event choice applied.");
                    })));
                }
            }
            AddSection(mainPanel, "Recent history");
            if (context.Events.History.Count == 0) AddMuted(mainPanel, "No resolved events yet.");
            foreach (var entry in context.Events.History.Reverse().Take(12))
            {
                var definition = context.Events.Catalog.Get(entry.DefinitionId);
                var title = definition.Nodes[0].Title;
                AddLine(mainPanel, title + " - " + entry.Status + " - " + entry.OutcomeCode);
                AddMuted(mainPanel, string.Join(", ", entry.Targets.Select(context.Events.GetTargetDisplayName)));
            }
        }

        private void DrawEconomyPanel()
        {
            AddTitle(mainPanel, "Settlement economy");
            var storage = context.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage).ToArray();
            AddLine(mainPanel, "Storage: " + storage.Sum(x => x.TotalUnits) + "/" + storage.Sum(x => x.Capacity));
            foreach (var definition in context.Resources.All)
            {
                var target = context.Production.GetTarget(definition.Id);
                var total = context.Production.GetCanonicalTotal(definition.Id);
                var row = Row();
                row.Add(new Label(definition.DisplayName + "  stored " + context.StoredResources.GetStoredTotal(definition.Id).Units +
                                  "  total " + total + "  target " + target));
                row.Add(ActionButton("-5", () => TryCommand(() =>
                    context.Production.SetTarget(definition.Id, Math.Max(0, target - 5)))));
                row.Add(ActionButton("+5", () => TryCommand(() =>
                    context.Production.SetTarget(definition.Id, checked(target + 5)))));
                mainPanel.Add(row);
            }
            AddLine(mainPanel, "Food consumed: " + context.Production.Food.TotalConsumed +
                " | shortage: " + context.Production.Food.TotalShortage);
            var weather = context.Weather.GetWeatherAtCalendarTick(context.Clock().State.CalendarTicks);
            AddMuted(mainPanel, "Current modifiers: crops " + Percent(weather.Modifiers.CropGrowthPerThousand) +
                ", outdoor work " + Percent(weather.Modifiers.OutdoorWorkPerThousand) +
                ", wild food " + Percent(weather.Modifiers.WildFoodGatheringPerThousand) + ".");
            AddSection(mainPanel, "Production sites");
            foreach (var site in context.Production.Sites)
            {
                var status = site.Kind == ProductionSiteKind.PrimitiveCropPlot
                    ? "crop " + site.CropPhase + " " + (site.GrowthRequiredTicks == 0 ? string.Empty :
                        ((double)site.GrowthProgressTicks / site.GrowthRequiredTicks).ToString("P0"))
                    : "source " + site.RemainingAmount + "/" + site.SourceCapacity;
                if (context.Buildings.TryGet(site.Id, out var building) && !building.IsOperational)
                    status += " | disabled: " + building.StructuralStatus;
                AddLine(mainPanel, site.RecipeId + " | " + status);
            }
            AddSection(mainPanel, "Active production");
            if (context.Production.ActiveTasks.Count == 0) AddMuted(mainPanel, "No active production tasks.");
            foreach (var task in context.Production.ActiveTasks.Take(12))
                AddLine(mainPanel, task.Kind + " | " + task.RecipeId + " | " +
                    ((double)task.AccumulatedWorkTicks / task.RequiredWorkTicks).ToString("P0"));
        }

        private void DrawBuildPanel()
        {
            AddTitle(mainPanel, "Build menu");
            AddLine(mainPanel, "Choose a structure, then place its ghost with LMB. R rotates; Esc or RMB cancels.");
            foreach (var entry in context.BuildingCatalog.Entries.OrderBy(x => x.DefinitionId, StringComparer.Ordinal))
            {
                var definition = entry.CreateDefinition();
                var available = context.Research.IsBuildingUnlocked(definition.Id);
                var cost = definition.ConstructionCost.Count == 0 ? "No cost" : string.Join(", ",
                    definition.ConstructionCost.Select(x => x.ResourceId + " " + x.Quantity.Units));
                var reason = available ? string.Empty : BuildingLockReason(definition.Id);
                var button = ActionButton(definition.DisplayName + "  |  " + cost,
                    () => StartBuilding(definition.Id));
                button.SetEnabled(available);
                mainPanel.Add(button);
                if (!available) AddMuted(mainPanel, reason);
            }
            if (context.BuildingPlacement.IsActive)
                AddLine(mainPanel, "Placement: " + context.BuildingPlacement.ActiveDefinitionId +
                    " | " + (context.BuildingPlacement.IsPreviewValid ? "valid" : PlacementFailureText()));
        }

        private void DrawResearchPanel()
        {
            AddTitle(mainPanel, "Stone Age research");
            var active = context.Research.ActiveProject;
            if (active == null) AddLine(mainPanel, "Active project: none");
            else
            {
                var definition = context.Research.Catalog.Get(active.TechnologyId);
                AddLine(mainPanel, "Active: " + definition.DisplayName + "   " + active.Progress.ToString("P1"));
                var status = ResearchWorkerStatusQuery.Capture(context.Research, context.AiAgents);
                AddLine(mainPanel, "Researchers: " + status.ActiveCount + " active / " + status.AssignedCount + " assigned");
                mainPanel.Add(ActionButton("Cancel active research", () => TryCommand(() =>
                {
                    if (!context.Research.Cancel()) throw new InvalidOperationException("There is no active research project.");
                })));
            }
            var workerRow = Row();
            workerRow.Add(ActionButton("Assign selected", () => TryCommand(() =>
                SetStatus(context.Research.AssignWorkers(context.Selection.SelectedCharacterIds) + " researcher(s) assigned."))));
            workerRow.Add(ActionButton("Remove selected", () => TryCommand(() =>
                SetStatus(context.Research.UnassignWorkers(context.Selection.SelectedCharacterIds) + " researcher(s) removed."))));
            mainPanel.Add(workerRow);
            foreach (var technology in context.Research.Catalog.All)
            {
                var state = context.Research.IsCompleted(technology.Id) ? "Completed" :
                    active != null && active.TechnologyId == technology.Id ? "Active" :
                    context.Research.IsAvailable(technology.Id) ? "Available" : "Locked";
                AddSection(mainPanel, technology.DisplayName + " - " + state);
                AddMuted(mainPanel, "Prerequisites: " + (technology.Prerequisites.Count == 0 ? "none" :
                    string.Join(", ", technology.Prerequisites.Select(x => context.Research.Catalog.Get(x).DisplayName))));
                AddMuted(mainPanel, "Research work: " + technology.ResearchWorkRequiredTicks + " | Unlocks: " +
                    (technology.Unlocks.Count == 0 ? "capability" : string.Join(", ", technology.Unlocks.Select(x => x.ContentId))));
                if (state == "Available" && active == null)
                    mainPanel.Add(ActionButton("Start " + technology.DisplayName,
                        () => TryCommand(() => context.Research.Start(technology.Id))));
            }
        }

        private void DrawWorkPanel()
        {
            AddTitle(mainPanel, "Work groups and jobs");
            var groups = context.Work.Groups.All;
            foreach (var group in groups)
            {
                var captured = group.Id;
                var button = ActionButton((selectedGroup == group.Id ? "> " : string.Empty) + group.DisplayName +
                                          "  (" + group.MemberCount + ")", () =>
                {
                    selectedGroup = captured;
                    SelectGroupPresenters();
                    RefreshPanel();
                });
                mainPanel.Add(button);
            }
            mainPanel.Add(ActionButton("Create from selected characters", () => TryCommand(() =>
            {
                if (context.Selection.SelectedCharacterIds.Count == 0) throw new InvalidOperationException("Select at least one character.");
                selectedGroup = context.Work.CreateGroup("Work Group " + (context.Work.Groups.Count + 1),
                    context.Selection.SelectedCharacterIds).Id;
            })));
            if (!selectedGroup.HasValue || !context.Work.Groups.TryGet(selectedGroup.Value, out var current))
            {
                AddMuted(mainPanel, "Select or create a group to manage it.");
                return;
            }
            AddSection(mainPanel, current.DisplayName);
            var rename = new TextField("Name") { value = current.DisplayName, name = "work-group-name" };
            mainPanel.Add(rename);
            mainPanel.Add(ActionButton("Rename", () => TryCommand(() => context.Work.RenameGroup(current.Id, rename.value))));
            var membership = Row();
            membership.Add(ActionButton("Add selected", () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds)
                    if (!current.Contains(id)) context.Work.AddMember(current.Id, id);
            })));
            membership.Add(ActionButton("Remove selected", () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds.ToArray()) context.Work.RemoveMember(current.Id, id);
            })));
            membership.Add(ActionButton("Commander = selected", () => TryCommand(() =>
            {
                if (context.Selection.SelectedCharacterIds.Count != 1) throw new InvalidOperationException("Select one group member.");
                context.Work.SetCommander(current.Id, context.Selection.SelectedCharacterIds[0]);
            })));
            mainPanel.Add(membership);
            AddLine(mainPanel, "Commander: " + CharacterName(current.CommanderId) + "   Priority: " + workPriority);
            var priority = Row();
            foreach (WorkPriority value in Enum.GetValues(typeof(WorkPriority)))
            {
                var captured = value;
                priority.Add(ActionButton(value.ToString(), () => { workPriority = captured; RefreshPanel(); }));
            }
            mainPanel.Add(priority);
            var commands = Row();
            commands.Add(ActionButton("Move to center", () => TryCommand(() =>
                context.ManualMoveCommands.IssueForGroup(current.Id, new WorldPosition(0f, 0f, 10f), workPriority))));
            commands.Add(ActionButton("Haul available", () => TryCommand(() => IssueGroupHaul(current.Id))));
            commands.Add(ActionButton("Cancel open jobs", () => TryCommand(() => CancelGroupJobs(current.Id))));
            mainPanel.Add(commands);
            AddMuted(mainPanel, "World Move remains available by selecting this group and right-clicking valid ground. Ctrl+1..9 assigns shortcuts; 1..9 recalls them.");
            foreach (var job in context.Work.Jobs.Where(x => x.GroupId == current.Id).OrderByDescending(x => x.Id).Take(10))
                AddLine(mainPanel, "#" + job.Id + "  " + job.Type + "  " + job.Priority + "  " + job.Status + "  claim " + job.ClaimState);
        }

        private void DrawDynastyPanel()
        {
            AddTitle(mainPanel, context.Dynasty.Name);
            AddLine(mainPanel, "Status: " + context.Dynasty.Status);
            AddLine(mainPanel, "Monarch: " + CharacterName(context.Dynasty.CurrentMonarchId));
            var monarch = context.Dynasty.CurrentMonarchId.HasValue
                ? context.Characters.Get(context.Dynasty.CurrentMonarchId.Value) : null;
            AddLine(mainPanel, "Spouse: " + CharacterName(monarch?.SpouseId));
            var children = monarch == null ? Array.Empty<Character>() : context.Family.GetChildren(monarch.Id).ToArray();
            AddLine(mainPanel, "Children: " + (children.Length == 0 ? "none" : string.Join(", ", children.Select(x => x.Name.DisplayName))));
            var heir = context.Dynasty.CurrentMonarchId.HasValue
                ? context.Family.GetOrderedSuccessionCandidates(context.Dynasty, context.Dynasty.CurrentMonarchId.Value).FirstOrDefault()
                : null;
            AddLine(mainPanel, "Current heir: " + (heir == null ? "none" : heir.Name.DisplayName));
            AddSection(mainPanel, "Succession candidates");
            if (context.Dynasty.CurrentMonarchId.HasValue)
                foreach (var candidate in context.Family.GetOrderedSuccessionCandidates(context.Dynasty,
                             context.Dynasty.CurrentMonarchId.Value).Take(8))
                    AddLine(mainPanel, candidate.Name.DisplayName + "  age " + candidate.AgeInCompletedYears(context.Clock().State.BiologicalTicks));
            AddSection(mainPanel, "Succession history");
            foreach (var entry in context.Dynasty.History.Reverse().Take(6))
                AddLine(mainPanel, CharacterName(entry.PreviousMonarchId) + " -> " + CharacterName(entry.NewMonarchId) + "  " + entry.Reason);
        }

        private void DrawMilitaryPanel()
        {
            AddTitle(mainPanel, "Military");
            mainPanel.Add(ActionButton("Create player unit from selected", () => TryCommand(CreatePlayerUnit)));
            var index = 1;
            foreach (var unit in context.Military.Units)
            {
                var captured = unit.Id;
                var label = UnitLabel(unit, index++);
                mainPanel.Add(ActionButton((selectedUnit == unit.Id ? "> " : string.Empty) + label,
                    () => { selectedUnit = captured; RefreshPanel(); }));
                var summary = context.Military.GetRosterSummary(unit.Id);
                AddMuted(mainPanel, "Captain " + CharacterName(unit.CaptainId) + " | roster " + summary.Active +
                    " | combat " + summary.CombatCapable + " | injured " + summary.Injured +
                    " | " + unit.Representation + " | " + unit.Order.Kind);
            }
            if (!selectedUnit.HasValue || !context.Military.TryGet(selectedUnit.Value, out var selected)) return;
            AddSection(mainPanel, "Selected unit actions");
            AddLine(mainPanel, "Supply: " + (selected.Supply.Initialized
                ? selected.Supply.Get(context.Military.SupplySettings.FoodResourceId) + " " +
                  context.Military.SupplySettings.FoodResourceId
                : "not provisioned"));
            mainPanel.Add(ActionButton("Resupply", () => TryCommand(() =>
                context.Military.LoadInitialSupply(selected.Id, context.Inventories,
                    context.Inventories.All.Select(x => x.Owner)))));
            var roster = Row();
            roster.Add(ActionButton("Add selected", () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds) context.Military.AddMember(selected.Id, id);
            })));
            roster.Add(ActionButton("Remove selected", () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds) context.Military.RemoveMember(selected.Id, id);
            })));
            roster.Add(ActionButton("Captain = selected", () => TryCommand(() =>
            {
                if (context.Selection.SelectedCharacterIds.Count != 1) throw new InvalidOperationException("Select one roster member.");
                var result = context.Military.AssignCaptain(selected.Id, context.Selection.SelectedCharacterIds[0]);
                if (result != CommanderAssignmentStatus.Assigned) throw new InvalidOperationException(result.ToString());
            })));
            mainPanel.Add(roster);
            var orders = Row();
            orders.Add(ActionButton("Idle", () => TryCommand(() => context.Military.SetOrder(selected.Id, MilitaryUnitOrderKind.Idle))));
            orders.Add(ActionButton("Move center", () => TryCommand(() => context.MilitaryCommands.Move(selected.Id, new WorldPosition(0, 0, 10)))));
            orders.Add(ActionButton("Hold", () => TryCommand(() => context.MilitaryCommands.Hold(selected.Id))));
            orders.Add(ActionButton("Engage", () => TryCommand(() => context.Military.SetOrder(selected.Id, MilitaryUnitOrderKind.Engage))));
            mainPanel.Add(orders);
            var representations = Row();
            representations.Add(ActionButton("Detailed", () => TryCommand(() => context.MilitaryRepresentations.Transition(selected.Id, MilitaryUnitRepresentationMode.Detailed))));
            representations.Add(ActionButton("Aggregate", () => TryCommand(() => context.MilitaryRepresentations.Transition(selected.Id, MilitaryUnitRepresentationMode.Aggregate))));
            mainPanel.Add(representations);
            AddSection(mainPanel, "Active battles");
            foreach (var battle in context.Battles.Battles.Where(x => x.Status == CombatEncounterStatus.Active))
            {
                AddLine(mainPanel, battle.Mode + " | " + BattleCounts(battle) + " | morale " +
                    SideMorale(battle, battle.SideA) + "/" + SideMorale(battle, battle.SideB));
                if (battle.Mode == BattleMode.Abstract)
                    mainPanel.Add(ActionButton("Enter physical battle", () => context.RequestEnterBattle(battle.BattleId)));
            }
        }

        private void DrawSavePanel()
        {
            AddTitle(mainPanel, "Save and load");
            AddLine(mainPanel, "Local files are always available. Loading requires confirmation and rebinds the existing UI once.");
            if (context.StartNewGame != null)
            {
                AddSection(mainPanel, "New game");
                var seed = new TextField("World seed") { value = context.Campaign?.State.WorldSeed.ToString() ?? "20261006" };
                seed.name = "new-game-seed";
                mainPanel.Add(seed);
                mainPanel.Add(ActionButton("Start New Game", () =>
                {
                    if (!long.TryParse(seed.value, out var parsed))
                        throw new InvalidOperationException("World seed must be a whole number.");
                    RequestNewGame(parsed);
                }, "new-game-button"));
            }
            foreach (var slot in context.Saves.ListSlots())
            {
                AddSection(mainPanel, slot.Label);
                AddLine(mainPanel, slot.Exists ? slot.Detail : "Empty slot");
                var row = Row();
                row.Add(ActionButton("Save", () => SaveSlot(slot.Index)));
                var load = ActionButton("Load", () => RequestLoadSlot(slot.Index));
                load.SetEnabled(slot.Exists && !slot.IsCorrupt);
                row.Add(load);
                mainPanel.Add(row);
                if (slot.IsCorrupt) AddMuted(mainPanel, "Slot unavailable/corrupt.");
            }
        }

        private void DrawGlobalPanel()
        {
            AddTitle(mainPanel, "Global map");
            AddLine(mainPanel, "Mode: " + context.GlobalMap.Mode + " | Active location: " +
                context.GlobalMap.GetLocation(context.GlobalMap.ActiveLocationId).Name);
            if (context.GlobalMap.Mode == GameWorldMode.Local)
            {
                mainPanel.Add(ActionButton("Open Global Map", context.RequestEnterGlobal));
                AddMuted(mainPanel, "Local simulation continues while the global map is open.");
                return;
            }
            AddSection(mainPanel, "Locations and destinations");
            foreach (var location in context.GlobalMap.Locations)
            {
                var captured = location.Id;
                var settlement = context.GlobalMap.GetSettlementAt(location.Id);
                var hidden = settlement != null && context.Campaign != null &&
                             !context.Campaign.CanRevealSettlement(settlement.Id);
                var owner = settlement == null ? "unclaimed" : settlement.OwnerSideId.Value;
                mainPanel.Add(ActionButton((selectedLocation == location.Id ? "> " : string.Empty) +
                    (hidden ? "Unknown territory  | settlement | unknown" :
                        location.Name + "  | " + location.Type + " | " + owner),
                    () => { selectedLocation = captured; RefreshPanel(); }));
            }
            AddSection(mainPanel, "Player armies");
            var index = 1;
            foreach (var unit in context.Military.Units.Where(x => x.OwnerSideId.Value == "player" &&
                         x.Representation == MilitaryUnitRepresentationMode.Aggregate))
            {
                var captured = unit.Id;
                var travel = context.GlobalMap.Travels.FirstOrDefault(x => x.UnitId == unit.Id);
                var detail = travel == null ? unit.AbstractLocationId : travel.Origin + " -> " + travel.Destination +
                    " | ETA " + FormatDuration(Math.Max(0, travel.ArrivalTick - context.Clock().State.CalendarTicks));
                mainPanel.Add(ActionButton((selectedUnit == unit.Id ? "> " : string.Empty) + UnitLabel(unit, index++) + " | " + detail,
                    () => { selectedUnit = captured; RefreshPanel(); }));
            }
            var actions = Row();
            var travelButton = ActionButton("Travel to selected", () => TryCommand(() =>
            {
                if (!selectedUnit.HasValue || !selectedLocation.HasValue)
                    throw new InvalidOperationException("Select a player army and destination.");
                var result = context.GlobalMap.IssueTravelOrder(selectedUnit.Value, selectedLocation.Value,
                    context.Clock().State.CalendarTicks);
                if (!result.Issued) throw new InvalidOperationException(TravelReason(result.Status));
                AddNotification("Army departed for " + context.GlobalMap.GetLocation(selectedLocation.Value).Name + ".");
            }));
            actions.Add(travelButton);
            actions.Add(ActionButton("Enter selected location", () => TryCommand(() =>
            {
                if (!selectedLocation.HasValue) throw new InvalidOperationException("Select a location.");
                context.RequestEnterLocation(selectedLocation.Value);
            })));
            mainPanel.Add(actions);
            AddSection(mainPanel, "Battles");
            foreach (var battle in context.Battles.Battles.Where(x => x.Status == CombatEncounterStatus.Active))
            {
                AddLine(mainPanel, battle.Mode + " | " + BattleCounts(battle) + " | " + battle.Outcome);
                if (battle.Mode == BattleMode.Abstract)
                    mainPanel.Add(ActionButton("Enter battle", () => context.RequestEnterBattle(battle.BattleId)));
            }
        }

        private void RefreshInspector()
        {
            var hasSelection = context.Selection.SelectedCharacterIds.Count > 0 ||
                               context.Selection.SelectedBuildingPresenter != null ||
                               context.Selection.SelectedWorldResourcePilePresenter != null;
            if (!hasSelection && emptyInspectorRendered) return;
            emptyInspectorRendered = !hasSelection;
            inspector.Clear();
            AddTitle(inspector, "Selection");
            if (context.Selection.SelectedCharacterIds.Count > 1)
            {
                AddLine(inspector, context.Selection.SelectedCharacterIds.Count + " characters selected.");
                AddMuted(inspector, "Choose one character for detailed actions, or use the group/military panels.");
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
                AddLine(inspector, "World resource pile");
                AddLine(inspector, context.Resources.Get(pile.ResourceId).DisplayName + ": " +
                    inventory.GetAmount(pile.ResourceId).Units);
                return;
            }
            AddLine(inspector, "Nothing selected");
            AddMuted(inspector, "Select a character, building or resource in the world.");
        }

        private void DrawCharacterInspector(StableEntityId id)
        {
            var character = context.Characters.Get(id);
            var capabilities = context.Health.GetCapabilities(id);
            AddSection(inspector, character.Name.DisplayName);
            AddLine(inspector, "Age " + character.AgeInCompletedYears(context.Clock().State.BiologicalTicks) +
                " | " + character.Sex + " | " + character.LifeState);
            AddLine(inspector, "Profession: " + (character.ProfessionId?.ToString() ?? "unassigned"));
            AddLine(inspector, "Health: " + (capabilities.IsIncapacitated ? "incapacitated" : "active") +
                " | work " + Percent(capabilities.WorkPerThousand) + " | research " + Percent(capabilities.ResearchPerThousand));
            var conditions = context.Health.Conditions.Where(x => x.TargetId == id &&
                x.State != HealthConditionState.Recovered).ToArray();
            AddLine(inspector, "Conditions: " + (conditions.Length == 0 ? "none" : string.Join(", ", conditions.Select(x =>
                context.Health.Catalog.Get(x.DefinitionId).DisplayName + " " + x.Severity))));
            var pregnancy = context.Health.Pregnancies.FirstOrDefault(x => x.MotherId == id && x.Status == PregnancyStatus.Active);
            if (pregnancy != null) AddLine(inspector, "Pregnancy: active");
            var inventoryOwner = new InventoryOwner(InventoryOwnerKind.Character, id);
            AddLine(inspector, "Inventory: " + (context.Inventories.TryGet(inventoryOwner, out var inventory)
                ? InventoryText(inventory) : "none"));
            var research = context.Research.AssignedWorkers.Contains(id) ? "assigned" : "not assigned";
            AddLine(inspector, "Research: " + research);
            AddLine(inspector, "Work group: " + (context.Work.Groups.TryGetForMember(id, out var group) ? group.DisplayName : "none"));
            AddLine(inspector, "Current job: " + (context.Work.TryGetAssigned(id, out var job) ? job.Type + " / " + job.Status : "none"));
            AddLine(inspector, "Dynasty role: " + DynastyRole(id));
            AddLine(inspector, "Military: " + (context.Military.TryGetForMember(id, out var unit)
                ? "member" + (unit.CaptainId == id ? ", captain" : string.Empty) : "civilian"));
            if (context.Events != null)
            {
                var eventHistory = context.Events.History.Where(x => x.Targets.Any(target =>
                    target.Kind == EventTargetKind.Character && target.TryGetStableEntityId(out var targetId) && targetId == id))
                    .Reverse().Take(3).ToArray();
                AddLine(inspector, "Key events: " + (eventHistory.Length == 0 ? "none" : string.Join(", ",
                    eventHistory.Select(x => context.Events.Catalog.Get(x.DefinitionId).Nodes[0].Title))));
            }
            var eligibility = context.PlayerControl.Evaluate(id);
            var actions = Row();
            actions.Add(ActionButton(context.Research.AssignedWorkers.Contains(id) ? "Remove research" : "Assign research", () => TryCommand(() =>
            {
                if (context.Research.AssignedWorkers.Contains(id)) context.Research.UnassignWorkers(new[] { id });
                else context.Research.AssignWorkers(new[] { id });
            })));
            var tps = ActionButton("Enter TPS", () => TryCommand(() =>
            {
                var result = context.ThirdPerson.TryEnter(id);
                if (!result.IsEligible) throw new InvalidOperationException(EligibilityReason(result.Failure));
            }));
            tps.SetEnabled(eligibility.IsEligible);
            actions.Add(tps);
            inspector.Add(actions);
            if (!eligibility.IsEligible) AddMuted(inspector, "TPS unavailable: " + EligibilityReason(eligibility.Failure));
            if (DeveloperUiVisible) AddMuted(inspector, "Stable ID: " + id);
        }

        private void DrawBuildingInspector(Building building)
        {
            var definition = context.BuildingCatalog.ResolveDefinition(building.DefinitionId);
            AddSection(inspector, definition.DisplayName);
            AddLine(inspector, "Owner: " + building.OwnerSideId.Value + " | " + building.ConstructionState);
            AddLine(inspector, "Integrity: " + building.CurrentIntegrity + "/" + building.MaxIntegrity + " | " + building.StructuralStatus);
            AddLine(inspector, "Fire: " + building.FireStatus + (building.FireStatus == BuildingFireStatus.Burning
                ? " intensity " + building.FireIntensity : string.Empty));
            var owner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id);
            if (context.Inventories.TryGet(owner, out var storage))
                AddLine(inspector, "Storage: " + storage.TotalUnits + "/" + storage.Capacity + " | " + InventoryText(storage));
            if (context.Production.Sites.FirstOrDefault(x => x.Id == building.Id) is ProductionSite site)
            {
                AddLine(inspector, "Production: " + (building.IsOperational ? "operational" : "unavailable"));
                if (site.Kind == ProductionSiteKind.PrimitiveCropPlot)
                    AddLine(inspector, "Crop: " + site.CropPhase + " | growth " +
                        (site.GrowthRequiredTicks == 0 ? "-" : ((double)site.GrowthProgressTicks / site.GrowthRequiredTicks).ToString("P0")));
            }
            if (DeveloperUiVisible) AddMuted(inspector, "Stable ID: " + building.Id);
        }

        private void RefreshNotifications()
        {
            if (renderedNotificationVersion == notificationVersion) return;
            renderedNotificationVersion = notificationVersion;
            notifications.Clear();
            AddSection(notifications, "Notifications");
            if (notificationFeed.Count == 0) AddMuted(notifications, "No important alerts.");
            foreach (var message in notificationFeed.Skip(Math.Max(0, notificationFeed.Count - 5)).Reverse())
                AddLine(notifications, message);
        }

        private void CaptureNotifications()
        {
            var researchCount = context.Research.CompletedTechnologies.Count;
            var characterCount = context.Characters.Count;
            var aliveCount = context.Characters.CountAlive();
            var dynastyCount = context.Dynasty.History.Count;
            var conditionCount = context.Health.Conditions.Count;
            var buildingHistory = context.Buildings.All.Sum(x => x.History.Count);
            var battleEvents = context.Battles.Battles.Sum(x => x.BattleHistory.Count);
            var travelCount = context.GlobalMap.Travels.Count;
            var shortage = context.Production.Food.TotalShortage;
            var eventHistoryCount = context.Events?.History.Count ?? 0;
            var pendingEventCount = context.Events?.PendingPlayerEvents.Count ?? 0;
            var campaignSequence = context.Campaign?.State.EventSequence ?? 0UL;
            if (notificationsPrimed && !suppressNotificationCaptureOnce)
            {
                if (researchCount > knownResearchCount) AddNotification("Research completed.");
                if (characterCount > knownCharacterCount) AddNotification("A child was born.");
                if (aliveCount < knownAliveCount) AddNotification("A character died.");
                if (dynastyCount > knownDynastyHistoryCount) AddNotification("Succession state changed.");
                if (conditionCount > knownConditionCount) AddNotification("A new injury or disease requires attention.");
                if (buildingHistory > knownBuildingHistoryCount)
                {
                    var change = context.Buildings.All.SelectMany(x => x.History).OrderByDescending(x => x.CalendarTick)
                        .ThenByDescending(x => x.Sequence).First();
                    AddNotification("Building " + change.Kind.ToString().ToLowerInvariant() + ".");
                }
                if (battleEvents > knownBattleEventCount)
                {
                    var change = context.Battles.Battles.SelectMany(x => x.BattleHistory)
                        .OrderByDescending(x => x.CalendarTick).ThenByDescending(x => x.Sequence).First();
                    AddNotification(change.Kind == BattleEventKind.Started ? "Settlement battle started." :
                        change.Kind == BattleEventKind.Finished ? "Battle finished." : "Battle state changed.");
                }
                if (travelCount < knownTravelCount) AddNotification("An army arrived.");
                if (shortage > knownFoodShortage) AddNotification("Food shortage increased.");
                if (pendingEventCount > knownPendingEventCount) AddNotification("A new event awaits your decision.");
                if (eventHistoryCount > knownEventHistoryCount)
                {
                    var entry = context.Events.History.Last();
                    AddNotification(context.Events.Catalog.Get(entry.DefinitionId).Nodes[0].Title + " resolved.");
                }
                if (campaignSequence > knownCampaignEventSequence && context.Campaign != null)
                {
                    if (context.Campaign.State.MilitaryVictory)
                        AddNotification("Military victory recorded. The sandbox continues.");
                    else if (context.Campaign.State.RivalDiscovered)
                        AddNotification("A rival dynasty has been discovered.");
                }
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
            tpsLabel.text = "TPS  |  " + character.Name.DisplayName + "  |  " +
                (capabilities.IsIncapacitated ? "INCAPACITATED" : "Health active") + "  |  Combat " +
                (combat ? "engaged" : "clear") + "  |  LMB attack  |  T or Esc return to RTS";
        }

        private void RefreshGameOver()
        {
            if (context.GameRun.Status == GameRunStatus.GameOver)
            {
                gameOverLabel.text = "DEFEAT\n" + context.GameRun.GameOverReason;
                gameOverLabel.style.display = DisplayStyle.Flex;
                return;
            }
            if (context.Campaign?.State.MilitaryVictory == true)
            {
                gameOverLabel.text = "RIVAL DEFEATED\nThe sandbox continues";
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
                SetStatus("Selected " + group.DisplayName + " with shortcut " + (i + 1) + ".");
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
                SetStatus("Placement started. LMB confirm, R rotate, Esc/RMB cancel.");
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
            if (source == null || destination == null) throw new InvalidOperationException("No available pile and storage route.");
            context.Work.CreateGroupHaulJobs(groupId, source.InventoryOwner, destination.Owner, source.ResourceId,
                new ResourceQuantity(1), workPriority);
        }

        private void CancelGroupJobs(WorkGroupId groupId)
        {
            foreach (var job in context.Work.Jobs.Where(x => x.GroupId == groupId && !x.IsTerminal).ToArray())
                context.Work.Cancel(job.Id);
        }

        private void CreatePlayerUnit()
        {
            var roster = context.Selection.SelectedCharacterIds.ToArray();
            if (roster.Length == 0) throw new InvalidOperationException("Select eligible characters first.");
            var captain = roster.FirstOrDefault(id => context.Health.GetCapabilities(id).CanFight);
            if (!captain.IsValid) throw new InvalidOperationException("No eligible captain in selection.");
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

        private string BuildingLockReason(BuildingDefinitionId id)
        {
            var technologies = context.Research.Catalog.All.Where(x => x.Unlocks.Any(unlock =>
                    unlock.Kind == TechnologyUnlockKind.Building && unlock.ContentId == id.ToString()))
                .Select(x => x.DisplayName).ToArray();
            return technologies.Length == 0 ? "Unavailable in the current era." : "Requires " + string.Join(" or ", technologies) + ".";
        }

        private string PlacementFailureText()
        {
            switch (context.BuildingPlacement.LastConstructionFailure)
            {
                case Game.Simulation.Resources.ConstructionFailureReason.LockedByResearch: return "Required technology is not complete.";
                case Game.Simulation.Resources.ConstructionFailureReason.InsufficientResources: return "Insufficient settlement resources.";
                default: return "Choose a clear, valid buildable grid area.";
            }
        }

        private string CharacterName(StableEntityId? id) => id.HasValue && context.Characters.TryGet(id.Value, out var character)
            ? character.Name.DisplayName : "none";

        private string DynastyRole(StableEntityId id)
        {
            if (context.Dynasty.CurrentMonarchId == id) return "Monarch";
            if (context.Dynasty.Contains(id)) return "Dynasty member";
            return "None";
        }

        private string InventoryText(ResourceInventory inventory)
        {
            var amounts = context.Resources.All.Select(x => new { x.DisplayName, Units = inventory.GetAmount(x.Id).Units })
                .Where(x => x.Units > 0).ToArray();
            return amounts.Length == 0 ? "empty" : string.Join(", ", amounts.Select(x => x.DisplayName + " " + x.Units));
        }

        private string UnitLabel(MilitaryUnitState unit, int index) =>
            (unit.OwnerSideId.Value == "player" ? "Player Unit " : "Known Rival Unit ") + index +
            (DeveloperUiVisible ? " [" + unit.Id + "]" : string.Empty);

        private static string BattleCounts(CombatEncounterState battle)
        {
            var active = battle.Combatants.Count(x => x.State != CombatantState.Dead &&
                x.State != CombatantState.Incapacitated && x.State != CombatantState.Disengaged);
            var dead = battle.Combatants.Count(x => x.State == CombatantState.Dead);
            var retreated = battle.Combatants.Count(x => x.State == CombatantState.Retreating ||
                x.State == CombatantState.Disengaged);
            return "active " + active + " / dead " + dead + " / retreated " + retreated;
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
                case DirectControlEligibilityFailure.Dead: return "Character is dead.";
                case DirectControlEligibilityFailure.Child: return "Character is a child.";
                case DirectControlEligibilityFailure.Incapacitated: return "Character is incapacitated.";
                case DirectControlEligibilityFailure.AggregateMilitaryUnit: return "Aggregate unit must return to Detailed mode.";
                case DirectControlEligibilityFailure.NotPlayerControlledRole: return "Only the monarch, heir or player captain is eligible.";
                default: return reason.ToString();
            }
        }

        private static string TravelReason(Game.Simulation.World.TravelOrderStatus status)
        {
            switch (status)
            {
                case Game.Simulation.World.TravelOrderStatus.NoDirectRoute: return "No valid route.";
                case Game.Simulation.World.TravelOrderStatus.UnitInBattle: return "Unit is already in battle.";
                case Game.Simulation.World.TravelOrderStatus.AlreadyTravelling: return "Unit is already travelling.";
                case Game.Simulation.World.TravelOrderStatus.UnitNotAggregate: return "Unit must be Aggregate on the global map.";
                default: return status.ToString();
            }
        }

        private static string Percent(int perThousand) => (perThousand / 10d).ToString("0") + "%";
        private static string FormatDuration(long ticks) => TimeSpan.FromTicks(ticks).TotalDays >= 1
            ? TimeSpan.FromTicks(ticks).TotalDays.ToString("0.0") + " days"
            : TimeSpan.FromTicks(ticks).TotalHours.ToString("0.0") + " hours";

        private bool IsTextInputFocused() => root?.panel?.focusController?.focusedElement is TextField;

        private void TryCommand(Action action)
        {
            try
            {
                action();
                SetStatus("Command accepted.");
                RefreshNow();
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message);
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
            return hash;
        }

        private void AddNavigation(string label, GameplayPanel panel)
        {
            navigation.Add(ActionButton(label, () => SetPanel(panel), "nav-" + panel.ToString().ToLowerInvariant()));
        }

        private static VisualElement Panel(string name)
        {
            var value = new VisualElement { name = name };
            StylePanel(value);
            return value;
        }

        private static void StylePanel(VisualElement value)
        {
            value.style.backgroundColor = PanelColor;
            value.style.color = TextColor;
            value.style.paddingLeft = 12; value.style.paddingRight = 12;
            value.style.paddingTop = 10; value.style.paddingBottom = 10;
        }

        private static Label HudLabel(float width)
        {
            var label = new Label();
            label.style.width = width;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static Button ActionButton(string label, Action action, string name = null)
        {
            var button = new Button(action) { text = label, name = name ?? string.Empty };
            button.style.marginTop = 3; button.style.marginBottom = 3;
            button.style.marginLeft = 2; button.style.marginRight = 2;
            button.style.minHeight = 28;
            button.style.backgroundColor = new Color(0.20f, 0.22f, 0.16f, 1f);
            button.style.color = TextColor;
            button.style.borderBottomColor = AccentColor;
            button.style.borderBottomWidth = 1;
            return button;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            return row;
        }

        private static void AddTitle(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.fontSize = 18;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 8;
            label.style.color = AccentColor;
            parent.Add(label);
        }

        private static void AddSection(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 9;
            label.style.marginBottom = 3;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
        }

        private static void AddLine(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 2;
            parent.Add(label);
        }

        private static void AddMuted(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new Color(0.68f, 0.68f, 0.62f, 1f);
            label.style.fontSize = 11;
            label.style.marginBottom = 3;
            parent.Add(label);
        }
    }

    internal static class GameplayUiContextExtensions
    {
        public static bool StarterEnemyDefeated(this GameplayUiContext context) =>
            context.GlobalMap.Settlements.Any(x => x.OwnerSideId.Value == "enemy") == false;
    }
}
