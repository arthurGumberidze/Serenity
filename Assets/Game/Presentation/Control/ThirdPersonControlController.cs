using System;
using Game.Domain;
using Game.Domain.Control;
using Game.Domain.Health;
using Game.Presentation.AI;
using Game.Presentation.Buildings;
using Game.Presentation.CameraControl;
using Game.Presentation.Characters;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Simulation.AI;
using Game.Simulation.Control;
using Game.Simulation.Military;
using Game.Simulation.Time;
using Game.Simulation.Work;
using Game.Simulation.Combat;
using Unity.Cinemachine;
using UnityEngine;
using Game.Presentation.Localization;

namespace Game.Presentation.Control
{
    /// <summary>One scene-level adapter from input/camera presentation to PlayerControlService.</summary>
    [DisallowMultipleComponent]
    public sealed class ThirdPersonControlController : MonoBehaviour, IWorldPointerUiBlocker
    {
        private readonly Rect overlayRect = new Rect(8f, 742f, 390f, 150f);
        private LocalGameplayInputSource input;
        private SelectionProbe selection;
        private CharacterPresentationRegistry presentations;
        private Tier1AiRuntimeDriver runtime;
        private RtsCameraController rtsController;
        private BuildingPlacementController buildingPlacement;
        private PlayerControlService control;
        private ICharacterCapabilityProvider capabilities;
        private MilitaryUnitService military;
        private WorkManager work;
        private WorldPointerRaycaster raycaster;
        private CombatService combat;
        private CinemachineCamera rtsCamera;
        private CinemachineCamera thirdPersonCamera;
        private CinemachineThirdPersonFollow thirdPersonFollow;
        private Transform cameraTarget;
        private NavMeshMovementDriver directMovement;
        private StableEntityId? activeCharacterId;
        private int rtsPriority;
        private float yaw;
        private float pitch = 12f;
        private bool initialized;

        public DirectControlEligibilityFailure LastEntryFailure { get; private set; }
        public string LastEntryMessage { get; private set; } = string.Empty;
        public CinemachineCamera ThirdPersonCamera => thirdPersonCamera;
        public int CameraRigCount => thirdPersonCamera == null ? 0 : 1;
        public int SubscriptionCount => initialized ? 4 : 0;
        public ManualCombatAttackStatus LastAttackStatus { get; private set; } = ManualCombatAttackStatus.Invalid;
        public Tier1MovementAuthority MovementAuthority => directMovement?.Authority ?? Tier1MovementAuthority.Autonomous;
        public bool ShowLegacyOverlay { get; set; }

        public void Initialize(LocalGameplayInputSource inputSource, SelectionProbe selectionProbe,
            CharacterPresentationRegistry presentationRegistry, Tier1AiRuntimeDriver aiRuntime,
            RtsCameraController rtsCameraController, BuildingPlacementController placementController,
            PlayerControlService playerControl, ICharacterCapabilityProvider capabilityProvider,
            MilitaryUnitService militaryUnits, WorkManager workManager, WorldPointerRaycaster worldRaycaster,
            CombatService combatService)
        {
            if (initialized) throw new InvalidOperationException("Third-person controller is already initialized.");
            input = inputSource ?? throw new ArgumentNullException(nameof(inputSource));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            presentations = presentationRegistry ?? throw new ArgumentNullException(nameof(presentationRegistry));
            runtime = aiRuntime != null ? aiRuntime : throw new ArgumentNullException(nameof(aiRuntime));
            rtsController = rtsCameraController != null ? rtsCameraController : throw new ArgumentNullException(nameof(rtsCameraController));
            buildingPlacement = placementController != null ? placementController : throw new ArgumentNullException(nameof(placementController));
            control = playerControl ?? throw new ArgumentNullException(nameof(playerControl));
            capabilities = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
            military = militaryUnits;
            work = workManager;
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            rtsCamera = rtsController.GetComponentInChildren<CinemachineCamera>(true);
            if (rtsCamera == null) throw new InvalidOperationException("RTS Cinemachine camera is required.");
            rtsPriority = rtsCamera.Priority.Value;
            CreateCameraRig();
            input.ControlModeToggled += OnToggleRequested;
            input.ThirdPersonExitRequested += OnExitRequested;
            input.ThirdPersonAttackRequested += OnAttackRequested;
            control.ControlChanged += ApplyControlState;
            raycaster.RegisterUiBlocker(this);
            input.SetPlayerControlMode(PlayerControlMode.Rts);
            initialized = true;
        }

        public DirectControlEligibility TryEnterSelected()
        {
            if (selection.SelectedCharacterIds.Count != 1)
            {
                LastEntryFailure = DirectControlEligibilityFailure.NotPlayerControlledRole;
                LastEntryMessage = RussianTextCatalog.Get(selection.SelectedCharacterIds.Count == 0
                    ? "debug.tps.select_one" : "debug.tps.multiple");
                return new DirectControlEligibility(LastEntryFailure);
            }
            return TryEnter(selection.SelectedCharacterIds[0]);
        }

