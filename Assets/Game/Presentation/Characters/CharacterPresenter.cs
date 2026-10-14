using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using UnityEngine;

namespace Game.Presentation.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class CharacterPresenter : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MovingParameter = Animator.StringToHash("Moving");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform rightHandSocket;
        [SerializeField] private Transform leftHandSocket;

        private Character character;
        private AppearanceState appearance;
        private CharacterPresentationRegistry registry;
        private bool hasSpeedParameter;
        private bool hasMovingParameter;
        private float displayedMovementSpeed = float.NaN;
        private bool? displayedMoving;
        private bool? displayedPaused;
        private bool manualAnimationInitialized;
        private bool automaticMovementAnimation;
        private bool automaticAnimationAllowed = true;
        private float disableAutomaticAnimationAfter = float.PositiveInfinity;
        private SkinnedMeshRenderer[] detailedRenderers = Array.Empty<SkinnedMeshRenderer>();
        private bool[] detailedRendererDefaults = Array.Empty<bool>();
        private GameObject detailedVisualRoot;
        private Material lightweightMaterial;
        private bool lightweightVisual;

        public bool IsBound => character != null;
        public Character Character => character;
        public AppearanceState Appearance => appearance;
        public StableEntityId CharacterId => character != null ? character.Id : default;
        public Animator Animator => animator;
        public Transform RightHandSocket => rightHandSocket;
        public Transform LeftHandSocket => leftHandSocket;
        public bool IsSelected { get; private set; }
        public bool UsesLightweightVisual => lightweightVisual;
        public Material LightweightMaterial => lightweightMaterial;
        public bool DetailedVisualActive => detailedVisualRoot == null || detailedVisualRoot.activeSelf;

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            ApplyVisualDetail(automaticAnimationAllowed || selected);
            if (animator == null || !Application.isPlaying || displayedMoving != true) return;
            if (selected) EnableAutomaticMovementAnimation();
            else if (!automaticAnimationAllowed) DisableAutomaticMovementAnimation();
        }

        public void SetAutomaticAnimationAllowed(bool allowed)
        {
            if (automaticAnimationAllowed == allowed) return;
            automaticAnimationAllowed = allowed;
            ApplyVisualDetail(allowed || IsSelected);
            if (animator == null || !Application.isPlaying) return;
            if (allowed && displayedMoving == true) EnableAutomaticMovementAnimation();
            else if (!allowed && !IsSelected) DisableAutomaticMovementAnimation();
        }

        public void Configure(Animator targetAnimator, Transform rightHand, Transform leftHand)
        {
            animator = targetAnimator;
            rightHandSocket = rightHand;
            leftHandSocket = leftHand;
            CacheAnimatorParameters();
            InvalidateAnimatorState();
        }

        public void Bind(Character domainCharacter, CharacterPresentationRegistry presentationRegistry,
            AppearanceState resolvedAppearance = null)
        {
            if (domainCharacter == null) throw new ArgumentNullException(nameof(domainCharacter));
            if (presentationRegistry == null) throw new ArgumentNullException(nameof(presentationRegistry));
            if (IsBound) throw new InvalidOperationException("Character presenter is already bound.");

            presentationRegistry.Register(domainCharacter.Id, this);
            character = domainCharacter;
            appearance = resolvedAppearance ?? StoneAgeAppearanceProfiles.CreateCatalog()
                .DefaultFor(domainCharacter.Sex).CreateState(domainCharacter.Id);
            registry = presentationRegistry;
            CacheDetailedRenderers();
            CacheAnimatorParameters();
            InvalidateAnimatorState();
            SetMovementSpeed(0f);
            InitializeManualAnimation();
        }

        public void Unbind()
        {
            if (!IsBound) return;
            IsSelected = false;
            registry?.Unregister(character.Id, this);
            character = null;
            appearance = null;
            registry = null;
        }

        public void SetMovementSpeed(float speed)
        {
            var safeSpeed = Mathf.Max(0f, speed);
            if (animator == null) return;
            if (hasSpeedParameter && (float.IsNaN(displayedMovementSpeed) ||
                Mathf.Abs(displayedMovementSpeed - safeSpeed) > 0.01f))
            {
                animator.SetFloat(SpeedParameter, safeSpeed);
                displayedMovementSpeed = safeSpeed;
            }
            var moving = safeSpeed > 0.05f;
            if (hasMovingParameter && displayedMoving != moving)
            {
                animator.SetBool(MovingParameter, moving);
                displayedMoving = moving;
            }
            if (!Application.isPlaying) return;
            if (moving && (automaticAnimationAllowed || IsSelected))
                EnableAutomaticMovementAnimation();
            else if (moving)
                DisableAutomaticMovementAnimation();
            else if (automaticMovementAnimation)
                BeginAutomaticMovementAnimationStop();
        }

        public void SetSimulationPaused(bool paused)
        {
            if (animator == null || displayedPaused == paused) return;
            animator.speed = paused ? 0f : 1f;
            displayedPaused = paused;
        }

        /// <summary>
        /// Retires an Animator after its movement-to-idle transition. Moving presenters remain in Unity's normal
        /// Animator loop so locomotion is smooth; idle presenters keep their last evaluated pose at zero frame cost.
        /// </summary>
        public void RefreshAnimation(float unscaledTime)
        {
            if (animator == null || !Application.isPlaying) return;
            if (float.IsNaN(unscaledTime) || float.IsInfinity(unscaledTime) || unscaledTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(unscaledTime));

            if (!manualAnimationInitialized)
            {
                InitializeManualAnimation();
                return;
            }

            if (automaticMovementAnimation || !animator.enabled ||
                unscaledTime < disableAutomaticAnimationAfter) return;
            animator.enabled = false;
            disableAutomaticAnimationAfter = float.PositiveInfinity;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            CacheAnimatorParameters();
            InvalidateAnimatorState();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void CacheAnimatorParameters()
        {
            hasSpeedParameter = animator != null && animator.parameters.Any(parameter => parameter.nameHash == SpeedParameter);
            hasMovingParameter = animator != null && animator.parameters.Any(parameter => parameter.nameHash == MovingParameter);
        }

        private void InvalidateAnimatorState()
        {
            displayedMovementSpeed = float.NaN;
            displayedMoving = null;
            displayedPaused = null;
            manualAnimationInitialized = false;
            automaticMovementAnimation = false;
            automaticAnimationAllowed = true;
            disableAutomaticAnimationAfter = float.PositiveInfinity;
        }

        private void InitializeManualAnimation()
        {
            if (animator == null || !Application.isPlaying) return;
            // A newly instantiated off-camera Animator can be culled before Unity evaluates its default state.
            // Force one uncullable evaluation so every renderer has an Idle pose before visual evidence/rendering.
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0f);
            animator.enabled = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            manualAnimationInitialized = true;
            automaticMovementAnimation = false;
            disableAutomaticAnimationAfter = float.PositiveInfinity;
        }

        private void EnableAutomaticMovementAnimation()
        {
            if (!manualAnimationInitialized) InitializeManualAnimation();
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            animator.enabled = true;
            automaticMovementAnimation = true;
            disableAutomaticAnimationAfter = float.PositiveInfinity;
        }

        private void BeginAutomaticMovementAnimationStop()
        {
            automaticMovementAnimation = false;
            // The controller's locomotion-to-idle transition is 0.15 seconds. Let Unity evaluate that transition
            // smoothly before the shared scheduler freezes the resulting idle pose.
            disableAutomaticAnimationAfter = Time.unscaledTime + 0.2f;
        }

        private void DisableAutomaticMovementAnimation()
        {
            automaticMovementAnimation = false;
            disableAutomaticAnimationAfter = float.PositiveInfinity;
            animator.enabled = false;
        }

        private void CacheDetailedRenderers()
        {
            detailedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            detailedRendererDefaults = detailedRenderers.Select(renderer => renderer != null && renderer.enabled).ToArray();
            detailedVisualRoot = animator != null && animator.gameObject != gameObject ? animator.gameObject : null;
            lightweightMaterial = detailedRenderers.SelectMany(renderer => renderer != null
                    ? renderer.sharedMaterials
                    : Array.Empty<Material>())
                .FirstOrDefault(material => material != null);
        }

        private void ApplyVisualDetail(bool detailed)
        {
            if (!Application.isPlaying || detailedRenderers.Length == 0) return;
            if (detailed && detailedVisualRoot != null && !detailedVisualRoot.activeSelf)
                detailedVisualRoot.SetActive(true);
            for (var index = 0; index < detailedRenderers.Length; index++)
                if (detailedRenderers[index] != null)
                    detailedRenderers[index].enabled = detailed && detailedRendererDefaults[index];
            lightweightVisual = !detailed && lightweightMaterial != null;
            if (!detailed && detailedVisualRoot != null && detailedVisualRoot.activeSelf)
                detailedVisualRoot.SetActive(false);
        }
    }
}
