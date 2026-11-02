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
using Game.Domain.Domestic;
using Game.Domain.Diagnostics;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Presentation.Resources;
using Game.Presentation.World;
using Game.Simulation.Control;
using Game.Simulation.Military;
using Game.Simulation.Production;
using Game.Simulation.Research;
using Game.Simulation.Time;
using Game.Simulation.Events;
using Game.Simulation.Diagnostics;
using Game.Simulation.Work;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// One player-facing UI Toolkit projection over the existing canonical services. It owns only panel,
    /// selection and confirmation state; gameplay mutations are routed back through Simulation services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class GameplayUiController : MonoBehaviour, IWorldPointerUiBlocker, IWorldPointerUiRegionBlocker
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
        private P102StrategicWorldView strategicWorldView;
        private UIDocument document;
        private PanelSettings panelSettings;
        private VisualElement root;
        private VisualElement topBar;
        private VisualElement navigation;
        private VisualElement resourceStrip;
        private VisualElement resourceDrawer;
        private VisualElement residentStrip;
        private VisualElement commandRibbon;
        private VisualElement commandActions;
        private VisualElement commandQueue;
        private VisualElement bottomDock;
        private Label settlementLabel;
        private Label settlementStatusLabel;
        private Label selectionSummaryLabel;
        private Label selectionQueueLabel;
        private Label populationLabel;
        private Label placementFeedback;
        private ScrollView mainPanel;
        private ScrollView inspector;
        private VisualElement notifications;
        private VisualElement statusBar;
        private VisualElement tpsHud;
        private VisualElement modal;
        private VisualElement campaignOutcome;
        private VisualElement onboardingCard;
        private VisualElement localMiniMap;
        private VisualElement localMiniMapGrid;
        private VisualElement realmWorkspace;
        private VisualElement realmBottomBackdrop;
        private ScrollView realmLayers;
        private ScrollView realmDetail;
        private VisualElement realmRouteBar;
        private VisualElement realmMapHeader;
        private VisualElement realmMapOverlay;
        private ScrollView contextMenu;
        private Button onboardingToggleButton;
        private Label resourcesLabel;
        private Label dateLabel;
        private Label weatherLabel;
        private Label modeLabel;
        private Label alertLabel;
        private Label statusLabel;
        private Label tpsLabel;
        private Label gameOverLabel;
        private Label campaignOutcomeDetail;
        private readonly List<string> notificationFeed = new List<string>();
        private sealed class NotificationTarget
        {
            public StableEntityId? CharacterId;
            public StableEntityId? BuildingId;
            public WorldPosition? Position;
            public GameplayPanel? Panel;
        }
        private readonly List<NotificationTarget> notificationTargets = new List<NotificationTarget>();
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
        private bool navigationExpanded;
        private bool resourcesExpanded;
        private bool compactLayout;
        private bool campaignOutcomeAcknowledged;
        private BuildPaletteCategory buildCategory = BuildPaletteCategory.All;
        private TechnologyId? selectedResearchTechnology;
        private string peopleSearch = string.Empty;
        private StableEntityId? selectedDynastyPerson;
        private readonly HashSet<StableEntityId> militaryDraftSelection = new HashSet<StableEntityId>();
        private bool showEventJournal;
        private bool showMapSettlements = true;
        private bool showMapArmies = true;
        private bool showMapRoutes = true;
        private bool showMapWeather = true;
        private ContextCommandTarget? activeContextTarget;
        private StableEntityId[] activeContextSelectionIds = Array.Empty<StableEntityId>();
        private int lastUiPointerFrame = -2;
        private readonly Dictionary<ContextActionKind, ContextActionAvailability> contextActionStates =
            new Dictionary<ContextActionKind, ContextActionAvailability>();
        private readonly FirstHourOnboardingProgress onboarding = new FirstHourOnboardingProgress();

        private enum BuildPaletteCategory
        {
            All = 0,
            Resources = 1,
            Production = 2,
            Housing = 3,
            Food = 4,
            Storage = 5,
            Defense = 6,
            Infrastructure = 7
        }

        private static bool IsPeopleRiskWorkspace(GameplayPanel panel) =>
            panel == GameplayPanel.Domestic || panel == GameplayPanel.Dynasty ||
            panel == GameplayPanel.Events || panel == GameplayPanel.Military ||
            panel == GameplayPanel.Research || panel == GameplayPanel.Economy;

        public bool DeveloperUiVisible { get; private set; }
        public GameplayPanel ActivePanel => activePanel;
        public bool FullScreenWorkspaceVisible => IsPeopleRiskWorkspace(activePanel);
        public int SubscriptionCount => subscribed ? 4 : 0;
        public VisualElement RootVisual => root;
        public IReadOnlyList<string> Notifications => notificationFeed.AsReadOnly();
        public string Locale => RussianTextCatalog.Locale;
        public float UiScale => panelSettings?.scale ?? 1f;
        public bool OnboardingVisible => onboarding.Visible;
        public FirstHourStage OnboardingStage => onboarding.CurrentStage;
        public int OnboardingCompletedCount => onboarding.CompletedCount;
        public bool ContextMenuVisible => contextMenu != null && contextMenu.resolvedStyle.display != DisplayStyle.None;
        public ContextCommandTarget? ActiveContextTarget => activeContextTarget;
        public IReadOnlyDictionary<ContextActionKind, ContextActionAvailability> ContextActionStates => contextActionStates;
        public MilitaryAttackCommandResult LastMilitaryAttackResult { get; private set; }
        public DiagnosticExecutionResult LastDiagnosticResult { get; private set; }
        public WorldLocationId? SelectedMapLocation => selectedLocation;
        public MilitaryUnitId? SelectedMapUnit => selectedUnit;
        public GameplayUiRouteId? ActiveRoute => GameplayUiRoutes.TryForPanel(activePanel, out var route)
            ? route.Id
            : null;

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
            strategicWorldView = GetComponent<P102StrategicWorldView>();
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
            // UI Toolkit may hide a command menu from its click callback before SelectionProbe observes
            // the same physical release. Keep the pointer consumed through the following frame so that
            // clicking an action cannot fall through to the world and clear the stable selection.
            if (lastUiPointerFrame >= Time.frameCount - 1) return true;
            var screenPoint = new Vector2(point.x, Screen.height - point.y);
            var panelPoint = root.panel == null ? point : RuntimePanelUtils.ScreenToPanel(root.panel, screenPoint);
            // Geometry-only checks used to miss some nested ScrollView content. Picking asks UI Toolkit for the
            // actual element under the cursor and therefore blocks camera zoom over every visible menu child.
            var picked = root.panel?.Pick(panelPoint);
            if (picked != null && picked != root) return true;
            if (Blocks(topBar, panelPoint) || Blocks(navigation, panelPoint) || Blocks(mainPanel, panelPoint) ||
                Blocks(inspector, panelPoint) || Blocks(notifications, panelPoint) || Blocks(onboardingCard, panelPoint) ||
                Blocks(commandRibbon, panelPoint) || Blocks(tpsHud, panelPoint) || Blocks(contextMenu, panelPoint) ||
                Blocks(resourceDrawer, panelPoint) || Blocks(residentStrip, panelPoint) || Blocks(bottomDock, panelPoint) ||
                Blocks(localMiniMap, panelPoint) || Blocks(realmLayers, panelPoint) || Blocks(realmDetail, panelPoint) ||
                Blocks(realmRouteBar, panelPoint) || Blocks(realmMapHeader, panelPoint) ||
                Blocks(realmMapOverlay, panelPoint)) return true;
            return Blocks(modal, panelPoint);
        }

        public void SetPanel(GameplayPanel panel)
        {
            if (panel == GameplayPanel.Codes && !context.Clock().IsPaused)
            {
                SetStatus("Раздел кодов доступен только на паузе.");
                return;
            }
            var closing = activePanel == panel;
            if (panel == GameplayPanel.Military && !closing)
            {
                militaryDraftSelection.Clear();
                foreach (var id in context.Selection.SelectedCharacterIds)
                    if (IsAvailableMilitaryRecruit(id)) militaryDraftSelection.Add(id);
            }
            if (panel == GameplayPanel.Global && !closing && context.GlobalMap.Mode == GameWorldMode.Local)
            {
                if (!selectedLocation.HasValue && IsKnownLocation(context.GlobalMap.ActiveLocationId))
                    selectedLocation = context.GlobalMap.ActiveLocationId;
                if (!selectedUnit.HasValue)
                    selectedUnit = context.Military.Units.FirstOrDefault(value => value.OwnerSideId.Value == "player")?.Id;
                context.RequestEnterGlobal();
            }
            else if ((panel != GameplayPanel.Global || closing) &&
                     context.GlobalMap.Mode != GameWorldMode.Local)
                context.RequestEnterLocation(context.GlobalMap.ActiveLocationId);
            activePanel = closing ? GameplayPanel.None : panel;
            mainPanel.style.display = activePanel == GameplayPanel.None ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshNavigationSelection();
            ApplyPanelLayout();
            RefreshNow();
        }

        public void Navigate(GameplayUiRouteId routeId) => SetPanel(GameplayUiRoutes.Get(routeId).Panel);

        public void SelectMapLocation(WorldLocationId id)
        {
            if (!context.GlobalMap.Locations.Any(value => value.Id == id))
                throw new ArgumentException("Unknown map location.", nameof(id));
            if (context.ConnectedWorld != null && !context.ConnectedWorld.IsLocationRevealed(id))
                throw new InvalidOperationException("Only a known location may be selected.");
            selectedLocation = id;
            RefreshNow();
        }

        public void SelectMapUnit(MilitaryUnitId id)
        {
            if (!context.Military.TryGet(id, out _)) throw new ArgumentException("Unknown military unit.", nameof(id));
            selectedUnit = id;
            RefreshNow();
        }

        public bool FocusLocalMiniMap(float normalizedX, float normalizedZ)
        {
            if (context.ConnectedWorld == null || normalizedX < 0f || normalizedX > 1f ||
                normalizedZ < 0f || normalizedZ > 1f) return false;
            var layout = context.ConnectedWorld.Layout;
            var x = layout.OriginX + normalizedX * layout.WidthMeters;
            var z = layout.OriginZ + normalizedZ * layout.DepthMeters;
            if (!context.ConnectedWorld.IsRevealed(x, z))
            {
                SetStatus("Эта область ещё не разведана.");
                return false;
            }
            context.FocusWorld(new WorldPosition(x, 0f, z));
            SetStatus("Камера перемещена по известной карте.");
            RefreshNow();
            return true;
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
            RefreshResidentStrip();
            RefreshPlacementFeedback();
            RefreshOnboarding();
            CaptureNotifications();
            RefreshNotifications();
            RefreshInspector();
            RefreshCommandRibbon();
            RefreshPanel();
            RefreshWorldMapSurfaces();
            RefreshTps();
            RefreshGameOver();
            ApplyResponsiveLayout();
            lastRefreshHash = ComputeRefreshHash();
        }

        public void RefreshAfterStateRebind()
        {
            seenWarWarnings.Clear();
            campaignOutcomeAcknowledged = false;
            CloseContextMenu();
            RefreshNow();
            RepaintTextTree();
            if (root?.panel == null) return;
            root.schedule.Execute(() => { RefreshNow(); RepaintTextTree(); }).ExecuteLater(100);
            root.schedule.Execute(() => { RefreshNow(); RepaintTextTree(); }).ExecuteLater(750);
        }

        private void RepaintTextTree()
        {
            root?.Query<TextElement>().ForEach(element => element.MarkDirtyRepaint());
            root?.MarkDirtyRepaint();
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
            row.Add(ActionButton(T("common.load"), () => ConfirmLoadSlot(index), "confirm-load-slot-" + index));
            row.Add(ActionButton(T("common.cancel"), CloseModal, "cancel-load-slot-" + index));
            modal.Add(row);
            FocusWhenReady("cancel-load-slot-" + index, modal);
        }

        public void ConfirmLoadSlot(int index) => TryCommand(() =>
        {
            suppressNotificationCaptureOnce = true;
            context.Saves.Load(index);
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
            row.Add(ActionButton(T("common.cancel"), CloseModal, "cancel-new-game"));
            modal.Add(row);
            FocusWhenReady("cancel-new-game", modal);
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
            FocusWhenReady("cancel-main-menu", modal);
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
            if (ContextMenuVisible && Mouse.current?.middleButton.wasPressedThisFrame == true) CloseContextMenu();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && resourcesExpanded)
            { CloseResourceDrawer(); return; }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame &&
                modal != null && modal.resolvedStyle.display != DisplayStyle.None)
            {
                CloseModal();
                return;
            }
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
            input.SecondaryClicked += OnSecondaryClicked;
            input.PrimaryClicked += OnPrimaryDismissContext;
            context.BuildingPlacement.BuildMenuRequested += OnBuildMenuRequested;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || input == null || context == null) return;
            input.PauseToggled -= OnPauseToggle;
            input.TimeSlowerRequested -= OnSlower;
            input.TimeFasterRequested -= OnFaster;
            input.SecondaryClicked -= OnSecondaryClicked;
            input.PrimaryClicked -= OnPrimaryDismissContext;
            context.BuildingPlacement.BuildMenuRequested -= OnBuildMenuRequested;
            subscribed = false;
        }

        private void OnPauseToggle()
        {
            if (!DeveloperUiVisible && !IsTextInputFocused()) TogglePause();
        }

        public void TogglePause() => context.SetPaused(!context.Clock().IsPaused);

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

        private void OnSecondaryClicked()
        {
            if (context.BuildingPlacement.WasCancelledThisFrame) return;
            if (DeveloperUiVisible || IsTextInputFocused() || context.BuildingPlacement.IsActive) return;
            if (!raycaster.TryGetContextHit(input.PointerPosition, out var hit) ||
                !TryResolveContextTarget(hit, out var target))
            {
                CloseContextMenu();
                return;
            }
            if (context.Selection.SelectedCharacterIds.Count == 0)
            {
                if (target.Kind == ContextTargetKind.Building) OpenContextMenu(target, input.PointerPosition);
                else CloseContextMenu();
                return;
            }
            // Ground remains a direct move; objects expose only their applicable commands.
            var primary = target.Kind == ContextTargetKind.Ground ? ContextActionKind.Move :
                target.Kind == ContextTargetKind.ResourceSource ?
                    target.ResourceClass == ContextResourceClass.Choppable ? ContextActionKind.Chop :
                    target.ResourceClass == ContextResourceClass.Mineable ? ContextActionKind.Mine : ContextActionKind.Gather :
                (ContextActionKind?)null;
            if (target.Kind == ContextTargetKind.Ground && primary.HasValue && Keyboard.current?.ctrlKey.isPressed != true &&
                EvaluateForSelection(primary.Value, target).Enabled)
            {
                activeContextTarget = target;
                activeContextSelectionIds = context.Selection.SelectedCharacterIds.ToArray();
                ExecuteContextAction(primary.Value, input.IsAdditiveSelectionPressed);
            }
            else OpenContextMenu(target, input.PointerPosition);
        }

        private void OnPrimaryDismissContext()
        {
            // Menu buttons consume their click first; outside clicks close the menu without
            // preventing the normal world selection. MMB dismissal is checked in Update.
            if (ContextMenuVisible && !IsPointerInsideContextMenu()) CloseContextMenu();
        }

        private bool IsPointerInsideContextMenu()
        {
            if (contextMenu == null || input == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(root.panel,
                new Vector2(input.PointerPosition.x, Screen.height - input.PointerPosition.y));
            return contextMenu.worldBound.Contains(point);
        }

        public void OpenContextMenu(ContextCommandTarget target, Vector2 screenPosition)
        {
            if (contextMenu == null) return;
            activeContextTarget = target;
            activeContextSelectionIds = context.Selection.SelectedCharacterIds.ToArray();
            contextActionStates.Clear();
            contextMenu.Clear();
            AddTitle(contextMenu, "Контекстные действия");
            AddMuted(contextMenu, ContextTargetName(target) + " · жителей: " +
                context.Selection.SelectedCharacterIds.Count);
            if (context.Selection.SelectedCharacterIds.Count == 0)
            {
                if (target.Kind == ContextTargetKind.Building && context.Buildings.TryGet(target.EntityId, out var settingsBuilding))
                    AddBuildingSettingsContext(settingsBuilding);
                PositionContextMenu(screenPosition);
                contextMenu.style.display = DisplayStyle.Flex;
                return;
            }
            var isCropTarget = TryGetCropTarget(target, out var cropTarget);
            var isPierTarget = TryGetFishingPierTarget(target,out var pierTarget);
            foreach (ContextActionKind action in Enum.GetValues(typeof(ContextActionKind)))
            {
                if (action == ContextActionKind.Gather && isCropTarget) continue;
                var actionTarget = action==ContextActionKind.Gather && isPierTarget ? pierTarget : target;
                if (action == ContextActionKind.Build && actionTarget.Kind == ContextTargetKind.Building)
                    actionTarget = ResolveBuildingWorkTarget(actionTarget, activeContextSelectionIds);
                if (!ContextActionRules.SupportsTarget(action, actionTarget)) continue;
                if (target.Kind == ContextTargetKind.Building && context.Buildings.TryGet(target.EntityId, out var menuBuilding))
                {
                    if (action == ContextActionKind.Build && menuBuilding.IsOperational) continue;
                    if (action == ContextActionKind.Craft && (!menuBuilding.IsOperational || !context.Production.Sites.Any(s => s.OwnerBuildingId == menuBuilding.Id))) continue;
                    if (action == ContextActionKind.Attack && menuBuilding.OwnerSideId.Value == "player") continue;
                    if (action == ContextActionKind.Guard && !CanGuardAt(menuBuilding)) continue;
                }
                var attackState = action == ContextActionKind.Attack ? EvaluateMilitaryAttack(target) : default;
                var state = action == ContextActionKind.Attack
                    ? new ContextActionAvailability(attackState.Enabled, attackState.Enabled
                        ? ContextUnavailableReason.None : ContextUnavailableReason.WrongRole)
                    : EvaluateForSelection(action, actionTarget);
                contextActionStates[action] = state;
                var captured = action;
                var label = ContextActionName(action);
                if(action==ContextActionKind.Gather && isPierTarget)label="Ловить рыбу";
                if (action == ContextActionKind.Craft && TryGetFishingCampTarget(target, out _)) label = "Ловить рыбу";
                if (action == ContextActionKind.Gather && context.Production.TryGetSite(target.EntityId, out var menuSite))
                    label = menuSite.RecipeId.ToString() == "fish_water" ? "Ловить рыбу" :
                        menuSite.RecipeId.ToString() == "hunt_animal" ? (context.Production.Animals.IsAlive(menuSite.Id) ? "Охотиться с луком" : "Освежевать") : label;
                var button = ActionButton(label, () =>
                {
                    if (captured == ContextActionKind.Attack) ExecuteMilitaryAttack();
                    else ExecuteContextAction(captured, Keyboard.current != null && Keyboard.current.shiftKey.isPressed);
                }, "context-" + action.ToString().ToLowerInvariant());
                button.SetEnabled(state.Enabled);
                if (state.Enabled) button.AddToClassList(SerenityUiDesign.PrimaryButtonClass);
                contextMenu.Add(button);
                AddMuted(contextMenu, action == ContextActionKind.Attack
                    ? (attackState.Enabled ? "Доступно" : "Недоступно: " + MilitaryAttackReason(attackState.Reason))
                    : state.Reason == ContextUnavailableReason.None
                        ? "Доступно" : (state.Delayed ? "Задержка: " : "Недоступно: ") +
                            ContextReason(state.Reason, action, actionTarget));
            }
            if (target.Kind == ContextTargetKind.Building &&
                context.Buildings.TryGet(target.EntityId, out var actionBuilding) &&
                actionBuilding.DefinitionId.FamilyId == MilitaryTrainingService.BarracksDefinitionId)
                AddBarracksTrainingContext(actionBuilding);
            if (isCropTarget) AddCropActions(cropTarget);
            AddMuted(contextMenu, "Обычный выбор отменяет прежнюю очередь. Shift + выбор добавляет приказ в хвост.");
            PositionContextMenu(screenPosition);
            contextMenu.style.display = DisplayStyle.Flex;
        }

        private void AddCropActions(ContextCommandTarget cropTarget)
        {
            if (!context.Production.TryGetSite(cropTarget.EntityId, out var site)) return;
            var availability = EvaluateForSelection(ContextActionKind.Gather, cropTarget);
            var canPlant = site.CropPhase == CropPlotPhase.Unplanted && availability.Enabled;
            var canHarvest = site.CropPhase == CropPlotPhase.Ready && availability.Enabled;
            contextActionStates[ContextActionKind.Gather] = new ContextActionAvailability(
                site.CropPhase == CropPlotPhase.Unplanted ? canPlant : canHarvest,
                availability.Enabled ? ContextUnavailableReason.None : availability.Reason);
            var plant = ActionButton("Выращивать", () => ExecuteCropContextAction(site.Id,CropPlotPhase.Unplanted), "context-crop-plant");
            plant.SetEnabled(canPlant);
            if (canPlant) plant.AddToClassList(SerenityUiDesign.PrimaryButtonClass);
            contextMenu.Add(plant);
            AddMuted(contextMenu, site.CropPhase == CropPlotPhase.Unplanted
                ? (canPlant ? "Доступно" : "Недоступно: " + ContextReason(availability.Reason,
                    ContextActionKind.Gather, cropTarget))
                : "Доступно только на незасеянном поле");
            var harvest = ActionButton("Собрать урожай", () => ExecuteCropContextAction(site.Id,CropPlotPhase.Ready), "context-crop-harvest");
            harvest.SetEnabled(canHarvest);
            if (canHarvest) harvest.AddToClassList(SerenityUiDesign.PrimaryButtonClass);
            contextMenu.Add(harvest);
            AddMuted(contextMenu, site.CropPhase == CropPlotPhase.Ready
                ? (canHarvest ? "Урожай созрел · " + CropPercent(site) : "Недоступно: " +
                    ContextReason(availability.Reason, ContextActionKind.Gather, cropTarget))
                : "Сбор доступен при созревании: " + CropPercent(site));
        }

        private void AddBuildingSettingsContext(Building building)
        {
            AddMuted(contextMenu, "Здание 1 уровня");
            AddBuildingPolicyControls(contextMenu, building);
            var storageOwner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id);
            if (context.Inventories.TryGet(storageOwner, out var storage))
            {
                var issuance = ActionButton(storage.IssuanceLocked ? "Разрешить выдачу" : "Запретить выдачу", () =>
                    TryCommand(() => storage.SetIssuanceLocked(!storage.IssuanceLocked)), "building-storage-issuance");
                contextMenu.Add(issuance);
            }
            var open = ActionButton("Открыть карточку здания", () =>
            {
                var presenter = FindObjectsByType<Game.Presentation.Buildings.BuildingPresenter>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(x => x.IsBound && x.BuildingId == building.Id);
                if (presenter != null) context.Selection.SelectBuilding(presenter);
                CloseContextMenu();
                RefreshNow();
            }, "building-open-settings");
            contextMenu.Add(open);
            AddMuted(contextMenu, "Ремонт и демонтаж находятся в карточке здания.");
        }

        private void AddBarracksTrainingContext(Building barracks)
        {
            var side = new CombatSideId("player");
            var sources = context.Inventories.All.Select(x => x.Owner).ToArray();
            var roster = activeContextSelectionIds.OrderBy(x => x).ToArray();
            var failures = roster.ToDictionary(id => id, id => context.MilitaryTraining.CheckStart(
                id, barracks.Id, side, context.Clock().State.CalendarTicks, sources));
            var button = ActionButton("Стать военным / обучаться", () => TryCommand(() =>
            {
                var started = 0;
                foreach (var id in roster)
                {
                    var result = context.MilitaryTraining.TryStart(id, barracks.Id, side,
                        context.Clock().State.CalendarTicks, sources);
                    if (!result.Started) continue;
                    if (context.Work.Groups.TryGetForMember(id, out var group))
                        context.Work.RemoveMember(group.Id, id);
                    context.Facilities.UnassignWorkplace(id);
                    started++;
                }
                SetStatus($"Начали курс: {started}. Свободных мест в казарме: {Math.Max(0, 6 - context.MilitaryTraining.Active.Count)}.");
                CloseContextMenu();
                RefreshNow();
            }), "barracks-train-selection");
            button.SetEnabled(failures.Values.Any(x => x == MilitaryTrainingStartFailure.None));
            contextMenu.Add(button);
            var firstFailure = failures.FirstOrDefault(x => x.Value != MilitaryTrainingStartFailure.None);
            if (!button.enabledSelf && roster.Length > 0)
                AddMuted(contextMenu, "Обучение недоступно: " +
                    MilitaryTrainingRequirementText(firstFailure.Key, firstFailure.Value));
        }

        private void AddBuildingPolicyControls(VisualElement parent, Building building)
        {
            if (context.Facilities == null) return;
            var policy = context.Facilities.GetPolicy(building.Id);
            var definition = context.BuildingCatalog.ResolveDefinition(building.DefinitionId);
            var hasWork = definition.Workplace != null || context.Production.Sites.Any(site =>
                site.OwnerBuildingId == building.Id || site.Id == building.Id);
            if (hasWork)
            {
                AddMuted(parent, "Приоритет работы");
                var priorityRow = Row();
                foreach (var value in new[] { BuildingWorkPriority.Low, BuildingWorkPriority.Normal,
                             BuildingWorkPriority.High, BuildingWorkPriority.Emergency })
                {
                    var captured = value;
                    var button = ActionButton(policy.Priority == value ? "●" : "○", () =>
                        TryCommand(() => context.Facilities.SetPolicy(building.Id, captured, policy.Shift,
                            policy.WorkPaused, policy.GateAccess)), "building-priority-" + value.ToString().ToLowerInvariant());
                    button.tooltip = value == BuildingWorkPriority.Low ? "Низкий приоритет" :
                        value == BuildingWorkPriority.Normal ? "Обычный приоритет" :
                        value == BuildingWorkPriority.High ? "Высокий приоритет" : "Аварийный приоритет";
                    button.style.color = value == BuildingWorkPriority.Low ? new StyleColor(new Color(.45f, .75f, 1f)) :
                        value == BuildingWorkPriority.Normal ? new StyleColor(new Color(.55f, .9f, .55f)) :
                        value == BuildingWorkPriority.High ? new StyleColor(new Color(1f, .78f, .3f)) :
                        new StyleColor(new Color(1f, .3f, .25f));
                    SerenityUiDesign.SetSelected(button, policy.Priority == value);
                    priorityRow.Add(button);
                }
                parent.Add(priorityRow);
                parent.Add(ActionButton(policy.WorkPaused ? "Возобновить работу" : "Остановить работу", () =>
                    TryCommand(() => context.Facilities.SetPolicy(building.Id, policy.Priority, policy.Shift,
                        !policy.WorkPaused, policy.GateAccess)), "building-work-toggle"));
            }

            if (building.DefinitionId.FamilyId == new BuildingDefinitionId("gate"))
            {
                AddMuted(parent, "Режим ворот");
                foreach (var value in new[] { GateAccessMode.Open, GateAccessMode.Closed,
                             GateAccessMode.AlliesOnly, GateAccessMode.Nobody })
                {
                    var captured = value;
                    var label = value == GateAccessMode.Open ? "Открыто" : value == GateAccessMode.Closed
                        ? "Закрыто" : value == GateAccessMode.AlliesOnly ? "Пропуск своих" : "Запрет всем";
                    var button = ActionButton((policy.GateAccess == value ? "✓ " : string.Empty) + label, () =>
                        TryCommand(() =>
                        {
                            context.Facilities.SetPolicy(building.Id, policy.Priority, policy.Shift,
                                policy.WorkPaused, captured);
                            context.BuildingPathing?.SetGateAccessMode(building.Id, captured);
                        }), "gate-access-" + value.ToString().ToLowerInvariant());
                    SerenityUiDesign.SetSelected(button, policy.GateAccess == value);
                    parent.Add(button);
                }
                var detained = context.BuildingPathing?.DetainedTravelers
                    .Where(x => x.Value == building.Id).OrderBy(x => x.Key).ToArray();
                if (detained != null && detained.Length > 0)
                {
                    AddMuted(parent, "Задержаны у ворот: " + detained.Length);
                    foreach (var traveler in detained)
                    {
                        var capturedTraveler = traveler.Key;
                        var row = Row();
                        row.Add(ActionButton("Пропустить", () => TryCommand(() =>
                            context.BuildingPathing.ResolveDetention(capturedTraveler, true))));
                        row.Add(ActionButton("Не пропускать", () => TryCommand(() =>
                            context.BuildingPathing.ResolveDetention(capturedTraveler, false))));
                        parent.Add(row);
                    }
                }
            }
        }

        private bool CanGuardAt(Building building)
        {
            var definition = context.BuildingCatalog.ResolveDefinition(building.DefinitionId);
            return building.DefinitionId.FamilyId == new BuildingDefinitionId("gate") ||
                building.DefinitionId.FamilyId == new BuildingDefinitionId("chief_hut") ||
                definition.Category == BuildingCategory.Military;
        }

        private static string CropPercent(ProductionSite site) =>
            site.CropPhase == CropPlotPhase.Ready ? "100%" : site.GrowthRequiredTicks <= 0 ? "0%" :
                RussianTextCatalog.Percent(Math.Clamp((double)site.GrowthProgressTicks / site.GrowthRequiredTicks,0d,1d));

        private void ExecuteCropContextAction(StableEntityId id,CropPlotPhase expectedPhase)
        {
            if(!context.Production.TryGetSite(id,out var current)||current.CropPhase!=expectedPhase)return;
            ExecuteContextAction(ContextActionKind.Gather,Keyboard.current!=null && Keyboard.current.shiftKey.isPressed);
        }

        private static string DomesticAnimalName(string species) => species switch
        {
            "cow"=>"Корова", "pig"=>"Свинья", "goat"=>"Коза", "duck"=>"Утка",
            "chicken"=>"Курица", "horse"=>"Лошадь", _=>"Овца"
        };

        private static string LivestockModeName(LivestockOperationMode mode) => mode switch
        {
            LivestockOperationMode.Breeding => "Разведение",
            LivestockOperationMode.Products => "Продукция",
            _ => "Сбалансированный"
        };

        private void DrawCropWorkButtons(ProductionSite site)
        {
            var canCommand=context.Buildings.TryGet(site.Id,out var owner) && owner.OwnerSideId.Value=="player" && owner.IsOperational;
            var plant=ActionButton("Выращивать",()=>TryCommand(()=>IssueDirect(WorkJobType.Farm,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite,site.Id,site.Position))),"crop-plant");
            plant.SetEnabled(canCommand && site.CropPhase==CropPlotPhase.Unplanted);
            inspector.Add(plant);
            var harvest=ActionButton("Собрать урожай",()=>TryCommand(()=>IssueDirect(WorkJobType.Farm,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite,site.Id,site.Position))),"crop-harvest");
            harvest.SetEnabled(canCommand && site.CropPhase==CropPlotPhase.Ready);
            inspector.Add(harvest);
        }

        public ContextCommandResult LastContextCommandResult { get; private set; }

        public bool TryGetCropTarget(ContextCommandTarget target, out ContextCommandTarget cropTarget)
        {
            cropTarget = default;
            if (target.Kind != ContextTargetKind.Building || !context.Buildings.TryGet(target.EntityId, out var garden) ||
                !garden.IsOperational || garden.OwnerSideId.Value != "player" ||
                garden.DefinitionId.FamilyId.ToString()=="animal_pasture" ||
                !context.Production.TryGetSite(garden.Id, out var site) || site.Kind != ProductionSiteKind.PrimitiveCropPlot)
                return false;
            cropTarget = new ContextCommandTarget(ContextTargetKind.ResourceSource, site.Position,
                site.Id, resourceClass: ContextResourceClass.Gatherable);
            return true;
        }

        public bool TryGetFishingCampTarget(ContextCommandTarget target, out ContextCommandTarget fishTarget)
        {
            fishTarget = default;
            if (target.Kind != ContextTargetKind.Building || !context.Buildings.TryGet(target.EntityId, out var camp) ||
                camp.DefinitionId.FamilyId.ToString() != "fishing_camp" || !camp.IsOperational || camp.OwnerSideId.Value != "player") return false;
            if (!context.Production.Sites.Any(s => s.OwnerBuildingId == camp.Id && s.RecipeId.ToString() == "fish_camp")) return false;
            fishTarget = target;
            return true;
        }

        private bool TryGetFishingPierTarget(ContextCommandTarget target,out ContextCommandTarget fishTarget)
        {
            fishTarget=default;
            if(target.Kind!=ContextTargetKind.Building || !context.Buildings.TryGet(target.EntityId,out var pier) ||
                pier.DefinitionId.FamilyId.ToString()!="fishing_pier" || !pier.IsOperational || pier.OwnerSideId.Value!="player")return false;
            var stock=context.Production.FishStockAt(target.Position);
            if(stock==null)return false;
            foreach(var resident in activeContextSelectionIds)context.Production.RequestedFishingPiers[resident]=pier.Id;
            fishTarget=new ContextCommandTarget(ContextTargetKind.ResourceSource,stock.Position,stock.Id,
                new ResourceId("fish"),ContextResourceClass.Gatherable);
            return true;
        }

        public ContextCommandResult ExecuteContextAction(ContextActionKind action, bool append)
        {
            if (!activeContextTarget.HasValue) throw new InvalidOperationException("Контекстная цель не выбрана.");
            var target = activeContextTarget.Value;
            if (action == ContextActionKind.Gather && TryGetCropTarget(target, out var cropTarget)) target = cropTarget;
            if (action == ContextActionKind.Gather && TryGetFishingPierTarget(target, out var pierTarget)) target = pierTarget;
            if (action == ContextActionKind.Gather && TryGetFishingCampTarget(target, out var fishingTarget))
            { target = fishingTarget; action = ContextActionKind.Craft; }
            var roster = activeContextSelectionIds.ToArray();
            if (action == ContextActionKind.Build && target.Kind == ContextTargetKind.Building)
                target = ResolveBuildingWorkTarget(target, roster);
            var destination = action == ContextActionKind.Haul ? FindHaulDestination() : null;
            InventoryOwner? source = null;
            if (action == ContextActionKind.Haul && target.Kind == ContextTargetKind.ResourcePile)
                source = new InventoryOwner(InventoryOwnerKind.WorldPile, target.EntityId);
            var result = context.ContextCommands.Issue(roster, action, target,
                append, id => BuildContextFacts(id, action, target), source, destination,
                action == ContextActionKind.Haul ? new ResourceQuantity(1) : default);
            LastContextCommandResult = result;
            var delayed = result.Entries.Count(x => x.Order != null && x.Availability.Delayed);
            SetStatus(ContextActionName(action) + ": приказов " + result.Orders.Count +
                (delayed > 0 ? ", с задержкой " + delayed : string.Empty));
            CloseContextMenu();
            // Restore after UI dispatch as well: this covers the release-frame ordering difference between
            // UI Toolkit and MonoBehaviour Update in a normal Player without changing the canonical roster.
            root.schedule.Execute(() => context.Selection.SelectCharacterIds(roster)).ExecuteLater(0);
            RefreshNow();
            return result;
        }

        private ContextCommandTarget ResolveBuildingWorkTarget(ContextCommandTarget target,
            IReadOnlyList<StableEntityId> roster)
        {
            var searchRadius = 12f;
            if (context.Buildings.TryGet(target.EntityId, out var building))
            {
                var footprint = context.BuildingCatalog.ResolveDefinition(building.DefinitionId).Footprint;
                searchRadius = Mathf.Clamp(Mathf.Max(footprint.Width, footprint.Depth) * 3f + 8f, 12f, 32f);
            }
            foreach (var id in roster)
            {
                if (!context.Presentations.TryGet(id, out var presenter)) continue;
                var destination = new Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                if (!Game.Presentation.AI.ReachableNavMeshPoint.TryFind(presenter.transform.position,
                    destination, out var reachable, searchRadius)) continue;
                return new ContextCommandTarget(target.Kind,
                    new WorldPosition(reachable.x, reachable.y, reachable.z), target.EntityId,
                    target.ResourceId, target.ResourceClass);
            }
            return target;
        }

        public MilitaryAttackCommandResult ExecuteMilitaryAttack()
        {
            if (!activeContextTarget.HasValue) throw new InvalidOperationException("Контекстная цель не выбрана.");
            var target = ToMilitaryTarget(activeContextTarget.Value);
            var result = context.MilitaryAttack.Issue(activeContextSelectionIds, target,
                context.Clock().State.CalendarTicks, SelectionHasPath(target.Position));
            LastMilitaryAttackResult = result;
            SetStatus(result.Issued ? "Напасть: военные физически идут к цели."
                : "Напасть недоступно: " + MilitaryAttackReason(result.Availability.Reason));
            CloseContextMenu(); RefreshNow(); return result;
        }

        private MilitaryAttackAvailability EvaluateMilitaryAttack(ContextCommandTarget target)
        {
            var militaryTarget = ToMilitaryTarget(target);
            return context.MilitaryAttack.Evaluate(context.Selection.SelectedCharacterIds, militaryTarget,
                SelectionHasPath(militaryTarget.Position));
        }

        private MilitaryDirectTarget ToMilitaryTarget(ContextCommandTarget target)
        {
            if (target.Kind == ContextTargetKind.Character)
            {
                var kind = context.Military.TryGetForMember(target.EntityId, out _)
                    ? MilitaryDirectTargetKind.EnemyMilitary : MilitaryDirectTargetKind.Civilian;
                return new MilitaryDirectTarget(kind, target.Position, target.EntityId);
            }
            if (target.Kind == ContextTargetKind.Building)
                return new MilitaryDirectTarget(MilitaryDirectTargetKind.EnemyMilitaryBuilding,
                    target.Position, target.EntityId);
            return new MilitaryDirectTarget(MilitaryDirectTargetKind.Area, target.Position);
        }

        private bool SelectionHasPath(WorldPosition target) => context.Selection.SelectedCharacterIds
            .Where(id => context.AiAgents.TryGet(id, out _)).All(id => HasContextPath(id, target));

        public void CloseContextMenu()
        {
            activeContextTarget = null;
            activeContextSelectionIds = Array.Empty<StableEntityId>();
            contextActionStates.Clear();
            if (contextMenu != null) contextMenu.style.display = DisplayStyle.None;
        }

        private ContextActionAvailability EvaluateForSelection(ContextActionKind action, ContextCommandTarget target)
        {
            var states = context.Selection.SelectedCharacterIds
                .Select(id => ContextActionRules.Evaluate(action, target, BuildContextFacts(id, action, target), true))
                .ToArray();
            var ready = states.FirstOrDefault(x => x.Enabled && !x.Delayed);
            if (ready.Enabled) return ready;
            var delayed = states.FirstOrDefault(x => x.Enabled);
            if (delayed.Enabled) return delayed;
            return states.Length == 0
                ? new ContextActionAvailability(false, ContextUnavailableReason.InvalidStatus)
                : states[0];
        }

        private ContextActionFacts BuildContextFacts(StableEntityId id, ContextActionKind action,
            ContextCommandTarget target)
        {
            var exists = context.Characters.TryGet(id, out var character);
            var capabilities = exists ? context.Health.GetCapabilities(id) : default;
            var resourceAvailable = true;
            var technology = true;
            var capacity = true;
            var targetStatusValid = true;
            var pathTarget = target.Position;
            if (target.Kind == ContextTargetKind.ResourceSource && context.Production.TryGetSite(target.EntityId, out var site))
            {
                resourceAvailable = !site.IsDepleted && site.IsWorkAvailable;
                technology = context.Production.IsRecipeAvailable(site.RecipeId, true);
                if(context.Production.WorkPosition!=null)pathTarget=context.Production.WorkPosition(id,site);
                if(site.RecipeId.ToString()=="fish_water" && context.Production.ManualFishingAccess!=null)
                    resourceAvailable &= context.Production.ManualFishingAccess(site.Id);
            }
            else if (target.Kind == ContextTargetKind.ResourcePile && target.ResourceId.HasValue)
            {
                var owner = new InventoryOwner(InventoryOwnerKind.WorldPile, target.EntityId);
                resourceAvailable = context.Inventories.TryGet(owner, out var inventory) &&
                    inventory.GetAmount(target.ResourceId.Value).Units > 0;
                if (action == ContextActionKind.Haul) capacity = FindHaulDestination().HasValue;
            }
            else if (action == ContextActionKind.Build && target.Kind == ContextTargetKind.Building)
            {
                targetStatusValid = context.Buildings.TryGet(target.EntityId, out var building) &&
                    !building.IsOperational && !building.IsDestroyed;
            }
            else if (action == ContextActionKind.Craft && target.Kind == ContextTargetKind.Building)
            {
                targetStatusValid = context.Buildings.TryGet(target.EntityId, out var building) &&
                    building.IsOperational && context.Production.Sites.Any(x =>
                        x.Kind == ProductionSiteKind.BuildingWorkplace && x.OwnerBuildingId == building.Id &&
                        x.IsWorkAvailable);
                var workplace=context.Production.Sites.FirstOrDefault(x=>x.OwnerBuildingId==target.EntityId && x.IsWorkAvailable);
                if(workplace!=null && context.Production.WorkPosition!=null)pathTarget=context.Production.WorkPosition(id,workplace);
            }
            var busy = context.Work.HasOpenJob(id);
            var criticalNeed = context.AiAgents.TryGet(id, out var agent) &&
                (agent.Needs.Energy <= Game.Simulation.AI.Tier1AiScheduler.CriticalRestEnergy ||
                 agent.Needs.Hunger >= Game.Simulation.AI.Tier1AiScheduler.CriticalHunger);
            var interruptible = !busy || context.AiScheduler.CanInterruptForDirectWork(id);
            var withinDistance = true;
            var hunting = action == ContextActionKind.Gather && target.Kind == ContextTargetKind.ResourceSource &&
                context.Production.TryGetSite(target.EntityId, out var huntedSite) && huntedSite.RecipeId.ToString() == "hunt_animal";
            var correctRole = context.PlayerCharacterIds().Contains(id) &&
                (HasContextRole(id, action, hunting) || hunting && context.Military.TryGetForMember(id, out _));
            // A confirmed construction already has an authoritative buildable footprint. Its live
            // interaction point is resolved again by the build action as nearby NavMesh tiles and
            // obstacles change; rejecting the order here made valid shore construction permanently
            // unavailable while its remote tile was not the resident's current presentation tile.
            var pathAvailable = action == ContextActionKind.Build && target.Kind == ContextTargetKind.Building ||
                                HasContextPath(id, pathTarget);
            return new ContextActionFacts(exists && character.LifeState == CharacterLifeState.Alive,
                exists && capabilities.IsAdult, correctRole,
                exists && targetStatusValid && (action == ContextActionKind.Move ||
                           capabilities.CanPerformOrdinaryWork),
                true, technology, withinDistance, pathAvailable, resourceAvailable,
                capacity, busy, true, criticalNeed, interruptible);
        }

        private bool HasContextRole(StableEntityId id, ContextActionKind action, bool hunting = false)
        {
            if (context.MilitaryTraining.GetRank(id).HasValue &&
                action != ContextActionKind.Move && action != ContextActionKind.Attack &&
                action != ContextActionKind.Guard)
                return false;
            if (!context.Work.Groups.TryGetForMember(id, out var group)) return true;
            WorkGroupRole required;
            switch (action)
            {
                case ContextActionKind.Gather:
                    required = hunting ? WorkGroupRole.Hunters : WorkGroupRole.Gatherers; break;
                case ContextActionKind.Mine:
                case ContextActionKind.Chop: required = WorkGroupRole.Gatherers; break;
                case ContextActionKind.Haul: required = WorkGroupRole.Haulers; break;
                case ContextActionKind.Build: required = WorkGroupRole.Builders; break;
                case ContextActionKind.Craft: required = WorkGroupRole.Crafters; break;
                case ContextActionKind.Guard: required = WorkGroupRole.Warriors; break;
                default: return true;
            }
            return group.Role == required || group.Role == WorkGroupRole.General;
        }

        private static float DistanceSquared(WorldPosition left, WorldPosition right)
        {
            var x = left.X - right.X;
            var y = left.Y - right.Y;
            var z = left.Z - right.Z;
            return x * x + y * y + z * z;
        }

        private bool HasContextPath(StableEntityId id, WorldPosition target)
        {
            if (!context.AiAgents.TryGet(id, out var agent)) return true;
            var origin=new Vector3(agent.Position.X,agent.Position.Y,agent.Position.Z);
            if(context.Presentations.TryGet(id,out var presenter))origin=presenter.transform.position;
            return Game.Presentation.AI.ReachableNavMeshPoint.TryFind(
                origin,
                new Vector3(target.X, target.Y, target.Z), out _);
        }

        private InventoryOwner? FindHaulDestination()
        {
            var inventory = context.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage &&
                    context.Buildings.TryGet(x.Owner.Id, out var building) && building.IsOperational &&
                    building.OwnerSideId.Value == "player" &&
                    x.TotalUnits < x.Capacity).OrderBy(x => x.Owner.Id).FirstOrDefault();
            return inventory?.Owner;
        }

        private bool TryResolveContextTarget(RaycastHit hit, out ContextCommandTarget target)
        {
            var position = new WorldPosition(hit.point.x, hit.point.y, hit.point.z);
            var wildlifeTarget = hit.collider.GetComponentInParent<Game.Presentation.Environment.WorldMapWildlifeView>();
            if (wildlifeTarget != null && context.Production.TryGetSite(wildlifeTarget.StableId, out var preySite))
            {
                target = new ContextCommandTarget(ContextTargetKind.ResourceSource, preySite.Position, preySite.Id,
                    new ResourceId("meat"), ContextResourceClass.Gatherable);
                return true;
            }
            var source = hit.collider.GetComponentInParent<Game.Presentation.Production.ProductionSourcePresenter>();
            if (source != null && source.IsBound && source.Site.Kind == ProductionSiteKind.NaturalSource)
            {
                var recipe = context.Production.Recipes.Get(source.Site.RecipeId);
                var resource = recipe.Outputs.Count > 0 ? (ResourceId?)recipe.Outputs[0].ResourceId : null;
                var resourceClass = ResourceClass(resource);
                target = new ContextCommandTarget(ContextTargetKind.ResourceSource, source.Site.Position,
                    source.Site.Id, resource, resourceClass);
                return true;
            }
            var pile = hit.collider.GetComponentInParent<WorldResourcePilePresenter>();
            if (pile != null)
            {
                target = new ContextCommandTarget(ContextTargetKind.ResourcePile, position, pile.PileId,
                    pile.ResourceId);
                return true;
            }
            var character = hit.collider.GetComponentInParent<Game.Presentation.Characters.CharacterPresenter>();
            if (character != null && character.IsBound)
            {
                target = new ContextCommandTarget(ContextTargetKind.Character, position, character.CharacterId);
                return true;
            }
            var building = hit.collider.GetComponentInParent<Game.Presentation.Buildings.BuildingPresenter>();
            if (building != null && building.IsBound)
            {
                target = new ContextCommandTarget(ContextTargetKind.Building, position, building.BuildingId);
                return true;
            }
            if (hit.collider.GetComponentInParent<BuildableGround>() != null || hit.collider is TerrainCollider)
            {
                if (context.Production.IsWaterAt?.Invoke(position)==true && context.Production.FishStockAt(position) is { } fish)
                {
                    target = new ContextCommandTarget(ContextTargetKind.ResourceSource,fish.Position,fish.Id,
                        new ResourceId("fish"),ContextResourceClass.Gatherable);
                    return true;
                }
                target = new ContextCommandTarget(ContextTargetKind.Ground, position);
                return true;
            }
            target = default;
            return false;
        }

        private static ContextResourceClass ResourceClass(ResourceId? resource)
        {
            if (!resource.HasValue) return ContextResourceClass.None;
            var id = resource.Value.ToString();
            if (id == "wood_log") return ContextResourceClass.Choppable;
            if (id == "stone" || id == "iron_ore" || id == "gold_ore") return ContextResourceClass.Mineable;
            return ContextResourceClass.Gatherable;
        }

        private void PositionContextMenu(Vector2 screenPosition)
        {
            var panelPoint = root.panel == null ? WorldPointerRaycaster.ScreenToGuiPoint(screenPosition) :
                RuntimePanelUtils.ScreenToPanel(root.panel, WorldPointerRaycaster.ScreenToGuiPoint(screenPosition));
            var width = float.IsNaN(root.resolvedStyle.width) ? Screen.width : root.resolvedStyle.width;
            var height = float.IsNaN(root.resolvedStyle.height) ? Screen.height : root.resolvedStyle.height;
            contextMenu.style.left = Mathf.Clamp(panelPoint.x + 12f, 8f, Mathf.Max(8f, width - 368f));
            contextMenu.style.top = Mathf.Clamp(panelPoint.y + 12f, 84f, Mathf.Max(84f, height - 568f));
        }

        private static string ContextActionName(ContextActionKind action)
        {
            switch (action)
            {
                case ContextActionKind.Gather: return "Собрать";
                case ContextActionKind.Mine: return "Добывать";
                case ContextActionKind.Chop: return "Рубить";
                case ContextActionKind.Haul: return "Нести";
                case ContextActionKind.Build: return "Строить";
                case ContextActionKind.Craft: return "Готовить / производить";
                case ContextActionKind.Guard: return "Охранять";
                case ContextActionKind.Move: return "Идти";
                case ContextActionKind.Attack: return "Напасть";
                default: return action.ToString();
            }
        }

        private static string MilitaryRankName(MilitaryRank rank) => MilitaryRankNames.Russian(rank);

        private static string MilitaryAttackReason(MilitaryAttackDisabledReason reason)
        {
            switch (reason)
            {
                case MilitaryAttackDisabledReason.NoAttackers: return "не выбраны атакующие";
                case MilitaryAttackDisabledReason.AttackerIsCivilian: return "прямую атаку могут приказать только обученные военные";
                case MilitaryAttackDisabledReason.MixedOrWrongSide: return "выбранные военные должны состоять в одном отряде";
                case MilitaryAttackDisabledReason.FriendlyTarget: return "цель принадлежит своей стороне";
                case MilitaryAttackDisabledReason.CivilianDirectTargetForbidden: return "мирные жители не могут быть прямой целью";
                case MilitaryAttackDisabledReason.NonMilitaryBuilding: return "прямой целью может быть только военное строение";
                case MilitaryAttackDisabledReason.TargetMissing: return "цель недоступна";
                case MilitaryAttackDisabledReason.NoPhysicalPath: return "нет физического пути к цели";
                case MilitaryAttackDisabledReason.UnitUnavailable: return "отряд сейчас недоступен";
                default: return reason.ToString();
            }
        }

        private static string ContextTargetName(ContextCommandTarget target)
        {
            switch (target.Kind)
            {
                case ContextTargetKind.Character: return "Персонаж";
                case ContextTargetKind.ResourcePile: return "Ресурс";
                case ContextTargetKind.ResourceSource: return "Природный источник";
                case ContextTargetKind.Building: return "Строение";
                case ContextTargetKind.Area: return "Область";
                default: return "Земля";
            }
        }

        private static string ContextReason(ContextUnavailableReason reason, ContextActionKind action,
            ContextCommandTarget target)
        {
            switch (reason)
            {
                case ContextUnavailableReason.WrongTargetType: return "неподходящий тип цели";
                case ContextUnavailableReason.WrongRole: return "требуется роль: " + RequiredRoleName(action, target);
                case ContextUnavailableReason.TooYoung: return "недопустимый возраст";
                case ContextUnavailableReason.InvalidStatus: return "статус жителя не позволяет выполнить приказ";
                case ContextUnavailableReason.MissingTool: return "нет подходящего инструмента";
                case ContextUnavailableReason.MissingTechnology: return "технология не открыта";
                case ContextUnavailableReason.TooFar: return "слишком далеко";
                case ContextUnavailableReason.NoPath: return "нет доступного пути";
                case ContextUnavailableReason.NoResource: return "ресурс исчерпан";
                case ContextUnavailableReason.NoCapacity: return "нет места на складе";
                case ContextUnavailableReason.Busy: return "текущая безопасная работа вернётся после приказа";
                case ContextUnavailableReason.Cooldown: return "действует время восстановления";
                case ContextUnavailableReason.CriticalNeed: return "сначала критическая потребность";
                case ContextUnavailableReason.NonInterruptiblePhase: return "сначала завершится непрерываемая фаза";
                default: return string.Empty;
            }
        }

        private static string RequiredRoleName(ContextActionKind action, ContextCommandTarget target)
        {
            if (action == ContextActionKind.Gather && target.Kind == ContextTargetKind.ResourceSource &&
                target.ResourceId.HasValue && (target.ResourceId.Value.ToString() == "meat" ||
                    target.ResourceId.Value.ToString().Contains("carcass"))) return "охотник";
            return action switch
            {
                ContextActionKind.Gather => "сборщик", ContextActionKind.Mine => "сборщик",
                ContextActionKind.Chop => "сборщик", ContextActionKind.Haul => "переносчик",
                ContextActionKind.Build => "строитель", ContextActionKind.Craft => "ремесленник",
                ContextActionKind.Guard => "воин", ContextActionKind.Attack => "обученный военный",
                _ => "подходящий работник"
            };
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
            topBar.style.left = 10; topBar.style.right = 10; topBar.style.top = 8; topBar.style.height = StyleKeyword.Auto;
            topBar.style.flexDirection = FlexDirection.Column;
            topBar.style.alignItems = Align.Stretch;
            topBar.style.paddingLeft = 8; topBar.style.paddingRight = 8;
            SerenityUiDesign.StyleSurface(topBar, true);
            topBar.AddToClassList("serenity-top-hud");
            topBar.pickingMode = PickingMode.Position;
            topBar.RegisterCallback<GeometryChangedEvent>(evt =>
            { if (Math.Abs(evt.oldRect.height - evt.newRect.height) > .5f && navigation != null) ApplyResponsiveLayout(); });
            var hudControls = new VisualElement { name = "hud-controls" };
            hudControls.style.flexDirection = FlexDirection.Row;
            hudControls.style.flexWrap = Wrap.Wrap;
            hudControls.style.alignItems = Align.Center;
            hudControls.style.minHeight = 50;
            topBar.Add(hudControls);

            var settlementIdentity = new VisualElement { name = "settlement-identity" };
            settlementIdentity.AddToClassList("serenity-settlement-identity");
            settlementLabel = new Label("Поселение") { name = "settlement-name" };
            settlementLabel.AddToClassList("serenity-settlement-name");
            settlementStatusLabel = new Label("Спокойно") { name = "settlement-status" };
            settlementStatusLabel.AddToClassList("serenity-settlement-status");
            settlementIdentity.Add(settlementLabel);
            settlementIdentity.Add(settlementStatusLabel);
            hudControls.Add(settlementIdentity);

            resourceStrip = new VisualElement { name = "resource-strip" };
            resourceStrip.AddToClassList("serenity-resource-strip");
            resourceStrip.style.flexDirection = FlexDirection.Row;
            resourceStrip.style.alignItems = Align.Center;
            populationLabel = new Label { name = "population-label" };
            populationLabel.AddToClassList("serenity-population-chip");
            hudControls.Add(populationLabel);
            resourcesLabel = populationLabel;
            BuildResourceGroups();

            var timeCluster = new VisualElement { name = "time-cluster" };
            timeCluster.AddToClassList("serenity-time-cluster");
            timeCluster.style.flexDirection = FlexDirection.Row;
            timeCluster.style.alignItems = Align.Center;
            dateLabel = HudLabel(156);
            weatherLabel = HudLabel(205);
            modeLabel = HudLabel(118);
            modeLabel.style.whiteSpace = WhiteSpace.NoWrap;
            modeLabel.style.fontSize = 13;
            timeCluster.Add(dateLabel);
            timeCluster.Add(weatherLabel);
            timeCluster.Add(modeLabel);
            hudControls.Add(timeCluster);
            timeCluster.style.flexGrow = 1;

            var attentionButton = ActionButton(string.Empty, () => SetPanel(GameplayPanel.Events), "attention-button");
            attentionButton.AddToClassList("serenity-attention-button");
            attentionButton.Add(SerenityIconLibrary.Image("alerts", "Центр внимания"));
            alertLabel = new Label();
            alertLabel.style.width = 28; alertLabel.style.height = 24;
            alertLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            alertLabel.style.whiteSpace = WhiteSpace.NoWrap;
            alertLabel.name = "alert-count";
            alertLabel.tooltip = "Непрочитанные уведомления";
            attentionButton.Add(alertLabel);
            hudControls.Add(attentionButton);
            attentionButton.style.width = 72; attentionButton.style.minWidth = 72; attentionButton.style.flexShrink = 0;
            var pause = ActionButton(T("ui.action.pause"), TogglePause, "pause-button");
            pause.AddToClassList(SerenityUiDesign.PrimaryButtonClass);
            pause.tooltip = "Пауза или продолжение симуляции · Space";
            hudControls.Add(pause);
            foreach (var speed in Speeds)
            {
                var captured = speed;
                var speedButton = ActionButton("x" + (int)speed, () => context.SetSpeed(captured), "speed-" + (int)speed);
                speedButton.AddToClassList("serenity-time-button");
                speedButton.tooltip = "Скорость симуляции x" + (int)speed;
                hudControls.Add(speedButton);
            }
            var settings = ActionButton(string.Empty, OpenSettings, "settings-button");
            SerenityIconLibrary.Apply(settings, "settings", "Настройки интерфейса");
            hudControls.Add(settings);
            topBar.Add(resourceStrip);
            root.Add(topBar);

            resourceDrawer = Panel("resource-drawer");
            resourceDrawer.AddToClassList("serenity-resource-drawer");
            resourceDrawer.style.position = Position.Absolute;
            resourceDrawer.style.left = 10; resourceDrawer.style.top = 122; resourceDrawer.style.width = 570;
            resourceDrawer.style.maxHeight = 480;
            resourceDrawer.style.display = DisplayStyle.None;
            resourceDrawer.pickingMode = PickingMode.Position;
            resourceDrawer.RegisterCallback<PointerEnterEvent>(_ => resourceHover = true);
            resourceDrawer.RegisterCallback<PointerLeaveEvent>(_ => { resourceHover = false; ScheduleResourceClose(); });
            resourceDrawer.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { CloseResourceDrawer(); evt.StopPropagation(); } });
            root.Add(resourceDrawer);

            residentStrip = new VisualElement { name = "resident-strip" };
            residentStrip.AddToClassList("serenity-resident-strip");
            residentStrip.style.position = Position.Absolute;
            residentStrip.style.left = 78; residentStrip.style.right = 376; residentStrip.style.top = 72;
            residentStrip.style.height = 62;
            residentStrip.style.flexDirection = FlexDirection.Row;
            residentStrip.pickingMode = PickingMode.Position;
            root.Add(residentStrip);

            var navigationScroll = new ScrollView(ScrollViewMode.Horizontal) { name = "navigation" };
            navigationScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            navigationScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            navigation = navigationScroll;
            navigation.style.position = Position.Absolute;
            navigation.style.left = 10; navigation.style.right = 10; navigation.style.bottom = 6; navigation.style.height = 78;
            navigation.style.paddingTop = 6; navigation.style.paddingBottom = 6;
            navigation.style.paddingLeft = 7; navigation.style.paddingRight = 7;
            SerenityUiDesign.StyleSurface(navigation);
            navigation.AddToClassList("serenity-navigation-rail");
            navigation.AddToClassList("serenity-bottom-navigation");
            navigation.pickingMode = PickingMode.Position;
            foreach (var route in GameplayUiRoutes.Ordered)
            {
                if (route.Id == GameplayUiRouteId.Attention && context.Events == null) continue;
                AddNavigation(route);
            }
            var utilityDivider = new VisualElement { name = "navigation-utility-divider" };
            utilityDivider.AddToClassList("serenity-navigation-divider");
            navigation.Add(utilityDivider);
            AddNavigation(T("ui.nav.saves"), GameplayPanel.SaveLoad, "save");
            AddNavigation("Коды", GameplayPanel.Codes, "settings");
            onboardingToggleButton = ActionButton("?", () => SetOnboardingVisible(!onboarding.Visible),
                "onboarding-toggle");
            onboardingToggleButton.tooltip = "Обучение первого часа";
            onboardingToggleButton.AddToClassList("serenity-navigation-utility");
            navigation.Add(onboardingToggleButton);
            var navigationToggle = ActionButton("⋯", OpenSettings, "navigation-toggle");
            navigationToggle.tooltip = "Настройки интерфейса";
            navigationToggle.AddToClassList("serenity-navigation-utility");
            navigation.Add(navigationToggle);
            var leaveGame = ActionButton("⌂", RequestReturnToMainMenu, "navigation-main-menu");
            leaveGame.tooltip = "Главное меню";
            leaveGame.AddToClassList("serenity-navigation-utility");
            navigation.Add(leaveGame);
            root.Add(navigation);

            var onboardingScroll = new ScrollView(ScrollViewMode.Vertical) { name = "onboarding-card" };
            onboardingScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            onboardingCard = onboardingScroll;
            onboardingCard.style.position = Position.Absolute;
            onboardingCard.style.right = 10; onboardingCard.style.top = 274; onboardingCard.style.width = 354;
            onboardingCard.style.maxHeight = 230;
            onboardingCard.style.paddingLeft = 14; onboardingCard.style.paddingRight = 14;
            onboardingCard.style.paddingTop = 12; onboardingCard.style.paddingBottom = 12;
            SerenityUiDesign.StyleCard(onboardingCard);
            onboardingCard.pickingMode = PickingMode.Position;
            root.Add(onboardingCard);

            mainPanel = new ScrollView(ScrollViewMode.Vertical) { name = "main-panel" };
            StylePanel(mainPanel);
            mainPanel.AddToClassList("serenity-main-panel");
            mainPanel.style.position = Position.Absolute;
            mainPanel.style.left = 78; mainPanel.style.top = 142; mainPanel.style.bottom = 130; mainPanel.style.width = 480;
            mainPanel.style.display = DisplayStyle.None;
            mainPanel.pickingMode = PickingMode.Position;
            root.Add(mainPanel);

            inspector = new ScrollView(ScrollViewMode.Vertical) { name = "selection-inspector" };
            StylePanel(inspector);
            inspector.AddToClassList("serenity-inspector");
            inspector.style.position = Position.Absolute;
            inspector.style.right = 10; inspector.style.top = 274; inspector.style.bottom = 52; inspector.style.width = 354;
            inspector.style.display = DisplayStyle.None;
            inspector.pickingMode = PickingMode.Position;
            root.Add(inspector);

            notifications = Panel("notifications");
            notifications.style.position = Position.Absolute;
            notifications.style.right = 10; notifications.style.top = 72; notifications.style.width = 354;
            notifications.style.maxHeight = 194; notifications.style.paddingLeft = 10; notifications.style.paddingRight = 10;
            notifications.AddToClassList("serenity-notifications");
            notifications.pickingMode = PickingMode.Position;
            root.Add(notifications);

            BuildWorldMapSurfaces();

            placementFeedback = new Label { name = "placement-feedback" };
            placementFeedback.AddToClassList("serenity-placement-feedback");
            placementFeedback.style.position = Position.Absolute;
            placementFeedback.style.left = new Length(31, LengthUnit.Percent);
            placementFeedback.style.right = new Length(31, LengthUnit.Percent);
            placementFeedback.style.bottom = 130;
            placementFeedback.style.display = DisplayStyle.None;
            placementFeedback.pickingMode = PickingMode.Ignore;
            root.Add(placementFeedback);

            bottomDock = Panel("bottom-build-dock");
            bottomDock.AddToClassList("serenity-bottom-dock");
            bottomDock.style.position = Position.Absolute;
            bottomDock.style.left = new Length(19, LengthUnit.Percent);
            bottomDock.style.right = new Length(19, LengthUnit.Percent);
            bottomDock.style.bottom = 90;
            bottomDock.style.height = 76;
            bottomDock.style.flexDirection = FlexDirection.Row;
            bottomDock.style.justifyContent = Justify.Center;
            bottomDock.style.alignItems = Align.Center;
            bottomDock.pickingMode = PickingMode.Position;
            AddBuildCategory("Все", BuildPaletteCategory.All, "build");
            AddBuildCategory("Ресурсы", BuildPaletteCategory.Resources, "gather");
            AddBuildCategory("Производство", BuildPaletteCategory.Production, "production");
            AddBuildCategory("Жильё", BuildPaletteCategory.Housing, "housing");
            AddBuildCategory("Еда", BuildPaletteCategory.Food, "food");
            AddBuildCategory("Склады", BuildPaletteCategory.Storage, "storage");
            AddBuildCategory("Защита", BuildPaletteCategory.Defense, "defense");
            AddBuildCategory("Инфраструктура", BuildPaletteCategory.Infrastructure, "settlement");
            root.Add(bottomDock);

            commandRibbon = Panel("context-command-ribbon");
            commandRibbon.AddToClassList("serenity-command-ribbon");
            commandRibbon.style.position = Position.Absolute;
            commandRibbon.style.left = 270;
            commandRibbon.style.right = 376;
            commandRibbon.style.bottom = 90;
            commandRibbon.style.height = 76;
            commandRibbon.style.flexDirection = FlexDirection.Row;
            commandRibbon.style.alignItems = Align.Center;
            commandRibbon.pickingMode = PickingMode.Position;

            var selectionSummary = new VisualElement { name = "selection-summary" };
            selectionSummary.AddToClassList("serenity-selection-summary");
            selectionSummaryLabel = new Label { name = "selection-summary-label" };
            selectionSummaryLabel.AddToClassList("serenity-selection-summary-title");
            selectionQueueLabel = new Label { name = "selection-queue-label" };
            selectionQueueLabel.AddToClassList("serenity-selection-queue-label");
            selectionSummary.Add(selectionSummaryLabel);
            selectionSummary.Add(selectionQueueLabel);
            commandRibbon.Add(selectionSummary);

            commandActions = new VisualElement { name = "context-command-actions" };
            commandActions.AddToClassList("serenity-command-actions");
            commandActions.style.flexDirection = FlexDirection.Row;
            commandRibbon.Add(commandActions);

            commandQueue = new VisualElement { name = "selection-queue" };
            commandQueue.style.display = DisplayStyle.None;
            commandRibbon.Add(commandQueue);

            statusBar = new VisualElement { name = "status-bar" };
            statusBar.AddToClassList("serenity-command-status");
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.alignItems = Align.Center;
            statusLabel = new Label(T("ui.status.player"));
            statusLabel.style.flexGrow = 1;
            statusBar.Add(statusLabel);
            var mainMenuButton = ActionButton(T("ui.action.main_menu"), RequestReturnToMainMenu, "main-menu-button");
            mainMenuButton.style.marginRight = 8;
            statusBar.Add(mainMenuButton);
            commandRibbon.Add(statusBar);
            root.Add(commandRibbon);

            tpsHud = Panel("tps-hud");
            tpsHud.style.position = Position.Absolute;
            tpsHud.style.left = new Length(36, LengthUnit.Percent);
            tpsHud.style.right = new Length(31, LengthUnit.Percent);
            tpsHud.style.bottom = 94;
            SerenityUiDesign.StyleSurface(tpsHud, true);
            tpsHud.style.paddingLeft = 12; tpsHud.style.paddingRight = 12;
            tpsLabel = new Label();
            tpsHud.Add(tpsLabel);
            tpsHud.style.display = DisplayStyle.None;
            root.Add(tpsHud);

            campaignOutcome = Panel("campaign-outcome");
            campaignOutcome.AddToClassList("serenity-campaign-outcome");
            campaignOutcome.style.position = Position.Absolute;
            campaignOutcome.style.left = new Length(28, LengthUnit.Percent);
            campaignOutcome.style.right = new Length(28, LengthUnit.Percent);
            campaignOutcome.style.top = new Length(34, LengthUnit.Percent);
            campaignOutcome.style.paddingLeft = 22; campaignOutcome.style.paddingRight = 22;
            campaignOutcome.style.paddingTop = 18; campaignOutcome.style.paddingBottom = 18;
            campaignOutcome.style.display = DisplayStyle.None;
            campaignOutcome.pickingMode = PickingMode.Position;
            gameOverLabel = new Label { name = "game-over" };
            gameOverLabel.AddToClassList(SerenityUiDesign.TitleClass);
            gameOverLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            campaignOutcomeDetail = new Label { name = "campaign-outcome-detail" };
            campaignOutcomeDetail.AddToClassList(SerenityUiDesign.BodyClass);
            campaignOutcomeDetail.style.unityTextAlign = TextAnchor.MiddleCenter;
            campaignOutcome.Add(gameOverLabel);
            campaignOutcome.Add(campaignOutcomeDetail);
            root.Add(campaignOutcome);

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

            contextMenu = new ScrollView(ScrollViewMode.Vertical) { name = "context-action-menu" };
            StylePanel(contextMenu);
            contextMenu.style.position = Position.Absolute;
            contextMenu.style.width = 360;
            contextMenu.style.maxHeight = 560;
            contextMenu.style.display = DisplayStyle.None;
            contextMenu.pickingMode = PickingMode.Position;
            contextMenu.style.borderLeftColor = AccentColor;
            contextMenu.style.borderLeftWidth = 3;
            root.Add(contextMenu);
            root.RegisterCallback<PointerDownEvent>(_ => lastUiPointerFrame = Time.frameCount,
                TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(_ => lastUiPointerFrame = Time.frameCount,
                TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(_ => lastUiPointerFrame = Time.frameCount,
                TrickleDown.TrickleDown);
            ApplyResponsiveLayout();
        }

        private void BuildWorldMapSurfaces()
        {
            localMiniMap = Panel("local-mini-map");
            localMiniMap.style.position = Position.Absolute;
            localMiniMap.style.right = 10;
            localMiniMap.style.bottom = 92;
            localMiniMap.style.width = 220;
            localMiniMap.style.height = 184;
            localMiniMap.style.paddingLeft = 8;
            localMiniMap.style.paddingRight = 8;
            localMiniMap.style.paddingTop = 8;
            localMiniMap.style.paddingBottom = 8;
            localMiniMap.pickingMode = PickingMode.Position;
            var miniTitle = new Label("Локальная карта") { name = "local-mini-map-title" };
            miniTitle.AddToClassList(SerenityUiDesign.SectionClass);
            localMiniMap.Add(miniTitle);
            localMiniMapGrid = new VisualElement { name = "local-mini-map-grid" };
            localMiniMapGrid.style.position = Position.Relative;
            localMiniMapGrid.style.flexGrow = 1;
            localMiniMapGrid.style.overflow = Overflow.Hidden;
            localMiniMapGrid.style.borderLeftColor = SerenityUiDesign.Accent;
            localMiniMapGrid.style.borderRightColor = SerenityUiDesign.Accent;
            localMiniMapGrid.style.borderTopColor = SerenityUiDesign.Accent;
            localMiniMapGrid.style.borderBottomColor = SerenityUiDesign.Accent;
            localMiniMapGrid.style.borderLeftWidth = 1;
            localMiniMapGrid.style.borderRightWidth = 1;
            localMiniMapGrid.style.borderTopWidth = 1;
            localMiniMapGrid.style.borderBottomWidth = 1;
            localMiniMapGrid.RegisterCallback<PointerUpEvent>(evt =>
            {
                var width = localMiniMapGrid.resolvedStyle.width;
                var height = localMiniMapGrid.resolvedStyle.height;
                if (width <= 0f || height <= 0f) return;
                FocusLocalMiniMap(Mathf.Clamp01(evt.localPosition.x / width),
                    Mathf.Clamp01(1f - evt.localPosition.y / height));
                evt.StopPropagation();
            });
            localMiniMap.Add(localMiniMapGrid);
            root.Add(localMiniMap);

            realmWorkspace = new VisualElement { name = "realm-workspace" };
            realmWorkspace.style.position = Position.Absolute;
            realmWorkspace.style.left = 0;
            realmWorkspace.style.right = 0;
            realmWorkspace.style.top = 0;
            realmWorkspace.style.bottom = 0;
            realmWorkspace.style.display = DisplayStyle.None;
            realmWorkspace.pickingMode = PickingMode.Ignore;

            realmBottomBackdrop = new VisualElement { name = "realm-bottom-backdrop" };
            realmBottomBackdrop.style.position = Position.Absolute;
            realmBottomBackdrop.style.left = 0;
            realmBottomBackdrop.style.right = 0;
            realmBottomBackdrop.style.bottom = 88;
            realmBottomBackdrop.style.height = 140;
            realmBottomBackdrop.style.backgroundColor = SerenityUiTokens.Canvas;
            realmBottomBackdrop.pickingMode = PickingMode.Ignore;
            realmWorkspace.Add(realmBottomBackdrop);

            realmMapHeader = Panel("realm-map-header");
            realmMapHeader.style.position = Position.Absolute;
            realmMapHeader.style.left = 268;
            realmMapHeader.style.right = 322;
            realmMapHeader.style.top = 72;
            realmMapHeader.style.height = 64;
            realmMapHeader.style.flexDirection = FlexDirection.Row;
            realmMapHeader.style.alignItems = Align.Center;
            realmMapHeader.style.justifyContent = Justify.SpaceBetween;
            realmMapHeader.style.paddingLeft = 12;
            realmMapHeader.style.paddingRight = 12;
            realmMapHeader.pickingMode = PickingMode.Position;
            realmWorkspace.Add(realmMapHeader);

            realmMapOverlay = new VisualElement { name = "realm-map-overlay" };
            realmMapOverlay.style.position = Position.Absolute;
            realmMapOverlay.style.left = new Length(20, LengthUnit.Percent);
            realmMapOverlay.style.right = new Length(24, LengthUnit.Percent);
            realmMapOverlay.style.top = 140;
            realmMapOverlay.style.bottom = 194;
            realmMapOverlay.style.overflow = Overflow.Hidden;
            realmMapOverlay.pickingMode = PickingMode.Ignore;
            realmMapOverlay.RegisterCallback<GeometryChangedEvent>(_ => UpdateStrategicViewport());
            realmWorkspace.Add(realmMapOverlay);

            realmLayers = new ScrollView(ScrollViewMode.Vertical) { name = "realm-layers" };
            StylePanel(realmLayers);
            realmLayers.style.position = Position.Absolute;
            realmLayers.style.left = 10;
            realmLayers.style.top = 72;
            realmLayers.style.bottom = 190;
            realmLayers.style.width = 246;
            realmLayers.pickingMode = PickingMode.Position;
            realmWorkspace.Add(realmLayers);

            realmDetail = new ScrollView(ScrollViewMode.Vertical) { name = "realm-detail" };
            StylePanel(realmDetail);
            realmDetail.style.position = Position.Absolute;
            realmDetail.style.right = 10;
            realmDetail.style.top = 72;
            realmDetail.style.bottom = 190;
            realmDetail.style.width = 310;
            realmDetail.pickingMode = PickingMode.Position;
            realmWorkspace.Add(realmDetail);

            realmRouteBar = Panel("realm-route-summary");
            realmRouteBar.style.position = Position.Absolute;
            realmRouteBar.style.left = 268;
            realmRouteBar.style.right = 322;
            realmRouteBar.style.bottom = 90;
            realmRouteBar.style.height = 118;
            realmRouteBar.style.flexDirection = FlexDirection.Column;
            realmRouteBar.style.alignItems = Align.Stretch;
            realmRouteBar.style.paddingLeft = 12;
            realmRouteBar.style.paddingRight = 12;
            realmRouteBar.pickingMode = PickingMode.Position;
            realmWorkspace.Add(realmRouteBar);
            root.Add(realmWorkspace);
        }

        private void RefreshWorldMapSurfaces()
        {
            if (localMiniMap == null || realmWorkspace == null || context == null) return;
            var strategic = context.GlobalMap.Mode != GameWorldMode.Local && activePanel == GameplayPanel.Global;
            var showLocal = context.GlobalMap.Mode == GameWorldMode.Local &&
                            !IsPeopleRiskWorkspace(activePanel) && activePanel != GameplayPanel.SaveLoad &&
                            activePanel != GameplayPanel.Codes;
            localMiniMap.style.display = showLocal ? DisplayStyle.Flex : DisplayStyle.None;
            realmWorkspace.style.display = strategic ? DisplayStyle.Flex : DisplayStyle.None;
            if (strategic)
            {
                mainPanel.style.display = DisplayStyle.None;
                mainPanel.style.visibility = Visibility.Hidden;
                residentStrip.style.display = DisplayStyle.None;
                commandRibbon.style.display = DisplayStyle.None;
                RefreshRealmMapOverlay();
            }
            else
            {
                mainPanel.style.visibility = Visibility.Visible;
                if (activePanel != GameplayPanel.None) mainPanel.style.display = DisplayStyle.Flex;
            }
            if (showLocal) RefreshLocalMiniMap();
        }

        private void RefreshLocalMiniMap()
        {
            localMiniMapGrid.Clear();
            if (context.ConnectedWorld == null)
            {
                var unavailable = new Label("Карта недоступна");
                unavailable.AddToClassList(SerenityUiDesign.MutedClass);
                localMiniMapGrid.Add(unavailable);
                return;
            }
            const int samples = 24;
            var layout = context.ConnectedWorld.Layout;
            for (var z = 0; z < samples; z++)
            for (var x = 0; x < samples; x++)
            {
                var worldX = layout.OriginX + (x + .5f) / samples * layout.WidthMeters;
                var worldZ = layout.OriginZ + (z + .5f) / samples * layout.DepthMeters;
                var cell = new VisualElement();
                cell.pickingMode = PickingMode.Ignore;
                cell.style.position = Position.Absolute;
                cell.style.left = new Length(x * 100f / samples, LengthUnit.Percent);
                cell.style.top = new Length((samples - 1 - z) * 100f / samples, LengthUnit.Percent);
                cell.style.width = new Length(100f / samples + .15f, LengthUnit.Percent);
                cell.style.height = new Length(100f / samples + .15f, LengthUnit.Percent);
                cell.style.backgroundColor = context.ConnectedWorld.IsRevealed(worldX, worldZ)
                    ? new Color(.17f, .25f, .18f, 1f)
                    : new Color(.025f, .025f, .024f, 1f);
                localMiniMapGrid.Add(cell);
            }
            foreach (var location in context.GlobalMap.Locations.Where(value => IsKnownLocation(value.Id)))
                AddMapDot(localMiniMapGrid, location.MapX, location.MapY, 7f,
                    context.GlobalMap.GetSettlementAt(location.Id) == null ? SerenityUiDesign.TextMuted : SerenityUiDesign.Accent,
                    "Мини-карта: " + LocationName(location.Id));
            var camera = context.CameraPosition();
            AddMapDot(localMiniMapGrid, camera.X, camera.Z, 9f, Color.white, "Положение камеры", "mini-camera-marker");
            if (selectedLocation.HasValue && IsKnownLocation(selectedLocation.Value))
            {
                var selected = context.GlobalMap.GetLocation(selectedLocation.Value);
                AddMapDot(localMiniMapGrid, selected.MapX, selected.MapY, 11f, SerenityUiDesign.Warning,
                    "Выбранная цель", "mini-selected-marker");
            }
        }

        private void AddMapDot(VisualElement parent, float worldX, float worldZ, float size, Color color,
            string tooltip, string name = null)
        {
            if (context.ConnectedWorld == null) return;
            var layout = context.ConnectedWorld.Layout;
            var nx = Mathf.Clamp01((worldX - layout.OriginX) / layout.WidthMeters);
            var nz = Mathf.Clamp01((worldZ - layout.OriginZ) / layout.DepthMeters);
            var marker = new VisualElement { name = name ?? string.Empty, tooltip = tooltip };
            marker.pickingMode = PickingMode.Ignore;
            marker.style.position = Position.Absolute;
            marker.style.left = new Length(nx * 100f, LengthUnit.Percent);
            marker.style.top = new Length((1f - nz) * 100f, LengthUnit.Percent);
            marker.style.width = size;
            marker.style.height = size;
            marker.style.translate = new Translate(-size * .5f, -size * .5f);
            marker.style.backgroundColor = color;
            marker.style.borderTopLeftRadius = size;
            marker.style.borderTopRightRadius = size;
            marker.style.borderBottomLeftRadius = size;
            marker.style.borderBottomRightRadius = size;
            parent.Add(marker);
        }

        private void RefreshRealmMapOverlay()
        {
            realmMapOverlay.Clear();
            realmMapHeader.Clear();
            if (context.ConnectedWorld == null) return;
            var headerTitle = new Label("Стратегическая карта") { name = "realm-map-header-title" };
            headerTitle.AddToClassList(SerenityUiDesign.SectionClass);
            headerTitle.style.position = Position.Absolute;
            headerTitle.style.left = 12;
            headerTitle.style.right = 154;
            headerTitle.style.top = 17;
            headerTitle.style.whiteSpace = WhiteSpace.NoWrap;
            headerTitle.style.overflow = Overflow.Hidden;
            realmMapHeader.Add(headerTitle);
            var weather = context.Weather.GetWeatherAtCalendarTick(context.Clock().State.CalendarTicks);
            var headerWeather = new Label("Погода: " + E(weather.Type)) { name = "realm-weather-layer" };
            headerWeather.AddToClassList(SerenityUiDesign.ChipClass);
            headerWeather.style.position = Position.Absolute;
            headerWeather.style.right = 8;
            headerWeather.style.top = 10;
            headerWeather.style.width = 140;
            realmMapHeader.Add(headerWeather);
            if (showMapRoutes)
                foreach (var route in context.GlobalMap.Routes.Where(value =>
                             IsKnownLocation(value.From) && IsKnownLocation(value.To)))
                    AddRouteDashes(route);
            if (showMapSettlements)
                foreach (var location in context.GlobalMap.Locations.Where(value => IsKnownLocation(value.Id)))
                    AddRealmLocationMarker(location);
            if (showMapArmies)
            {
                var index = 1;
                foreach (var unit in context.Military.Units.Where(IsKnownMapUnit))
                    AddRealmUnitMarker(unit, index++);
            }
            headerWeather.style.display = showMapWeather ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AddRouteDashes(WorldRouteState route)
        {
            var from = context.GlobalMap.GetLocation(route.From);
            var to = context.GlobalMap.GetLocation(route.To);
            for (var step = 1; step < 10; step++)
            {
                var t = step / 10f;
                var dash = new VisualElement();
                dash.pickingMode = PickingMode.Ignore;
                dash.style.position = Position.Absolute;
                dash.style.left = new Length(MapX(Mathf.Lerp(from.MapX, to.MapX, t)), LengthUnit.Percent);
                dash.style.top = new Length(MapY(Mathf.Lerp(from.MapY, to.MapY, t)), LengthUnit.Percent);
                dash.style.width = 4;
                dash.style.height = 4;
                dash.style.backgroundColor = new Color(SerenityUiDesign.Accent.r, SerenityUiDesign.Accent.g,
                    SerenityUiDesign.Accent.b, .72f);
                realmMapOverlay.Add(dash);
            }
        }

        private void AddRealmLocationMarker(WorldLocationState location)
        {
            var settlement = context.GlobalMap.GetSettlementAt(location.Id);
            if (settlement != null && context.Campaign != null && !context.Campaign.CanRevealSettlement(settlement.Id))
                return;
            var captured = location.Id;
            var marker = ActionButton(LocationName(location.Id), () => SelectMapLocation(captured),
                "realm-map-location-" + location.Id.Value);
            marker.style.position = Position.Absolute;
            marker.style.left = new Length(MapX(location.MapX), LengthUnit.Percent);
            marker.style.top = new Length(MapY(location.MapY), LengthUnit.Percent);
            marker.style.maxWidth = 132;
            marker.style.translate = new Translate(-18f, -12f);
            marker.AddToClassList("serenity-card-action");
            SerenityUiDesign.SetSelected(marker, selectedLocation == location.Id);
            realmMapOverlay.Add(marker);
        }

        private void AddRealmUnitMarker(MilitaryUnitState unit, int index)
        {
            var point = UnitMapPoint(unit);
            var captured = unit.Id;
            var marker = ActionButton("⚑ " + index, () => SelectMapUnit(captured),
                "realm-map-unit-" + unit.Id);
            marker.style.position = Position.Absolute;
            marker.style.left = new Length(MapX(point.x), LengthUnit.Percent);
            marker.style.top = new Length(MapY(point.y), LengthUnit.Percent);
            marker.style.translate = new Translate(-10f, 14f);
            marker.tooltip = UnitLabel(unit, index) + " · " + UnitMapLocation(unit);
            SerenityUiDesign.SetSelected(marker, selectedUnit == unit.Id);
            realmMapOverlay.Add(marker);
        }

        private bool IsKnownMapUnit(MilitaryUnitState unit)
        {
            var travel = context.GlobalMap.Travels.FirstOrDefault(value => value.UnitId == unit.Id);
            if (travel != null) return IsKnownLocation(travel.Origin) && IsKnownLocation(travel.Destination);
            if (string.IsNullOrWhiteSpace(unit.AbstractLocationId)) return false;
            return context.GlobalMap.Locations.Any(value => value.Id.Value == unit.AbstractLocationId &&
                IsKnownLocation(value.Id));
        }

        private Vector2 UnitMapPoint(MilitaryUnitState unit)
        {
            var travel = context.GlobalMap.Travels.FirstOrDefault(value => value.UnitId == unit.Id);
            if (travel == null)
            {
                var location = context.GlobalMap.GetLocation(new WorldLocationId(unit.AbstractLocationId));
                return new Vector2(location.MapX, location.MapY);
            }
            var from = context.GlobalMap.GetLocation(travel.Origin);
            var to = context.GlobalMap.GetLocation(travel.Destination);
            var progress = Mathf.InverseLerp(travel.DepartureTick, travel.ArrivalTick,
                context.Clock().State.CalendarTicks);
            return Vector2.Lerp(new Vector2(from.MapX, from.MapY), new Vector2(to.MapX, to.MapY), progress);
        }

        private float MapX(float worldX)
        {
            var layout = context.ConnectedWorld.Layout;
            return Mathf.Clamp01((worldX - layout.OriginX) / layout.WidthMeters) * 100f;
        }

        private float MapY(float worldZ)
        {
            var layout = context.ConnectedWorld.Layout;
            return (1f - Mathf.Clamp01((worldZ - layout.OriginZ) / layout.DepthMeters)) * 100f;
        }

        private void ObserveOnboardingSignals()
        {
            var berries = new ResourceId("berries");
            var shelter = context.Buildings.All.Any(x =>
                x.DefinitionId.FamilyId == new BuildingDefinitionId("stone_age_hut") && x.IsOperational);
            var storage = context.Buildings.All.Any(x => x.DefinitionId.FamilyId == new BuildingDefinitionId("storage_basket") &&
                                                          x.IsOperational && context.Inventories.TryGet(
                                                              new InventoryOwner(InventoryOwnerKind.BuildingStorage, x.Id), out _));
            var researchStarted = context.Research.ActiveProject != null || context.Research.CompletedTechnologies.Any(x =>
                !context.Research.Catalog.Get(x).StartingKnowledge);
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
            RefreshNavigationAppearance();
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
            var available = PlayerCharacterIds().Count(id => context.Characters.TryGet(id, out var person) &&
                person.LifeState == CharacterLifeState.Alive && !context.Work.TryGetAssigned(id, out _) &&
                !context.MilitaryTraining.IsExternallyControlled(id));
            populationLabel.text = F("ui.hud.population", alive) + " · свободны " + available;
            var playerBuildings = context.Buildings.All.Where(building => building.OwnerSideId.Value == "player").ToArray();
            var burning = playerBuildings.Count(building => building.FireStatus == BuildingFireStatus.Burning);
            settlementLabel.text = "Поселение игрока";
            settlementStatusLabel.text = burning > 0 ? "Пожар · " + burning : "Спокойно";
            settlementStatusLabel.EnableInClassList("is-danger", burning > 0);
            settlementStatusLabel.EnableInClassList("is-warning", false);
            RefreshResourceGroups();
            RefreshResourceDrawer();
            var ticks = clock.State.CalendarTicks;
            dateLabel.text = F("ui.hud.date", GameCalendar.Year(ticks), GameCalendar.DayOfYear(ticks),
                GameCalendar.HourOfDay(ticks).ToString("00"), GameCalendar.MinuteOfHour(ticks).ToString("00"));
            var weather = context.Weather.GetWeatherAtCalendarTick(ticks);
            weatherLabel.text = E(weather.Season) + " · " + E(weather.Type) + " · " + RussianTextCatalog.Decimal(weather.TemperatureCelsius) + " °C";
            modeLabel.text = T(context.GlobalMap.Mode == GameWorldMode.Local ? "ui.hud.mode.local" : "ui.hud.mode.global");
            var pendingEvents = context.Events?.PendingPlayerEvents.Count ?? 0;
            var alertText = F("ui.hud.alerts", notificationFeed.Count) +
                            (pendingEvents > 0 ? F("ui.hud.pending_events", pendingEvents) : string.Empty);
            var alertCount = pendingEvents > 0 ? pendingEvents : notificationFeed.Count;
            alertLabel.text = alertCount > 99 ? "99+" : alertCount.ToString();
            alertLabel.tooltip = alertText;
            var pauseButton = topBar.Q<Button>("pause-button");
            pauseButton.text = T(clock.IsPaused ? "ui.action.resume" : "ui.action.pause");
            SerenityUiDesign.SetSelected(pauseButton, clock.IsPaused);
            foreach (var speed in Speeds)
                SerenityUiDesign.SetSelected(topBar.Q<Button>("speed-" + (int)speed), speed == clock.Speed);
            var codesButton = navigation.Q<Button>("nav-codes");
            if (codesButton != null) codesButton.SetEnabled(clock.IsPaused);
            if (!clock.IsPaused && activePanel == GameplayPanel.Codes)
            {
                activePanel = GameplayPanel.None;
                mainPanel.style.display = DisplayStyle.None;
            }
            RefreshNavigationSelection();
        }

        private void RefreshResourceDrawer() => UpdateResourceDrawer();

        private void RefreshResidentStrip()
        {
            if (residentStrip == null) return;
            residentStrip.Clear();
            var selectedId = context.Selection.SelectedCharacterId;
            if (selectedId.HasValue && context.Characters.TryGet(selectedId.Value, out var person) &&
                person.LifeState == CharacterLifeState.Alive)
            {
                var captured = person.Id;
                var status = context.MilitaryTraining.IsExternallyControlled(person.Id) ? "В казарме" :
                    context.Work.TryGetAssigned(person.Id, out var job) ? E(job.Type) :
                    context.AiAgents.TryGet(person.Id, out var agent) ? E(agent.CurrentAction) : "ожидает";
                var card = ActionButton(RussianTextCatalog.PersonName(person.Name.DisplayName) + "\n" + status,
                    () => SelectResident(captured), "resident-" + person.Id);
                card.AddToClassList("serenity-resident-card");
                card.tooltip = RussianTextCatalog.PersonName(person.Name.DisplayName) + " · " + status +
                               "\nЛКМ — выбрать; повторный клик — переместить камеру";
                SerenityIconLibrary.Apply(card, "population", card.tooltip);
                SerenityUiDesign.SetSelected(card, true);
                residentStrip.Add(card);
            }
            var openRoster = ActionButton("Жители · " + PlayerAliveCount(), () => SetPanel(GameplayPanel.Domestic),
                "resident-roster-open");
            openRoster.tooltip = "Открыть масштабируемый реестр жителей";
            SerenityIconLibrary.Apply(openRoster, "population", openRoster.tooltip);
            residentStrip.Add(openRoster);
        }

        private void RefreshCommandRibbon()
        {
            if (commandRibbon == null || commandActions == null) return;
            var strategic = context.GlobalMap.Mode != GameWorldMode.Local;
            bottomDock.style.display = !strategic && (activePanel == GameplayPanel.Build ||
                context.BuildingPlacement.IsActive) ? DisplayStyle.Flex : DisplayStyle.None;
            commandActions.Clear();
            var selectedIds = context.Selection.SelectedCharacterIds;
            var buildingPresenter = context.Selection.SelectedBuildingPresenter;
            var sourcePresenter = context.Selection.SelectedProductionSourcePresenter;
            var pilePresenter = context.Selection.SelectedWorldResourcePilePresenter;
            var wildlife = context.Selection.SelectedWildlife;
            var hasSelection = selectedIds.Count > 0 || buildingPresenter != null || sourcePresenter != null ||
                               pilePresenter != null || wildlife != null;
            commandRibbon.style.display = !strategic && hasSelection
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (!hasSelection) return;

            if (selectedIds.Count > 0)
            {
                var projection = GameplaySelectionQueueProjection.Build(selectedIds, context.Work, 3);
                selectionSummaryLabel.text = projection.SelectedCount == 1
                    ? CharacterName(selectedIds[0])
                    : "Выбрано жителей: " + projection.SelectedCount;
                selectionQueueLabel.text = projection.TotalOrders == 0
                    ? "Готовы к приказу"
                    : "Очередь: " + projection.TotalOrders + " · заняты: " + projection.BusyCount;
                commandActions.Add(ActionButton("К выбранному", FocusPrimarySelection, "command-focus-selection"));
                if (selectedIds.Count == 1)
                {
                    var id = selectedIds[0];
                    var direct = ActionButton("Прямое управление", () => TryCommand(() =>
                    {
                        var result = context.ThirdPerson.TryEnter(id);
                        if (!result.IsEligible) throw new PlayerFacingException(EligibilityReason(result.Failure));
                    }), "command-enter-tps");
                    direct.SetEnabled(context.PlayerControl.Evaluate(id).IsEligible);
                    commandActions.Add(direct);
                }
            }
            else if (buildingPresenter != null && buildingPresenter.IsBound &&
                     context.Buildings.TryGet(buildingPresenter.BuildingId, out var building))
            {
                var definition = context.BuildingCatalog.ResolveDefinition(building.DefinitionId);
                selectionSummaryLabel.text = Content("building", definition.Id.ToString());
                selectionQueueLabel.text = building.IsOperational ? "Здание работает" :
                    "Строительство: " + building.ConstructionProgress.ToString("P0");
                commandActions.Add(ActionButton("К зданию", FocusPrimarySelection, "command-focus-selection"));
                commandActions.Add(ActionButton("Хозяйство", () => SetPanel(GameplayPanel.Economy),
                    "command-open-economy"));
            }
            else
            {
                selectionSummaryLabel.text = sourcePresenter != null ? "Источник ресурсов" :
                    pilePresenter != null ? "Груз в мире" : wildlife != null ? wildlife.DisplayName : "Объект";
                selectionQueueLabel.text = "Контекстные действия доступны в карточке";
                commandActions.Add(ActionButton("К объекту", FocusPrimarySelection, "command-focus-selection"));
                commandActions.Add(ActionButton("Хозяйство", () => SetPanel(GameplayPanel.Economy),
                    "command-open-economy"));
            }
            commandActions.Add(ActionButton("Снять выбор", ClearSelectionFromUi, "command-clear-selection"));
        }

        private void FocusPrimarySelection()
        {
            var id = context.Selection.InspectedCharacterId;
            if (id.HasValue && context.Presentations.TryGet(id.Value, out var presenter) && presenter != null)
            {
                context.FocusWorld(new WorldPosition(presenter.transform.position.x, presenter.transform.position.y,
                    presenter.transform.position.z));
                return;
            }
            var building = context.Selection.SelectedBuildingPresenter;
            if (building != null)
            {
                var position = building.transform.position;
                context.FocusWorld(new WorldPosition(position.x, position.y, position.z));
                return;
            }
            var source = context.Selection.SelectedProductionSourcePresenter;
            if (source != null) context.FocusWorld(source.Site.Position);
        }

        private void ClearSelectionFromUi()
        {
            context.Selection.ClearSelection();
            RefreshNow();
        }

        private void RefreshPlacementFeedback()
        {
            if (placementFeedback == null) return;
            var active = context.BuildingPlacement.IsActive;
            placementFeedback.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (!active) return;
            placementFeedback.RemoveFromClassList("is-free");
            placementFeedback.RemoveFromClassList("requires-clearance");
            placementFeedback.RemoveFromClassList("is-blocked");
            switch (context.BuildingPlacement.VisualState)
            {
                case Game.Presentation.Buildings.BuildingPlacementVisualState.Free:
                    placementFeedback.text = "Свободная площадка · можно строить";
                    placementFeedback.AddToClassList("is-free");
                    break;
                case Game.Presentation.Buildings.BuildingPlacementVisualState.RequiresClearance:
                    placementFeedback.text = "Нужна расчистка · жители сначала уберут " +
                                             context.BuildingPlacement.PreviewClearanceSourceIds.Count + " объектов";
                    placementFeedback.AddToClassList("requires-clearance");
                    break;
                default:
                    placementFeedback.text = "Строительство невозможно · " + PlacementFailureText();
                    placementFeedback.AddToClassList("is-blocked");
                    break;
            }
        }

        private void RefreshPanel()
        {
            if (mainPanel == null || activePanel == GameplayPanel.None) return;
            if (activePanel == GameplayPanel.Research && mainPanel.Q<VisualElement>("research-viewport") != null)
            { UpdateResearchView(); return; }
            if (activePanel == GameplayPanel.Economy && mainPanel.Q<VisualElement>("stock-workspace") != null)
            { UpdateStockNumbers(); return; }
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
                case GameplayPanel.Domestic: DrawDomesticPanel(); break;
                case GameplayPanel.Codes: DrawCodesPanel(); break;
            }
        }

        private void DrawCodesPanel()
        {
            AddTitle(mainPanel, "Коды и диагностика");
            AddLine(mainPanel, context.Diagnostics.CheatsUsed
                ? "Коды использованы · достижения этой партии навсегда отключены."
                : "Достижения включены. Первый успешно применённый код отключит их для этой партии навсегда.");
            AddMuted(mainPanel, "Команды применяются атомарно к каноническому состоянию. Ошибка ничего не изменяет.");
            AddMuted(mainPanel, "Для ресурсов общий склад при необходимости расширяется целиком; скрытых остатков вне склада нет.");
            var code = new TextField("Код") { name = "codes-input" };
            StyleTextField(code);
            mainPanel.Add(code);
            mainPanel.Add(ActionButton("Применить", () => SubmitDiagnosticCode(code.value), "codes-apply"));
            AddSection(mainPanel, "Доступные коды");
            AddLine(mainPanel, "serenity — открыть все текущие технологии и возможности");
            AddLine(mainPanel, "res_serenity — добавить по 10 000 каждого текущего ресурса");
            AddLine(mainPanel, "army_serenity — создать по одному бойцу каждого ранга");
            AddLine(mainPanel, "map_serenity — открыть всю карту мира");
            AddSection(mainPanel, "Семейная диагностика");
            AddMuted(mainPanel, "Создаёт двух взрослых, отношения, взаимное согласие, кровать в доме и каноническую беременность.");
            mainPanel.Add(ActionButton("Запустить семейную диагностику", () => RunFamilyDiagnostic(),
                "family-diagnostic"));
        }

        public bool SubmitDiagnosticCode(string rawCode)
        {
            if (!context.Clock().IsPaused)
            {
                SetStatus("Сначала поставьте игру на паузу.");
                return false;
            }
            return ApplyDiagnostic(context.Diagnostics.Execute(rawCode));
        }

        public bool RunFamilyDiagnostic()
        {
            if (!context.Clock().IsPaused)
            {
                SetStatus("Сначала поставьте игру на паузу.");
                return false;
            }
            var succeeded = ApplyDiagnostic(context.Diagnostics.Execute(DiagnosticCommandKind.CreateFamily));
            if (!succeeded) return false;
            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddTitle(modal, "Время проходит…");
            AddLine(modal, "Личная сцена остаётся за кадром. Семейное событие записано в состоянии партии.");
            modal.Add(ActionButton("Пропустить", CloseModal, "family-diagnostic-skip"));
            return true;
        }

        private bool ApplyDiagnostic(DiagnosticExecutionResult result)
        {
            LastDiagnosticResult = result;
            if (!result.Succeeded)
            {
                SetStatus("Код не применён: " + DiagnosticFailureText(result.Failure));
                RefreshNow();
                return false;
            }
            string message;
            switch (result.Command)
            {
                case DiagnosticCommandKind.UnlockAll:
                    message = "Открыты все текущие технологии: " + result.AffectedCount + ".";
                    break;
                case DiagnosticCommandKind.GrantResources:
                    message = "Добавлено по 10 000 каждого ресурса (" + result.AffectedCount + " видов).";
                    break;
                case DiagnosticCommandKind.CreateArmy:
                    message = "Созданы бойцы всех рангов: " + result.AffectedCount + ".";
                    break;
                case DiagnosticCommandKind.CreateFamily:
                    message = "Семья создана: дом, кровать, взаимное согласие и беременность подтверждены.";
                    break;
                case DiagnosticCommandKind.RevealWorld:
                    message = "Открыта вся карта мира (новых ячеек: " + result.AffectedCount + ").";
                    break;
                default:
                    message = "Диагностическая команда выполнена.";
                    break;
            }
            AddNotification(message);
            SetStatus(message + " Достижения этой партии отключены.");
            RefreshNow();
            return true;
        }

        private static string DiagnosticFailureText(DiagnosticFailure failure)
        {
            switch (failure)
            {
                case DiagnosticFailure.EmptyCode: return "введите код";
                case DiagnosticFailure.UnknownCode: return "неизвестный код";
                case DiagnosticFailure.MissingStorage: return "нет подходящего действующего склада";
                case DiagnosticFailure.MissingBarracks: return "нет действующих казарм";
                case DiagnosticFailure.MissingHome: return "нет закрытого обитаемого дома";
                case DiagnosticFailure.NoIndoorBedPosition: return "в доме нет свободного места для кровати";
                case DiagnosticFailure.MissingWorld: return "карта мира недоступна";
                default: return "транзакция отменена без изменений";
            }
        }

        private void DrawEventsPanel()
        {
            AddTitle(mainPanel, "Центр внимания");
            AddMuted(mainPanel, "Решения, угрозы и история поселения.");
            var eventTabs = Row();
            var decisions = ActionButton("Требуют решения", () => { showEventJournal = false; RefreshPanel(); },
                "attention-decisions");
            var journalButton = ActionButton("Журнал", () => { showEventJournal = true; RefreshPanel(); },
                "attention-journal");
            SerenityUiDesign.SetSelected(decisions, !showEventJournal);
            SerenityUiDesign.SetSelected(journalButton, showEventJournal);
            eventTabs.Add(decisions);
            eventTabs.Add(journalButton);
            mainPanel.Add(eventTabs);
            if (context.Events == null)
            {
                AddMuted(mainPanel, T("ui.events.unavailable"));
                return;
            }
            var pending = context.Events.PendingPlayerEvents;
            if (showEventJournal)
            {
                AddSection(mainPanel, "Последние сообщения");
                if (notificationFeed.Count == 0) AddMuted(mainPanel, "Новых сообщений нет.");
                foreach (var message in notificationFeed.Take(12))
                {
                    var card = SerenityUiDesign.Card();
                    card.AddToClassList("serenity-attention-message");
                    AddLine(card, message);
                    mainPanel.Add(card);
                }
                AddSection(mainPanel, T("ui.events.history"));
                if (context.Events.History.Count == 0) AddMuted(mainPanel, T("ui.events.no_history"));
                foreach (var entry in context.Events.History.Reverse().Take(24))
                {
                    var definition = context.Events.Catalog.Get(entry.DefinitionId);
                    var card = SerenityUiDesign.Card();
                    AddLine(card, EventTitle(entry.DefinitionId, definition.Nodes[0].Id));
                    AddMuted(card, F("ui.events.history_line", string.Empty, E(entry.Status),
                        EventOutcome(entry.OutcomeCode)));
                    AddMuted(card, string.Join(", ", entry.Targets.Select(context.Events.GetTargetDisplayName)));
                    mainPanel.Add(card);
                }
                return;
            }

            AddSection(mainPanel, F("ui.events.pending", pending.Count));
            if (pending.Count == 0)
            {
                AddMuted(mainPanel, T("ui.events.none_pending"));
                AddMuted(mainPanel, "Обычные сообщения доступны во вкладке «Журнал».");
                return;
            }
            var eventList = Row();
            eventList.name = "attention-pending-list";
            eventList.style.flexWrap = Wrap.Wrap;
            foreach (var instance in pending)
            {
                var definition = context.Events.Catalog.Get(instance.DefinitionId);
                var node = definition.Nodes.Single(x => x.Id == instance.CurrentNodeId);
                var captured = instance.InstanceId;
                var pendingButton = ActionButton((selectedEvent == instance.InstanceId ? T("common.selected_marker") : string.Empty) +
                    EventTitle(instance.DefinitionId, node.Id),
                    () => { selectedEvent = captured; RefreshPanel(); });
                pendingButton.AddToClassList("serenity-card-action");
                pendingButton.style.minWidth = 240;
                SerenityUiDesign.SetSelected(pendingButton, selectedEvent == instance.InstanceId);
                eventList.Add(pendingButton);
            }
            mainPanel.Add(eventList);
            if (!selectedEvent.HasValue || pending.All(x => x.InstanceId != selectedEvent.Value))
                selectedEvent = pending.Count == 0 ? (EventInstanceId?)null : pending[0].InstanceId;
            if (selectedEvent.HasValue)
            {
                var instance = pending.First(x => x.InstanceId == selectedEvent.Value);
                var definition = context.Events.Catalog.Get(instance.DefinitionId);
                var node = definition.Nodes.Single(x => x.Id == instance.CurrentNodeId);
                var sceneCard = SerenityUiDesign.Card();
                sceneCard.name = "attention-decision-card";
                AddSection(sceneCard, "РЕШЕНИЕ · " + EventTitle(instance.DefinitionId, node.Id));
                AddLine(sceneCard, EventDescription(instance.DefinitionId, node.Id));
                AddMuted(sceneCard, F("ui.events.involving", string.Join(", ",
                    instance.Targets.Select(context.Events.GetTargetDisplayName))));
                AddMuted(sceneCard, instance.DueTick > context.Clock().State.CalendarTicks
                    ? "Срок: " + FormatDuration(instance.DueTick - context.Clock().State.CalendarTicks)
                    : "Решение ожидается сейчас");
                foreach (var choice in node.Choices)
                {
                    var capturedChoice = choice.Id;
                    var choiceButton = ActionButton(EventChoice(instance.DefinitionId, node.Id, choice.Id), () => TryCommand(() =>
                    {
                        var result = context.Events.ResolvePlayerChoice(instance.InstanceId, capturedChoice,
                            context.Clock().State.CalendarTicks);
                        if (!result.Applied) throw new PlayerFacingException(EventChoiceFailureText(result.Status));
                        selectedEvent = null;
                        SetStatus(T("ui.events.choice_applied"));
                    }), "attention-choice-" + choice.Id.Value);
                    choiceButton.AddToClassList("serenity-card-action");
                    sceneCard.Add(choiceButton);
                }
                mainPanel.Add(sceneCard);
            }
        }

        private static string EventChoiceFailureText(EventChoiceCommandStatus status) => status switch
        {
            EventChoiceCommandStatus.InstanceNotFound => "Событие уже завершено.",
            EventChoiceCommandStatus.NotPending => "Это решение сейчас не ожидается.",
            EventChoiceCommandStatus.NotPlayerOwned => "Решение принимает другой правитель.",
            EventChoiceCommandStatus.ChoiceNotFound => "Этот вариант больше недоступен.",
            EventChoiceCommandStatus.TargetInvalid => "Участник события больше недоступен.",
            EventChoiceCommandStatus.EffectRejected => "Условия выбора изменились.",
            _ => "Решение не применено."
        };

        private void DrawEconomyPanel() => DrawStockWorkspace();

        private void DrawBuildPanel()
        {
            AddTitle(mainPanel, T("ui.build.title") + " · " + BuildCategoryLabel(buildCategory));
            AddLine(mainPanel, T("ui.build.instructions"));
            mainPanel.Add(ActionButton("Повернуть постройку на 90° (R)", () => context.BuildingPlacement.RotatePreview(), "rotate-building"));
            foreach (var entry in context.BuildingCatalog.Entries.OrderBy(x => x.DefinitionId, StringComparer.Ordinal))
            {
                // Retain the legacy ID for existing scenes/saves, not as a second player option.
                if (entry.DefinitionId == "primitive_crop_plot" || entry.DefinitionId == "primitive_shelter" || entry.DefinitionId=="clan_house") continue;
                if (entry.DefinitionId.EndsWith("_level2",StringComparison.Ordinal)) continue;
                var definition = entry.CreateDefinition();
                if (!MatchesBuildCategory(definition, buildCategory)) continue;
                var available = context.Research.IsBuildingUnlocked(definition.Id);
                var cost = definition.ConstructionCost.Count == 0 ? T("ui.build.no_cost") : F("ui.build.cost", string.Join(", ",
                    definition.ConstructionCost.Select(x => Content("resource", x.ResourceId.ToString()) + " " + x.Quantity.Units)));
                var shortage = BuildingResourceShortage(definition);
                var reason = !available ? BuildingLockReason(definition.Id) : shortage;
                var card = SerenityUiDesign.Card();
                card.AddToClassList("serenity-build-card");
                AddSection(card, Content("building", definition.Id.ToString()));
                var explanation = definition.Id.ToString() switch
                {
                    "training_ground" => "Тренировка боевого навыка выбранных жителей.",
                    "palisade" => "Защитная стена, перекрывающая проход. Для входа нужны ворота. Зажмите ЛКМ и тяните.",
                    "meeting_area" => "Площадка для общих собраний. Сценарий собраний и бонус настроения пока не подключены.",
                    "gathering_area" => "Обозначенная площадка для организации сбора дикорастущих ресурсов.",
                    "primitive_garden" => "Зажмите ЛКМ и растяните огород: от 2×2 до 8×8 клеток.",
                    "primitive_path" => "Зажмите ЛКМ и протяните тропу, до 32 клеток за раз.",
                    "fishing_camp" => "Ставится прямо у берега, входом на сушу. Рыбак работает внутри; до 30 рыб ждут переноса на склад.",
                    _ => null
                };
                if (explanation != null) AddMuted(card, explanation);
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

        private void DrawWorkPanel()
        {
            AddTitle(mainPanel, T("ui.work.title"));
            AddMuted(mainPanel, "Профессия, смена, приоритет, командир и состав каждой группы.");
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
                AddMuted(mainPanel, ProfessionGroupService.DisplayName(group.Role) + " · " +
                    AllowedWorkLabel(group.Role) + " · " + group.Members.Count(id => !context.Work.HasOpenJob(id)) +
                    "/" + group.MemberCount + " доступно");
            }
            mainPanel.Add(ActionButton("Восстановить отсутствующие профессиональные группы", () => TryCommand(() =>
                context.DirectWork.ProfessionGroups.EnsureGroups()), "work-create-profession-group"));
            if (!selectedGroup.HasValue || !context.Work.Groups.TryGet(selectedGroup.Value, out var current))
            {
                AddMuted(mainPanel, T("ui.work.select_group"));
                return;
            }
            AddSection(mainPanel, current.DisplayName);
            AddLine(mainPanel, $"Дневная смена (06:00–18:00): {current.MembersForShift(WorkShiftKind.Day).Count}");
            AddLine(mainPanel, $"Ночная смена (18:00–06:00): {current.MembersForShift(WorkShiftKind.Night).Count}");
            AddLine(mainPanel, "Профессия: " + ProfessionGroupService.ProfessionForRole(current.Role).Value +
                " · допустимая работа: " + AllowedWorkLabel(current.Role));
            var shiftAssignments = Row();
            shiftAssignments.Add(ActionButton("Выбранных → день", () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds.Where(current.Contains))
                    context.Work.SetMemberShift(current.Id, id, WorkShiftKind.Day);
            }), "work-shift-day"));
            shiftAssignments.Add(ActionButton("Выбранных → ночь", () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds.Where(current.Contains))
                    context.Work.SetMemberShift(current.Id, id, WorkShiftKind.Night);
            }), "work-shift-night"));
            mainPanel.Add(shiftAssignments);
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
            membership.Add(ActionButton("Перевести выбранных", () => TryCommand(() =>
            {
                foreach (var id in context.Selection.SelectedCharacterIds)
                    if (!current.Contains(id)) context.DirectWork.ProfessionGroups.Reassign(id, current.Id);
            })));
            membership.Add(ActionButton(T("ui.work.commander_selected"), () => TryCommand(() =>
            {
                if (context.Selection.SelectedCharacterIds.Count != 1) throw PlayerError("ui.work.need_one_member");
                context.Work.SetCommander(current.Id, context.Selection.SelectedCharacterIds[0]);
            })));
            mainPanel.Add(membership);
            if (current.Role != WorkGroupRole.General)
                mainPanel.Add(ActionButton("Удалить группу и перевести состав в Разнорабочие", () => TryCommand(() =>
                {
                    var fallback = context.DirectWork.ProfessionGroups.Primary(WorkGroupRole.General);
                    context.DirectWork.ProfessionGroups.DeleteGroup(current.Id, fallback.Id);
                    selectedGroup = fallback.Id;
                }), "work-delete-group"));
            AddLine(mainPanel, F("ui.work.commander_priority", CharacterName(current.CommanderId), E(workPriority)));
            var priority = Row();
            foreach (var value in new[] { WorkPriority.Normal, WorkPriority.High, WorkPriority.Immediate })
            {
                var captured = value;
                var priorityButton = ActionButton(value == WorkPriority.Immediate ? "Срочно" : E(value),
                    () => { workPriority = captured; RefreshPanel(); });
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
                    jobActions.Add(ActionButton("Срочно", () => TryCommand(() => context.DirectWork.MakeUrgent(captured))));
                    jobActions.Add(ActionButton("Отменить", () => TryCommand(() => context.Work.Cancel(captured))));
                    mainPanel.Add(jobActions);
                }
            }
        }

        private void DrawDynastyPanel()
        {
            AddTitle(mainPanel, context.Dynasty.Name);
            AddMuted(mainPanel, "Правление, семья и линия наследования.");
            var monarch = context.Dynasty.CurrentMonarchId.HasValue
                ? context.Characters.Get(context.Dynasty.CurrentMonarchId.Value) : null;
            var biologicalTick = context.Clock().State.BiologicalTicks;
            var candidates = monarch == null ? Array.Empty<Character>() : context.Family
                .GetOrderedSuccessionCandidates(context.Dynasty, monarch.Id).ToArray();
            var summary = Row();
            summary.name = "dynasty-summary";
            summary.style.flexWrap = Wrap.Wrap;
            AddMetricCard(summary, "ПРАВИТЕЛЬ", CharacterName(context.Dynasty.CurrentMonarchId),
                F("ui.dynasty.status", E(context.Dynasty.Status)));
            AddMetricCard(summary, "ДИНАСТИЯ", context.Dynasty.MemberIds.Count + " членов",
                "Основатель: " + CharacterName(context.Dynasty.FounderId));
            AddMetricCard(summary, "НАСЛЕДОВАНИЕ", candidates.Length == 0 ? "Нет наследника" : CharacterName(candidates[0].Id),
                candidates.Length + " допустимых кандидатов");
            mainPanel.Add(summary);

            AddSection(mainPanel, "Семейное дерево");
            var tree = new VisualElement();
            tree.name = "dynasty-family-tree";
            tree.style.alignItems = Align.Center;
            var members = context.Dynasty.MemberIds
                .Select(id => context.Characters.TryGet(id, out var person) ? person : null)
                .Where(person => person != null)
                .ToArray();
            var memberIds = members.Select(person => person.Id).ToHashSet();
            var spouseIds = members.Where(person => person.SpouseId.HasValue &&
                    !memberIds.Contains(person.SpouseId.Value) && context.Characters.TryGet(person.SpouseId.Value, out _))
                .Select(person => person.SpouseId.Value).Distinct().ToHashSet();
            var generations = new Dictionary<StableEntityId, int>();
            foreach (var member in members) GetDynastyGeneration(member, memberIds, generations);
            foreach (var generation in members.GroupBy(member => generations[member.Id]).OrderBy(group => group.Key))
            {
                if (generation.Key > 0)
                {
                    var branch = new Label("↓") { name = "dynasty-generation-connector-" + generation.Key };
                    branch.style.fontSize = 28;
                    branch.style.color = AccentColor;
                    tree.Add(branch);
                }
                var generationBlock = new VisualElement { name = "dynasty-generation-" + generation.Key };
                generationBlock.style.alignItems = Align.Center;
                AddMuted(generationBlock, "ПОКОЛЕНИЕ " + (generation.Key + 1));
                var generationRow = Row();
                generationRow.style.flexWrap = Wrap.Wrap;
                generationRow.style.justifyContent = Justify.Center;
                foreach (var member in generation.OrderBy(value => value.BiologicalBirthTick).ThenBy(value => value.Id))
                {
                    var place = Array.FindIndex(candidates, value => value.Id == member.Id);
                    var role = member.Id == context.Dynasty.CurrentMonarchId ? "Правитель" :
                        place >= 0 ? (place + 1) + "-й в очереди" :
                        member.LifeState == CharacterLifeState.Alive ? "Член династии" : "Умер(ла)";
                    var familyUnit = Row();
                    familyUnit.name = "dynasty-couple-" + member.Id;
                    familyUnit.style.alignItems = Align.Center;
                    familyUnit.style.marginLeft = 6;
                    familyUnit.style.marginRight = 6;
                    AddDynastyPersonNode(familyUnit, member, role,
                        place == 0 || member.Id == context.Dynasty.CurrentMonarchId);
                    if (member.SpouseId.HasValue && spouseIds.Contains(member.SpouseId.Value) &&
                        context.Characters.TryGet(member.SpouseId.Value, out var spouse))
                    {
                        var marriage = new Label("↔") { name = "dynasty-spouse-connector-" + member.Id };
                        marriage.style.color = AccentColor;
                        marriage.style.fontSize = 22;
                        familyUnit.Add(marriage);
                        AddDynastyPersonNode(familyUnit, spouse,
                            member.Id == context.Dynasty.CurrentMonarchId ? "Супруг(а) правителя" : "Супруг(а)", false);
                    }
                    generationRow.Add(familyUnit);
                }
                generationBlock.Add(generationRow);
                tree.Add(generationBlock);
            }
            mainPanel.Add(tree);

            AddSection(mainPanel, "Линия престолонаследия");
            if (candidates.Length == 0) AddMuted(mainPanel, "Среди живых членов династии нет допустимого наследника.");
            var succession = Row();
            succession.name = "dynasty-succession-line";
            succession.style.flexWrap = Wrap.Wrap;
            for (var index = 0; index < Math.Min(12, candidates.Length); index++)
            {
                var candidate = candidates[index];
                var captured = candidate.Id;
                var button = ActionButton((index + 1) + " · " + CharacterName(candidate.Id), () =>
                {
                    selectedDynastyPerson = captured;
                    SelectResidentFromWorkspace(captured);
                }, "dynasty-candidate-" + index);
                SerenityUiDesign.SetSelected(button, index == 0);
                succession.Add(button);
            }
            mainPanel.Add(succession);

            var pregnancies = context.Health.Pregnancies.Where(x => x.Status == PregnancyStatus.Active &&
                context.Dynasty.Contains(x.MotherId)).OrderBy(x => x.DueCalendarTick).ToArray();
            if (pregnancies.Length > 0)
            {
                AddSection(mainPanel, F("ui.dynasty.pregnancies", pregnancies.Length));
                foreach (var pregnancy in pregnancies.Take(8))
                    AddMuted(mainPanel, F("ui.dynasty.pregnancy", CharacterName(pregnancy.MotherId),
                        FormatDuration(Math.Max(0, pregnancy.DueCalendarTick - context.Clock().State.CalendarTicks))));
            }
            AddSection(mainPanel, T("ui.dynasty.history"));
            if (context.Dynasty.History.Count == 0) AddMuted(mainPanel, "Смена правителя ещё не происходила.");
            foreach (var entry in context.Dynasty.History.Reverse().Take(6))
                AddLine(mainPanel, F("ui.dynasty.history_line", CharacterName(entry.PreviousMonarchId),
                    CharacterName(entry.NewMonarchId), E(entry.Reason)));
        }

        private void AddDynastyPersonNode(VisualElement parent, Character person, string role, bool emphasized)
        {
            var captured = person.Id;
            var card = ActionButton(CharacterName(person.Id) + "\n" + role + " · " +
                person.AgeInCompletedYears(context.Clock().State.BiologicalTicks) + " лет", () =>
                {
                    selectedDynastyPerson = captured;
                    SelectResidentFromWorkspace(captured);
                }, "dynasty-person-" + person.Id);
            card.style.minWidth = 220;
            card.style.minHeight = 62;
            card.AddToClassList("serenity-card-action");
            SerenityUiDesign.SetSelected(card, emphasized || selectedDynastyPerson == person.Id);
            parent.Add(card);
        }

        private int GetDynastyGeneration(Character person, ISet<StableEntityId> dynastyMembers,
            IDictionary<StableEntityId, int> generations)
        {
            if (generations.TryGetValue(person.Id, out var known)) return known;
            var parents = person.Parents.Where(link => dynastyMembers.Contains(link.ParentId) &&
                    context.Characters.TryGet(link.ParentId, out _))
                .Select(link => context.Characters.Get(link.ParentId)).ToArray();
            var generation = parents.Length == 0 ? 0 :
                1 + parents.Max(parent => GetDynastyGeneration(parent, dynastyMembers, generations));
            generations[person.Id] = generation;
            return generation;
        }

        private void DrawDomesticPanel()
        {
            AddTitle(mainPanel, "Люди · дом, семья и здоровье");
            AddMuted(mainPanel, "Реестр жителей и подробности выбранного человека.");
            var search = new TextField("Поиск") { value = peopleSearch, name = "people-search" };
            StyleTextField(search);
            search.RegisterValueChangedCallback(evt =>
            {
                peopleSearch = evt.newValue ?? string.Empty;
                RefreshPanel();
            });
            mainPanel.Add(search);
            var residents = PlayerCharacterIds().Select(residentId =>
                    context.Characters.TryGet(residentId, out var resident) ? resident : null)
                .Where(resident => resident != null && resident.LifeState == CharacterLifeState.Alive &&
                    (string.IsNullOrWhiteSpace(peopleSearch) || resident.Name.DisplayName.IndexOf(peopleSearch,
                        StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(resident => resident.Name.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            AddLine(mainPanel, "Жителей: " + residents.Count);
            var roster = new ListView(residents, 38,
                () =>
                {
                    var button = ActionButton(string.Empty, () => { });
                    button.clicked += () => SelectResidentFromWorkspace((StableEntityId)button.userData);
                    button.AddToClassList("serenity-roster-row");
                    return button;
                },
                (element, index) =>
                {
                    var resident = residents[index];
                    var button = (Button)element;
                    button.userData = resident.Id;
                    var groupName = context.Work.Groups.TryGetForMember(resident.Id, out var group)
                        ? group.DisplayName : "Ожидает распределения";
                    button.text = RussianTextCatalog.PersonName(resident.Name.DisplayName) + " · " +
                        ProfessionName(resident.ProfessionId?.ToString()) + " · " + groupName;
                })
            {
                name = "people-roster",
                selectionType = SelectionType.None
            };
            roster.style.height = 150;
            mainPanel.Add(roster);
            var id = context.Selection.SelectedCharacterId ?? context.Dynasty.CurrentMonarchId;
            if (!id.HasValue)
            {
                AddMuted(mainPanel, "Выберите жителя.");
                return;
            }
            var character = context.Characters.Get(id.Value);
            AddSection(mainPanel, RussianTextCatalog.PersonName(character.Name.DisplayName));
            var detail = Row();
            detail.name = "people-detail-cards";
            detail.style.flexWrap = Wrap.Wrap;

            var identityCard = SerenityUiDesign.Card();
            identityCard.name = "people-identity-card";
            identityCard.style.minWidth = 280;
            identityCard.style.flexGrow = 1;
            AddSection(identityCard, "Состояние");
            AddLine(identityCard, character.AgeInCompletedYears(context.Clock().State.BiologicalTicks) + " лет · " +
                ProfessionName(character.ProfessionId?.ToString()) + " · " + DynastyRole(id.Value));
            var capabilities = context.Health.GetCapabilities(id.Value);
            AddLine(identityCard, capabilities.IsIncapacitated ? "Недееспособен" : "Может выполнять приказы");
            if (context.AiAgents.TryGet(id.Value, out var agent))
                AddLine(identityCard, $"Голод {agent.Needs.Hunger:P0} · жажда {agent.Needs.Thirst:P0} · " +
                    $"энергия {agent.Needs.Energy:P0} · настроение {agent.Needs.Mood:P0}");
            else AddMuted(identityCard, T("ui.character.offcamera"));
            var conditions = context.Health.Conditions.Where(x => x.TargetId == id.Value &&
                x.State != HealthConditionState.Recovered).OrderByDescending(x => x.Severity).ToArray();
            AddLine(identityCard, "Здоровье: " + (conditions.Length == 0 ? "без активных состояний" :
                string.Join(", ", conditions.Select(x => Content("condition", x.DefinitionId.ToString()) +
                    " · " + E(x.Severity)))));
            detail.Add(identityCard);

            var homeCard = SerenityUiDesign.Card();
            homeCard.name = "people-home-card";
            homeCard.style.minWidth = 280;
            homeCard.style.flexGrow = 1;
            AddSection(homeCard, "Дом и сон");
            var home = context.Domestic.Homes.FirstOrDefault(value => value.Allows(id.Value));
            var bed = context.Domestic.Beds.FirstOrDefault(value => value.AssignedResidentId == id.Value);
            if (home == null) AddLine(homeCard, "Дом не назначен");
            else
            {
                var homeName = context.Buildings.TryGet(home.BuildingId, out var homeBuilding)
                    ? Content("building", homeBuilding.DefinitionId.ToString()) : "Жилой дом";
                AddLine(homeCard, homeName + " · жильцов " + home.ResidentIds.Count);
                AddMuted(homeCard, "Владелец: " + CharacterName(home.OwnerId));
            }
            var sleep = context.Domestic.EvaluateSleep(id.Value);
            AddLine(homeCard, sleep.Allowed
                ? "Кровать назначена и доступна."
                : "Сон недоступен: " + SleepReason(sleep.Reason));
            if (bed != null) AddMuted(homeCard, bed.OccupiedResidentId.HasValue ? "Кровать занята" : "Кровать свободна");
            var sleepButton = ActionButton("Лечь спать", () => TrySleep(id.Value), "domestic-sleep");
            sleepButton.SetEnabled(sleep.Allowed);
            homeCard.Add(sleepButton);
            detail.Add(homeCard);

            var familyCard = SerenityUiDesign.Card();
            familyCard.name = "people-family-card";
            familyCard.style.minWidth = 280;
            familyCard.style.flexGrow = 1;
            AddSection(familyCard, "Семья");
            AddLine(familyCard, "Супруг(а): " + CharacterName(character.SpouseId));
            AddLine(familyCard, "Родители: " + (character.Parents.Count == 0 ? "нет сведений" :
                string.Join(", ", character.Parents.Select(value => CharacterName(value.ParentId)))));
            var selectedChildren = context.Family.GetChildren(id.Value).ToArray();
            AddLine(familyCard, "Дети: " + (selectedChildren.Length == 0 ? "нет" :
                string.Join(", ", selectedChildren.Select(value => CharacterName(value.Id)))));
            detail.Add(familyCard);
            mainPanel.Add(detail);

            if (!character.SpouseId.HasValue) return;
            var spouseId = character.SpouseId.Value;
            AddSection(mainPanel, "Семейные решения");
            var consent = context.Domestic.Consents.FirstOrDefault(x =>
                x.FirstId == id.Value && x.SecondId == spouseId || x.FirstId == spouseId && x.SecondId == id.Value);
            AddLine(mainPanel, "Согласие: " + (consent != null && consent.IsMutual ? "взаимное" : "ожидается от обоих взрослых"));
            var consentRow = Row();
            consentRow.Add(ActionButton("Согласие: " + CharacterName(id.Value), () =>
                TryCommand(() => context.Domestic.SetConsent(id.Value, spouseId, true)), "domestic-consent-first"));
            consentRow.Add(ActionButton("Согласие: " + CharacterName(spouseId), () =>
                TryCommand(() => context.Domestic.SetConsent(spouseId, id.Value, true)), "domestic-consent-second"));
            mainPanel.Add(consentRow);
            var eligibility = context.Domestic.EvaluateFamilyInteraction(id.Value, spouseId,
                context.Clock().State.BiologicalTicks);
            if (!eligibility.Allowed) AddMuted(mainPanel, "Семейное событие недоступно: " + FamilyReason(eligibility.Reason));
            var family = ActionButton("Провести время вместе", () => RequestFamilyInteraction(id.Value, spouseId),
                "domestic-family-event");
            family.SetEnabled(eligibility.Allowed);
            mainPanel.Add(family);
            var pregnancy = context.Health.Pregnancies.FirstOrDefault(x => x.Status == PregnancyStatus.Active &&
                (x.MotherId == id.Value || x.MotherId == spouseId));
            if (pregnancy != null)
                AddLine(mainPanel, "Беременность · до события: " +
                    FormatDuration(Math.Max(0, pregnancy.DueCalendarTick - context.Clock().State.CalendarTicks)));
        }

        public bool TrySleep(StableEntityId characterId)
        {
            if (context.AiScheduler.TryStartSleepNow(characterId, out var reason))
            {
                AddNotification("Житель направился к своей кровати.");
                RefreshNow();
                return true;
            }
            SetStatus("Сон недоступен: " + SleepReason(reason));
            return false;
        }

        public bool RequestFamilyInteraction(StableEntityId firstId, StableEntityId secondId)
        {
            var eligibility = context.Domestic.EvaluateFamilyInteraction(firstId, secondId,
                context.Clock().State.BiologicalTicks);
            if (!eligibility.Allowed)
            {
                SetStatus("Семейное событие недоступно: " + FamilyReason(eligibility.Reason));
                return false;
            }
            context.Domestic.StartFamilyInteraction(firstId, secondId, context.Clock().State.CalendarTicks,
                context.Clock().State.BiologicalTicks);
            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddTitle(modal, "Время проходит…");
            AddLine(modal, "Личная сцена остаётся за кадром.");
            modal.Add(ActionButton("Пропустить", CloseModal, "domestic-fade-skip"));
            return true;
        }

        private static string SleepReason(SleepDenialReason reason)
        {
            switch (reason)
            {
                case SleepDenialReason.HomeIncomplete: return "дом ещё не достроен";
                case SleepDenialReason.HomeNotClosed: return "помещение не закрыто";
                case SleepDenialReason.HomeUninhabitable: return "помещение непригодно для жизни";
                case SleepDenialReason.AccessDenied: return "нет доступа к чужому дому";
                case SleepDenialReason.BedOutsideHome: return "кровать стоит снаружи";
                case SleepDenialReason.BedOccupied: return "кровать занята";
                case SleepDenialReason.BedAssignedToAnotherResident: return "кровать назначена другому жителю";
                case SleepDenialReason.ProtectedActivity: return "сначала нужно закончить перенос предмета или прямое управление";
                case SleepDenialReason.CriticalHunger: return "сначала нужно поесть";
                case SleepDenialReason.NoAssignedBed: return "нет назначенной кровати";
                default: return reason.ToString();
            }
        }

        private static string FamilyReason(FamilyInteractionDenialReason reason)
        {
            switch (reason)
            {
                case FamilyInteractionDenialReason.NotAdult: return "доступно только взрослым";
                case FamilyInteractionDenialReason.ConsentMissing: return "нужно независимое согласие обоих";
                case FamilyInteractionDenialReason.NotMutualSpouses: return "нет взаимной семейной связи";
                case FamilyInteractionDenialReason.RelationshipTooWeak: return "отношения недостаточно близкие";
                case FamilyInteractionDenialReason.HealthPreventsPregnancy: return "состояние здоровья не позволяет";
                case FamilyInteractionDenialReason.PregnancyAlreadyActive: return "беременность уже идёт";
                default: return reason.ToString();
            }
        }

        private void DrawMilitaryPanel()
        {
            AddTitle(mainPanel, "Военные");
            AddMuted(mainPanel, "Состав, обучение, приказы, снабжение и состояние боя.");
            var playerSide = new MilitarySideId("player");
            var units = context.Military.Units.Where(x => x.OwnerSideId == playerSide &&
                x.Status != MilitaryUnitStatus.Disbanded).OrderBy(x => x.Id).ToList();
            var unitLimit = context.Military.GetActiveUnitLimit(playerSide);
            var activeMembers = context.Military.Units
                .Where(unit => unit.Status != MilitaryUnitStatus.Disbanded)
                .SelectMany(unit => unit.ActiveMemberIds).ToHashSet();
            var recruits = PlayerCharacterIds().Select(id =>
                    context.Characters.TryGet(id, out var person) ? person : null)
                .Where(person => person != null && person.LifeState == CharacterLifeState.Alive &&
                    context.Military.IsEligibleMilitaryMember(playerSide, person.Id) &&
                    !activeMembers.Contains(person.Id))
                .OrderBy(person => person.Name.DisplayName, StringComparer.Ordinal).ToList();
            militaryDraftSelection.RemoveWhere(id => recruits.All(person => person.Id != id));
            AddSection(mainPanel, "Новый отряд");
            AddMuted(mainPanel, "Выберите военных прямо здесь. Гражданские жители не могут входить в отряд.");
            AddLine(mainPanel, "Активные отряды: " + units.Count + " / " + unitLimit);
            var recruitList = new ListView(recruits, 42,
                () =>
                {
                    var button = ActionButton(string.Empty, () => { });
                    button.clicked += () => ToggleMilitaryRecruit((StableEntityId)button.userData);
                    button.AddToClassList("serenity-roster-row");
                    return button;
                },
                (element, recruitIndex) =>
                {
                    var recruit = recruits[recruitIndex];
                    var button = (Button)element;
                    button.userData = recruit.Id;
                    var selected = militaryDraftSelection.Contains(recruit.Id);
                    button.text = (selected ? "☑ " : "☐ ") + CharacterName(recruit.Id) + " · " +
                                  ProfessionName(recruit.ProfessionId?.ToString());
                    SerenityUiDesign.SetSelected(button, selected);
                })
            {
                name = "military-recruit-roster",
                selectionType = SelectionType.None
            };
            if (recruits.Count == 0) recruitList.style.display = DisplayStyle.None;
            else recruitList.style.height = Mathf.Min(252, Math.Max(46, recruits.Count * 42));
            mainPanel.Add(recruitList);
            SuppressTechnicalEmptyListLabel(recruitList);
            AddMuted(mainPanel, "Выбрано: " + militaryDraftSelection.Count + " / " +
                MilitaryUnitService.MaximumSquadMembers);
            var hasSelectedCharacters = militaryDraftSelection.Count > 0;
            var hasCaptain = militaryDraftSelection.Any(id => context.Military.IsEligibleMilitaryMember(playerSide, id));
            var createUnit = ActionButton("Сформировать отряд", () => TryCommand(CreatePlayerUnit),
                "military-create-unit");
            createUnit.SetEnabled(hasSelectedCharacters && hasCaptain && units.Count < unitLimit);
            mainPanel.Add(createUnit);
            if (units.Count >= unitLimit) AddMuted(mainPanel, "Достигнут текущий предел активных отрядов.");
            else if (!hasSelectedCharacters) AddMuted(mainPanel, recruits.Count == 0
                ? "Нет свободных военных. Сначала подготовьте жителя в казарме или назначьте военную профессию."
                : T("ui.military.need_characters"));
            else if (!hasCaptain) AddMuted(mainPanel, T("ui.military.no_captain"));
            if ((!selectedUnit.HasValue || units.All(x => x.Id != selectedUnit.Value)) && units.Count > 0)
                selectedUnit = units[0].Id;
            AddSection(mainPanel, "Отряды");
            if (units.Count == 0) AddMuted(mainPanel, "Сформированных отрядов нет.");
            var unitList = new ListView(units, 54,
                () =>
                {
                    var button = ActionButton(string.Empty, () => { });
                    button.clicked += () => { selectedUnit = (MilitaryUnitId)button.userData; RefreshPanel(); };
                    button.AddToClassList("serenity-roster-row");
                    return button;
                },
                (element, unitIndex) =>
                {
                    var unit = units[unitIndex];
                    var button = (Button)element;
                    var unitSummary = context.Military.GetRosterSummary(unit.Id);
                    button.userData = unit.Id;
                    button.text = UnitLabel(unit, unitIndex + 1) + " · " + unitSummary.Active + " бойцов · " +
                        E(unit.Status) + " · " + E(unit.Order.Kind);
                    SerenityUiDesign.SetSelected(button, selectedUnit == unit.Id);
                })
            {
                name = "military-unit-roster",
                selectionType = SelectionType.None
            };
            if (units.Count == 0) unitList.style.display = DisplayStyle.None;
            else unitList.style.height = Mathf.Min(180, Math.Max(58, units.Count * 54));
            mainPanel.Add(unitList);
            SuppressTechnicalEmptyListLabel(unitList);
            if (!selectedUnit.HasValue || !context.Military.TryGet(selectedUnit.Value, out var selected)) return;
            var selectedSummary = context.Military.GetRosterSummary(selected.Id);
            var metrics = Row();
            metrics.name = "military-readiness-summary";
            metrics.style.flexWrap = Wrap.Wrap;
            AddMetricCard(metrics, "ЧИСЛЕННОСТЬ", selectedSummary.Active + " / " + MilitaryUnitService.MaximumSquadMembers,
                selectedSummary.CombatCapable + " боеспособны");
            AddMetricCard(metrics, "МОРАЛЬ", RussianTextCatalog.Percent(selected.MoraleSummary / 1000d), E(selected.Status));
            AddMetricCard(metrics, "СНАБЖЕНИЕ", selected.Supply.Initialized
                    ? selected.Supply.Get(context.Military.SupplySettings.FoodResourceId) + " ед."
                    : "Не загружено",
                Content("resource", context.Military.SupplySettings.FoodResourceId.ToString()));
            AddMetricCard(metrics, "ГОТОВНОСТЬ", E(selected.Order.Kind),
                E(selected.Representation) + " · " + LocationName(selected.AbstractLocationId));
            mainPanel.Add(metrics);

            AddSection(mainPanel, "Состав");
            var selectedMembers = selected.ActiveMemberIds.Select(id => context.Characters.Get(id)).ToList();
            var memberList = new ListView(selectedMembers, 38,
                () =>
                {
                    var button = ActionButton(string.Empty, () => { });
                    button.clicked += () => SelectResidentFromWorkspace((StableEntityId)button.userData);
                    button.AddToClassList("serenity-roster-row");
                    return button;
                },
                (element, memberIndex) =>
                {
                    var person = selectedMembers[memberIndex];
                    var button = (Button)element;
                    button.userData = person.Id;
                    var rank = context.MilitaryTraining.GetRank(person.Id);
                    var capability = context.Health.GetCapabilities(person.Id);
                    button.text = CharacterName(person.Id) + " · " +
                        (rank.HasValue ? MilitaryRankName(rank.Value) : "без звания") + " · " +
                        (selected.CaptainId == person.Id ? "командир · " : string.Empty) +
                        (capability.CanFight ? "готов" : "небоеспособен");
                })
            {
                name = "military-member-roster",
                selectionType = SelectionType.None
            };
            memberList.style.height = Mathf.Min(190, Math.Max(42, selectedMembers.Count * 38));
            mainPanel.Add(memberList);

            AddSection(mainPanel, "Состав и командование");
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
            if (context.Selection.SelectedCharacterIds.Count == 0)
                AddMuted(mainPanel, "Выберите жителя, чтобы изменить состав или командира.");
            AddSection(mainPanel, "Приказы");
            var orders = Row();
            orders.Add(ActionButton(T("ui.military.order.idle"), () => TryCommand(() => context.Military.SetOrder(selected.Id, MilitaryUnitOrderKind.Idle))));
            orders.Add(ActionButton(T("ui.military.order.move"), () => TryCommand(() => context.MilitaryCommands.Move(selected.Id, new WorldPosition(0, 0, 10)))));
            orders.Add(ActionButton(T("ui.military.order.hold"), () => TryCommand(() => context.MilitaryCommands.Hold(selected.Id))));
            var engage = ActionButton(T("ui.military.order.engage"), () => { }, "military-engage-disabled");
            engage.SetEnabled(false);
            engage.tooltip = "Выберите вражеский военный объект в мире и используйте «Напасть».";
            orders.Add(engage);
            mainPanel.Add(orders);
            AddMuted(mainPanel, "Для нападения выберите в мире вражеский военный объект; приказ проверит состав, сторону и путь.");
            var representations = Row();
            representations.Add(ActionButton(T("ui.military.detailed"), () => TryCommand(() => context.MilitaryRepresentations.Transition(selected.Id, MilitaryUnitRepresentationMode.Detailed))));
            representations.Add(ActionButton(T("ui.military.aggregate"), () => TryCommand(() => context.MilitaryRepresentations.Transition(selected.Id, MilitaryUnitRepresentationMode.Aggregate))));
            mainPanel.Add(representations);

            AddSection(mainPanel, "Снабжение");
            var foodId = context.Military.SupplySettings.FoodResourceId;
            var currentSupply = selected.Supply.Get(foodId);
            var targetSupply = selectedSummary.Active * context.Military.SupplySettings.InitialFoodPerMember;
            var availableSupply = context.Inventories.All.Where(x => !x.IssuanceLocked)
                .Sum(x => x.GetAmount(foodId).Units);
            AddLine(mainPanel, Content("resource", foodId.ToString()) + ": " + currentSupply + " / " + targetSupply);
            var loadSupply = ActionButton("Пополнить снабжение", () => TryCommand(() =>
            {
                var loaded = context.Military.LoadInitialSupply(selected.Id, context.Inventories,
                    context.Inventories.All.Select(x => x.Owner));
                if (loaded <= 0) throw new PlayerFacingException("Снабжение уже полное или на складах нет подходящих запасов.");
                SetStatus("В отряд передано: " + loaded + " " + Content("resource", foodId.ToString()) + ".");
            }), "military-load-supply");
            loadSupply.SetEnabled(currentSupply < targetSupply && availableSupply > 0);
            mainPanel.Add(loadSupply);
            if (currentSupply >= targetSupply) AddMuted(mainPanel, "Отряд полностью снабжён.");
            else if (availableSupply <= 0) AddMuted(mainPanel, "Пополнение недоступно: на доступных складах нет " +
                Content("resource", foodId.ToString()) + ".");

            DrawMilitaryTraining();
            AddSection(mainPanel, T("ui.military.battles"));
            var activeBattles = context.Battles.Battles.Where(x => x.Status == CombatEncounterStatus.Active)
                .OrderBy(x => x.BattleId).ToArray();
            if (activeBattles.Length == 0) AddMuted(mainPanel, "Активных боёв нет.");
            foreach (var battle in activeBattles)
            {
                var battleCard = SerenityUiDesign.Card();
                battleCard.name = "military-combat-status";
                AddSection(battleCard, "Бой · " + E(battle.Mode));
                AddLine(battleCard, BattleCounts(battle));
                AddMuted(battleCard, "Мораль: " + SideName(battle.SideA) + " " +
                    RussianTextCatalog.Percent(SideMorale(battle, battle.SideA) / 1000d) + " · " +
                    SideName(battle.SideB) + " " + RussianTextCatalog.Percent(SideMorale(battle, battle.SideB) / 1000d));
                if (battle.Mode == BattleMode.Abstract)
                    battleCard.Add(ActionButton(T("ui.military.enter_battle"), () => context.RequestEnterBattle(battle.BattleId)));
                mainPanel.Add(battleCard);
            }
            AddSection(mainPanel, "Захват поселений");
            foreach (var capture in context.SettlementCapture.States)
            {
                AddLine(mainPanel, LocationName(context.GlobalMap.Settlements.Single(x => x.Id == capture.SettlementId).LocationId) +
                    " · " + capture.Status + " · " + RussianTextCatalog.Percent(
                        (double)capture.ProgressTicks / capture.RequiredTicks));
                AddMuted(mainPanel, "Главное здание: " + capture.MainBuildingId + " · главные казармы: " +
                    (capture.ActiveBarracksId?.ToString() ?? "нет — нужен только флаг главного здания"));
            }
        }

        private void DrawMilitaryTraining()
        {
            AddSection(mainPanel, "Обучение и звания");
            var traineeId = context.Selection.SelectedCharacterId;
            var barracks = context.Buildings.All.Where(x =>
                    x.DefinitionId.FamilyId == MilitaryTrainingService.BarracksDefinitionId &&
                    x.IsOperational && x.OwnerSideId.Value == "player")
                .OrderBy(x => x.Id).FirstOrDefault();
            if (!traineeId.HasValue)
            {
                AddMuted(mainPanel, "Выберите одного взрослого жителя.");
                return;
            }
            var rank = context.MilitaryTraining.GetRank(traineeId.Value);
            var next = context.MilitaryTraining.GetNextLevel(traineeId.Value);
            AddLine(mainPanel, CharacterName(traineeId) + " · звание: " +
                (rank.HasValue ? MilitaryRankName(rank.Value) : "нет"));
            if (context.MilitaryTraining.IsExternallyControlled(traineeId.Value))
            {
                var active = context.MilitaryTraining.Active.Single(x => x.CharacterId == traineeId.Value);
                AddLine(mainPanel, "Обучение: " + MilitaryRankName(active.Rank) + " · осталось " +
                    FormatDuration(Math.Max(0, active.CompletionTick - context.Clock().State.CalendarTicks)));
                return;
            }
            if (next == null)
            {
                AddMuted(mainPanel, "Достигнуто высшее утверждённое звание.");
                return;
            }
            AddLine(mainPanel, "Следующий курс: " + MilitaryRankName(next.Rank) + " · " +
                FormatDuration(next.DurationTicks));
            AddMuted(mainPanel, "Цена: " + string.Join(", ", next.Cost.Select(x =>
                x.Quantity.Units + " " + Content("resource", x.ResourceId.ToString()))));
            var availability = barracks == null ? MilitaryTrainingStartFailure.MissingBarracks :
                context.MilitaryTraining.CheckStart(traineeId.Value, barracks.Id, new CombatSideId("player"),
                    context.Clock().State.CalendarTicks, context.Inventories.All.Select(x => x.Owner));
            var train = ActionButton("Начать обучение", () => TryCommand(() =>
            {
                if (barracks == null) throw new PlayerFacingException("Нет действующих казарм.");
                var result = context.MilitaryTraining.TryStart(traineeId.Value, barracks.Id,
                    new CombatSideId("player"), context.Clock().State.CalendarTicks,
                    context.Inventories.All.Select(x => x.Owner));
                if (!result.Started) throw new PlayerFacingException("Обучение недоступно: " +
                    MilitaryTrainingRequirementText(traineeId.Value, result.Failure));
            }), "military-train");
            train.SetEnabled(availability == MilitaryTrainingStartFailure.None);
            mainPanel.Add(train);
            if (availability != MilitaryTrainingStartFailure.None)
                AddMuted(mainPanel, "Обучение недоступно: " +
                    MilitaryTrainingRequirementText(traineeId.Value, availability));
        }

        private static string MilitaryTrainingFailureText(MilitaryTrainingStartFailure failure)
        {
            switch (failure)
            {
                case MilitaryTrainingStartFailure.MissingCharacter: return "житель недоступен";
                case MilitaryTrainingStartFailure.NotAdult: return "требуется взрослый житель";
                case MilitaryTrainingStartFailure.Incapacitated: return "житель тяжело ранен";
                case MilitaryTrainingStartFailure.MissingBarracks: return "нет действующей казармы";
                case MilitaryTrainingStartFailure.BarracksInactive: return "казарма не работает";
                case MilitaryTrainingStartFailure.WrongOwner: return "чужая казарма";
                case MilitaryTrainingStartFailure.AlreadyTraining: return "курс уже идёт";
                case MilitaryTrainingStartFailure.MaximumRank: return "достигнуто высшее звание";
                case MilitaryTrainingStartFailure.InsufficientResources: return "не хватает доступного снаряжения или ресурсов";
                case MilitaryTrainingStartFailure.BarracksFull: return "все шесть мест заняты";
                case MilitaryTrainingStartFailure.RankLimitReached: return "достигнут государственный лимит звания";
                case MilitaryTrainingStartFailure.ReservedForRuler: return "генералиссимусом является правящий вождь";
                case MilitaryTrainingStartFailure.InsufficientService: return "не завершён срок службы в текущем звании";
                case MilitaryTrainingStartFailure.InsufficientBattleExperience: return "не хватает боевого опыта";
                default: return failure.ToString();
            }
        }

        private string MilitaryTrainingRequirementText(StableEntityId characterId,
            MilitaryTrainingStartFailure failure)
        {
            var next = context.MilitaryTraining.GetNextLevel(characterId);
            if (failure == MilitaryTrainingStartFailure.InsufficientResources && next != null)
            {
                var missing = next.Cost.Select(cost =>
                {
                    var available = context.Inventories.All.Where(x => !x.IssuanceLocked)
                        .Sum(x => x.GetAmount(cost.ResourceId).Units);
                    return (cost, missing: Math.Max(0, cost.Quantity.Units - available));
                }).Where(x => x.missing > 0).ToArray();
                if (missing.Length > 0) return "нужно принести: " + string.Join(", ", missing.Select(x =>
                    x.missing + " " + Content("resource", x.cost.ResourceId.ToString())));
            }
            if (failure == MilitaryTrainingStartFailure.InsufficientService && next != null)
            {
                var last = context.MilitaryTraining.Records.Where(x => x.CharacterId == characterId && x.Completed)
                    .OrderByDescending(x => x.CompletionTick).ThenByDescending(x => x.Sequence).FirstOrDefault();
                var served = last == null ? 0 : Math.Max(0, context.Clock().State.CalendarTicks - last.CompletionTick);
                return "для курса «" + MilitaryRankName(next.Rank) + "» осталось отслужить " +
                    FormatDuration(Math.Max(0, next.MinimumServiceTicks - served)) +
                    " (требуется " + FormatDuration(next.MinimumServiceTicks) + ")";
            }
            if (failure == MilitaryTrainingStartFailure.InsufficientBattleExperience && next != null &&
                context.Characters.TryGet(characterId, out var character))
            {
                var current = context.MilitaryTraining.GetRank(characterId);
                var required = current.HasValue ? Math.Min(SkillLevel.Maximum,
                    context.MilitaryTraining.Catalog.Get(current.Value).CombatSkillLevel + next.RequiredBattleExperience) : 0;
                return "для курса «" + MilitaryRankName(next.Rank) + "» нужен боевой навык " + required +
                    ", сейчас " + CharacterSkillRules.Level(character, MilitaryTrainingService.CombatSkillId);
            }
            return MilitaryTrainingFailureText(failure);
        }

        private void DrawSavePanel()
        {
            AddTitle(mainPanel, T("ui.save.title"));
            AddLine(mainPanel, T("ui.save.explanation"));
            AddLine(mainPanel, context.Diagnostics.CheatsUsed
                ? "Коды использованы · достижения отключены для этой партии."
                : "Коды не использованы · достижения включены.");
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
                row.Add(ActionButton(T("common.save"), () => SaveSlot(slot.Index), "save-slot-" + slot.Index));
                var load = ActionButton(T("common.load"), () => RequestLoadSlot(slot.Index), "load-slot-" + slot.Index);
                load.SetEnabled(slot.Exists && !slot.IsCorrupt);
                if (!slot.Exists) load.tooltip = T("ui.save.empty_slot");
                else if (slot.IsCorrupt) load.tooltip = T("ui.save.corrupt_slot");
                row.Add(load);
                card.Add(row);
                if (slot.IsCorrupt) AddMuted(card, T("ui.save.corrupt_slot"));
                mainPanel.Add(card);
            }
        }

        private readonly HashSet<string> seenWarWarnings = new HashSet<string>();

        private string WarWarning(Game.Simulation.AI.AiFactionRuntime faction, AiPendingOrder order)
        {
            var remaining = Math.Max(0, order.IssuedTick + context.StrategicAi.AttackWarningTicks - context.Clock().State.CalendarTicks);
            return SideName(faction.Strategic.SideId) + " готовит нападение на " +
                LocationName(order.TargetLocationId.Value) + ". Выход армии не раньше чем через " + FormatDuration(remaining) + ".";
        }

        private bool TargetsPlayer(AiPendingOrder order) => order.TargetSettlementId.HasValue &&
            context.GlobalMap.Settlements.Any(x => x.Id == order.TargetSettlementId.Value && x.OwnerSideId.Value == "player");

        private void CaptureWarWarnings()
        {
            if (context.StrategicAi == null) return;
            foreach (var faction in context.StrategicAi.Factions)
            foreach (var order in faction.Strategic.PendingOrders.Where(x => x.Kind == AiDecisionKind.PrepareAttack && TargetsPlayer(x)))
                if (seenWarWarnings.Add(faction.Strategic.Id + ":" + order.Sequence))
                    AddNotification("Предупреждение: " + WarWarning(faction, order));
        }

        private void DrawDiplomacy(VisualElement parent)
        {
            AddSection(parent, "Дипломатия — будущая возможность");
            var knownForeign = context.GlobalMap.Settlements.Where(settlement =>
                    settlement.OwnerSideId.Value != "player" && IsKnownLocation(settlement.LocationId) &&
                    (context.Campaign == null || context.Campaign.CanRevealSettlement(settlement.Id)))
                .OrderBy(settlement => settlement.LocationId).ToArray();
            if (knownForeign.Length == 0)
                AddMuted(parent, "Контакт с другим государством ещё не подтверждён разведкой.");
            else
                foreach (var settlement in knownForeign)
                    AddLine(parent, "Известный факт: " + LocationName(settlement.LocationId) + " · " +
                        SideName(settlement.OwnerSideId));
            AddMuted(parent, "Отношения, договоры, торговля, требования и последствия пока не моделируются.");
            foreach (var label in new[] { "Предложить торговое соглашение", "Заключить договор", "Отправить требование" })
            {
                var button = ActionButton(label + " · недоступно", () => { }, "diplomacy-unavailable");
                button.SetEnabled(false);
                button.tooltip = "Действие появится вместе с полноценной дипломатической системой.";
                parent.Add(button);
            }
        }

        private void DrawGlobalPanel()
        {
            if (realmLayers == null || realmDetail == null || realmRouteBar == null) return;
            realmLayers.Clear();
            realmDetail.Clear();
            realmRouteBar.Clear();
            if (context.GlobalMap.Mode == GameWorldMode.Local)
            {
                AddTitle(mainPanel, T("ui.map.title"));
                mainPanel.Add(ActionButton(T("ui.map.open"), context.RequestEnterGlobal));
                AddMuted(mainPanel, T("ui.map.local_continues"));
                return;
            }

            AddTitle(realmLayers, "Держава");
            AddMuted(realmLayers, "Известный мир · туман принадлежит разведке");
            AddSection(realmLayers, "Слои и легенда");
            AddFixedMapLayer(realmLayers, "Известная местность", "показана");
            AddMapLayer(realmLayers, "Поселения", showMapSettlements, value => showMapSettlements = value);
            AddMapLayer(realmLayers, "Армии", showMapArmies, value => showMapArmies = value);
            AddMapLayer(realmLayers, "Маршруты", showMapRoutes, value => showMapRoutes = value);
            AddMapLayer(realmLayers, "Погода", showMapWeather, value => showMapWeather = value);
            AddFixedMapLayer(realmLayers, "Туман и разведка", "канонический");
            AddMuted(realmLayers, "Инфраструктура, богатство и лояльность появятся после пространственных данных этих систем.");

            AddSection(realmLayers, T("ui.map.locations"));
            foreach (var location in context.GlobalMap.Locations.Where(value => IsKnownLocation(value.Id)))
            {
                var captured = location.Id;
                var button = ActionButton(LocationName(location.Id), () => SelectMapLocation(captured),
                    "realm-location-" + location.Id.Value);
                SerenityUiDesign.SetSelected(button, selectedLocation == location.Id);
                realmLayers.Add(button);
            }
            var scouting = context.ConnectedWorld?.CaptureState().ScoutingAssignments ??
                           Array.Empty<ScoutingAssignmentState>();
            AddSection(realmLayers, "Разведка");
            AddLine(realmLayers, "Открыто ячеек: " + (context.ConnectedWorld?.RevealedCellCount ?? 0));
            AddLine(realmLayers, scouting.Count == 0 ? "Активных назначений нет." :
                "Назначений: " + scouting.Count + " · завершено: " + scouting.Count(value => value.Completed));

            AddTitle(realmDetail, "Выбранная цель");
            DrawSelectedMapDetail(realmDetail);
            DrawDiplomacy(realmDetail);
            AddSection(realmDetail, T("ui.map.battles"));
            foreach (var battle in context.Battles.Battles.Where(value => value.Status == CombatEncounterStatus.Active))
            {
                AddLine(realmDetail, F("ui.map.battle", E(battle.Mode), BattleCounts(battle), E(battle.Outcome)));
                if (battle.Mode == BattleMode.Abstract)
                    realmDetail.Add(ActionButton(T("ui.map.enter_battle"), () => context.RequestEnterBattle(battle.BattleId),
                        "realm-enter-battle"));
            }

            DrawRouteSummary(realmRouteBar);
        }

        private void AddMapLayer(VisualElement parent, string label, bool enabled, Action<bool> change)
        {
            var button = ActionButton((enabled ? "● " : "○ ") + label, () =>
            {
                change(!enabled);
                RefreshNow();
            }, "realm-layer-" + label.ToLowerInvariant().Replace(' ', '-'));
            SerenityUiDesign.SetSelected(button, enabled);
            parent.Add(button);
        }

        private static void AddFixedMapLayer(VisualElement parent, string label, string state)
        {
            var row = SerenityUiDesign.Card();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            var title = new Label("● " + label);
            title.AddToClassList(SerenityUiDesign.BodyClass);
            var value = new Label(state);
            value.AddToClassList(SerenityUiDesign.MutedClass);
            row.Add(title);
            row.Add(value);
            parent.Add(row);
        }

        private void DrawSelectedMapDetail(VisualElement parent)
        {
            if (selectedLocation.HasValue)
            {
                var location = context.GlobalMap.GetLocation(selectedLocation.Value);
                var settlement = context.GlobalMap.GetSettlementAt(location.Id);
                AddSection(parent, LocationName(location.Id));
                AddLine(parent, "Тип: " + E(location.Type));
                AddLine(parent, settlement == null ? T("ui.map.unclaimed") :
                    "Владелец: " + SideName(settlement.OwnerSideId));
                AddLine(parent, "Маршрутов: " + context.GlobalMap.GetRoutes(location.Id).Count);
                parent.Add(ActionButton("Войти в локацию", () => EnterSelectedMapLocation(),
                    "realm-enter-location"));
            }
            else AddMuted(parent, "Выберите известную локацию на карте или в списке.");

            if (selectedUnit.HasValue && context.Military.TryGet(selectedUnit.Value, out var unit))
            {
                AddSection(parent, "Армия");
                AddLine(parent, UnitLabel(unit, 1));
                AddLine(parent, "Положение: " + UnitMapLocation(unit));
                AddLine(parent, "Снабжение: " + UnitSupplyText(unit));
                AddLine(parent, "Состояние: " + E(unit.Status) + " · приказ: " + E(unit.Order.Kind));
            }
            else AddMuted(parent, "Выберите известную армию, чтобы рассчитать маршрут.");
        }

        private void DrawRouteSummary(VisualElement parent)
        {
            var summary = new VisualElement { name = "realm-route-facts" };
            summary.style.position = Position.Absolute;
            summary.style.left = 8;
            summary.style.right = 8;
            summary.style.top = 6;
            summary.style.height = 66;
            summary.style.overflow = Overflow.Hidden;
            if (!selectedUnit.HasValue || !selectedLocation.HasValue ||
                !context.Military.TryGet(selectedUnit.Value, out var unit))
            {
                AddLine(summary, "Маршрут: выберите армию и известную локацию.");
                parent.Add(summary);
                parent.Add(ActionButton("Вернуться в локальный мир", ReturnToLocalWorld, "realm-return-local"));
                return;
            }

            var origin = new WorldLocationId(unit.AbstractLocationId);
            var route = context.GlobalMap.GetRoutes(origin)
                .FirstOrDefault(value => value.Connects(origin, selectedLocation.Value));
            var weather = context.Weather.GetWeatherAtCalendarTick(context.Clock().State.CalendarTicks);
            var duration = route == null ? 0L : EstimateRouteDuration(route.TravelDurationTicks, unit,
                weather.Modifiers.MovementPerThousand);
            AddRouteFact(summary, 0, "Маршрут", LocationName(origin) + " → " + LocationName(selectedLocation.Value),
                route == null ? "Прямого пути нет" : FormatDuration(duration));
            AddRouteFact(summary, 1, "Погода", showMapWeather ? E(weather.Type) : "слой скрыт",
                "Скорость: " + Percent(weather.Modifiers.MovementPerThousand));
            AddRouteFact(summary, 2, "Снабжение", UnitSupplyText(unit),
                "Расход: " + RequiredTravelSupply(unit));
            AddRouteFact(summary, 3, "Риск", RouteRisk(unit, selectedLocation.Value, weather.Modifiers.MovementPerThousand),
                "Только подтверждённые факторы");
            parent.Add(summary);
            var actions = new VisualElement { name = "realm-route-actions" };
            actions.style.position = Position.Absolute;
            actions.style.right = 8;
            actions.style.bottom = 2;
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.height = 44;
            actions.style.overflow = Overflow.Hidden;
            actions.Add(RouteActionButton("Отправить", IssueSelectedMapTravel, "realm-travel"));
            actions.Add(RouteActionButton("Войти", EnterSelectedMapLocation, "realm-enter-selected"));
            actions.Add(RouteActionButton("Вернуться", ReturnToLocalWorld, "realm-return-local"));
            parent.Add(actions);
        }

        private static Button RouteActionButton(string label, Action action, string name)
        {
            var button = ActionButton(label, action, name);
            button.style.minWidth = 0;
            button.style.width = 96;
            button.style.height = 40;
            button.style.fontSize = 11;
            return button;
        }

        private static void AddRouteFact(VisualElement parent, int index, string title, string value, string detail)
        {
            // Route facts stay bounded so the strategic projection never obscures the world or bottom navigation.
            var card = new VisualElement { name = "realm-route-fact-" + index };
            card.AddToClassList(SerenityUiDesign.CardClass);
            card.style.position = Position.Absolute;
            card.style.left = new Length(index * 25f, LengthUnit.Percent);
            card.style.width = new Length(25, LengthUnit.Percent);
            card.style.minWidth = 0;
            card.style.height = 62;
            card.style.marginRight = 4;
            card.style.paddingLeft = 7;
            card.style.paddingRight = 7;
            card.style.paddingTop = 4;
            card.style.paddingBottom = 3;
            card.style.overflow = Overflow.Hidden;
            card.tooltip = title + ": " + value + "\n" + detail;
            var heading = new Label(title);
            heading.AddToClassList(SerenityUiDesign.MutedClass);
            heading.style.fontSize = 10;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(heading);
            var primary = new Label(value);
            primary.style.fontSize = 13;
            primary.style.unityFontStyleAndWeight = FontStyle.Bold;
            primary.style.whiteSpace = WhiteSpace.NoWrap;
            primary.style.overflow = Overflow.Hidden;
            card.Add(primary);
            var secondary = new Label(detail);
            secondary.AddToClassList(SerenityUiDesign.MutedClass);
            secondary.style.fontSize = 9;
            secondary.style.whiteSpace = WhiteSpace.NoWrap;
            secondary.style.overflow = Overflow.Hidden;
            card.Add(secondary);
            parent.Add(card);
        }

        private void IssueSelectedMapTravel() => TryCommand(() =>
        {
            if (!selectedUnit.HasValue || !selectedLocation.HasValue)
                throw PlayerError("ui.map.need_unit_destination");
            var result = context.GlobalMap.IssueTravelOrder(selectedUnit.Value, selectedLocation.Value,
                context.Clock().State.CalendarTicks);
            if (!result.Issued) throw new PlayerFacingException(TravelReason(result.Status));
            AddNotification(F("ui.map.departed", LocationName(selectedLocation.Value)));
        });

        private void EnterSelectedMapLocation()
        {
            if (!selectedLocation.HasValue)
            {
                SetStatus(T("ui.map.need_location"));
                return;
            }
            context.RequestEnterLocation(selectedLocation.Value);
            activePanel = GameplayPanel.None;
            mainPanel.style.display = DisplayStyle.None;
            RefreshNow();
        }

        private void ReturnToLocalWorld()
        {
            context.RequestEnterLocation(context.GlobalMap.ActiveLocationId);
            activePanel = GameplayPanel.None;
            mainPanel.style.display = DisplayStyle.None;
            RefreshNow();
        }

        private long RequiredTravelSupply(MilitaryUnitState unit) => checked(unit.ActiveMemberIds.Count *
            context.Military.SupplySettings.TravelFoodPerMember);

        private string UnitSupplyText(MilitaryUnitState unit)
        {
            var available = unit.Supply.Get(context.Military.SupplySettings.FoodResourceId);
            return available + " " + Content("resource", context.Military.SupplySettings.FoodResourceId.ToString());
        }

        private long EstimateRouteDuration(long baseTicks, MilitaryUnitState unit, int weatherModifier)
        {
            var supplyModifier = unit.Supply.Get(context.Military.SupplySettings.FoodResourceId) >= RequiredTravelSupply(unit)
                ? 1000 : context.Military.SupplySettings.ShortageMovementDurationPerThousand;
            var modifier = Math.Min(10000L, (weatherModifier * (long)supplyModifier + 999L) / 1000L);
            return Math.Max(1L, (checked(baseTicks * modifier) + 999L) / 1000L);
        }

        private string RouteRisk(MilitaryUnitState unit, WorldLocationId destination, int weatherModifier)
        {
            var settlement = context.GlobalMap.GetSettlementAt(destination);
            var hostile = settlement != null && settlement.OwnerSideId.Value != unit.OwnerSideId.Value;
            var shortSupply = unit.Supply.Get(context.Military.SupplySettings.FoodResourceId) < RequiredTravelSupply(unit);
            if (hostile || shortSupply) return "Высокий";
            return weatherModifier > 1200 ? "Повышенный" : "Низкий";
        }

        private string UnitMapLocation(MilitaryUnitState unit)
        {
            var travel = context.GlobalMap.Travels.FirstOrDefault(value => value.UnitId == unit.Id);
            return travel == null ? LocationName(unit.AbstractLocationId) :
                F("ui.map.travel_eta", LocationName(travel.Origin), LocationName(travel.Destination),
                    FormatDuration(Math.Max(0, travel.ArrivalTick - context.Clock().State.CalendarTicks)));
        }

        private bool IsKnownLocation(WorldLocationId id) => context.ConnectedWorld == null ||
            context.ConnectedWorld.IsLocationRevealed(id);

        private void RefreshInspector()
        {
            var hasSelection = context.Selection.InspectedCharacterId.HasValue ||
                               context.Selection.SelectedBuildingPresenter != null ||
                               context.Selection.SelectedWorldResourcePilePresenter != null ||
                               context.Selection.SelectedProductionSourcePresenter != null ||
                               context.Selection.SelectedWildlife != null;
            inspector.style.display = hasSelection && context.GlobalMap.Mode == GameWorldMode.Local
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (!hasSelection && emptyInspectorRendered) return;
            emptyInspectorRendered = !hasSelection;
            inspector.Clear();
            AddTitle(inspector, T("ui.selection.title"));
            var wildlife = context.Selection.SelectedWildlife;
            if (wildlife != null)
            {
                AddSection(inspector, wildlife.DisplayName);
                AddLine(inspector, "Дикое животное");
                AddMuted(inspector, "Обитает в окрестностях. Не принадлежит поселению.");
                if (context.Production.Animals.TryGet(wildlife.StableId, out var animal) &&
                    context.Production.TryGetSite(wildlife.StableId, out var animalSite))
                {
                    AddLine(inspector, $"Здоровье: {animal.Health}/{animal.Maximum}");
                    inspector.Add(ActionButton(animal.Alive ? "Охотиться с луком" : "Освежевать", () => TryCommand(() =>
                        IssueDirect(WorkJobType.Gather, WorkTarget.ForEntity(WorkTargetKind.ProductionSite,
                            wildlife.StableId, animalSite.Position)))));
                }
                return;
            }
            if (context.Selection.SelectedCharacterIds.Count > 1)
            {
                var projection = GameplaySelectionQueueProjection.Build(context.Selection.SelectedCharacterIds,
                    context.Work, 4);
                AddInspectorHeader("Группа выбора", F("ui.selection.characters", projection.SelectedCount),
                    projection.BusyCount + " выполняют приказы");
                var names = context.Selection.SelectedCharacterIds.Take(6).Select(id => CharacterName(id)).ToArray();
                AddLine(inspector, string.Join(" · ", names) +
                    (projection.SelectedCount > names.Length ? " · ещё " + (projection.SelectedCount - names.Length) : string.Empty));
                AddSection(inspector, "Общая очередь");
                AddLine(inspector, projection.TotalOrders == 0 ? "Приказов нет" :
                    "Приказов: " + projection.TotalOrders + " · заняты: " + projection.BusyCount);
                foreach (var order in projection.VisibleOrders)
                    AddQueueOrder(inspector, order);
                AddMuted(inspector, "ПКМ по миру задаёт общий приказ; Shift добавляет его в конец очереди.");
                return;
            }
            if (context.Selection.InspectedCharacterId.HasValue)
            {
                DrawCharacterInspector(context.Selection.InspectedCharacterId.Value);
                return;
            }
            var buildingPresenter = context.Selection.SelectedBuildingPresenter;
            if (buildingPresenter != null && buildingPresenter.IsBound && context.Buildings.TryGet(buildingPresenter.BuildingId, out var building))
            {
                DrawBuildingInspector(building);
                return;
            }
            var pile = context.Selection.SelectedWorldResourcePilePresenter;
            if (pile != null)
            {
                if (context.Production.Industry != null && context.Production.Industry.TryGetZone(pile.PileId, out var zone))
                {
                    DrawIndustrialZone(pile, zone);
                    return;
                }
                if (!context.Inventories.TryGet(new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId), out var inventory))
                {
                    AddMuted(inspector, "Эта куча ресурсов уже убрана или недоступна.");
                    return;
                }
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
                if (context.Production.Animals.TryGet(source.Site.Id, out var prey))
                {
                    AddLine(inspector, $"Здоровье: {prey.Health}/{prey.Maximum} · " +
                        (prey.Alive ? "Охота с луком" : "Туша готова к разделке"));
                    if (prey.Domestic)
                    {
                        AddLine(inspector, DomesticAnimalName(prey.Species) + " · " + (prey.Male ? "самец" : "самка"));
                        if (!context.Buildings.TryGet(prey.Home, out var home) || home.OwnerSideId.Value != "player")
                        { AddMuted(inspector, "Животное принадлежит другому поместью."); return; }
                    }
                }
                if(source.Site.Kind==ProductionSiteKind.PrimitiveCropPlot)
                    AddLine(inspector,"Рост: "+CropPercent(source.Site));
                else AddLine(inspector, F("ui.selection.source_remaining", source.RemainingAmount, source.SourceCapacity));
                AddLine(inspector, string.Join(" + ", recipe.OutputsFor(source.Site).Select(x =>
                    Content("resource", x.ResourceId.ToString()) + " ×" + x.Quantity.Units)));
                var harvest = context.Production.ActiveTasks.FirstOrDefault(x => x.SiteId == source.Site.Id);
                if (harvest != null)
                    AddLine(inspector, "Прогресс: " + RussianTextCatalog.Percent(
                        (double)harvest.AccumulatedWorkTicks / harvest.RequiredWorkTicks));
                if (recipe.HarvestEntireSource)
                    AddMuted(inspector, "Один приказ — весь источник. Добытое попадает в инвентарь работника или свободный склад.");
                DrawDirectPriority();
                if(source.Site.Kind==ProductionSiteKind.PrimitiveCropPlot)
                {
                    DrawCropWorkButtons(source.Site);
                    return;
                }
                inspector.Add(ActionButton(prey != null ? (prey.Alive ? "Охотиться с луком" : "Освежевать") :
                    source.Site.Kind == ProductionSiteKind.NaturalSource ? "Собирать" : "Обрабатывать",
                    () => TryCommand(() => IssueDirect(source.Site.Kind == ProductionSiteKind.NaturalSource
                        ? WorkJobType.Gather : WorkJobType.Farm,
                        WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position)))));
                inspector.Add(ActionButton(T("ui.selection.open_economy"), () => SetPanel(GameplayPanel.Economy)));
                return;
            }
            AddLine(inspector, T("ui.selection.nothing"));
            AddMuted(inspector, T("ui.selection.help"));
        }

        private void DrawIndustrialZone(WorldResourcePilePresenter pile, IndustrialZoneState zone)
        {
            var industry = context.Production.Industry;
            var definition = industry.GetDefinition(zone.DefinitionId);
            var output = context.Inventories.Get(new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId));
            AddLine(inspector, "Промышленная зона");
            AddSection(inspector, zone.DefinitionId == "stone_quarry" ? "Каменный карьер" : zone.DefinitionId.Replace('_', ' '));
            AddLine(inspector, $"Запас: {zone.Remaining}/{definition.Capacity}");
            AddLine(inspector, "Выход: " + string.Join(", ", definition.OutputsPerCycle.Select(x =>
                Content("resource", x.ResourceId.ToString()) + " ×" + x.Quantity.Units)));
            AddLine(inspector, $"На площадке: {output.TotalUnits}/{output.Capacity}");
            if (!zone.FacilityBuilt)
            {
                AddMuted(inspector, "Нужна технология: " + Content("technology", definition.RequiredTechnology.ToString()));
                AddMuted(inspector, "Стоимость: " + string.Join(", ", definition.ConstructionCost.Select(x =>
                    Content("resource", x.ResourceId.ToString()) + " ×" + x.Quantity.Units)));
                inspector.Add(ActionButton("Построить объект добычи", () => TryCommand(() =>
                {
                    var storage = context.Inventories.All.FirstOrDefault(x =>
                        x.Owner.Kind == InventoryOwnerKind.BuildingStorage &&
                        context.Buildings.TryGet(x.Owner.Id, out var building) && building.IsOperational &&
                        building.OwnerSideId.Value == "player");
                    var funding = context.Inventories.All.FirstOrDefault(x =>
                        (context.PlayerCharacterIds().Contains(x.Owner.Id) ||
                            (storage != null && x.Owner.Equals(storage.Owner))) &&
                        definition.ConstructionCost.All(cost => x.CanRemove(cost.ResourceId, cost.Quantity)));
                    var failure = industry.BuildFacility(zone.Id,
                        funding != null ? funding.Owner : new InventoryOwner(InventoryOwnerKind.WorldPile, zone.Id),
                        storage != null ? storage.Owner.Id : default,
                        context.Research.IsCompleted(definition.RequiredTechnology), true);
                    if (failure != IndustrialCommandFailure.None)
                        throw new PlayerFacingException("Строительство недоступно: " + failure);
                    AddNotification("Промышленный объект построен");
                    RefreshNow();
                })));
                return;
            }

            AddLine(inspector, zone.Remaining > 0 ? "Объект работает" : "Месторождение истощено");
            var quarryJobs = context.Work.Jobs.Where(job => !job.IsTerminal && job.Target.EntityId == zone.Id).ToArray();
            AddLine(inspector, $"Добыча: {quarryJobs.Count(job => job.Type == WorkJobType.Mine)}/{definition.WorkerCapacity} · " +
                $"Доставка: {quarryJobs.Count(job => job.Type == WorkJobType.Haul)}");
            AddMuted(inspector, "Свободные жители приходят на добычу и доставляют камень на склад. Полный склад приостанавливает добычу.");
            if (definition.FacilityKind == IndustrialFacilityKind.Mine && definition.RestorativeUnitsPerDay > 0)
            {
                if (zone.RestorativeInstalled) AddMuted(inspector, "Восстановительный блок активен");
                else inspector.Add(ActionButton("Установить восстановительный блок", () => TryCommand(() =>
                {
                    var failure = industry.InstallRestorativeBlock(zone.Id,
                        context.Research.IsCompleted(definition.RestorativeTechnology));
                    if (failure != IndustrialCommandFailure.None)
                        throw new PlayerFacingException("Установка недоступна: " + failure);
                    AddNotification("Восстановительный блок установлен");
                    RefreshNow();
                })));
            }
            DrawDirectPriority();
            inspector.Add(ActionButton("Перенести продукцию", () => TryCommand(() => IssueSelectedPileHaul(pile))));
        }

        private void DrawCharacterInspector(StableEntityId id)
        {
            var character = context.Characters.Get(id);
            var capabilities = context.Health.GetCapabilities(id);
            var queueProjection = GameplaySelectionQueueProjection.Build(new[] { id }, context.Work, 4);
            var current = queueProjection.VisibleOrders.FirstOrDefault();
            AddInspectorHeader("Житель", RussianTextCatalog.PersonName(character.Name.DisplayName),
                current == null ? "Готов к приказу" : E(current.Type) + " · " + E(current.Status));

            var identity = Row();
            identity.name = "character-identity-metrics";
            identity.style.flexWrap = Wrap.Wrap;
            AddInspectorMetric(identity, "ВОЗРАСТ",
                character.AgeInCompletedYears(context.Clock().State.BiologicalTicks) + " лет");
            AddInspectorMetric(identity, "ПРОФЕССИЯ", ProfessionName(character.ProfessionId?.ToString()));
            AddInspectorMetric(identity, "СТАТУС", capabilities.IsIncapacitated ? "Недееспособен" : "Активен");
            AddInspectorMetric(identity, "ДИНАСТИЯ", DynastyRole(id));
            inspector.Add(identity);

            var body = character.Health;
            var hitPoints = new[] { body.Head.Value, body.Torso.Value, body.LeftArm.Value, body.RightArm.Value,
                body.LeftLeg.Value, body.RightLeg.Value };
            var healthPoints = character.LifeState == CharacterLifeState.Dead ? 0 : hitPoints.Sum() / hitPoints.Length;
            var maximumHealth = context.MilitaryTraining.GetMaximumHealth(id);
            var wellbeing = SerenityUiDesign.Card();
            wellbeing.name = "character-wellbeing-card";
            AddSection(wellbeing, "Самочувствие");
            AddInspectorBar(wellbeing, "Здоровье", maximumHealth <= 0 ? 0 : healthPoints * 100f / maximumHealth,
                healthPoints + "/" + maximumHealth);
            if (context.AiAgents.TryGet(id, out var agent))
            {
                AddInspectorBar(wellbeing, "Сытость", (1f - (float)agent.Needs.Hunger) * 100f,
                    RussianTextCatalog.Percent(1d - agent.Needs.Hunger));
                AddInspectorBar(wellbeing, "Энергия", (float)agent.Needs.Energy * 100f,
                    RussianTextCatalog.Percent(agent.Needs.Energy));
                AddInspectorBar(wellbeing, "Без жажды", (1f - (float)agent.Needs.Thirst) * 100f,
                    RussianTextCatalog.Percent(1d - agent.Needs.Thirst));
                AddInspectorBar(wellbeing, "Настроение", (float)agent.Needs.Mood * 100f,
                    RussianTextCatalog.Percent(agent.Needs.Mood));
                AddMuted(wellbeing, "Сейчас: " + E(agent.CurrentAction) + " · " + E(agent.Phase));
            }
            else AddMuted(wellbeing, T("ui.character.offcamera"));
            inspector.Add(wellbeing);

            var activity = SerenityUiDesign.Card();
            activity.name = "character-activity-card";
            AddSection(activity, "Занятость");
            AddLine(activity, context.MilitaryTraining.IsExternallyControlled(id)
                ? "Текущее дело: обучение в казарме"
                : context.Work.TryGetAssigned(id, out var assignedJob)
                    ? "Текущее дело: " + E(assignedJob.Type) + " · " + E(assignedJob.Status)
                    : "Текущее дело: свободен");
            AddLine(activity, "Группа: " + (context.Work.Groups.TryGetForMember(id, out var group)
                ? group.DisplayName : T("common.none")));
            AddLine(activity, "Исследования: " + (context.Research.AssignedWorkers.Contains(id)
                ? T("ui.character.research_assigned") : T("ui.character.research_not_assigned")));
            var militaryRank = context.MilitaryTraining.GetRank(id);
            var militaryStatus = context.MilitaryTraining.IsExternallyControlled(id)
                ? "В казарме · курс " + MilitaryRankName(context.MilitaryTraining.Active.Single(x => x.CharacterId == id).Rank)
                : militaryRank.HasValue
                    ? MilitaryRankName(militaryRank.Value) + (context.Military.TryGetForMember(id, out var unit) && unit.CaptainId == id
                        ? " · " + T("ui.character.captain") : string.Empty)
                    : T("ui.character.civilian");
            AddLine(activity, "Военная служба: " + militaryStatus);
            var inventoryOwner = context.Inventories.WorkerOwner(id);
            if (context.Inventories.TryGet(inventoryOwner, out var inventory) && inventory.TotalUnits > 0)
                AddMuted(activity, "Несёт: " + InventoryText(inventory));
            inspector.Add(activity);

            var skills = new Foldout { text = "Навыки и склонности", value = false,
                name = "character-skills-foldout" };
            AddLine(skills, "Любимое занятие: " + CharacterSkillRules.Favorite(character));
            foreach (var skill in character.CaptureState().Skills
                         .Where(x => !CharacterSkillRules.IsFavoriteMarker(x.Id))
                         .OrderByDescending(x => x.Level.Value).ThenBy(x => x.Id.Value).Take(8))
                AddInspectorBar(skills, skill.Id.ToString(), skill.Level.Value, skill.Level.Value.ToString());
            inspector.Add(skills);

            var conditions = context.Health.Conditions.Where(x => x.TargetId == id &&
                x.State != HealthConditionState.Recovered).ToArray();
            var pregnancy = context.Health.Pregnancies.FirstOrDefault(x => x.MotherId == id && x.Status == PregnancyStatus.Active);
            var health = new Foldout { text = "Здоровье и состояния", value = conditions.Length > 0,
                name = "character-health-foldout" };
            AddLine(health, conditions.Length == 0 ? "Активных состояний нет" : string.Join("\n",
                conditions.Select(x => Content("condition", x.DefinitionId.ToString()) + " · " + E(x.Severity))));
            AddMuted(health, "Минимум части тела: " + hitPoints.Min() + "/" + maximumHealth +
                " · работа " + Percent(capabilities.WorkPerThousand) +
                " · исследования " + Percent(capabilities.ResearchPerThousand));
            if (pregnancy != null) AddLine(health, T("ui.character.pregnancy"));
            if (context.Combat.TryGetCombatant(id, out var combatant))
                AddLine(health, "Оружие: " + combatant.WeaponId + " · прочность " + combatant.WeaponDurability + "/100");
            inspector.Add(health);

            var orders = new Foldout { text = "Очередь приказов · " + queueProjection.TotalOrders,
                value = queueProjection.TotalOrders > 0, name = "character-orders-foldout" };
            if (queueProjection.TotalOrders == 0) AddMuted(orders, "Очередь пуста");
            else foreach (var order in queueProjection.VisibleOrders) AddQueueOrder(orders, order);
            inspector.Add(orders);

            var family = new Foldout { text = "Дом и семья", value = false,
                name = "character-family-foldout" };
            var assignedHome = context.Domestic.Homes.FirstOrDefault(value => value.Allows(id));
            AddLine(family, assignedHome == null ? "Дом не назначен" :
                "Дом: " + (context.Buildings.TryGet(assignedHome.BuildingId, out var assignedHomeBuilding)
                    ? Content("building", assignedHomeBuilding.DefinitionId.ToString()) : "жилой дом"));
            AddLine(family, "Супруг(а): " + CharacterName(character.SpouseId));
            var familyChildren = context.Family.GetChildren(id).ToArray();
            AddLine(family, "Дети: " + (familyChildren.Length == 0 ? "нет" :
                string.Join(", ", familyChildren.Select(value => CharacterName(value.Id)))));
            inspector.Add(family);

            if (context.Events != null)
            {
                var eventHistory = context.Events.History.Where(x => x.Targets.Any(target =>
                    target.Kind == EventTargetKind.Character && target.TryGetStableEntityId(out var targetId) && targetId == id))
                    .Reverse().Take(3).ToArray();
                var history = new Foldout { text = "Недавние события", value = false,
                    name = "character-events-foldout" };
                AddLine(history, eventHistory.Length == 0 ? T("common.none") : string.Join("\n",
                    eventHistory.Select(x => EventTitle(x.DefinitionId,
                        context.Events.Catalog.Get(x.DefinitionId).Nodes[0].Id))));
                inspector.Add(history);
            }
            if (!context.PlayerCharacterIds().Contains(id))
            {
                AddMuted(inspector, "Житель другого поселения. Доступен только осмотр.");
                return;
            }
            var eligibility = context.PlayerControl.Evaluate(id);
            var actions = Row();
            var tps = ActionButton(T("ui.character.enter_tps"), () => TryCommand(() =>
            {
                var result = context.ThirdPerson.TryEnter(id);
                if (!result.IsEligible) throw new PlayerFacingException(EligibilityReason(result.Failure));
            }));
            tps.SetEnabled(eligibility.IsEligible);
            actions.Add(tps);
            inspector.Add(actions);
            var targetPosition = context.AiAgents.TryGet(id, out var selectedAgent) ? selectedAgent.Position : default;
            inspector.Add(ActionButton("Лечиться", () => TryCommand(() =>
                context.DirectWork.Issue(WorkJobType.Care,
                    WorkTarget.ForEntity(WorkTargetKind.Character, id, targetPosition), WorkPriority.High)),
                "character-seek-care"));
            if (!eligibility.IsEligible) AddMuted(inspector, F("ui.character.tps_unavailable", EligibilityReason(eligibility.Failure)));
            if (DeveloperUiVisible) AddMuted(inspector, F("ui.character.stable_id", id));
        }

        private static void SuppressTechnicalEmptyListLabel(ListView listView)
        {
            foreach (var label in listView.Query<Label>().ToList())
            {
                if (!string.Equals(label.text, "List is empty", StringComparison.Ordinal)) continue;
                label.text = string.Empty;
                label.style.display = DisplayStyle.None;
            }
        }

        private static void AddInspectorMetric(VisualElement parent, string label, string value)
        {
            var card = SerenityUiDesign.Card();
            card.style.minWidth = 136;
            card.style.flexGrow = 1;
            var kicker = new Label(label);
            kicker.AddToClassList(SerenityUiDesign.MutedClass);
            var text = new Label(value);
            text.AddToClassList(SerenityUiDesign.BodyClass);
            card.Add(kicker);
            card.Add(text);
            parent.Add(card);
        }

        private static void AddInspectorBar(VisualElement parent, string label, float value, string valueText)
        {
            var progress = new ProgressBar
            {
                title = label + " · " + valueText,
                lowValue = 0f,
                highValue = 100f,
                value = Mathf.Clamp(value, 0f, 100f)
            };
            progress.name = "character-progress-" + (label ?? string.Empty).ToLowerInvariant().Replace(' ', '-');
            progress.AddToClassList("serenity-inspector-progress");
            progress.style.height = 22;
            progress.style.marginTop = 2;
            progress.style.marginBottom = 2;
            progress.style.backgroundColor = Color.clear;
            var background = progress.Q(className: "unity-progress-bar__background");
            if (background != null)
            {
                background.style.backgroundColor = SerenityUiDesign.Canvas;
                background.style.borderTopColor = SerenityUiDesign.Accent;
                background.style.borderRightColor = SerenityUiDesign.Accent;
                background.style.borderBottomColor = SerenityUiDesign.Accent;
                background.style.borderLeftColor = SerenityUiDesign.Accent;
                background.style.borderTopWidth = 1;
                background.style.borderRightWidth = 1;
                background.style.borderBottomWidth = 1;
                background.style.borderLeftWidth = 1;
            }
            var fill = progress.Q(className: "unity-progress-bar__progress");
            if (fill != null) fill.style.backgroundColor = SerenityUiDesign.Accent;
            var title = progress.Q(className: "unity-progress-bar__title");
            if (title != null)
            {
                title.style.color = SerenityUiDesign.Text;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            parent.Add(progress);
        }

        private void DrawBuildingInspector(Building building)
        {
            var definition = context.BuildingCatalog.ResolveDefinition(building.DefinitionId);
            AddInspectorHeader("Здание", Content("building", definition.Id.ToString()),
                building.IsOperational ? "Работает" : "Строится · " + building.ConstructionProgress.ToString("P0"));
            AddSection(inspector, Content("building", definition.Id.ToString()));
            AddLine(inspector, "Уровень здания: "+(definition.Id==definition.Id.FamilyId&&definition.Id.ToString()!="clan_house"?"1":"2"));
            AddLine(inspector, F("ui.building.owner", SideName(building.OwnerSideId), E(building.ConstructionState)));
            if (building.IsOperational) AddBuildingPolicyControls(inspector, building);
            if (building.IsOperational && definition.Housing != null)
            {
                var residents = context.Facilities.HomeAssignments
                    .Where(pair => pair.Value == building.Id)
                    .Select(pair => pair.Key).OrderBy(id => id).ToArray();
                AddSection(inspector, $"Жильцы: {residents.Length}/{definition.Housing.ResidentCapacity}");
                if (residents.Length == 0) AddMuted(inspector, "Дом свободен.");
                else foreach (var resident in residents)
                {
                    var capturedResident = resident;
                    inspector.Add(ActionButton(CharacterName(resident), () => SelectAndFocusResident(capturedResident),
                        "home-resident-" + resident));
                }
            }
            if (building.IsOperational && building.DefinitionId.FamilyId == MilitaryTrainingService.BarracksDefinitionId)
            {
                var trainees = context.MilitaryTraining.Active.Where(x => x.BarracksId == building.Id)
                    .OrderBy(x => x.CompletionTick).ThenBy(x => x.CharacterId).ToArray();
                AddSection(inspector, $"В казарме: {trainees.Length}/6");
                if (trainees.Length == 0) AddMuted(inspector, "Сейчас никто не обучается.");
                foreach (var trainee in trainees)
                {
                    var capturedTrainee = trainee.CharacterId;
                    inspector.Add(ActionButton(CharacterName(capturedTrainee) + " · " + MilitaryRankName(trainee.Rank) +
                        " · " + FormatDuration(Math.Max(0, trainee.CompletionTick - context.Clock().State.CalendarTicks)),
                        () => SelectAndFocusResident(capturedTrainee), "barracks-trainee-" + capturedTrainee));
                }
            }
            if (!building.IsOperational && building.ConstructionRequiredTicks > 0)
                AddLine(inspector, $"Строительство: {building.ConstructionProgress:P0} · требуется работа строителя");
            if (!building.IsOperational && context.ConstructionClearance != null &&
                context.ConstructionClearance.TryGetStatus(building.Id, out var clearanceStatus))
            {
                if (clearanceStatus.IsClearing)
                {
                    AddLine(inspector, $"Расчистка площадки: {clearanceStatus.Completed}/{clearanceStatus.Total}");
                    AddMuted(inspector, "Жители уберут деревья, кусты и камни обычными заданиями, затем автоматически доставят материалы и начнут стройку.");
                }
                else if (!clearanceStatus.DeliveriesScheduled)
                    AddMuted(inspector, "Площадка свободна. Ожидаются доступные строительные материалы.");
                else if (!clearanceStatus.BuildScheduled)
                    AddMuted(inspector, "Материалы назначены к доставке. Строитель будет вызван автоматически.");
                var priorityRow = Row();
                foreach (var value in new[] { WorkPriority.Low, WorkPriority.Normal, WorkPriority.High, WorkPriority.Urgent })
                {
                    var captured = value;
                    var priority = ActionButton(E(value), () => TryCommand(() =>
                    {
                        if (!context.ConstructionClearance.SetPriority(building.Id, captured))
                            throw new PlayerFacingException("Приоритет этой стройки изменить нельзя.");
                    }), "construction-priority-" + value.ToString().ToLowerInvariant());
                    SerenityUiDesign.SetSelected(priority, clearanceStatus.Priority == value);
                    priorityRow.Add(priority);
                }
                inspector.Add(priorityRow);
            }
            AddLine(inspector, F("ui.building.integrity", building.CurrentIntegrity, building.MaxIntegrity, E(building.StructuralStatus)));
            AddLine(inspector, F("ui.building.fire", E(building.FireStatus)) + (building.FireStatus == BuildingFireStatus.Burning
                ? F("ui.building.fire_intensity", building.FireIntensity) : string.Empty));
            var owner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id);
            if (context.Inventories.TryGet(owner, out var storage))
            {
                AddLine(inspector, F("ui.building.storage", storage.TotalUnits, storage.Capacity, InventoryText(storage)));
                var issueButton = ActionButton(storage.IssuanceLocked ? "Разрешить выдачу" : "Запретить выдачу",
                    () => TryCommand(() => storage.SetIssuanceLocked(!storage.IssuanceLocked)), "storage-issuance-lock");
                SerenityUiDesign.SetSelected(issueButton, storage.IssuanceLocked);
                inspector.Add(issueButton);
                var categoryFilters = new Foldout { text = "Фильтр категорий", value = false };
                foreach (ResourceCategory category in Enum.GetValues(typeof(ResourceCategory)))
                {
                    var captured = category;
                    var accepted = storage.AcceptsAllCategories || storage.AcceptedCategories.Contains(category);
                    var toggle = ActionButton((accepted ? "✓ " : "✕ ") + E(category), () => TryCommand(() =>
                    {
                        var selected = storage.AcceptsAllCategories
                            ? Enum.GetValues(typeof(ResourceCategory)).Cast<ResourceCategory>().ToHashSet()
                            : storage.AcceptedCategories.ToHashSet();
                        if (!selected.Add(captured)) selected.Remove(captured);
                        storage.ConfigureAcceptance(selected.Count == Enum.GetValues(typeof(ResourceCategory)).Length
                            ? null : selected, storage.AcceptsAllResources ? null : storage.AcceptedResources);
                    }), "storage-category-" + category.ToString().ToLowerInvariant());
                    SerenityUiDesign.SetSelected(toggle, accepted);
                    categoryFilters.Add(toggle);
                }
                inspector.Add(categoryFilters);
                var resourceFilters = new Foldout { text = "Фильтр ресурсов", value = false };
                var playerResources = context.Resources.All.Where(x => IsPlayerManagedResource(x.Id)).ToArray();
                foreach (var resource in playerResources)
                {
                    var captured = resource.Id;
                    var accepted = storage.AcceptsAllResources || storage.AcceptedResources.Contains(captured);
                    var toggle = ActionButton((accepted ? "✓ " : "✕ ") + Content("resource", captured.ToString()),
                        () => TryCommand(() =>
                        {
                            var selected = storage.AcceptsAllResources
                                ? playerResources.Select(x => x.Id).ToHashSet()
                                : storage.AcceptedResources.ToHashSet();
                            if (!selected.Add(captured)) selected.Remove(captured);
                            storage.ConfigureAcceptance(storage.AcceptsAllCategories ? null : storage.AcceptedCategories,
                                selected.Count == playerResources.Length ? null : selected);
                        }), "storage-resource-" + captured);
                    SerenityUiDesign.SetSelected(toggle, accepted);
                    resourceFilters.Add(toggle);
                }
                inspector.Add(resourceFilters);
            }
            if (context.Production.Sites.FirstOrDefault(x => x.Id == building.Id) is ProductionSite site)
            {
                AddLine(inspector, F("ui.building.production", building.IsOperational ? T("ui.building.operational") : T("common.unavailable")));
                if (context.Production.IsPastureSite(site.Id))
                {
                    var herd = context.Production.Animals.AtHome(site.Id);
                    var policy = context.Production.Animals.Policy;
                    AddLine(inspector, $"Загон · живых животных: {herd.Count(x => x.Alive)}/{policy.PastureCapacity}");
                    if (herd.Count == 0)
                    {
                        AddMuted(inspector, "Загон пуст. Выберите один вид животных для заселения первой пары.");
                        var species = policy.DomesticSpecies.ToArray();
                        var selector = new DropdownField("Вид животных",
                            species.Select(DomesticAnimalName).ToList(), 0);
                        selector.SetEnabled(building.OwnerSideId == new CombatSideId("player") && building.IsOperational);
                        inspector.Add(selector);
                        var settle = ActionButton("Заселить основное стадо (2♂ + 2♀)", () => TryCommand(() =>
                        {
                            var index = selector.index;
                            if (index < 0 || index >= species.Length) return;
                            var established = context.Production.EstablishPasture(site.Id,
                                new CombatSideId("player"), species[index]);
                            SetStatus(established
                                ? $"В загон заселено основное стадо: {DomesticAnimalName(species[index])}, 2 самца и 2 самки."
                                : "Заселить этот загон сейчас нельзя.");
                        }), "pasture-settle-pair");
                        settle.SetEnabled(building.OwnerSideId == new CombatSideId("player") && building.IsOperational);
                        inspector.Add(settle);
                    }
                    else
                    {
                        var pen = context.Production.LivestockPolicies.FirstOrDefault(x => x.BuildingId == site.Id);
                        if (pen != null)
                        {
                            AddLine(inspector, $"Режим: {LivestockModeName(pen.Mode)} · предел {pen.PopulationLimit}");
                            AddLine(inspector, $"Внутренний запас воды: {pen.WaterUnits} · пополняет назначенный работник из колодца");
                            var modes = Row();
                            foreach (var mode in new[] { LivestockOperationMode.Breeding,
                                         LivestockOperationMode.Products, LivestockOperationMode.Balanced })
                            {
                                var captured = mode;
                                modes.Add(ActionButton((pen.Mode == mode ? "✓ " : string.Empty) + LivestockModeName(mode),
                                    () => TryCommand(() => context.Production.SetLivestockPolicy(site.Id,
                                        captured, pen.PopulationLimit))));
                            }
                            inspector.Add(modes);
                            var limit = Row();
                            limit.Add(ActionButton("Предел −", () => TryCommand(() =>
                                context.Production.SetLivestockPolicy(site.Id, pen.Mode,
                                    Math.Max(4, pen.PopulationLimit - 1)))));
                            limit.Add(ActionButton("Предел +", () => TryCommand(() =>
                                context.Production.SetLivestockPolicy(site.Id, pen.Mode,
                                    pen.PopulationLimit + 1))));
                            inspector.Add(limit);
                            var species = policy.DomesticSpecies.ToArray();
                            var selector = new DropdownField("Сменить вид",
                                species.Select(DomesticAnimalName).ToList(),
                                Math.Max(0, Array.IndexOf(species, pen.Species)));
                            inspector.Add(selector);
                            inspector.Add(ActionButton("Сменить вид (сначала забить текущее стадо)", () =>
                                TryCommand(() => context.Production.ChangeLivestockSpecies(site.Id,
                                    species[Math.Max(0, selector.index)]))));
                        }
                        AddMuted(inspector, $"Для приплода нужны взрослые самец и самка. Взросление — {policy.LivestockMaturityDays} дн., приплод — раз в {policy.LivestockBirthDays} дн. при свободном месте.");
                        foreach (var animal in herd.Where(x => !x.Harvested))
                        {
                            var row = Row();
                            AddLine(row, $"{DomesticAnimalName(animal.Species)} · {(animal.Male ? "самец" : "самка")}: {animal.Health}/{animal.Maximum} HP");
                            var slaughter = ActionButton("Забить", () => TryCommand(() =>
                                context.Production.SlaughterLivestock(animal.Id)));
                            var canSlaughter = context.Production.CanSlaughterLivestock(animal.Id);
                            slaughter.SetEnabled(canSlaughter);
                            if (!canSlaughter) slaughter.tooltip = animal.Species == "horse"
                                ? "Лошадей не забивают." : "Племенная четвёрка защищена от ручного забоя.";
                            row.Add(slaughter);
                            inspector.Add(row);
                        }
                    }
                }
                else if (site.Kind == ProductionSiteKind.PrimitiveCropPlot)
                {
                    AddLine(inspector, F("ui.building.crop", E(site.CropPhase), CropPercent(site)));
                    DrawCropWorkButtons(site);
                    AddLine(inspector, "Культура: " + Content("recipe", site.RecipeId.ToString()));
                    var crops = context.Production.Recipes.All.Where(recipe =>
                        recipe.SiteKind == ProductionSiteKind.PrimitiveCropPlot &&
                        context.Production.IsRecipeAvailable(recipe.Id)).ToArray();
                    if (crops.Length > 0)
                    {
                        var selector = new DropdownField("Выращивать", crops.Select(recipe =>
                            Content("recipe", recipe.Id.ToString())).ToList(),
                            Math.Max(0, Array.FindIndex(crops, recipe => recipe.Id == site.RecipeId)));
                        selector.SetEnabled(building.OwnerSideId == new CombatSideId("player") &&
                            building.IsOperational && site.CropPhase == CropPlotPhase.Unplanted &&
                            !context.Production.ActiveTasks.Any(task => task.SiteId == site.Id) && crops.Length > 0);
                        selector.RegisterValueChangedCallback(change => TryCommand(() =>
                        {
                            var index = selector.index;
                            if (index < 0 || index >= crops.Length) return;
                            var changed = context.Production.SelectCrop(site.Id, crops[index].Id,
                                new CombatSideId("player"));
                            SetStatus(changed ? "Культура выбрана. Жители посадят её при работе на поле." :
                                "Дождитесь окончания работы и сбора урожая: менять культуру можно на пустом поле.");
                        }));
                        inspector.Add(selector);
                    }
                    else AddLine(inspector, "Сначала изучите земледелие.");
                    if (site.CropPhase != CropPlotPhase.Unplanted)
                        AddLine(inspector, "Смена культуры доступна после сбора урожая.");
                }
            }
            if (building.DefinitionId.FamilyId == new BuildingDefinitionId("hunting_camp"))
            {
                var hunting = context.Production.HuntingPolicies.FirstOrDefault(x => x.BuildingId == building.Id) ??
                    new HuntingLodgePolicyState(building.Id, HuntingOperationMode.Hunt, 0);
                AddSection(inspector, "Режим охотничьей стоянки");
                inspector.Add(ActionButton((hunting.Mode == HuntingOperationMode.Hunt ? "✓ " : string.Empty) + "Охота",
                    () => TryCommand(() => context.Production.SetHuntingPolicy(building.Id,
                        HuntingOperationMode.Hunt, hunting.TargetCarcasses))));
                inspector.Add(ActionButton((hunting.Mode == HuntingOperationMode.RestorePopulation ? "✓ " : string.Empty) +
                    "Восстановление популяции", () => TryCommand(() => context.Production.SetHuntingPolicy(building.Id,
                        HuntingOperationMode.RestorePopulation, hunting.TargetCarcasses))));
            }
            if (building.DefinitionId.FamilyId == new BuildingDefinitionId("healer_hut"))
            {
                var patients = context.Facilities.ClinicPatients.Where(x => x.ClinicId == building.Id).ToArray();
                AddSection(inspector, $"Лечебница: врачи занимают 2 из 6 мест · пациентов {patients.Length}/4");
                foreach (var patient in patients)
                    AddLine(inspector, $"{context.Characters.Get(patient.PatientId).Name} · тяжесть {patient.Severity}" +
                        (patient.InTreatment ? " · лечится" : " · ждёт"));
            }
            var workplaceSites = context.Production.Sites.Where(x =>
                x.Kind == ProductionSiteKind.BuildingWorkplace && x.OwnerBuildingId == building.Id).ToArray();
            if (workplaceSites.Length > 0)
            {
                if (building.DefinitionId.FamilyId.ToString()=="fishing_camp")
                {
                    var stock = context.Production.FishStockAt(workplaceSites[0].Position);
                    AddLine(inspector,stock == null ? "Нет доступного водоёма" : $"Рыба в водоёме: {stock.RemainingAmount}/{stock.SourceCapacity}");
                    var mode = new DropdownField("Режим",new List<string>{"Ловля рыбы","Разведение"},
                        workplaceSites.Any(x=>x.RecipeId.ToString()=="fish_breed" && !x.Paused)?1:0);
                    mode.RegisterValueChangedCallback(_=>TryCommand(()=>context.Production.SetFisheryBreeding(building.Id,mode.index==1)));
                    inspector.Add(mode);
                }
                var inputOwner = context.Production.InputOwner(building.Id);
                var outputOwner = context.Production.OutputOwner(building.Id);
                if (context.Inventories.TryGet(inputOwner, out var input))
                    AddLine(inspector, $"Вход: {input.TotalUnits}/{input.Capacity} · {InventoryText(input)}");
                if (context.Inventories.TryGet(outputOwner, out var output))
                    AddLine(inspector, $"Выход: {output.TotalUnits}/{output.Capacity} · {InventoryText(output)}");
                foreach (var workplaceSite in workplaceSites)
                {
                    var recipe = context.Production.Recipes.Get(workplaceSite.RecipeId);
                    var active = context.Production.ActiveTasks.FirstOrDefault(x => x.SiteId == workplaceSite.Id);
                    if (workplaceSite.Continuous)
                    {
                        var reason = context.Production.GetWaitingReason(workplaceSite);
                        AddLine(inspector, (workplaceSite.RecipeId.ToString()=="fish_breed" ? "Разведение · " : "Ловля рыбы · ") + (reason == Game.Simulation.Production.ProductionWaitingReason.OutputFull ? "нет свободного запаса или места" :
                            workplaceSite.Paused ? "пауза" : active == null ? "нет работника" : "работает"));
                        if (active != null) AddLine(inspector, $"Работник: {CharacterName(active.WorkerId)} · цикл {active.AccumulatedWorkTicks * 100 / active.RequiredWorkTicks}%");
                    }
                    else AddLine(inspector, $"{Content("recipe", recipe.Id.ToString())}: очередь {workplaceSite.QueuedBatches} · " +
                        (active == null ? $"ожидание: {E(context.Production.GetWaitingReason(workplaceSite))}" :
                            $"рабочий {CharacterName(active.WorkerId)} · работа {active.AccumulatedWorkTicks * 100 / active.RequiredWorkTicks}%"));
                    var capturedRecipe = recipe.Id;
                    var recipeRow = Row();
                    if (!workplaceSite.Continuous)
                    {
                        recipeRow.Add(ActionButton("+1", () => TryCommand(() => QueueProduction(building, capturedRecipe, 1))));
                        recipeRow.Add(ActionButton("+5", () => TryCommand(() => QueueProduction(building, capturedRecipe, 5))));
                    }
                    if (building.DefinitionId.FamilyId.ToString() != "fishing_camp") recipeRow.Add(ActionButton(workplaceSite.Paused ? "Продолжить" : "Пауза", () => TryCommand(() =>
                        context.Production.SetWorkplacePaused(building.Id, capturedRecipe, !workplaceSite.Paused))));
                    inspector.Add(recipeRow);
                }
            }
            DrawDirectPriority();
            var buildingPresenter = FindObjectsByType<Game.Presentation.Buildings.BuildingPresenter>(FindObjectsInactive.Exclude)
                .FirstOrDefault(x => x.IsBound && x.BuildingId == building.Id);
            var buildingPosition = buildingPresenter != null ? buildingPresenter.transform.position :
                new Vector3(building.Coordinate.X, building.Coordinate.Level, building.Coordinate.Z);
            var workTarget = ResolveBuildingWorkTarget(new ContextCommandTarget(ContextTargetKind.Building,
                new WorldPosition(buildingPosition.x, buildingPosition.y, buildingPosition.z), building.Id),
                context.Selection.SelectedCharacterIds);
            var target = WorkTarget.ForEntity(WorkTargetKind.Building, building.Id, workTarget.Position);
            if(building.IsOperational && building.DefinitionId.FamilyId.ToString()=="fishing_pier")
                inspector.Add(ActionButton("Ловить рыбу",()=>TryCommand(()=>IssuePierFishing(building,workTarget.Position)),"fish-from-built-pier"));
            else inspector.Add(ActionButton(building.IsOperational ? building.DefinitionId.FamilyId.ToString() == "fishing_camp" ? workplaceSites.Any(x=>x.RecipeId.ToString()=="fish_breed" && !x.Paused) ? "Разводить рыбу" : "Ловить рыбу" : "Изготавливать" : "Строить",
                () => TryCommand(() => IssueDirect(building.IsOperational ? WorkJobType.Craft : WorkJobType.Build, target))));
            if (building.IsOperational && context.Facilities != null && context.Selection.SelectedCharacterIds.Count > 0)
            {
                if (definition.Housing != null)
                {
                    inspector.Add(ActionButton("Поселить выбранных", () => TryCommand(() =>
                    {
                        var assigned = context.Selection.SelectedCharacterIds.Count(id =>
                            context.Facilities.AssignHome(id, building.Id).Succeeded);
                        SetStatus($"Назначено жильцов: {assigned}.");
                    })));
                    inspector.Add(ActionButton("Выселить выбранных", () => TryCommand(() =>
                    {
                        var evicted = context.Selection.SelectedCharacterIds.Count(id =>
                            context.Facilities.HomeAssignments.TryGetValue(id, out var home) && home == building.Id &&
                            context.Facilities.UnassignHome(id));
                        SetStatus($"Выселено жильцов: {evicted}.");
                    })));
                }
                if (definition.Workplace != null)
                    inspector.Add(ActionButton("Работать здесь / сменить профессию", () => TryCommand(() =>
                    {
                        var assigned = context.Selection.SelectedCharacterIds.Count(id =>
                            context.Facilities.ChangeWorkplace(id, building.Id).Succeeded);
                        SetStatus($"Назначено работников: {assigned}.");
                    })));
                if (building.DefinitionId.FamilyId == new BuildingDefinitionId("training_ground"))
                    inspector.Add(ActionButton("Провести тренировку", () => TryCommand(() =>
                    {
                        var trained = 0;
                        foreach (var id in context.Selection.SelectedCharacterIds)
                        {
                            context.Facilities.AssignWorkplace(id, building.Id);
                            if (context.Facilities.Train(id)) trained++;
                        }
                        SetStatus($"Прошли тренировку: {trained}.");
                    })));
                if (building.DefinitionId.FamilyId == new BuildingDefinitionId("healer_hut") &&
                    context.Selection.SelectedCharacterIds.Count >= 2)
                    inspector.Add(ActionButton("Лечить выбранных", () => TryCommand(() =>
                    {
                        var healerId = context.Selection.SelectedCharacterIds[0];
                        var assignment = context.Facilities.AssignWorkplace(healerId, building.Id);
                        if (!assignment.Succeeded && assignment.BuildingId != building.Id)
                            throw new PlayerFacingException("Первого выбранного жителя нельзя назначить лекарем.");
                        var recovered = 0;
                        foreach (var patientId in context.Selection.SelectedCharacterIds.Skip(1))
                            recovered += context.Facilities.Treat(healerId, patientId);
                        SetStatus($"Лекарь завершил лечение; снято состояний: {recovered}.");
                    })));
            }
            if (building.DefinitionId.FamilyId == new BuildingDefinitionId("military_barracks"))
            {
                var rally = context.Buildings.All.FirstOrDefault(x => x.IsOperational &&
                    x.DefinitionId.FamilyId == new BuildingDefinitionId("rally_point") && x.OwnerSideId == building.OwnerSideId);
                var gather = ActionButton("Собрать военных на точке сбора", () => TryCommand(() =>
                {
                    if (rally == null) throw new PlayerFacingException("Сначала постройте точку сбора.");
                    foreach (var id in context.PlayerCharacterIds().Where(id => context.MilitaryTraining.GetRank(id).HasValue))
                        context.Work.CreateMoveJob(id,
                            new WorldPosition(rally.Coordinate.X, rally.Coordinate.Level, rally.Coordinate.Z),
                            WorkPriority.Immediate, queuePlacement: WorkQueuePlacement.Front);
                    SetStatus("Военные направлены к точке сбора.");
                }), "barracks-gather-military");
                gather.SetEnabled(rally != null);
                gather.tooltip = rally == null ? "Недоступно: в поселении нет точки сбора." :
                    "Все военные поселения соберутся в указанной точке.";
                inspector.Add(gather);
                if (rally == null) AddMuted(inspector, "Сбор недоступен: сначала постройте точку сбора.");
            }
            if (!building.IsOperational)
                inspector.Add(ActionButton("Отменить стройку", () => TryCommand(() =>
                {
                    if (!context.BuildingPlacement.CancelConstruction(building))
                        throw new PlayerFacingException("Стройку уже нельзя отменить.");
                    SetStatus("Стройка отменена; доставленные материалы возвращены на землю.");
                })));
            else if (context.BuildingLifecycle != null)
            {
                var upgradeOption=context.BuildingLifecycle.UpgradeOption(building);
                var upgradeJob=context.BuildingLifecycle.UpgradeJob(building.Id);
                if(upgradeOption!=null && building.OwnerSideId==new CombatSideId("player"))
                {
                    var upgradedDefinition=context.BuildingCatalog.ResolveDefinition(upgradeOption.TargetDefinitionId);
                    AddLine(inspector,"Следующий уровень: "+Content("building",upgradedDefinition.Id.ToString()));
                    AddMuted(inspector,"Стоимость: "+string.Join(", ",upgradeOption.Cost.Select(x=>
                        Content("resource",x.ResourceId.ToString())+" ×"+x.Quantity.Units))+
                        "; работа: "+TimeSpan.FromTicks(upgradeOption.WorkRequiredTicks).TotalMinutes+" игровых мин.");
                    if(upgradeJob!=null)
                    {
                        AddLine(inspector,"Улучшение: "+RussianTextCatalog.Percent((double)upgradeJob.ProgressTicks/upgradeOption.WorkRequiredTicks));
                        AddMuted(inspector,"Требуются свободный строитель, доступ к зданию и материалы на складе. Материалы списываются при завершении.");
                        inspector.Add(ActionButton("Отменить улучшение",()=>TryCommand(()=>context.Work.Cancel(upgradeJob.Id))));
                    }
                    else
                    {
                        var unlocked=upgradedDefinition.RequiredTechnologies.All(id=>context.Research.IsCompleted(id));
                        var button=ActionButton("Улучшить до уровня II",()=>TryCommand(()=>
                        {
                            var result=context.BuildingLifecycle.RequestUpgrade(building.Id);
                            if(!result.Succeeded)throw new PlayerFacingException(result.Failure==Game.Simulation.Buildings.BuildingLifecycleFailure.InsufficientResources
                                ? "Не хватает материалов для улучшения." : "Улучшение недоступно: "+result.Failure);
                            SetStatus("Улучшение назначено строителям.");
                        }));
                        button.SetEnabled(unlocked);inspector.Add(button);
                        if(!unlocked)AddMuted(inspector,"Сначала завершите исследования: "+string.Join(", ",upgradedDefinition.RequiredTechnologies.Select(id=>Content("technology",id.ToString()))));
                    }
                }
                if (definition.Maintenance != null && building.CurrentIntegrity < building.MaxIntegrity)
                    inspector.Add(ActionButton("Ремонтировать", () => TryCommand(() =>
                    {
                        var result = context.BuildingLifecycle.Repair(building.Id, StableEntityId.NewId(),
                            TimeSpan.FromTicks(definition.Maintenance.RepairWorkRequiredTicks),
                            context.Clock().State.CalendarTicks, LifecycleFundingSources());
                        if (!result.Succeeded) throw new PlayerFacingException("Ремонт недоступен: " + result.Failure);
                        SetStatus($"Ремонт завершён: +{result.IntegrityChanged} прочности.");
                    })));
                if (building.DefinitionId.FamilyId == new BuildingDefinitionId("gate") && context.BuildingPathing != null)
                {
                    var open = context.BuildingPathing.IsGateOpen(building.Id);
                    inspector.Add(ActionButton(open ? "Закрыть ворота" : "Открыть ворота", () => TryCommand(() =>
                    {
                        if (!context.BuildingPathing.SetGateAccessMode(building.Id,
                                open ? GateAccessMode.Closed : GateAccessMode.Open))
                            throw new PlayerFacingException("Состояние ворот изменить нельзя.");
                        SetStatus(open ? "Ворота закрыты." : "Ворота открыты.");
                    })));
                }
                var demolition = context.BuildingLifecycle.DemolitionJob(building.Id);
                if (demolition != null)
                {
                    AddLine(inspector, "Разборка: " + RussianTextCatalog.Percent((double)demolition.ProgressTicks /
                        context.BuildingLifecycle.DemolitionRequiredTicks(building)));
                    AddMuted(inspector, demolition.Status == WorkJobStatus.Active ? "Житель разбирает постройку." :
                        "Ожидает свободного жителя и его подхода к зданию.");
                    inspector.Add(ActionButton("Отменить разборку", () => TryCommand(() => context.Work.Cancel(demolition.Id))));
                }
                else if (definition.Maintenance != null && definition.Maintenance.CanDemolish)
                    inspector.Add(ActionButton("Демонтировать", () => TryCommand(() =>
                    {
                        var result = context.BuildingLifecycle.Demolish(building.Id,
                            context.Clock().State.CalendarTicks);
                        if (!result.Succeeded) throw new PlayerFacingException("Демонтаж недоступен: " + result.Failure);
                        SetStatus("Разборка назначена. Житель подойдёт и разберёт здание; материалы вернутся после завершения.");
                    })));
            }
            if (DeveloperUiVisible) AddMuted(inspector, F("ui.character.stable_id", building.Id));
        }

        private IEnumerable<InventoryOwner> LifecycleFundingSources()
        {
            foreach (var pile in context.WorldPiles.All.OrderBy(x => x.Id)) yield return pile.InventoryOwner;
            foreach (var inventory in context.Inventories.All.OrderBy(x => x.Owner.Id))
                if (inventory.Owner.Kind == InventoryOwnerKind.BuildingStorage) yield return inventory.Owner;
        }

        private void QueueProduction(Building building, ProductionRecipeId recipeId, int batches)
        {
            if (context.Production.QueueBatchesWithDeliveries(building.Id, recipeId, batches, context.Work) == 0 &&
                !context.Production.Sites.Any(x => x.Kind == ProductionSiteKind.BuildingWorkplace &&
                    x.OwnerBuildingId == building.Id && x.RecipeId == recipeId))
                throw new PlayerFacingException("Производственный рецепт недоступен.");
        }

        private void RefreshNotifications()
        {
            if (renderedNotificationVersion == notificationVersion) return;
            renderedNotificationVersion = notificationVersion;
            notifications.Clear();
            AddSection(notifications, T("ui.notifications.title"));
            if (notificationFeed.Count == 0) AddMuted(notifications, T("ui.notifications.none"));
            // The fixed-height player HUD intentionally shows only the three newest messages; older entries remain
            // in the bounded feed for tests/history without spilling into the selection inspector.
            for (var index = notificationFeed.Count - 1; index >= Math.Max(0, notificationFeed.Count - 3); index--)
            {
                var captured = index;
                var button = ActionButton(notificationFeed[index], () => ActivateNotification(captured));
                button.tooltip = "Перейти к событию";
                button.AddToClassList("serenity-notification");
                var lower = notificationFeed[index].ToLowerInvariant();
                button.AddToClassList(lower.Contains("травм") || lower.Contains("болез") || lower.Contains("смерт") ||
                                      lower.Contains("атак") || lower.Contains("пожар")
                    ? "is-danger"
                    : lower.Contains("не хват") || lower.Contains("голод") || lower.Contains("предуп")
                        ? "is-warning"
                        : "is-info");
                notifications.Add(button);
            }
        }

        private void CaptureNotifications()
        {
            CaptureWarWarnings();
            var researchCount = context.Research.CompletedTechnologies.Count;
            var characterCount = PlayerCharacterIds().Count;
            var aliveCount = PlayerAliveCount();
            var dynastyCount = context.Dynasty.History.Count;
            var conditionCount = context.Health.Conditions.Count(x=>PlayerCharacterIds().Contains(x.TargetId));
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
                if (researchCount > knownResearchCount) AddNotification(T("ui.notification.research"),
                    new NotificationTarget { Panel = GameplayPanel.Research });
                if (characterCount > knownCharacterCount)
                {
                    var born = PlayerCharacterIds().Select(id => context.Characters.Get(id))
                        .OrderByDescending(x => x.BiologicalBirthTick).ThenBy(x => x.Id).First();
                    AddNotification(T("ui.notification.birth"), new NotificationTarget { CharacterId = born.Id });
                }
                if (aliveCount < knownAliveCount)
                {
                    var dead = PlayerCharacterIds().Select(id => context.Characters.Get(id))
                        .Where(x => x.LifeState == CharacterLifeState.Dead)
                        .OrderByDescending(x => x.BiologicalDeathTick).ThenBy(x => x.Id).FirstOrDefault();
                    AddNotification(T("ui.notification.death"),
                        new NotificationTarget { CharacterId = dead?.Id, Panel = GameplayPanel.Dynasty });
                }
                if (dynastyCount > knownDynastyHistoryCount) AddNotification(T("ui.notification.succession"),
                    new NotificationTarget { CharacterId = context.Dynasty.CurrentMonarchId, Panel = GameplayPanel.Dynasty });
                if (conditionCount > knownConditionCount)
                {
                    var condition = context.Health.Conditions.LastOrDefault(x => PlayerCharacterIds().Contains(x.TargetId));
                    if (condition != null)
                    AddNotification(T("ui.notification.condition"),
                        new NotificationTarget { CharacterId = condition.TargetId });
                }
                if (buildingHistory > knownBuildingHistoryCount)
                {
                    var entry = context.Buildings.All.SelectMany(building => building.History.Select(change =>
                            new { Building = building, Change = change }))
                        .OrderByDescending(x => x.Change.CalendarTick).ThenByDescending(x => x.Change.Sequence).First();
                    AddNotification(F("ui.notification.building", E(entry.Change.Kind)),
                        new NotificationTarget { BuildingId = entry.Building.Id });
                }
                if (battleEvents > knownBattleEventCount)
                {
                    var change = context.Battles.Battles.SelectMany(x => x.BattleHistory)
                        .OrderByDescending(x => x.CalendarTick).ThenByDescending(x => x.Sequence).First();
                    AddNotification(T(change.Kind == BattleEventKind.Started ? "ui.notification.battle_started" :
                        change.Kind == BattleEventKind.Finished ? "ui.notification.battle_finished" : "ui.notification.battle_changed"),
                        new NotificationTarget { Panel = GameplayPanel.Military });
                }
                if (travelCount < knownTravelCount) AddNotification(T("ui.notification.arrival"),
                    new NotificationTarget { Panel = GameplayPanel.Global });
                if (shortage > knownFoodShortage) AddNotification(T("ui.notification.shortage"),
                    new NotificationTarget { Panel = GameplayPanel.Economy });
                if (pendingEventCount > knownPendingEventCount) AddNotification(T("ui.notification.event"),
                    new NotificationTarget { Panel = GameplayPanel.Events });
                if (eventHistoryCount > knownEventHistoryCount)
                {
                    var entry = context.Events.History.Last();
                    AddNotification(F("ui.notification.event_resolved", EventTitle(entry.DefinitionId,
                        context.Events.Catalog.Get(entry.DefinitionId).Nodes[0].Id)),
                        new NotificationTarget { Panel = GameplayPanel.Events });
                }
                if (campaignSequence > knownCampaignEventSequence && context.Campaign != null)
                {
                    if (context.Campaign.State.MilitaryVictory)
                        AddNotification(T("ui.notification.victory"), new NotificationTarget { Panel = GameplayPanel.Military });
                    else if (context.Campaign.State.RivalDiscovered)
                        AddNotification(T("ui.notification.discovery"), new NotificationTarget { Panel = GameplayPanel.Global });
                }
                if (occupationSequence > knownOccupationEventSequence)
                {
                    var change = context.GlobalMap.OccupationEvents.Last();
                    var key = change.Kind == SettlementOccupationEventKind.Started
                        ? "ui.notification.occupation_started"
                        : change.Kind == SettlementOccupationEventKind.Completed
                            ? "ui.notification.occupation_completed"
                            : "ui.notification.occupation_interrupted";
                    AddNotification(F(key, SideName(change.ClaimantSideId)),
                        new NotificationTarget { Panel = GameplayPanel.Global });
                }
                if (adultCount > knownAdultCount)
                {
                    var adult = PlayerCharacterIds().Select(id => context.Characters.Get(id))
                        .Where(x => x.AgeInCompletedYears(context.Clock().State.BiologicalTicks) >=
                            Game.Simulation.Health.HealthAndFamilyService.AdultAgeYears)
                        .OrderByDescending(x => x.BiologicalBirthTick).ThenBy(x => x.Id).First();
                    AddNotification(T("ui.notification.adulthood"), new NotificationTarget { CharacterId = adult.Id });
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
            if (!string.IsNullOrEmpty(context.ThirdPerson.LastAttackMessage))
                tpsLabel.text += "\n" + context.ThirdPerson.LastAttackMessage;
        }

        private void RefreshGameOver()
        {
            if (campaignOutcome == null) return;
            campaignOutcome.Clear();
            campaignOutcome.Add(gameOverLabel);
            campaignOutcome.Add(campaignOutcomeDetail);
            if (context.GameRun.Status == GameRunStatus.GameOver)
            {
                gameOverLabel.text = F("ui.game_over.defeat", E(context.GameRun.GameOverReason));
                campaignOutcomeDetail.text = "Партия завершена. Можно загрузить более раннее сохранение или вернуться в главное меню.";
                var actions = Row();
                actions.style.justifyContent = Justify.Center;
                actions.Add(ActionButton("Загрузить сохранение", () =>
                {
                    campaignOutcomeAcknowledged = true;
                    SetPanel(GameplayPanel.SaveLoad);
                }, "defeat-load-button"));
                actions.Add(ActionButton(T("ui.action.main_menu"), RequestReturnToMainMenu, "outcome-main-menu-button"));
                campaignOutcome.Add(actions);
                campaignOutcome.style.display = campaignOutcomeAcknowledged ? DisplayStyle.None : DisplayStyle.Flex;
                if (!campaignOutcomeAcknowledged) FocusWhenReady("defeat-load-button", campaignOutcome);
                return;
            }
            if (context.Campaign?.State.MilitaryVictory == true)
            {
                gameOverLabel.text = T("ui.game_over.victory");
                campaignOutcomeDetail.text = "Военная цель достигнута. Мир и симуляция остаются активны — можно продолжить эту партию.";
                var actions = Row();
                actions.style.justifyContent = Justify.Center;
                actions.Add(ActionButton("Продолжить партию", () =>
                {
                    campaignOutcomeAcknowledged = true;
                    campaignOutcome.style.display = DisplayStyle.None;
                }, "victory-continue-button"));
                actions.Add(ActionButton(T("ui.action.main_menu"), RequestReturnToMainMenu, "outcome-main-menu-button"));
                campaignOutcome.Add(actions);
                campaignOutcome.style.display = campaignOutcomeAcknowledged ? DisplayStyle.None : DisplayStyle.Flex;
                if (!campaignOutcomeAcknowledged) FocusWhenReady("victory-continue-button", campaignOutcome);
                return;
            }
            campaignOutcomeAcknowledged = false;
            campaignOutcome.style.display = DisplayStyle.None;
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

        private void SelectResidentFromWorkspace(StableEntityId characterId)
        {
            context.Selection.SelectCharacterIds(new[] { characterId });
            if (context.AiAgents.TryGet(characterId, out var agent)) context.FocusWorld(agent.Position);
            RefreshNow();
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
            AddMuted(inspector, "Приоритет: " + (workPriority == WorkPriority.Immediate ? "Срочно" : E(workPriority)));
            var row = Row();
            foreach (var value in new[] { WorkPriority.Normal, WorkPriority.High, WorkPriority.Immediate })
            {
                var captured = value;
                var button = ActionButton(value == WorkPriority.Immediate ? "Срочно" : E(value),
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
                case WorkGroupRole.Hunters: return WorkJobType.Gather;
                default: return null;
            }
        }

        private static string AllowedWorkLabel(WorkGroupRole role)
        {
            var type = JobTypeForRole(role);
            return type.HasValue ? E(type.Value) : "общие поручения";
        }

        public WorkOrder IssuePierFishing(Building building, WorldPosition position)
        {
            if(building==null || !building.IsOperational || building.OwnerSideId.Value!="player" ||
                building.DefinitionId.FamilyId.ToString()!="fishing_pier")
                throw new InvalidOperationException("Этот пирс недоступен для рыбалки.");
            var stock=context.Production.FishStockAt(position);
            if(stock==null)throw new InvalidOperationException("Рядом с пирсом нет доступной рыбы.");
            var worker=context.Selection.SelectedCharacterIds.FirstOrDefault();
            if(!worker.IsValid)
                worker=context.Characters.GetAll().Where(person=>person.LifeState==CharacterLifeState.Alive &&
                        context.Combat.TryGetSide(person.Id,out var side) && side.Value=="player" &&
                        !context.MilitaryTraining.GetRank(person.Id).HasValue && !context.Work.HasOpenJob(person.Id) &&
                        context.AiAgents.TryGet(person.Id,out _) && context.AiScheduler.CanInterruptForDirectWork(person.Id))
                    .OrderBy(person=>context.Tiers.GetRuntimeState(person.Id).Position.DistanceSquared(position))
                    .ThenBy(person=>person.Id).Select(person=>person.Id).FirstOrDefault();
            if(!worker.IsValid)throw new InvalidOperationException("Нет свободного жителя для рыбалки.");
            context.Production.RequestedFishingPiers[worker]=building.Id;
            var job=context.Work.CreateDirectJob(WorkJobType.Gather,worker,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite,stock.Id,stock.Position),workPriority);
            SetStatus("Житель направляется ловить рыбу на выбранном пирсе.");
            return job;
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
            var roster = militaryDraftSelection.Where(IsAvailableMilitaryRecruit)
                .Take(MilitaryUnitService.MaximumSquadMembers).ToArray();
            if (roster.Length == 0) throw PlayerError("ui.military.need_characters");
            var side = new MilitarySideId("player");
            var captain = roster.FirstOrDefault(id => context.Military.IsEligibleMilitaryMember(side, id));
            if (!captain.IsValid) throw PlayerError("ui.military.no_captain");
            var unit = context.Military.Create(side, roster, captain);
            context.Military.SetAbstractLocation(unit.Id, context.GlobalMap.ActiveLocationId.Value);
            selectedUnit = unit.Id;
            militaryDraftSelection.Clear();
        }

        private void ToggleMilitaryRecruit(StableEntityId id)
        {
            if (!IsAvailableMilitaryRecruit(id)) return;
            if (!militaryDraftSelection.Remove(id) &&
                militaryDraftSelection.Count < MilitaryUnitService.MaximumSquadMembers)
                militaryDraftSelection.Add(id);
            RefreshPanel();
        }

        private bool IsAvailableMilitaryRecruit(StableEntityId id)
        {
            if (!context.Characters.TryGet(id, out var person) || person.LifeState != CharacterLifeState.Alive ||
                !context.Military.IsEligibleMilitaryMember(new MilitarySideId("player"), id)) return false;
            return context.Military.Units.Where(unit => unit.Status != MilitaryUnitStatus.Disbanded)
                .All(unit => !unit.ActiveMemberIds.Contains(id));
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

        private static bool IsPlayerManagedResource(ResourceId id) => id.ToString() != "water";

        private IReadOnlyList<StableEntityId> PlayerCharacterIds()
        {
            var configured = context.PlayerCharacterIds();
            return configured;
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
                case Game.Simulation.Resources.ConstructionFailureReason.InvalidPlacement:
                    return string.IsNullOrWhiteSpace(context.BuildingPlacement.SurfaceBlockedReason)
                        ? T("ui.build.invalid_place")
                        : context.BuildingPlacement.SurfaceBlockedReason;
                default:
                    return !string.IsNullOrWhiteSpace(context.BuildingPlacement.SurfaceBlockedReason)
                        ? context.BuildingPlacement.SurfaceBlockedReason
                        : T("ui.build.invalid_place");
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
            var amounts = context.Resources.All.Where(x => IsPlayerManagedResource(x.Id))
                .Select(x => new { DisplayName = Content("resource", x.Id.ToString()), Units = inventory.GetAmount(x.Id).Units })
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
                case Game.Simulation.World.TravelOrderStatus.UnitNotFound: return "Армия больше недоступна.";
                case Game.Simulation.World.TravelOrderStatus.UnknownDestination: return "Локация больше недоступна.";
                case Game.Simulation.World.TravelOrderStatus.InvalidOrigin: return "Текущее положение армии не связано с картой.";
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

        private void AddNotification(string message, NotificationTarget target = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            notificationFeed.Add(message.Trim());
            notificationTargets.Add(target ?? new NotificationTarget());
            if (notificationFeed.Count > 20)
            {
                notificationFeed.RemoveAt(0);
                notificationTargets.RemoveAt(0);
            }
            notificationVersion++;
        }

        private void ActivateNotification(int index)
        {
            if (index < 0 || index >= notificationFeed.Count || index >= notificationTargets.Count) return;
            var target = notificationTargets[index];
            notificationFeed.RemoveAt(index);
            notificationTargets.RemoveAt(index);
            notificationVersion++;
            FocusNotification(target);
        }

        private void FocusNotification(NotificationTarget target)
        {
            if (target == null) return;
            if (target.Panel.HasValue) SetPanel(target.Panel.Value);
            if (target.CharacterId.HasValue)
            {
                context.Selection.SelectCharacterIds(new[] { target.CharacterId.Value });
                if (context.Presentations.TryGet(target.CharacterId.Value, out var presenter) && presenter != null)
                    context.FocusWorld(new WorldPosition(presenter.transform.position.x, presenter.transform.position.y,
                        presenter.transform.position.z));
                else if (context.Tiers.TryGetTier(target.CharacterId.Value, out _))
                    context.FocusWorld(context.Tiers.GetRuntimeState(target.CharacterId.Value).Position);
            }
            else if (target.BuildingId.HasValue)
            {
                var presenter = FindObjectsByType<Game.Presentation.Buildings.BuildingPresenter>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(x => x.IsBound && x.BuildingId == target.BuildingId.Value);
                if (presenter != null)
                {
                    context.Selection.SelectBuilding(presenter);
                    context.FocusWorld(new WorldPosition(presenter.transform.position.x, presenter.transform.position.y,
                        presenter.transform.position.z));
                }
            }
            else if (target.Position.HasValue) context.FocusWorld(target.Position.Value);
            SetStatus("Переход к событию.");
            RefreshNow();
        }

        private void CloseModal()
        {
            pendingLoadSlot = 0;
            pendingNewGameSeed = 0;
            modal.style.display = DisplayStyle.None;
            modal.Clear();
        }

        private void FocusWhenReady(string elementName, VisualElement focusScope = null)
        {
            if (root == null || string.IsNullOrWhiteSpace(elementName)) return;
            root.schedule.Execute(() =>
            {
                var target = root.Q<VisualElement>(elementName);
                if (target == null || !target.focusable || target.resolvedStyle.display == DisplayStyle.None) return;
                var focused = root.panel?.focusController?.focusedElement as VisualElement;
                if (focused == null || focused == root || !focused.enabledInHierarchy ||
                    (focusScope != null && !focusScope.Contains(focused))) target.Focus();
            }).ExecuteLater(1);
        }

        private ulong ComputeRefreshHash()
        {
            var clock = context.Clock();
            var hash = unchecked((ulong)clock.State.CalendarTicks) ^ ((ulong)clock.Speed << 4) ^
                       (clock.IsPaused ? 0x8000000000000000UL : 0UL) ^ context.GlobalMap.ComputeStateHash() ^
                       context.Military.ComputeStateHash() ^ context.Combat.ComputeStateHash() ^
                       context.MilitaryTraining.ComputeStateHash() ^ context.SettlementCapture.ComputeStateHash() ^
                       context.Research.ComputeStateHash() ^ context.Health.ComputeStateHash() ^
                       (context.Events?.ComputeStateHash() ?? 0UL) ^ (context.Campaign?.ComputeStateHash() ?? 0UL) ^
                       unchecked((ulong)(context.ConnectedWorld?.RevealedCellCount ?? 0) << 12) ^
                       unchecked((ulong)context.Selection.SelectedCharacterIds.Count << 20) ^
                       unchecked((ulong)context.Buildings.All.Sum(x => x.History.Count) << 24) ^
                       unchecked((ulong)context.Work.JobCount << 32);
            if (context.Selection.InspectedCharacterId.HasValue) hash ^= unchecked((ulong)context.Selection.InspectedCharacterId.Value.GetHashCode());
            if (context.Selection.SelectedBuildingPresenter != null) hash ^= unchecked((ulong)context.Selection.SelectedBuildingPresenter.BuildingId.GetHashCode());
            if (context.Selection.SelectedProductionSourcePresenter != null)
                hash ^= unchecked((ulong)context.Selection.SelectedProductionSourcePresenter.Site.Id.GetHashCode());
            if (context.Selection.SelectedWildlife != null)
                hash ^= unchecked((ulong)context.Selection.SelectedWildlife.StableId.GetHashCode());
            hash ^= unchecked((ulong)context.Characters.Count << 40);
            hash ^= unchecked((ulong)(context.ThirdPerson.LastAttackMessage?.GetHashCode() ?? 0));
            hash ^= unchecked((ulong)(selectedLocation?.GetHashCode() ?? 0) << 7);
            hash ^= unchecked((ulong)(selectedUnit?.GetHashCode() ?? 0) << 9);
            return hash;
        }

        private void AddPrimaryResourceChip(string resourceId, string iconKey, string tooltip)
        {
            var chip = new VisualElement { name = "resource-primary-chip-" + resourceId };
            chip.AddToClassList("serenity-resource-chip");
            chip.tooltip = tooltip;
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.Add(SerenityIconLibrary.Image(iconKey, tooltip));
            var value = new Label("0") { name = "resource-primary-" + resourceId };
            value.AddToClassList("serenity-resource-value");
            chip.Add(value);
            resourceStrip.Add(chip);
        }

        private void ToggleResourceDrawer()
        {
            resourcesExpanded = !resourcesExpanded;
            RefreshResourceDrawer();
        }

        private void ToggleNavigation()
        {
            navigationExpanded = !navigationExpanded;
            RefreshNavigationAppearance();
            ApplyResponsiveLayout();
        }

        private void RefreshNavigationAppearance()
        {
            if (navigation == null) return;
            navigation.EnableInClassList("is-expanded", true);
            foreach (var button in navigation.Query<Button>(className: "serenity-nav-button").ToList())
                button.text = button.userData as string ?? button.tooltip;
            if (onboardingToggleButton != null)
                onboardingToggleButton.text = "?";
        }

        private void AddBuildCategory(string label, BuildPaletteCategory category, string icon)
        {
            var captured = category;
            var button = ActionButton(string.Empty, () => OpenBuildCategory(captured),
                "build-category-" + category.ToString().ToLowerInvariant());
            button.AddToClassList("serenity-build-category");
            SerenityIconLibrary.Apply(button, icon, label);
            bottomDock.Add(button);
        }

        private void OpenBuildCategory(BuildPaletteCategory category)
        {
            buildCategory = category;
            activePanel = GameplayPanel.Build;
            mainPanel.style.display = DisplayStyle.Flex;
            RefreshNavigationSelection();
            ApplyPanelLayout();
            RefreshNow();
        }

        private void SelectResident(StableEntityId id)
        {
            var wasSelected = context.Selection.SelectedCharacterId == id;
            context.Selection.SelectCharacterIds(new[] { id });
            if (wasSelected && context.Presentations.TryGet(id, out var presenter) && presenter != null)
                context.FocusWorld(new WorldPosition(presenter.transform.position.x, presenter.transform.position.y,
                    presenter.transform.position.z));
            RefreshNow();
        }

        private void SelectAndFocusResident(StableEntityId id)
        {
            context.Selection.SelectCharacterIds(new[] { id });
            if (context.Presentations.TryGet(id, out var presenter) && presenter != null)
                context.FocusWorld(new WorldPosition(presenter.transform.position.x, presenter.transform.position.y,
                    presenter.transform.position.z));
            RefreshNow();
        }

        private void OpenSettings()
        {
            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddTitle(modal, "Настройки интерфейса");
            AddLine(modal, "Масштаб интерфейса");
            var scale = Row();
            foreach (var value in new[] { 1f, 1.15f, 1.25f, 1.4f })
            {
                var captured = value;
                scale.Add(ActionButton(Mathf.RoundToInt(value * 100f) + "%", () =>
                {
                    SetUiScale(captured);
                    ApplyResponsiveLayout();
                }));
            }
            modal.Add(scale);
            modal.Add(ActionButton(T("ui.action.main_menu"), RequestReturnToMainMenu, "settings-main-menu"));
            modal.Add(ActionButton("Закрыть", CloseModal, "settings-close"));
        }

        private static string PrimaryResourceIcon(string id)
        {
            if (id.Contains("wood") || id.Contains("log")) return "wood_log";
            if (id.Contains("stone") || id.Contains("ore")) return "stone";
            if (id.Contains("water")) return "water";
            if (id.Contains("fish")) return "fish";
            if (id.Contains("meat") || id.Contains("food") || id.Contains("berry")) return "meat";
            return "storage";
        }

        private static string BuildCategoryLabel(BuildPaletteCategory category)
        {
            return category switch
            {
                BuildPaletteCategory.Resources => "Ресурсы",
                BuildPaletteCategory.Production => "Производство",
                BuildPaletteCategory.Housing => "Жильё",
                BuildPaletteCategory.Food => "Еда",
                BuildPaletteCategory.Storage => "Склады",
                BuildPaletteCategory.Defense => "Защита",
                BuildPaletteCategory.Infrastructure => "Инфраструктура",
                _ => "Все"
            };
        }

        private static bool MatchesBuildCategory(BuildingDefinition definition, BuildPaletteCategory category)
        {
            if (category == BuildPaletteCategory.All) return true;
            var id = definition.Id.ToString().ToLowerInvariant();
            bool Has(params string[] words) => words.Any(id.Contains);
            return category switch
            {
                BuildPaletteCategory.Resources => Has("gather", "wood", "quarry", "mine", "fishing"),
                BuildPaletteCategory.Production => Has("workshop", "kiln", "forge", "smith", "campfire", "production"),
                BuildPaletteCategory.Housing => Has("shelter", "hut", "house", "hall", "home"),
                BuildPaletteCategory.Food => Has("garden", "farm", "crop", "fishing", "kitchen", "campfire"),
                BuildPaletteCategory.Storage => Has("storage", "basket", "warehouse", "stockpile"),
                BuildPaletteCategory.Defense => Has("palisade", "gate", "tower", "barracks", "training"),
                BuildPaletteCategory.Infrastructure => Has("path", "road", "meeting", "well", "bridge", "dock", "assembly", "torch", "pier"),
                _ => true
            };
        }

        private void ApplyPanelLayout()
        {
            if (mainPanel == null) return;
            var build = activePanel == GameplayPanel.Build;
            var peopleRisk = IsPeopleRiskWorkspace(activePanel);
            mainPanel.EnableInClassList("is-build-palette", build);
            mainPanel.EnableInClassList("is-people-risk-workspace", peopleRisk);
            mainPanel.style.left = peopleRisk ? 22 : 10;
            mainPanel.style.top = build ? StyleKeyword.Auto : Math.Max(130, topBar.layout.yMax + 10);
            mainPanel.style.bottom = build ? 170 : peopleRisk ? 94 : 130;
            mainPanel.style.height = build ? 292 : StyleKeyword.Auto;
            mainPanel.style.width = build || peopleRisk ? StyleKeyword.Auto : 480;
            mainPanel.style.right = build ? 374 : peopleRisk ? 22 : StyleKeyword.Auto;
            mainPanel.contentContainer.style.flexDirection = build ? FlexDirection.Row : FlexDirection.Column;
            mainPanel.contentContainer.style.flexWrap = build ? Wrap.Wrap : Wrap.NoWrap;
        }

        private void AddNavigation(string label, GameplayPanel panel, string icon = null)
        {
            var button = ActionButton(label, () => SetPanel(panel), "nav-" + panel.ToString().ToLowerInvariant());
            SerenityUiDesign.StyleButton(button, navigation: true);
            button.userData = label;
            button.tooltip = label;
            SerenityIconLibrary.Apply(button, icon ?? "settings", label);
            navigation.Add(button);
        }

        private void AddNavigation(GameplayUiRoute route)
        {
            var captured = route.Id;
            var button = ActionButton(route.Label, () => Navigate(captured), route.StableElementName);
            SerenityUiDesign.StyleButton(button, navigation: true);
            button.userData = route.Label;
            button.tooltip = string.IsNullOrEmpty(route.Shortcut)
                ? route.Label
                : route.Label + " · " + route.Shortcut;
            button.viewDataKey = "ui02-route-" + route.Id.ToString().ToLowerInvariant();
            button.AddToClassList("serenity-route-" + route.Id.ToString().ToLowerInvariant());
            SerenityIconLibrary.Apply(button, route.IconKey, button.tooltip);
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
            compactLayout = compact;
            root.EnableInClassList("serenity-compact", compact);
            if (topBar == null) return;
            resourceStrip.style.maxWidth = StyleKeyword.None;
            dateLabel.style.width = compact ? 126 : 156;
            weatherLabel.style.width = compact ? 155 : 205;
            modeLabel.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            alertLabel.style.width = 28;
            alertLabel.style.minWidth = 28;
            populationLabel.style.fontSize = compact ? 12 : 14;
            populationLabel.style.minWidth = 145;
            modeLabel.style.display = DisplayStyle.None;
            if (resourceDrawer != null) resourceDrawer.style.top = Math.Max(122, topBar.layout.yMax + 2);
            if (residentStrip != null) residentStrip.style.top = Math.Max(130, topBar.layout.yMax + 10);
            navigation.style.left = 10;
            navigation.style.right = 10;
            navigation.style.width = StyleKeyword.Auto;
            residentStrip.style.left = 10;
            residentStrip.style.right = compact ? 334 : 376;
            commandRibbon.style.left = compact ? 18 : 270;
            commandRibbon.style.right = compact ? 340 : 376;
            commandRibbon.style.height = compact ? 70 : 76;
            bottomDock.style.left = compact ? new Length(13, LengthUnit.Percent) : new Length(19, LengthUnit.Percent);
            bottomDock.style.right = compact ? new Length(13, LengthUnit.Percent) : new Length(19, LengthUnit.Percent);
            inspector.style.width = compact ? 322 : 354;
            inspector.style.bottom = 90;
            notifications.style.width = compact ? 322 : 354;
            notifications.style.top = Math.Max(130, topBar.layout.yMax + 10);
            onboardingCard.style.width = compact ? 322 : 354;
            localMiniMap.style.width = compact ? 184 : 220;
            localMiniMap.style.height = compact ? 154 : 184;
            realmLayers.style.width = compact ? 210 : 246;
            realmDetail.style.width = compact ? 270 : 310;
            realmMapHeader.style.left = compact ? 222 : 268;
            realmMapHeader.style.right = compact ? 282 : 322;
            realmMapOverlay.style.left = compact ? new Length(18, LengthUnit.Percent) : new Length(20, LengthUnit.Percent);
            realmMapOverlay.style.right = compact ? new Length(23, LengthUnit.Percent) : new Length(24, LengthUnit.Percent);
            realmMapOverlay.style.top = 140;
            realmMapOverlay.style.bottom = compact ? 224 : 212;
            realmRouteBar.style.left = compact ? 222 : 268;
            realmRouteBar.style.right = compact ? 282 : 322;
            realmRouteBar.style.height = compact ? 126 : 118;
            var strategicCenterWidth = Mathf.Max(320f, width - (compact ? 504f : 590f));
            realmMapHeader.style.right = StyleKeyword.Auto;
            realmMapHeader.style.width = strategicCenterWidth;
            realmRouteBar.style.right = StyleKeyword.Auto;
            realmRouteBar.style.width = strategicCenterWidth;
            var mapHeaderTitle = realmMapHeader.Q<Label>("realm-map-header-title");
            if (mapHeaderTitle != null) mapHeaderTitle.style.width = Mathf.Max(140f, strategicCenterWidth - 174f);
            var routeFacts = realmRouteBar.Q("realm-route-facts");
            if (routeFacts != null)
            {
                var factsWidth = Mathf.Max(296f, strategicCenterWidth - 24f);
                routeFacts.style.right = StyleKeyword.Auto;
                routeFacts.style.width = factsWidth;
                var factWidth = factsWidth / 4f;
                for (var index = 0; index < 4; index++)
                {
                    var fact = routeFacts.Q("realm-route-fact-" + index);
                    if (fact == null) continue;
                    fact.style.left = index * factWidth;
                    fact.style.width = Mathf.Max(68f, factWidth - 4f);
                }
            }
            ApplyPanelLayout();
            UpdateStrategicViewport();
            ResizeResearchWorkspace();
            var strategic = context != null && context.GlobalMap.Mode != GameWorldMode.Local;
            if (strategic)
            {
                mainPanel.style.display = DisplayStyle.None;
                mainPanel.style.visibility = Visibility.Hidden;
                // Leave the same right-hand viewport free as P102StrategicWorldView.
                mainPanel.style.left = 10;
                mainPanel.style.width = Mathf.Max(170f, width * 0.32f - 20f);
            }
            else mainPanel.style.visibility = Visibility.Visible;
            var hasSelection = context != null && (context.Selection.InspectedCharacterId.HasValue ||
                context.Selection.SelectedWildlife != null ||
                context.Selection.SelectedBuildingPresenter != null ||
                context.Selection.SelectedWorldResourcePilePresenter != null ||
                context.Selection.SelectedProductionSourcePresenter != null);
            var peopleRisk = IsPeopleRiskWorkspace(activePanel);
            inspector.style.top = hasSelection ? Math.Max(130, topBar.layout.yMax + 10) : onboarding.Visible ? 512 : 274;
            inspector.style.display = strategic || peopleRisk || !hasSelection ? DisplayStyle.None : DisplayStyle.Flex;
            localMiniMap.style.display = !strategic && !peopleRisk && !hasSelection
                ? DisplayStyle.Flex : DisplayStyle.None;
            notifications.style.display = strategic || peopleRisk || hasSelection ? DisplayStyle.None : DisplayStyle.Flex;
            onboardingCard.style.display = !strategic && !peopleRisk && onboarding.Visible && !hasSelection
                ? DisplayStyle.Flex : DisplayStyle.None;
            residentStrip.style.display = strategic || peopleRisk ? DisplayStyle.None : DisplayStyle.Flex;
            bottomDock.style.display = !strategic && (activePanel == GameplayPanel.Build ||
                context.BuildingPlacement.IsActive) ? DisplayStyle.Flex : DisplayStyle.None;
            if (commandRibbon != null && (strategic || peopleRisk)) commandRibbon.style.display = DisplayStyle.None;
            placementFeedback.style.display = !strategic && context.BuildingPlacement.IsActive
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            realmWorkspace.style.display = strategic && activePanel == GameplayPanel.Global
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateStrategicViewport()
        {
            if (strategicWorldView == null || realmMapOverlay == null || root == null ||
                Screen.width <= 0 || Screen.height <= 0) return;
            var rootWidth = root.resolvedStyle.width;
            var rootHeight = root.resolvedStyle.height;
            var bounds = realmMapOverlay.worldBound;
            if (float.IsNaN(rootWidth) || float.IsNaN(rootHeight) || rootWidth <= 0f || rootHeight <= 0f ||
                bounds.width <= 1f || bounds.height <= 1f) return;
            var pixelScaleX = Screen.width / rootWidth;
            var pixelScaleY = Screen.height / rootHeight;
            var left = Mathf.Clamp(bounds.xMin * pixelScaleX, 0f, Screen.width);
            var right = Mathf.Clamp(bounds.xMax * pixelScaleX, 0f, Screen.width);
            var top = Mathf.Clamp(bounds.yMin * pixelScaleY, 0f, Screen.height);
            var bottom = Mathf.Clamp(bounds.yMax * pixelScaleY, 0f, Screen.height);
            if (right - left < 64f || bottom - top < 64f) return;
            strategicWorldView.SetViewport(new Rect(left / Screen.width, 1f - bottom / Screen.height,
                (right - left) / Screen.width, (bottom - top) / Screen.height));
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
            var button = new Button(action) { text = label, name = name ?? string.Empty, tabIndex = 0 };
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
            SerenityUiTypography.ApplyDisplay(label);
            parent.Add(label);
        }

        private static void AddSection(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList(SerenityUiDesign.SectionClass);
            parent.Add(label);
        }

        private static void AddMetricCard(VisualElement parent, string kicker, string value, string detail)
        {
            var card = SerenityUiDesign.Card();
            card.AddToClassList("serenity-metric-card");
            card.style.minWidth = 210;
            card.style.flexGrow = 1;
            var kickerLabel = new Label(kicker);
            kickerLabel.AddToClassList(SerenityUiDesign.MutedClass);
            var valueLabel = new Label(value);
            valueLabel.AddToClassList(SerenityUiDesign.TitleClass);
            var detailLabel = new Label(detail);
            detailLabel.AddToClassList(SerenityUiDesign.BodyClass);
            card.Add(kickerLabel);
            card.Add(valueLabel);
            card.Add(detailLabel);
            parent.Add(card);
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

        private void AddInspectorHeader(string kind, string title, string now)
        {
            var header = new VisualElement { name = "inspector-entity-header" };
            header.AddToClassList("serenity-inspector-header");
            var kicker = new Label(kind ?? string.Empty) { name = "inspector-kind" };
            kicker.AddToClassList("serenity-inspector-kicker");
            var heading = new Label(title ?? string.Empty) { name = "inspector-title" };
            heading.AddToClassList("serenity-inspector-title");
            var current = new Label(now ?? string.Empty) { name = "inspector-now" };
            current.AddToClassList("serenity-inspector-now");
            header.Add(kicker);
            header.Add(heading);
            header.Add(current);
            inspector.Add(header);
        }

        private static void AddQueueOrder(VisualElement parent, WorkOrder order)
        {
            var line = new Label(E(order.Type) + " · " + E(order.Status) + " · " + E(order.Priority));
            line.AddToClassList("serenity-queue-order");
            parent.Add(line);
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
