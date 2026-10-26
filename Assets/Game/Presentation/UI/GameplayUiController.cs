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
        private VisualElement resourceStrip;
        private VisualElement resourceDrawer;
        private VisualElement residentStrip;
        private VisualElement bottomDock;
        private Label populationLabel;
        private Label placementFeedback;
        private ScrollView mainPanel;
        private ScrollView inspector;
        private VisualElement notifications;
        private VisualElement statusBar;
        private VisualElement tpsHud;
        private VisualElement modal;
        private VisualElement onboardingCard;
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
        private BuildPaletteCategory buildCategory = BuildPaletteCategory.All;
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
        public bool ContextMenuVisible => contextMenu != null && contextMenu.resolvedStyle.display != DisplayStyle.None;
        public ContextCommandTarget? ActiveContextTarget => activeContextTarget;
        public IReadOnlyDictionary<ContextActionKind, ContextActionAvailability> ContextActionStates => contextActionStates;
        public MilitaryAttackCommandResult LastMilitaryAttackResult { get; private set; }
        public DiagnosticExecutionResult LastDiagnosticResult { get; private set; }

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
            // UI Toolkit may hide a command menu from its click callback before SelectionProbe observes
            // the same physical release. Keep the pointer consumed through the following frame so that
            // clicking an action cannot fall through to the world and clear the stable selection.
            if (lastUiPointerFrame >= Time.frameCount - 1) return true;
            var screenPoint = new Vector2(point.x, Screen.height - point.y);
            var panelPoint = root.panel == null ? point : RuntimePanelUtils.ScreenToPanel(root.panel, screenPoint);
            if (Blocks(topBar, panelPoint) || Blocks(navigation, panelPoint) || Blocks(mainPanel, panelPoint) ||
                Blocks(inspector, panelPoint) || Blocks(notifications, panelPoint) || Blocks(onboardingCard, panelPoint) ||
                Blocks(statusBar, panelPoint) || Blocks(tpsHud, panelPoint) || Blocks(contextMenu, panelPoint) ||
                Blocks(resourceDrawer, panelPoint) || Blocks(residentStrip, panelPoint) || Blocks(bottomDock, panelPoint)) return true;
            return Blocks(modal, panelPoint);
        }

        public void SetPanel(GameplayPanel panel)
        {
            if (panel == GameplayPanel.Codes && !context.Clock().IsPaused)
            {
                SetStatus("Раздел кодов доступен только на паузе.");
                return;
            }
            activePanel = activePanel == panel ? GameplayPanel.None : panel;
            mainPanel.style.display = activePanel == GameplayPanel.None ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshNavigationSelection();
            ApplyPanelLayout();
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
            RefreshResidentStrip();
            RefreshPlacementFeedback();
            RefreshOnboarding();
            CaptureNotifications();
            RefreshNotifications();
            RefreshInspector();
            RefreshPanel();
            RefreshTps();
            RefreshGameOver();
            ApplyResponsiveLayout();
            lastRefreshHash = ComputeRefreshHash();
        }

        public void RefreshAfterStateRebind()
        {
            seenWarWarnings.Clear();
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
            row.Add(ActionButton(T("common.load"), () => ConfirmLoadSlot(index)));
            row.Add(ActionButton(T("common.cancel"), CloseModal));
            modal.Add(row);
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
            if (ContextMenuVisible && Mouse.current?.middleButton.wasPressedThisFrame == true) CloseContextMenu();
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

        private void OnSecondaryClicked()
        {
            if (context.BuildingPlacement.WasCancelledThisFrame) return;
            if (DeveloperUiVisible || IsTextInputFocused() || context.BuildingPlacement.IsActive) return;
            if (context.Selection.SelectedCharacterIds.Count == 0)
            {
                CloseContextMenu();
                return;
            }
            if (!raycaster.TryGetContextHit(input.PointerPosition, out var hit) ||
                !TryResolveContextTarget(hit, out var target))
            {
                CloseContextMenu();
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
            foreach (ContextActionKind action in Enum.GetValues(typeof(ContextActionKind)))
            {
                if (!ContextActionRules.SupportsTarget(action, target)) continue;
                if (target.Kind == ContextTargetKind.Building && context.Buildings.TryGet(target.EntityId, out var menuBuilding))
                {
                    if (action == ContextActionKind.Build && menuBuilding.IsOperational) continue;
                    if (action == ContextActionKind.Craft && (!menuBuilding.IsOperational || !context.Production.Sites.Any(s => s.OwnerBuildingId == menuBuilding.Id))) continue;
                    if (action == ContextActionKind.Attack && menuBuilding.OwnerSideId.Value == "player") continue;
                }
                var attackState = action == ContextActionKind.Attack ? EvaluateMilitaryAttack(target) : default;
                var state = action == ContextActionKind.Attack
                    ? new ContextActionAvailability(attackState.Enabled, attackState.Enabled
                        ? ContextUnavailableReason.None : ContextUnavailableReason.WrongRole)
                    : EvaluateForSelection(action, target);
                contextActionStates[action] = state;
                var captured = action;
                var label = ContextActionName(action);
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
                        ? "Доступно" : (state.Delayed ? "Задержка: " : "Недоступно: ") + ContextReason(state.Reason));
            }
            AddMuted(contextMenu, "Обычный выбор отменяет прежнюю очередь. Shift + выбор добавляет приказ в хвост.");
            PositionContextMenu(screenPosition);
            contextMenu.style.display = DisplayStyle.Flex;
        }

        public ContextCommandResult LastContextCommandResult { get; private set; }

        public bool TryGetFishingCampTarget(ContextCommandTarget target, out ContextCommandTarget fishTarget)
        {
            fishTarget = default;
            if (target.Kind != ContextTargetKind.Building || !context.Buildings.TryGet(target.EntityId, out var camp) ||
                camp.DefinitionId.ToString() != "fishing_camp" || !camp.IsOperational || camp.OwnerSideId.Value != "player") return false;
            if (!context.Production.Sites.Any(s => s.OwnerBuildingId == camp.Id && s.RecipeId.ToString() == "fish_camp")) return false;
            fishTarget = target;
            return true;
        }

        public ContextCommandResult ExecuteContextAction(ContextActionKind action, bool append)
        {
            if (!activeContextTarget.HasValue) throw new InvalidOperationException("Контекстная цель не выбрана.");
            var target = activeContextTarget.Value;
            if (action == ContextActionKind.Gather && TryGetFishingCampTarget(target, out var fishingTarget))
            { target = fishingTarget; action = ContextActionKind.Craft; }
            var roster = activeContextSelectionIds.ToArray();
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
            if (target.Kind == ContextTargetKind.ResourceSource && context.Production.TryGetSite(target.EntityId, out var site))
            {
                resourceAvailable = !site.IsDepleted && site.IsWorkAvailable;
                technology = context.Production.IsRecipeAvailable(site.RecipeId, true);
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
            }
            var busy = context.Work.HasOpenJob(id);
            var criticalNeed = context.AiAgents.TryGet(id, out var agent) &&
                (agent.Needs.Energy <= Game.Simulation.AI.Tier1AiScheduler.CriticalRestEnergy ||
                 agent.Needs.Hunger >= Game.Simulation.AI.Tier1AiScheduler.CriticalHunger);
            var interruptible = !busy || context.AiScheduler.CanInterruptForDirectWork(id);
            var withinDistance = !context.AiAgents.TryGet(id, out var distanceAgent) ||
                DistanceSquared(distanceAgent.Position, target.Position) <= 250f * 250f;
            var hunting = action == ContextActionKind.Gather && target.Kind == ContextTargetKind.ResourceSource &&
                context.Production.TryGetSite(target.EntityId, out var huntedSite) && huntedSite.RecipeId.ToString() == "hunt_animal";
            var correctRole = context.PlayerCharacterIds().Contains(id) &&
                (HasContextRole(id, action) || hunting && context.Military.TryGetForMember(id, out _));
            return new ContextActionFacts(exists && character.LifeState == CharacterLifeState.Alive,
                exists && capabilities.IsAdult, correctRole,
                exists && targetStatusValid && (action == ContextActionKind.Move || action == ContextActionKind.Inspect ||
                           capabilities.CanPerformOrdinaryWork),
                true, technology, withinDistance, HasContextPath(id, target.Position), resourceAvailable,
                capacity, busy, true, criticalNeed, interruptible);
        }

        private bool HasContextRole(StableEntityId id, ContextActionKind action)
        {
            if (!context.Work.Groups.TryGetForMember(id, out var group)) return true;
            WorkGroupRole required;
            switch (action)
            {
                case ContextActionKind.Gather:
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
            return Game.Presentation.AI.ReachableNavMeshPoint.TryFind(
                new Vector3(agent.Position.X, agent.Position.Y, agent.Position.Z),
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
                case ContextActionKind.Inspect: return "Осмотреть";
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

        private static string ContextReason(ContextUnavailableReason reason)
        {
            switch (reason)
            {
                case ContextUnavailableReason.WrongTargetType: return "неподходящий тип цели";
                case ContextUnavailableReason.WrongRole: return "неподходящая роль";
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
            topBar.style.left = 10; topBar.style.right = 10; topBar.style.top = 8; topBar.style.height = 58;
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems = Align.Center;
            topBar.style.paddingLeft = 8; topBar.style.paddingRight = 8;
            SerenityUiDesign.StyleSurface(topBar, true);
            topBar.AddToClassList("serenity-top-hud");
            topBar.pickingMode = PickingMode.Position;

            resourceStrip = new VisualElement { name = "resource-strip" };
            resourceStrip.AddToClassList("serenity-resource-strip");
            resourceStrip.style.flexDirection = FlexDirection.Row;
            resourceStrip.style.alignItems = Align.Center;
            populationLabel = new Label { name = "population-label" };
            populationLabel.AddToClassList("serenity-population-chip");
            resourceStrip.Add(SerenityIconLibrary.Image("population", "Жители поселения"));
            resourceStrip.Add(populationLabel);
            resourcesLabel = populationLabel;
            AddPrimaryResourceChip("wood_log", "wood_log", "Древесина");
            AddPrimaryResourceChip("stone", "stone", "Камень");
            AddPrimaryResourceChip("water", "water", "Вода");
            AddPrimaryResourceChip("meat", "meat", "Мясо");
            AddPrimaryResourceChip("fish", "fish", "Рыба");
            var resourceToggle = ActionButton("▾", ToggleResourceDrawer, "resource-drawer-toggle");
            resourceToggle.AddToClassList("serenity-compact-button");
            resourceToggle.tooltip = "Все ресурсы и резервы";
            resourceStrip.Add(resourceToggle);
            topBar.Add(resourceStrip);

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
            topBar.Add(timeCluster);

            alertLabel = HudLabel(66);
            alertLabel.name = "alert-count";
            alertLabel.tooltip = "Непрочитанные уведомления";
            topBar.Add(SerenityIconLibrary.Image("alerts", "Уведомления"));
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
            var settings = ActionButton(string.Empty, OpenSettings, "settings-button");
            SerenityIconLibrary.Apply(settings, "settings", "Настройки интерфейса");
            topBar.Add(settings);
            root.Add(topBar);

            resourceDrawer = Panel("resource-drawer");
            resourceDrawer.AddToClassList("serenity-resource-drawer");
            resourceDrawer.style.position = Position.Absolute;
            resourceDrawer.style.left = 10; resourceDrawer.style.top = 72; resourceDrawer.style.width = 510;
            resourceDrawer.style.maxHeight = 310;
            resourceDrawer.style.display = DisplayStyle.None;
            resourceDrawer.pickingMode = PickingMode.Position;
            root.Add(resourceDrawer);

            residentStrip = new VisualElement { name = "resident-strip" };
            residentStrip.AddToClassList("serenity-resident-strip");
            residentStrip.style.position = Position.Absolute;
            residentStrip.style.left = 78; residentStrip.style.right = 376; residentStrip.style.top = 72;
            residentStrip.style.height = 62;
            residentStrip.style.flexDirection = FlexDirection.Row;
            residentStrip.pickingMode = PickingMode.Position;
            root.Add(residentStrip);

            navigation = Panel("navigation");
            navigation.style.position = Position.Absolute;
            navigation.style.left = 10; navigation.style.top = 72; navigation.style.bottom = 52; navigation.style.width = 58;
            navigation.style.paddingTop = 8; navigation.style.paddingLeft = 7; navigation.style.paddingRight = 7;
            SerenityUiDesign.StyleSurface(navigation);
            navigation.AddToClassList("serenity-navigation-rail");
            navigation.pickingMode = PickingMode.Position;
            var navigationToggle = ActionButton("☰", ToggleNavigation, "navigation-toggle");
            navigationToggle.tooltip = "Развернуть меню";
            navigationToggle.AddToClassList("serenity-nav-toggle");
            navigation.Add(navigationToggle);
            onboardingToggleButton = ActionButton(T("ui.onboarding.hide"),
                () => SetOnboardingVisible(!onboarding.Visible), "onboarding-toggle");
            onboardingToggleButton.userData = T("ui.onboarding.hide");
            onboardingToggleButton.tooltip = "Обучение первого часа";
            navigation.Add(onboardingToggleButton);
            AddNavigation(T("ui.nav.build"), GameplayPanel.Build, "build");
            AddNavigation(T("ui.nav.settlement"), GameplayPanel.Economy, "settlement");
            AddNavigation(T("ui.nav.research"), GameplayPanel.Research, "research");
            AddNavigation(T("ui.nav.work"), GameplayPanel.Work, "work");
            AddNavigation(T("ui.nav.dynasty"), GameplayPanel.Dynasty, "dynasty");
            AddNavigation("Дом и семья", GameplayPanel.Domestic, "domestic");
            AddNavigation(T("ui.nav.military"), GameplayPanel.Military, "military");
            AddNavigation(T("ui.nav.saves"), GameplayPanel.SaveLoad, "save");
            AddNavigation("Коды", GameplayPanel.Codes, "settings");
            AddNavigation(T("ui.nav.world_map"), GameplayPanel.Global, "world");
            if (context.Events != null) AddNavigation(T("ui.nav.events"), GameplayPanel.Events, "events");
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
            bottomDock.style.bottom = 48;
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

            statusBar = Panel("status-bar");
            statusBar.style.position = Position.Absolute;
            statusBar.style.left = 10; statusBar.style.right = 10; statusBar.style.bottom = 6; statusBar.style.height = 36;
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

        private void ObserveOnboardingSignals()
        {
            var berries = new ResourceId("berries");
            var shelter = context.Buildings.All.Any(x => x.DefinitionId == new BuildingDefinitionId("primitive_shelter") &&
                                                          x.IsOperational);
            var storage = context.Buildings.All.Any(x => x.DefinitionId == new BuildingDefinitionId("storage_basket") &&
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
            populationLabel.text = F("ui.hud.population", alive);
            foreach (var definition in context.Resources.All)
            {
                var label = resourceStrip.Q<Label>("resource-primary-" + definition.Id);
                if (label == null) continue;
                label.text = TotalResource(definition.Id).ToString("N0");
                label.tooltip = Content("resource", definition.Id.ToString()) + ": всего " +
                                TotalResource(definition.Id).ToString("N0");
            }
            RefreshResourceDrawer();
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
            var codesButton = navigation.Q<Button>("nav-codes");
            if (codesButton != null) codesButton.SetEnabled(clock.IsPaused);
            if (!clock.IsPaused && activePanel == GameplayPanel.Codes)
            {
                activePanel = GameplayPanel.None;
                mainPanel.style.display = DisplayStyle.None;
            }
            RefreshNavigationSelection();
        }

        private void RefreshResourceDrawer()
        {
            if (resourceDrawer == null) return;
            resourceDrawer.style.display = resourcesExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (!resourcesExpanded) return;
            resourceDrawer.Clear();
            AddSection(resourceDrawer, "Ресурсы поселения");
            foreach (var definition in context.Resources.All.OrderBy(x => x.Id))
            {
                var total = TotalResource(definition.Id);
                var reserved = context.Work.Jobs.Where(x => !x.IsTerminal && x.ResourceId == definition.Id &&
                    x.ClaimState == WorkClaimState.Reserved).Sum(x => x.Quantity.Units);
                var row = Row();
                row.Add(SerenityIconLibrary.Image(PrimaryResourceIcon(definition.Id.ToString()),
                    Content("resource", definition.Id.ToString())));
                var line = new Label(Content("resource", definition.Id.ToString()) + "   " + total.ToString("N0") +
                                     "   доступно " + Math.Max(0, total - reserved).ToString("N0") +
                                     "   зарезервировано " + reserved.ToString("N0"));
                line.AddToClassList("serenity-resource-line");
                row.Add(line);
                resourceDrawer.Add(row);
            }
        }

        private void RefreshResidentStrip()
        {
            if (residentStrip == null) return;
            residentStrip.Clear();
            foreach (var person in PlayerCharacterIds().Select(id => context.Characters.TryGet(id, out var value) ? value : null)
                         .Where(x => x != null && x.LifeState == CharacterLifeState.Alive).Take(12))
            {
                var captured = person.Id;
                var status = context.Work.TryGetAssigned(person.Id, out var job) ? E(job.Type) :
                    context.AiAgents.TryGet(person.Id, out var agent) ? E(agent.CurrentAction) : "ожидает";
                var card = ActionButton(RussianTextCatalog.PersonName(person.Name.DisplayName) + "\n" + status,
                    () => SelectResident(captured), "resident-" + person.Id);
                card.AddToClassList("serenity-resident-card");
                card.tooltip = RussianTextCatalog.PersonName(person.Name.DisplayName) + " · " + status +
                               "\nЛКМ — выбрать; повторный клик — переместить камеру";
                SerenityIconLibrary.Apply(card, "population", card.tooltip);
                SerenityUiDesign.SetSelected(card, context.Selection.SelectedCharacterId == person.Id);
                residentStrip.Add(card);
            }
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
            var edibleUnits = context.Production.Food.EdibleResources.Sum(TotalResource);
            var foodDays = population == 0 ? 0d : edibleUnits * context.Production.Settings.FoodTicksPerPersonUnit.TotalDays / population;
            AddSection(mainPanel, T("ui.economy.overview"));
            AddLine(mainPanel, F("ui.economy.ruler_population", CharacterName(context.Dynasty.CurrentMonarchId), population));
            AddLine(mainPanel, F("ui.economy.camp_ready", T(shelterReady ? "ui.economy.ready" : "ui.economy.missing"),
                T(storageReady ? "ui.economy.ready" : "ui.economy.missing")));
            AddLine(mainPanel, F("ui.economy.food_days", edibleUnits, RussianTextCatalog.Decimal(foodDays, "0.#")));
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
            AddTitle(mainPanel, T("ui.build.title") + " · " + BuildCategoryLabel(buildCategory));
            AddLine(mainPanel, T("ui.build.instructions"));
            foreach (var entry in context.BuildingCatalog.Entries.OrderBy(x => x.DefinitionId, StringComparer.Ordinal))
            {
                // Retain the legacy ID for existing scenes/saves, not as a second player option.
                if (entry.DefinitionId == "primitive_crop_plot") continue;
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

        private void DrawDomesticPanel()
        {
            AddTitle(mainPanel, "Дом, сон и семья");
            var id = context.Selection.SelectedCharacterId ?? context.Dynasty.CurrentMonarchId;
            if (!id.HasValue)
            {
                AddMuted(mainPanel, "Выберите жителя.");
                return;
            }
            var character = context.Characters.Get(id.Value);
            AddLine(mainPanel, RussianTextCatalog.PersonName(character.Name.DisplayName));
            if (context.AiAgents.TryGet(id.Value, out var agent))
                AddLine(mainPanel, $"Голод: {agent.Needs.Hunger:P0} · энергия: {agent.Needs.Energy:P0} · настроение: {agent.Needs.Mood:P0}");
            var sleep = context.Domestic.EvaluateSleep(id.Value);
            AddLine(mainPanel, sleep.Allowed
                ? "Спальное место назначено в закрытом обитаемом доме."
                : "Сон недоступен: " + SleepReason(sleep.Reason));
            if (sleep.Bed != null)
                AddMuted(mainPanel, "Кровать: " + sleep.Bed.Id + " · дом: " + sleep.Bed.HomeBuildingId);
            var sleepButton = ActionButton("Лечь спать", () => TrySleep(id.Value), "domestic-sleep");
            sleepButton.SetEnabled(sleep.Allowed);
            mainPanel.Add(sleepButton);

            if (!character.SpouseId.HasValue) return;
            var spouseId = character.SpouseId.Value;
            AddSection(mainPanel, "Взрослые отношения");
            AddLine(mainPanel, "Партнёр: " + CharacterName(spouseId));
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
            AddTitle(mainPanel, T("ui.military.title"));
            var hasSelectedCharacters = context.Selection.SelectedCharacterIds.Count > 0;
            var hasCaptain = context.Selection.SelectedCharacterIds.Any(id => context.Health.GetCapabilities(id).CanFight);
            var createUnit = ActionButton(T("ui.military.create"), () => TryCommand(CreatePlayerUnit));
            createUnit.SetEnabled(hasSelectedCharacters && hasCaptain);
            mainPanel.Add(createUnit);
            if (!hasSelectedCharacters) AddMuted(mainPanel, T("ui.military.need_characters"));
            else if (!hasCaptain) AddMuted(mainPanel, T("ui.military.no_captain"));
            AddSection(mainPanel, "Обучение в казармах");
            var traineeId = context.Selection.SelectedCharacterId;
            var barracks = context.Buildings.All.Where(x => x.DefinitionId == MilitaryTrainingService.BarracksDefinitionId &&
                    x.IsOperational && x.OwnerSideId.Value == "player").OrderBy(x => x.Id).FirstOrDefault();
            if (!traineeId.HasValue) AddMuted(mainPanel, "Выберите одного взрослого жителя.");
            else
            {
                var rank = context.MilitaryTraining.GetRank(traineeId.Value);
                var next = context.MilitaryTraining.GetNextLevel(traineeId.Value);
                AddLine(mainPanel, CharacterName(traineeId) + " · звание: " +
                    (rank.HasValue ? rank.Value.ToString() : "нет"));
                if (context.MilitaryTraining.IsExternallyControlled(traineeId.Value))
                {
                    var active = context.MilitaryTraining.Active.Single(x => x.CharacterId == traineeId.Value);
                    AddLine(mainPanel, "Обучение: " + active.Rank + " · осталось " +
                        FormatDuration(Math.Max(0, active.CompletionTick - context.Clock().State.CalendarTicks)));
                }
                else if (next == null) AddMuted(mainPanel, "Достигнуто высшее утверждённое звание.");
                else
                {
                    AddLine(mainPanel, "Следующий курс: " + next.Rank + " · " + FormatDuration(next.DurationTicks));
                    AddMuted(mainPanel, "Цена: " + string.Join(", ", next.Cost.Select(x =>
                        x.Quantity.Units + " " + Content("resource", x.ResourceId.ToString()))));
                    var train = ActionButton("Начать обучение", () => TryCommand(() =>
                    {
                        if (barracks == null) throw new PlayerFacingException("Нет действующих казарм.");
                        var result = context.MilitaryTraining.TryStart(traineeId.Value, barracks.Id,
                            new CombatSideId("player"), context.Clock().State.CalendarTicks,
                            context.Inventories.All.Select(x => x.Owner));
                        if (!result.Started) throw new PlayerFacingException("Обучение недоступно: " + result.Failure);
                    }), "military-train");
                    train.SetEnabled(barracks != null && context.Health.GetCapabilities(traineeId.Value).IsAdult);
                    mainPanel.Add(train);
                }
            }
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
            if (selected.Supply.Initialized) AddLine(mainPanel, F("ui.military.supply", selected.Supply.Initialized
                ? selected.Supply.Get(context.Military.SupplySettings.FoodResourceId) + " " +
                  Content("resource", context.Military.SupplySettings.FoodResourceId.ToString())
                : T("ui.military.not_supplied")));
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
                row.Add(ActionButton(T("common.save"), () => SaveSlot(slot.Index)));
                var load = ActionButton(T("common.load"), () => RequestLoadSlot(slot.Index));
                load.SetEnabled(slot.Exists && !slot.IsCorrupt);
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

        private void DrawDiplomacy()
        {
            if (context.StrategicAi == null) return;
            AddSection(mainPanel, "Соседние поселения и отношения");
            var player = new CombatSideId("player");
            foreach (var faction in context.StrategicAi.Factions.Where(x => !x.Strategic.IsDefeated))
            {
                AddLine(mainPanel, SideName(faction.Strategic.SideId) + ": отношения " + faction.RelationTo(player) +
                    " · жителей " + faction.Residents.Count + " · обучено " + (faction.Training?.Records
                        .Where(x => x.Completed).Select(x => x.CharacterId).Distinct().Count() ?? 0));
                foreach (var order in faction.Strategic.PendingOrders.Where(x => x.Kind == AiDecisionKind.PrepareAttack && TargetsPlayer(x)))
                    AddLine(mainPanel, "⚠ " + WarWarning(faction, order));
                mainPanel.Add(ActionButton("Дар: 10 ягод (+20 к отношениям)", () => TryCommand(() =>
                {
                    if (!context.StrategicAi.TrySendGift(faction.Strategic.Id, player, context.Inventories))
                        throw new PlayerFacingException("Дар не отправлен: нужно 10 ягод и свободное место у получателя.");
                    AddNotification("Дар принят. Отношения улучшились.");
                })));
                mainPanel.Add(ActionButton("Объявить вражду", () => TryCommand(() =>
                {
                    faction.SetRelation(player, -100);
                    AddNotification("Отношения стали враждебными. Сосед может начать подготовку к войне.");
                })));
            }
        }

        private void DrawGlobalPanel()
        {
            AddTitle(mainPanel, T("ui.map.title"));
            DrawDiplomacy();
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
            foreach (var unit in context.Military.Units.Where(x => x.OwnerSideId.Value == "player"))
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
            var attackButton = ActionButton("Напасть", () => TryCommand(() =>
            {
                if (!selectedUnit.HasValue || !selectedLocation.HasValue)
                    throw PlayerError("ui.map.need_unit_destination");
                var unit = context.Military.Get(selectedUnit.Value);
                if (unit.Representation != MilitaryUnitRepresentationMode.Detailed)
                    unit = context.MilitaryRepresentations.Transition(unit.Id, MilitaryUnitRepresentationMode.Detailed);
                var location = context.GlobalMap.Locations.Single(x => x.Id == selectedLocation.Value);
                var result = context.MilitaryAttack.Issue(unit.ActiveMemberIds,
                    new MilitaryDirectTarget(MilitaryDirectTargetKind.Area,
                        new WorldPosition(location.MapX, 0, location.MapY)), context.Clock().State.CalendarTicks, true);
                if (!result.Issued) throw new PlayerFacingException(MilitaryAttackReason(result.Availability.Reason));
                AddNotification("Военные идут к цели через единый физический мир.");
            }), "strategic-attack");
            attackButton.SetEnabled(selectedUnit.HasValue && selectedLocation.HasValue);
            actions.Add(attackButton);
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
                AddLine(inspector, F("ui.selection.characters", context.Selection.SelectedCharacterIds.Count));
                AddMuted(inspector, T("ui.selection.many_help"));
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
                if (context.Production.Animals.TryGet(source.Site.Id, out var prey))
                {
                    AddLine(inspector, $"Здоровье: {prey.Health}/{prey.Maximum} · " +
                        (prey.Alive ? "Охота с луком" : "Туша готова к разделке"));
                    if (prey.Domestic)
                    {
                        AddLine(inspector, "Домашняя овца · " + (prey.Male ? "самец" : "самка"));
                        if (!context.Buildings.TryGet(prey.Home, out var home) || home.OwnerSideId.Value != "player")
                        { AddMuted(inspector, "Животное принадлежит другому поместью."); return; }
                    }
                }
                AddLine(inspector, F("ui.selection.source_remaining", source.RemainingAmount, source.SourceCapacity));
                AddLine(inspector, string.Join(" + ", recipe.OutputsFor(source.Site).Select(x =>
                    Content("resource", x.ResourceId.ToString()) + " ×" + x.Quantity.Units)));
                var harvest = context.Production.ActiveTasks.FirstOrDefault(x => x.SiteId == source.Site.Id);
                if (harvest != null)
                    AddLine(inspector, "Прогресс: " + RussianTextCatalog.Percent(
                        (double)harvest.AccumulatedWorkTicks / harvest.RequiredWorkTicks));
                if (recipe.HarvestEntireSource)
                    AddMuted(inspector, "Один приказ — весь источник. Добытое попадает в инвентарь работника или свободный склад.");
                DrawDirectPriority();
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
            AddSection(inspector, RussianTextCatalog.PersonName(character.Name.DisplayName));
            AddLine(inspector, F("ui.character.age", character.AgeInCompletedYears(context.Clock().State.BiologicalTicks),
                E(character.Sex), E(character.LifeState)));
            AddLine(inspector, F("ui.character.profession", ProfessionName(character.ProfessionId?.ToString())));
            AddLine(inspector, F("ui.character.health", capabilities.IsIncapacitated ? T("ui.character.incapacitated") : T("common.active"),
                Percent(capabilities.WorkPerThousand), Percent(capabilities.ResearchPerThousand)));
            var body = character.Health;
            var hitPoints = new[] { body.Head.Value, body.Torso.Value, body.LeftArm.Value, body.RightArm.Value,
                body.LeftLeg.Value, body.RightLeg.Value };
            var healthPoints = character.LifeState == CharacterLifeState.Dead ? 0 : hitPoints.Sum() / hitPoints.Length;
            AddLine(inspector, $"Жизнь: {healthPoints}/100 · минимум части тела {hitPoints.Min()}/100");
            AddLine(inspector, $"Любимое занятие: {CharacterSkillRules.Favorite(character)}");
            AddMuted(inspector, "Навыки: " + string.Join(", ", character.CaptureState().Skills
                .Where(x => !CharacterSkillRules.IsFavoriteMarker(x.Id))
                .OrderByDescending(x => x.Level.Value).ThenBy(x => x.Id.Value)
                .Select(x => $"{x.Id} {x.Level.Value}")));
            if (context.AiAgents.TryGet(id, out var agent))
                AddLine(inspector, F("ui.character.needs", RussianTextCatalog.Percent(agent.Needs.Hunger),
                    RussianTextCatalog.Percent(agent.Needs.Energy), E(agent.CurrentAction), E(agent.Phase)) +
                    $" · настроение {agent.Needs.Mood:P0}");
            else AddMuted(inspector, T("ui.character.offcamera"));
            var conditions = context.Health.Conditions.Where(x => x.TargetId == id &&
                x.State != HealthConditionState.Recovered).ToArray();
            AddLine(inspector, F("ui.character.conditions", conditions.Length == 0 ? T("common.none") : string.Join(", ", conditions.Select(x =>
                Content("condition", x.DefinitionId.ToString()) + " · " + E(x.Severity)))));
            var pregnancy = context.Health.Pregnancies.FirstOrDefault(x => x.MotherId == id && x.Status == PregnancyStatus.Active);
            if (pregnancy != null) AddLine(inspector, T("ui.character.pregnancy"));
            var inventoryOwner = context.Inventories.WorkerOwner(id);
            if (context.Inventories.TryGet(inventoryOwner, out var inventory) && inventory.TotalUnits > 0)
                AddLine(inspector, "Несёт на склад: " + InventoryText(inventory));
            var research = context.Research.AssignedWorkers.Contains(id) ? T("ui.character.research_assigned") : T("ui.character.research_not_assigned");
            AddLine(inspector, F("ui.character.research", research));
            AddLine(inspector, F("ui.character.work_group", context.Work.Groups.TryGetForMember(id, out var group) ? group.DisplayName : T("common.none")));
            AddLine(inspector, F("ui.character.current_job", context.Work.TryGetAssigned(id, out var job)
                ? E(job.Type) + " · " + E(job.Status) : T("common.none")));
            AddLine(inspector, F("ui.character.dynasty_role", DynastyRole(id)));
            AddLine(inspector, F("ui.character.military", context.Military.TryGetForMember(id, out var unit)
                ? T("ui.character.member") + (unit.CaptainId == id ? ", " + T("ui.character.captain") : string.Empty)
                : T("ui.character.civilian")));
            if (context.Combat.TryGetCombatant(id, out var combatant))
                AddLine(inspector, $"Оружие: {combatant.WeaponId} · прочность {combatant.WeaponDurability}/100");
            if (context.Events != null)
            {
                var eventHistory = context.Events.History.Where(x => x.Targets.Any(target =>
                    target.Kind == EventTargetKind.Character && target.TryGetStableEntityId(out var targetId) && targetId == id))
                    .Reverse().Take(3).ToArray();
                AddLine(inspector, F("ui.character.events", eventHistory.Length == 0 ? T("common.none") : string.Join(", ",
                    eventHistory.Select(x => EventTitle(x.DefinitionId, context.Events.Catalog.Get(x.DefinitionId).Nodes[0].Id)))));
            }
            if (!context.PlayerCharacterIds().Contains(id))
            {
                AddMuted(inspector, "Житель другого поселения. Доступен только осмотр.");
                return;
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
                AddLine(inspector, F("ui.building.storage", storage.TotalUnits, storage.Capacity, InventoryText(storage)));
            if (context.Production.Sites.FirstOrDefault(x => x.Id == building.Id) is ProductionSite site)
            {
                AddLine(inspector, F("ui.building.production", building.IsOperational ? T("ui.building.operational") : T("common.unavailable")));
                if (context.Production.Animals.IsPasture(site.Id))
                {
                    var herd = context.Production.Animals.AtHome(site.Id);
                    var policy = context.Production.Animals.Policy;
                    AddLine(inspector, $"Овечий загон · живых животных: {herd.Count(x => x.Alive)}/{policy.PastureCapacity}");
                    AddMuted(inspector, $"Для приплода нужны взрослые самец и самка. Взросление — {policy.LivestockMaturityDays} дн., приплод — раз в {policy.LivestockBirthDays} дн. при свободном месте.");
                    foreach (var sheep in herd.Where(x => !x.Harvested))
                        AddLine(inspector, $"{(sheep.Male ? "Самец" : "Самка")}: {sheep.Health}/{sheep.Maximum} HP");
                }
                else if (site.Kind == ProductionSiteKind.PrimitiveCropPlot)
                {
                    if (building.OwnerSideId.Value == "player" && building.IsOperational &&
                        site.CropPhase == CropPlotPhase.Unplanted && !context.Production.Animals.States.Any(x => x.Domestic))
                    {
                        AddMuted(inspector, "Первое пустое поле можно отдать стартовой паре овец. Пара доступна один раз; дальше стадо размножается.");
                        inspector.Add(ActionButton("Сделать овечий загон", () => TryCommand(() =>
                        {
                            var changed = context.Production.EstablishSheepPasture(site.Id, new CombatSideId("player"));
                            SetStatus(changed ? "В загон заселены самец и самка." : "Нужно свободное поле без выполняемых работ.");
                        })));
                    }
                    AddLine(inspector, F("ui.building.crop", E(site.CropPhase),
                        site.GrowthRequiredTicks == 0 ? "—" : RussianTextCatalog.Percent((double)site.GrowthProgressTicks / site.GrowthRequiredTicks)));
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
            var workplaceSites = context.Production.Sites.Where(x =>
                x.Kind == ProductionSiteKind.BuildingWorkplace && x.OwnerBuildingId == building.Id).ToArray();
            if (workplaceSites.Length > 0)
            {
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
                        AddLine(inspector, "Ловля рыбы · " + (reason == Game.Simulation.Production.ProductionWaitingReason.OutputFull ? "инвентарь заполнен" :
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
                    recipeRow.Add(ActionButton(workplaceSite.Paused ? "Продолжить" : "Пауза", () => TryCommand(() =>
                        context.Production.SetWorkplacePaused(building.Id, capturedRecipe, !workplaceSite.Paused))));
                    inspector.Add(recipeRow);
                }
            }
            DrawDirectPriority();
            var target = WorkTarget.ForEntity(WorkTargetKind.Building, building.Id,
                new WorldPosition(building.Coordinate.X, building.Coordinate.Level, building.Coordinate.Z));
            inspector.Add(ActionButton(building.IsOperational ? building.DefinitionId.ToString() == "fishing_camp" ? "Ловить рыбу" : "Изготавливать" : "Строить",
                () => TryCommand(() => IssueDirect(building.IsOperational ? WorkJobType.Craft : WorkJobType.Build, target))));
            if (building.IsOperational && context.Facilities != null && context.Selection.SelectedCharacterIds.Count > 0)
            {
                if (definition.Housing != null)
                    inspector.Add(ActionButton("Поселить выбранных", () => TryCommand(() =>
                    {
                        var assigned = context.Selection.SelectedCharacterIds.Count(id =>
                            context.Facilities.AssignHome(id, building.Id).Succeeded);
                        SetStatus($"Назначено жильцов: {assigned}.");
                    })));
                if (definition.Workplace != null && definition.Production == null)
                    inspector.Add(ActionButton("Назначить выбранных работников", () => TryCommand(() =>
                    {
                        var assigned = context.Selection.SelectedCharacterIds.Count(id =>
                            context.Facilities.AssignWorkplace(id, building.Id).Succeeded);
                        SetStatus($"Назначено работников: {assigned}.");
                    })));
                if (building.DefinitionId == new BuildingDefinitionId("training_ground"))
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
                if (building.DefinitionId == new BuildingDefinitionId("healer_hut") &&
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
            if (!building.IsOperational)
                inspector.Add(ActionButton("Отменить стройку", () => TryCommand(() =>
                {
                    if (!context.BuildingPlacement.CancelConstruction(building))
                        throw new PlayerFacingException("Стройку уже нельзя отменить.");
                    SetStatus("Стройка отменена; доставленные материалы возвращены на землю.");
                })));
            else if (context.BuildingLifecycle != null)
            {
                if (definition.Maintenance != null && building.CurrentIntegrity < building.MaxIntegrity)
                    inspector.Add(ActionButton("Ремонтировать", () => TryCommand(() =>
                    {
                        var result = context.BuildingLifecycle.Repair(building.Id, StableEntityId.NewId(),
                            TimeSpan.FromTicks(definition.Maintenance.RepairWorkRequiredTicks),
                            context.Clock().State.CalendarTicks, LifecycleFundingSources());
                        if (!result.Succeeded) throw new PlayerFacingException("Ремонт недоступен: " + result.Failure);
                        SetStatus($"Ремонт завершён: +{result.IntegrityChanged} прочности.");
                    })));
                if (definition.Upgrades != null)
                    foreach (var upgrade in definition.Upgrades.Options)
                    {
                        var capturedUpgrade = upgrade;
                        inspector.Add(ActionButton("Улучшить: " + Content("building", upgrade.TargetDefinitionId.ToString()),
                            () => TryCommand(() =>
                            {
                                var result = context.BuildingLifecycle.Upgrade(building.Id,
                                    capturedUpgrade.TargetDefinitionId, StableEntityId.NewId(),
                                    TimeSpan.FromTicks(capturedUpgrade.WorkRequiredTicks),
                                    context.Clock().State.CalendarTicks, LifecycleFundingSources());
                                if (!result.Succeeded) throw new PlayerFacingException("Улучшение недоступно: " + result.Failure);
                                SetStatus("Здание улучшено.");
                            })));
                    }
                if (building.DefinitionId == new BuildingDefinitionId("gate") && context.BuildingPathing != null)
                {
                    var open = context.BuildingPathing.IsGateOpen(building.Id);
                    inspector.Add(ActionButton(open ? "Закрыть ворота" : "Открыть ворота", () => TryCommand(() =>
                    {
                        if (!context.BuildingPathing.SetGateOpen(building.Id, !open))
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
            // The fixed-height player HUD intentionally shows only the two newest messages; older entries remain
            // in the bounded feed for tests/history without spilling into the selection inspector.
            for (var index = notificationFeed.Count - 1; index >= Math.Max(0, notificationFeed.Count - 2); index--)
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
                    var condition = context.Health.Conditions.Last();
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

        private ulong ComputeRefreshHash()
        {
            var clock = context.Clock();
            var hash = unchecked((ulong)clock.State.CalendarTicks) ^ ((ulong)clock.Speed << 4) ^
                       (clock.IsPaused ? 0x8000000000000000UL : 0UL) ^ context.GlobalMap.ComputeStateHash() ^
                       context.Military.ComputeStateHash() ^ context.Combat.ComputeStateHash() ^
                       context.MilitaryTraining.ComputeStateHash() ^ context.SettlementCapture.ComputeStateHash() ^
                       context.Research.ComputeStateHash() ^ context.Health.ComputeStateHash() ^
                       (context.Events?.ComputeStateHash() ?? 0UL) ^ (context.Campaign?.ComputeStateHash() ?? 0UL) ^
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
            navigation.EnableInClassList("is-expanded", navigationExpanded);
            var toggle = navigation.Q<Button>("navigation-toggle");
            if (toggle != null)
            {
                toggle.text = navigationExpanded ? "‹" : "☰";
                toggle.tooltip = navigationExpanded ? "Свернуть меню" : "Развернуть меню";
            }
            foreach (var button in navigation.Query<Button>(className: "serenity-nav-button").ToList())
                button.text = navigationExpanded ? button.userData as string ?? button.tooltip : string.Empty;
            if (onboardingToggleButton != null)
                onboardingToggleButton.text = navigationExpanded
                    ? T(onboarding.Visible ? "ui.onboarding.hide" : "ui.onboarding.show")
                    : "?";
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
                BuildPaletteCategory.Infrastructure => Has("path", "road", "meeting", "well", "bridge", "dock", "assembly"),
                _ => true
            };
        }

        private void ApplyPanelLayout()
        {
            if (mainPanel == null) return;
            var build = activePanel == GameplayPanel.Build;
            mainPanel.EnableInClassList("is-build-palette", build);
            mainPanel.style.left = navigationExpanded ? 190 : 78;
            mainPanel.style.top = build ? StyleKeyword.Auto : 142;
            mainPanel.style.bottom = build ? 130 : 52;
            mainPanel.style.height = build ? 292 : StyleKeyword.Auto;
            mainPanel.style.width = build ? StyleKeyword.Auto : 480;
            mainPanel.style.right = build ? 374 : StyleKeyword.Auto;
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
            resourceStrip.style.maxWidth = compact ? 515 : 650;
            dateLabel.style.width = compact ? 126 : 156;
            weatherLabel.style.width = compact ? 155 : 205;
            modeLabel.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            alertLabel.style.width = compact ? 46 : 66;
            navigation.style.width = navigationExpanded ? (compact ? 164 : 180) : 58;
            residentStrip.style.left = navigationExpanded ? (compact ? 174 : 190) : 78;
            residentStrip.style.right = compact ? 334 : 376;
            bottomDock.style.left = compact ? new Length(13, LengthUnit.Percent) : new Length(19, LengthUnit.Percent);
            bottomDock.style.right = compact ? new Length(13, LengthUnit.Percent) : new Length(19, LengthUnit.Percent);
            inspector.style.width = compact ? 322 : 354;
            notifications.style.width = compact ? 322 : 354;
            onboardingCard.style.width = compact ? 322 : 354;
            inspector.style.top = onboarding.Visible ? 512 : 274;
            ApplyPanelLayout();
            var strategic = context != null && context.GlobalMap.Mode != GameWorldMode.Local;
            if (strategic)
            {
                // Leave the same right-hand viewport free as P102StrategicWorldView.
                navigation.style.width = 126;
                mainPanel.style.left = 146;
                mainPanel.style.width = Mathf.Max(170f, width * 0.32f - 156f);
            }
            var hasSelection = context != null && (context.Selection.InspectedCharacterId.HasValue ||
                context.Selection.SelectedWildlife != null ||
                context.Selection.SelectedBuildingPresenter != null ||
                context.Selection.SelectedWorldResourcePilePresenter != null ||
                context.Selection.SelectedProductionSourcePresenter != null);
            inspector.style.display = strategic || !hasSelection ? DisplayStyle.None : DisplayStyle.Flex;
            notifications.style.display = strategic ? DisplayStyle.None : DisplayStyle.Flex;
            onboardingCard.style.display = !strategic && onboarding.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            residentStrip.style.display = strategic ? DisplayStyle.None : DisplayStyle.Flex;
            bottomDock.style.display = strategic ? DisplayStyle.None : DisplayStyle.Flex;
            placementFeedback.style.display = !strategic && context.BuildingPlacement.IsActive
                ? DisplayStyle.Flex
                : DisplayStyle.None;
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
            // Space belongs to pause in gameplay; a focused button must not also submit on it.
            var button = new Button(action) { text = label, name = name ?? string.Empty, focusable = false };
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