        public DirectControlEligibility TryEnter(StableEntityId characterId)
        {
            if (buildingPlacement.IsActive) buildingPlacement.CancelPlacement();
            var result = control.TryEnterThirdPerson(characterId);
            LastEntryFailure = result.Failure;
            LastEntryMessage = result.IsEligible ? string.Empty : EligibilityReason(result.Failure);
            return result;
        }

        public void Exit() => control.ExitThirdPerson();

        public void Tick(float unscaledDeltaTime)
        {
            if (!initialized || unscaledDeltaTime < 0f) return;
            control.RefreshValidity();
            if (control.State.Mode != PlayerControlMode.ThirdPerson || !activeCharacterId.HasValue ||
                directMovement == null) return;
            var look = input.ThirdPersonLook;
            yaw += look.x * 0.08f;
            pitch = Mathf.Clamp(pitch - look.y * 0.06f, -35f, 65f);
            cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
            var local = input.ThirdPersonMove;
            var desired = Quaternion.Euler(0f, yaw, 0f) * new Vector3(local.x, 0f, local.y);
            var capability = capabilities.GetCapabilities(activeCharacterId.Value);
            directMovement.MoveDirect(desired, unscaledDeltaTime, input.ThirdPersonRun,
                runtime.Clock.IsPaused, (int)runtime.Clock.Speed, capability.MovementPerThousand);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = overlayRect;
            return initialized && ShowLegacyOverlay;
        }


        public void SynchronizePresentation() => ApplyControlState();

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void OnToggleRequested()
        {
            if (control.State.Mode == PlayerControlMode.ThirdPerson) Exit();
            else TryEnterSelected();
        }

        private void OnExitRequested()
        {
            if (control.State.Mode == PlayerControlMode.ThirdPerson) Exit();
        }

        public ManualCombatAttackStatus TryAttack(StableEntityId targetId)
        {
            if (control.State.Mode != PlayerControlMode.ThirdPerson || !activeCharacterId.HasValue ||
                runtime.Clock.IsPaused)
                return LastAttackStatus = ManualCombatAttackStatus.Invalid;
            return LastAttackStatus = combat.TryManualAttack(activeCharacterId.Value, targetId,
                runtime.Clock.State.CalendarTicks, runtime.Clock.State.BiologicalTicks);
        }

