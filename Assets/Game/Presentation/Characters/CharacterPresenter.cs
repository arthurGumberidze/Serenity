using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Work;
using Game.Presentation.Interaction;
using UnityEngine;

namespace Game.Presentation.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed partial class CharacterPresenter : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MovingParameter = Animator.StringToHash("Moving");
        private static readonly int ActionIntentParameter = Animator.StringToHash("ActionIntent");

        [SerializeField] private Animator animator;
        [SerializeField] private Transform rightHandSocket;
        [SerializeField] private Transform leftHandSocket;

        private Character character;
        private AppearanceState appearance;
        private CharacterPresentationRegistry registry;
        private bool hasSpeedParameter;
        private bool hasMovingParameter;
        private bool hasActionIntentParameter;
        private bool hasAuthoredWork;
        private float displayedMovementSpeed = float.NaN;
        private bool? displayedMoving;
        private bool? displayedPaused;
        private bool manualAnimationInitialized;
        private bool automaticMovementAnimation;
        private bool automaticAnimationAllowed = true;
        private float disableAutomaticAnimationAfter = float.PositiveInfinity;
        private SkinnedMeshRenderer[] detailedRenderers = Array.Empty<SkinnedMeshRenderer>();
        private bool[] detailedRendererDefaults = Array.Empty<bool>();
        private Renderer[] activityRenderers = Array.Empty<Renderer>();
        private bool[] activityRendererDefaults = Array.Empty<bool>();
        private GameObject detailedVisualRoot;
        private Material lightweightMaterial;
        private bool lightweightVisual;
        private CharacterAnimationIntent animationIntent;
        private Transform spineBone;
        private Transform rightUpperArmBone;
        private Transform leftUpperArmBone;
        private Transform rightLowerArmBone;
        private Transform leftLowerArmBone;
        private Quaternion lastSpineOffset = Quaternion.identity;
        private Quaternion lastRightUpperArmOffset = Quaternion.identity;
        private Quaternion lastLeftUpperArmOffset = Quaternion.identity;
        private Quaternion lastRightLowerArmOffset = Quaternion.identity;
        private Quaternion lastLeftLowerArmOffset = Quaternion.identity;
        private float locomotionPlaybackRate = 1f;
        private GameObject workAxe, workPickaxe, workHammer, huntingBow;
        private GameObject axePrefab, pickaxePrefab, hammerPrefab, bowPrefab;
        private bool activityHidden;
        private bool preserveAuthoredVisualAtDistance;

        public void PreserveAuthoredVisualAtDistance()
        {
            preserveAuthoredVisualAtDistance = true;
            ApplyVisualDetail(automaticAnimationAllowed || IsSelected);
        }

        public void ConfigureWorkTool(GameObject prefab)
        {
            ConfigureWorkTools(prefab, null, null);
        }

        public void ConfigureWorkTools(GameObject axe, GameObject pickaxe, GameObject hammer, GameObject bow = null)
        {
            axePrefab = axe; pickaxePrefab = pickaxe; hammerPrefab = hammer;
            bowPrefab = bow;
            RebindWorkTools();
        }

        private void RebindWorkTools()
        {
            foreach (var tool in new[] { workAxe, workPickaxe, workHammer, huntingBow, fishingRod })
                if (tool != null) { tool.SetActive(false); Destroy(tool); }
            workAxe = workPickaxe = workHammer = huntingBow = null;
            fishingRod = null;
            if (animator == null || !animator.isHuman) return;
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return;
            workAxe = CreateTool(axePrefab, hand, "Work axe", 0.8f);
            workPickaxe = CreateTool(pickaxePrefab, hand, "Work pickaxe", 0.95f);
            workHammer = CreateTool(hammerPrefab, hand, "Work hammer", 0.65f);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (left != null) huntingBow = CreateTool(bowPrefab, left, "Hunting bow", 1.1f);
            CreateFishingRod(hand);
            RefreshWorkTools();
        }

        private GameObject CreateTool(GameObject prefab, Transform hand, string label, float length)
        {
            if (prefab == null) return null;
            var holder = new GameObject(label);
            // Disable before cloning: non-readable decorative meshes must never enter PhysX
            // under the non-uniformly scaled character skeleton. The body capsule owns collision.
            holder.SetActive(false);
            holder.transform.SetParent(hand, false);
            var visual = Instantiate(prefab, holder.transform, false);
            var bounds = new Bounds();
            var hasBounds = false;
            foreach (var mesh in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null) continue;
                var b = mesh.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var point = holder.transform.InverseTransformPoint(mesh.transform.TransformPoint(b.center +
                        Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                            (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                    if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (hasBounds)
            {
                var size = bounds.size;
                var axis = size.x > size.y && size.x > size.z ? Vector3.right :
                    size.z > size.y ? Vector3.forward : Vector3.up;
                float positiveWidth = 0, negativeWidth = 0;
                foreach (var mesh in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mesh.sharedMesh == null || !mesh.sharedMesh.isReadable) continue;
                    foreach (var vertex in mesh.sharedMesh.vertices)
                    {
                        var local = holder.transform.InverseTransformPoint(mesh.transform.TransformPoint(vertex)) - bounds.center;
                        var along = Vector3.Dot(local,axis);
                        var width = (local-axis*along).sqrMagnitude;
                        if (along > 0) positiveWidth = Mathf.Max(positiveWidth,width);
                        else negativeWidth = Mathf.Max(negativeWidth,width);
                    }
                }
                if (label != "Hunting bow" && negativeWidth > positiveWidth * 1.15f) axis = -axis;
                var turn = Quaternion.FromToRotation(axis, Vector3.up);
                var scale = length / Mathf.Max(0.01f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
                visual.transform.localRotation = turn * visual.transform.localRotation;
                visual.transform.localScale *= scale;
                if(label=="Hunting bow") visual.transform.localRotation=Quaternion.Euler(0,180,0)*visual.transform.localRotation;
                visual.transform.localPosition = turn * (visual.transform.localPosition - bounds.center) * scale +
                    Vector3.up * length * (label == "Hunting bow" ? 0f : 0.25f);
            }
            holder.transform.localPosition = new Vector3(0.01f, 0.03f, 0.02f);
            holder.transform.localRotation = Quaternion.Euler(15f, 8f, 92f);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            holder.SetActive(false);
            return holder;
        }

        private void RefreshWorkTools()
        {
            if (workAxe != null) workAxe.SetActive(!activityHidden && animationIntent == CharacterAnimationIntent.Chop);
            if (workPickaxe != null) workPickaxe.SetActive(!activityHidden && animationIntent == CharacterAnimationIntent.Mine);
            if (workHammer != null) workHammer.SetActive(!activityHidden && animationIntent == CharacterAnimationIntent.Build);
            if (huntingBow != null) huntingBow.SetActive(!activityHidden && animationIntent == CharacterAnimationIntent.Hunt);
            if (fishingRod != null) fishingRod.SetActive(!activityHidden && animationIntent == CharacterAnimationIntent.Fish);
        }

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
        public CharacterAnimationIntent AnimationIntent => animationIntent;
        public bool IsActivityHidden => activityHidden;
        public CharacterLodStyle LodStyle => CharacterLodStyle.From(appearance);

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            var hitProxy = GetComponent<StableWorldHitProxy>();
            if (hitProxy != null) hitProxy.SetSelected(selected);
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
            if (animator != null) animator.applyRootMotion = false;
            rightHandSocket = rightHand;
            leftHandSocket = leftHand;
            CacheAnimatorParameters();
            InvalidateAnimatorState();
        }

        public void SetVisualAnimator(Animator targetAnimator)
        {
            if (targetAnimator == null) throw new ArgumentNullException(nameof(targetAnimator));
            animator = targetAnimator;
            // Navigation owns the root for UMA as well as role prefabs. Imported clips
            // must never move a visual away from its collider/selection identity.
            animator.applyRootMotion = false;
            rightHandSocket = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            leftHandSocket = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            RebindWorkTools();
            CacheDetailedRenderers();
            CacheAnimatorParameters();
            CacheHumanoidBones();
            InvalidateAnimatorState();
            SetMovementSpeed(0f);
            InitializeManualAnimation();
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
            var hitProxy = GetComponent<StableWorldHitProxy>() ?? gameObject.AddComponent<StableWorldHitProxy>();
            hitProxy.Bind(domainCharacter.Id, ContextTargetKind.Character);
            CacheDetailedRenderers();
            CacheAnimatorParameters();
            CacheHumanoidBones();
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
            locomotionPlaybackRate = !moving ? 1f : safeSpeed <= 1.5f
                ? Mathf.Clamp(safeSpeed / 1.15f, 0.65f, 1.6f)
                : Mathf.Clamp(safeSpeed / 3.4f, 0.75f, 12f);
            ApplyAnimatorPlaybackRate();
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
            displayedPaused = paused;
            ApplyAnimatorPlaybackRate();
        }

        public void SetActivityHidden(bool hidden)
        {
            if (activityHidden == hidden) return;
            activityHidden = hidden;
            RefreshWorkTools();
            if (hidden)
            {
                foreach (var renderer in activityRenderers)
                    if (renderer != null) renderer.enabled = false;
                if (detailedVisualRoot != null) detailedVisualRoot.SetActive(false);
                lightweightVisual = false;
            }
            else
            {
                for (var index = 0; index < activityRenderers.Length; index++)
                    if (activityRenderers[index] != null)
                        activityRenderers[index].enabled = activityRendererDefaults[index];
                ApplyVisualDetail(automaticAnimationAllowed || IsSelected);
            }
        }

        public void SetAnimationIntent(CharacterAnimationIntent intent)
        {
            if (Time.unscaledTime < manualAttackUntil) intent = CharacterAnimationIntent.Combat;
            var previousIntent = animationIntent;
            animationIntent = intent;
            RefreshWorkTools();
            if (animator == null || !hasActionIntentParameter) return;
            if (Application.isPlaying && intent != CharacterAnimationIntent.Idle &&
                (automaticAnimationAllowed || IsSelected) && DetailedVisualActive)
            {
                if (!manualAnimationInitialized) InitializeManualAnimation();
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                animator.enabled = true;
                disableAutomaticAnimationAfter = float.PositiveInfinity;
            }
            // Procedural drawing/fishing owns the arms; the generic labour clip bends the torso and
            // raises a hand as if striking, fighting those poses. Keep a neutral base stance instead.
            var baseIntent = intent == CharacterAnimationIntent.Hunt || intent == CharacterAnimationIntent.Fish && !hasAuthoredWork
                ? CharacterAnimationIntent.Idle : intent;
            animator.SetInteger(ActionIntentParameter, (int)baseIntent);
            if (Application.isPlaying && intent == CharacterAnimationIntent.Idle &&
                previousIntent != CharacterAnimationIntent.Idle && animator.enabled && !automaticMovementAnimation)
                disableAutomaticAnimationAfter = Time.unscaledTime + 0.2f;
        }

        private float manualAttackUntil;
        private Vector3 workContact;
        private float workContactUntil;
        private Quaternion lastWorkHandOffset = Quaternion.identity;
        public void SetWorkContact(Vector3 point) { workContact = point; workContactUntil = Time.unscaledTime + .3f; }

        private bool ApplyWorkContactPose(float time)
        {
            if (time > workContactUntil || (animationIntent != CharacterAnimationIntent.Chop &&
                animationIntent != CharacterAnimationIntent.Mine)) return false;
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var tool = animationIntent == CharacterAnimationIntent.Chop ? workAxe : workPickaxe;
            if (hand == null || tool == null || rightUpperArmBone == null || rightLowerArmBone == null) return false;
            if(animationIntent==CharacterAnimationIntent.Chop)
            {
                var phase=Mathf.Repeat(workPoseClock,1.45f)/1.45f;
                var raise=phase<.55f ? Mathf.SmoothStep(0,1,phase/.55f) :
                    phase<.76f ? 1-Mathf.SmoothStep(0,1,(phase-.55f)/.21f) : 0;
                var chopHead=workContact+Vector3.up*raise*.85f-transform.forward*raise*.4f+transform.right*raise*.18f;
                var chopGrip=chopHead-(transform.forward*.55f+Vector3.up*.12f);
                PoseArm(false,chopGrip);PoseArm(true,chopGrip-transform.forward*.2f);
                var chopAxis=(chopHead-hand.position).normalized;
                var desired=Quaternion.LookRotation(transform.right,chopAxis)*Quaternion.Inverse(tool.transform.localRotation);
                ApplyOffset(hand,Quaternion.Inverse(hand.rotation)*desired,ref lastWorkHandOffset);
                var contactCycle=Mathf.FloorToInt(workPoseClock/1.45f-.76f);
                if(contactCycle>lastWorkImpactCycle){WorkImpactSequence++;lastWorkImpactCycle=contactCycle;}
                return true;
            }
            var length = .95f;
            var stroke = (1f - Mathf.Cos(workPoseClock * 5.2f)) * .5f;
            var head = workContact + Vector3.up * stroke * .8f - transform.forward * stroke * .25f;
            var reach = head - rightUpperArmBone.position;
            var grip = head - reach.normalized * length * .75f;
            var shoulder = rightUpperArmBone.position;
            var upper = Vector3.Distance(shoulder,rightLowerArmBone.position);
            var lower = Vector3.Distance(rightLowerArmBone.position,hand.position);
            var toward = grip - shoulder;
            var distance = Mathf.Clamp(toward.magnitude,.05f,upper+lower-.015f);
            var axis = toward.normalized;
            var along = (upper*upper-lower*lower+distance*distance)/(2*distance);
            var side = Vector3.Cross(axis,transform.right).normalized;
            var elbow = shoulder + axis * along + side * Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            var rotation = Quaternion.FromToRotation(rightLowerArmBone.position-shoulder,elbow-shoulder) * rightUpperArmBone.rotation;
            ApplyOffset(rightUpperArmBone,Quaternion.Inverse(rightUpperArmBone.rotation)*rotation,ref lastRightUpperArmOffset);
            rotation = Quaternion.FromToRotation(hand.position-rightLowerArmBone.position,grip-rightLowerArmBone.position)*rightLowerArmBone.rotation;
            ApplyOffset(rightLowerArmBone,Quaternion.Inverse(rightLowerArmBone.rotation)*rotation,ref lastRightLowerArmOffset);
            var toolAxis = (head - hand.position).normalized;
            var fixedRoll = Quaternion.LookRotation(transform.right, toolAxis);
            rotation = fixedRoll * Quaternion.Inverse(tool.transform.localRotation);
            ApplyOffset(hand,Quaternion.Inverse(hand.rotation)*rotation,ref lastWorkHandOffset);
            var cycle = Mathf.FloorToInt(workPoseClock * 5.2f / (Mathf.PI * 2f));
            if (cycle > lastWorkImpactCycle) { WorkImpactSequence++; lastWorkImpactCycle = cycle; }
            return true;
        }

        /// <summary>Short local feedback only. Damage is exclusively resolved by CombatService.</summary>
        public void PlayManualAttackPresentation()
        {
            manualAttackUntil = Time.unscaledTime + 0.45f;
            SetAnimationIntent(CharacterAnimationIntent.Combat);
        }

        /// <summary>Centralized late-frame additive pose; callers update a bounded presenter set.</summary>
        public void RefreshActionPose(float unscaledTime)
        {
            if (animator == null || !animator.isHuman || !DetailedVisualActive) return;
            RemoveLastPoseOffsets();
            var elapsed = Mathf.Clamp(unscaledTime - previousPoseTime, 0f, .1f);
            previousPoseTime = unscaledTime;
            if (displayedPaused != true) workPoseClock += elapsed;
            if (animationIntent == CharacterAnimationIntent.Hunt)
            {
                ApplyBowDrawPose();
                return;
            }
            if (hasAuthoredWork && (animationIntent == CharacterAnimationIntent.Fish ||
                animationIntent == CharacterAnimationIntent.Mine || animationIntent == CharacterAnimationIntent.Gather ||
                animationIntent == CharacterAnimationIntent.Build))
            {
                if (animationIntent == CharacterAnimationIntent.Fish) UpdateFishingLine();
                if (animationIntent == CharacterAnimationIntent.Mine)
                {
                    var cycle = Mathf.FloorToInt(animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
                    if (cycle != lastWorkImpactCycle) { WorkImpactSequence++; lastWorkImpactCycle = cycle; }
                }
                return;
            }
            if (animationIntent == CharacterAnimationIntent.Fish) { ApplyFishingPose(); return; }
            if (ApplyWorkContactPose(unscaledTime)) return;
            if (hasActionIntentParameter) return;
            if (animationIntent == CharacterAnimationIntent.Idle) return;
            var wave = Mathf.Sin(unscaledTime * (animationIntent == CharacterAnimationIntent.Combat ? 7.5f : 5.2f));
            var lift = animationIntent == CharacterAnimationIntent.Carry ? 48f :
                animationIntent == CharacterAnimationIntent.Fish ? 34f :
                animationIntent == CharacterAnimationIntent.Combat ? 44f : 58f;
            var sweep = animationIntent == CharacterAnimationIntent.Carry ? 4f : 22f * wave;
            var opposite = animationIntent == CharacterAnimationIntent.Carry ? 0f : -sweep * 0.72f;
            var bend = animationIntent == CharacterAnimationIntent.Mine || animationIntent == CharacterAnimationIntent.Chop
                ? 8f + 5f * wave : animationIntent == CharacterAnimationIntent.Gather ? 13f : 3f;
            ApplyOffset(spineBone, Quaternion.Euler(bend, 0f, 0f), ref lastSpineOffset);
            ApplyOffset(rightUpperArmBone, Quaternion.Euler(lift + sweep, 0f, -12f), ref lastRightUpperArmOffset);
            ApplyOffset(leftUpperArmBone, Quaternion.Euler(lift + opposite, 0f, 12f), ref lastLeftUpperArmOffset);
            ApplyOffset(rightLowerArmBone, Quaternion.Euler(-35f + sweep * 0.35f, 0f, 0f), ref lastRightLowerArmOffset);
            ApplyOffset(leftLowerArmBone, Quaternion.Euler(-35f + opposite * 0.35f, 0f, 0f), ref lastLeftLowerArmOffset);
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

            // A visible resident must finish transitions and continue idle/work cycles. A fixed
            // 0.2s retirement timer could freeze Run -> Walk -> Idle halfway through the transition.
            if ((automaticAnimationAllowed || IsSelected) && DetailedVisualActive)
            {
                if (!manualAnimationInitialized) InitializeManualAnimation();
                animator.enabled = true;
                return;
            }

            if (!manualAnimationInitialized)
            {
                InitializeManualAnimation();
                return;
            }

            if (automaticMovementAnimation || animationIntent != CharacterAnimationIntent.Idle || !animator.enabled ||
                unscaledTime < disableAutomaticAnimationAfter) return;
            animator.enabled = false;
            disableAutomaticAnimationAfter = float.PositiveInfinity;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.applyRootMotion = false;
            CacheAnimatorParameters();
            CacheHumanoidBones();
            InvalidateAnimatorState();
        }

        private void OnDestroy()
        {
            if (workLineMaterial != null) Destroy(workLineMaterial);
            if (rodMaterial != null) Destroy(rodMaterial);
            Unbind();
        }

        private void CacheAnimatorParameters()
        {
            hasSpeedParameter = animator != null && animator.parameters.Any(parameter => parameter.nameHash == SpeedParameter);
            hasMovingParameter = animator != null && animator.parameters.Any(parameter => parameter.nameHash == MovingParameter);
            hasActionIntentParameter = animator != null && animator.parameters.Any(parameter => parameter.nameHash == ActionIntentParameter);
            hasAuthoredWork = animator != null && animator.parameters.Any(parameter => parameter.name == "AuthoredWork");
        }

        private void CacheHumanoidBones()
        {
            spineBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            rightUpperArmBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            leftUpperArmBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
            rightLowerArmBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
            leftLowerArmBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
            lastSpineOffset = lastRightUpperArmOffset = lastLeftUpperArmOffset =
                lastRightLowerArmOffset = lastLeftLowerArmOffset = Quaternion.identity;
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
            animationIntent = CharacterAnimationIntent.Idle;
            manualAttackUntil = 0f;
            locomotionPlaybackRate = 1f;
        }

        private void ApplyAnimatorPlaybackRate()
        {
            if (animator != null) animator.speed = displayedPaused == true ? 0f : locomotionPlaybackRate;
        }

        private void RemoveLastPoseOffsets()
        {
            if (animator != null && animator.isHuman)
            {
                RemoveOffset(animator.GetBoneTransform(HumanBodyBones.RightHand), ref lastWorkHandOffset);
                RemoveOffset(animator.GetBoneTransform(HumanBodyBones.LeftHand), ref lastLeftHandOffset);
            }
            RemoveOffset(spineBone, ref lastSpineOffset);
            RemoveOffset(rightUpperArmBone, ref lastRightUpperArmOffset);
            RemoveOffset(leftUpperArmBone, ref lastLeftUpperArmOffset);
            RemoveOffset(rightLowerArmBone, ref lastRightLowerArmOffset);
            RemoveOffset(leftLowerArmBone, ref lastLeftLowerArmOffset);
        }

        private void ApplyOffset(Transform bone, Quaternion offset, ref Quaternion previous)
        {
            if (bone == null) return;
            poseBefore[bone] = bone.localRotation;
            bone.localRotation *= offset;
            poseAfter[bone] = bone.localRotation;
            previous = offset;
        }

        private void RemoveOffset(Transform bone, ref Quaternion previous)
        {
            // Animator may already have written a fresh pose. Never subtract yesterday's additive
            // rotation from today's animation, which produced alternating wrist twists.
            if (bone != null && poseAfter.TryGetValue(bone, out var applied) &&
                Quaternion.Angle(bone.localRotation, applied) < .01f && poseBefore.TryGetValue(bone, out var original))
                bone.localRotation = original;
            if (bone != null) { poseBefore.Remove(bone); poseAfter.Remove(bone); }
            previous = Quaternion.identity;
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
            activityRenderers = GetComponentsInChildren<Renderer>(true);
            activityRendererDefaults = activityRenderers
                .Select(renderer => renderer != null && renderer.enabled).ToArray();
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
            if (activityHidden)
            {
                foreach (var renderer in detailedRenderers)
                    if (renderer != null) renderer.enabled = false;
                lightweightVisual = false;
                if (detailedVisualRoot != null && detailedVisualRoot.activeSelf) detailedVisualRoot.SetActive(false);
                return;
            }
            // The animation budget may freeze a peasant's pose, but must not replace their
            // explicitly chosen model with the old generic crowd body. Tier promotion remains bounded.
            detailed |= preserveAuthoredVisualAtDistance;
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