        private void OnAttackRequested()
        {
            if (control.State.Mode != PlayerControlMode.ThirdPerson || !activeCharacterId.HasValue ||
                runtime.Clock.IsPaused || raycaster.WorldCamera == null)
            { LastAttackStatus = ManualCombatAttackStatus.Invalid; return; }
            var ray = new Ray(raycaster.WorldCamera.transform.position, raycaster.WorldCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            { LastAttackStatus = ManualCombatAttackStatus.Invalid; return; }
            var presenter = hit.collider.GetComponentInParent<CharacterPresenter>();
            LastAttackStatus = presenter != null && presenter.IsBound
                ? TryAttack(presenter.CharacterId) : ManualCombatAttackStatus.Invalid;
        }

        private void ApplyControlState()
        {
            if (control.State.Mode == PlayerControlMode.ThirdPerson)
            {
                var id = control.State.ControlledCharacterId.Value;
                if (!presentations.TryGet(id, out var presenter) || presenter == null || !presenter.IsBound ||
                    !runtime.TryGetMovement(id, out directMovement))
                {
                    control.ExitThirdPerson(PlayerControlExitReason.InvalidAfterLoad);
                    return;
                }
                activeCharacterId = id;
                directMovement.BeginDirectControl();
                cameraTarget.position = presenter.transform.position + Vector3.up * 1.45f;
                yaw = presenter.transform.eulerAngles.y;
                cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
                cameraTarget.SetParent(presenter.transform, true);
                thirdPersonCamera.Follow = cameraTarget;
                thirdPersonCamera.LookAt = cameraTarget;
                thirdPersonCamera.Priority = 20;
                rtsCamera.Priority = -10;
                rtsController.enabled = false;
                input.SetPlayerControlMode(PlayerControlMode.ThirdPerson);
                SetCursor(true);
            }
            else
            {
                directMovement?.EndDirectControl();
                directMovement = null;
                activeCharacterId = null;
                cameraTarget.SetParent(transform, false);
                thirdPersonCamera.Follow = null;
                thirdPersonCamera.LookAt = null;
                thirdPersonCamera.Priority = -20;
                rtsCamera.Priority = rtsPriority;
                rtsController.enabled = true;
                input.SetPlayerControlMode(PlayerControlMode.Rts);
                SetCursor(false);
            }
        }

        private void CreateCameraRig()
        {
            var targetObject = new GameObject("TPS Camera Target");
            targetObject.transform.SetParent(transform, false);
            cameraTarget = targetObject.transform;
            var cameraObject = new GameObject("TPS Cinemachine Camera");
            cameraObject.transform.SetParent(transform, false);
            thirdPersonCamera = cameraObject.AddComponent<CinemachineCamera>();
            thirdPersonCamera.Lens.FieldOfView = 60f;
            thirdPersonCamera.Priority = -20;
            thirdPersonFollow = cameraObject.AddComponent<CinemachineThirdPersonFollow>();
            thirdPersonFollow.Damping = new Vector3(0.08f, 0.15f, 0.1f);
            thirdPersonFollow.ShoulderOffset = new Vector3(0.45f, 0.05f, 0f);
            thirdPersonFollow.VerticalArmLength = 0.25f;
            thirdPersonFollow.CameraSide = 1f;
            thirdPersonFollow.CameraDistance = 3.25f;
        }

        private void OnGUI()
        {
            if (!initialized || !ShowLegacyOverlay) return;
            GUI.Box(overlayRect, GUIContent.none);
            var mode = control.State.Mode;
            GUI.Label(new Rect(18f, overlayRect.y + 8f, 365f, 22f), RussianTextCatalog.Format("debug.tps.mode", RussianTextCatalog.Enum(mode)));
            if (mode == PlayerControlMode.ThirdPerson && activeCharacterId.HasValue &&
                presentations.TryGet(activeCharacterId.Value, out var presenter))
            {
                var capability = capabilities.GetCapabilities(activeCharacterId.Value);
                GUI.Label(new Rect(18f, overlayRect.y + 30f, 365f, 22f),
                    RussianTextCatalog.Format("debug.tps.controlled", presenter.Character.Name, activeCharacterId.Value.ToString().Substring(0, 8)));
                GUI.Label(new Rect(18f, overlayRect.y + 52f, 365f, 22f),
                    RussianTextCatalog.Format("debug.tps.state", RussianTextCatalog.Enum(presenter.Character.LifeState),
                        capability.IsIncapacitated ? RussianTextCatalog.Get("common.yes") : RussianTextCatalog.Get("common.no"), RussianTextCatalog.Enum(MovementAuthority)));
                var unit = military != null && military.TryGetForMember(activeCharacterId.Value, out var value)
                    ? RussianTextCatalog.Format("debug.tps.unit", value.Id,
                        value.CaptainId == activeCharacterId ? RussianTextCatalog.Get("common.yes") : RussianTextCatalog.Get("common.no"))
                    : RussianTextCatalog.Get("debug.tps.no_unit");
                GUI.Label(new Rect(18f, overlayRect.y + 74f, 365f, 22f), unit);
                var order = work != null && work.TryGetAssigned(activeCharacterId.Value, out var job)
                    ? RussianTextCatalog.Format("debug.tps.order", RussianTextCatalog.Enum(job.Type), RussianTextCatalog.Enum(job.Status))
                    : RussianTextCatalog.Get("debug.tps.no_order");
                GUI.Label(new Rect(18f, overlayRect.y + 96f, 365f, 22f), order);
                GUI.Label(new Rect(18f, overlayRect.y + 118f, 365f, 22f), RussianTextCatalog.Get("debug.tps.help"));
            }
            else
            {
                GUI.Label(new Rect(18f, overlayRect.y + 30f, 365f, 22f), RussianTextCatalog.Get("debug.tps.select"));
                if (!string.IsNullOrEmpty(LastEntryMessage))
                    GUI.Label(new Rect(18f, overlayRect.y + 52f, 365f, 22f), LastEntryMessage);
            }
        }

        private void OnDestroy()
        {
            if (input != null)
            {
                input.ControlModeToggled -= OnToggleRequested;
                input.ThirdPersonExitRequested -= OnExitRequested;
                input.ThirdPersonAttackRequested -= OnAttackRequested;
                input.SetPlayerControlMode(PlayerControlMode.Rts);
            }
            if (control != null) control.ControlChanged -= ApplyControlState;
            raycaster?.UnregisterUiBlocker(this);
            directMovement?.EndDirectControl();
            SetCursor(false);
        }

        private static string EligibilityReason(DirectControlEligibilityFailure reason)
        {
            switch (reason)
            {
                case DirectControlEligibilityFailure.Dead: return RussianTextCatalog.Get("ui.tps.dead");
                case DirectControlEligibilityFailure.Child: return RussianTextCatalog.Get("ui.tps.child");
                case DirectControlEligibilityFailure.Incapacitated: return RussianTextCatalog.Get("ui.tps.incapacitated");
                case DirectControlEligibilityFailure.AggregateMilitaryUnit: return RussianTextCatalog.Get("ui.tps.aggregate");
                case DirectControlEligibilityFailure.NotPlayerControlledRole: return RussianTextCatalog.Get("ui.tps.role");
                default: return RussianTextCatalog.Get("ui.status.command_failed");
            }
        }

        private static void SetCursor(bool thirdPerson)
        {
            if (Application.isBatchMode) return;
            Cursor.lockState = thirdPerson ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !thirdPerson;
        }
    }
}
