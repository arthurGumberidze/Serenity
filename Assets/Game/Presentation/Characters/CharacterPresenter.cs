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
        private CharacterPresentationRegistry registry;
        private bool hasSpeedParameter;
        private bool hasMovingParameter;

        public bool IsBound => character != null;
        public Character Character => character;
        public StableEntityId CharacterId => character != null ? character.Id : default;
        public Animator Animator => animator;
        public Transform RightHandSocket => rightHandSocket;
        public Transform LeftHandSocket => leftHandSocket;
        public bool IsSelected { get; private set; }

        public void SetSelected(bool selected) => IsSelected = selected;

        public void Configure(Animator targetAnimator, Transform rightHand, Transform leftHand)
        {
            animator = targetAnimator;
            rightHandSocket = rightHand;
            leftHandSocket = leftHand;
            CacheAnimatorParameters();
        }

        public void Bind(Character domainCharacter, CharacterPresentationRegistry presentationRegistry)
        {
            if (domainCharacter == null) throw new ArgumentNullException(nameof(domainCharacter));
            if (presentationRegistry == null) throw new ArgumentNullException(nameof(presentationRegistry));
            if (IsBound) throw new InvalidOperationException("Character presenter is already bound.");

            presentationRegistry.Register(domainCharacter.Id, this);
            character = domainCharacter;
            registry = presentationRegistry;
            CacheAnimatorParameters();
            SetMovementSpeed(0f);
        }

        public void Unbind()
        {
            if (!IsBound) return;
            IsSelected = false;
            registry?.Unregister(character.Id, this);
            character = null;
            registry = null;
        }

        public void SetMovementSpeed(float speed)
        {
            var safeSpeed = Mathf.Max(0f, speed);
            if (animator == null) return;
            if (hasSpeedParameter) animator.SetFloat(SpeedParameter, safeSpeed);
            if (hasMovingParameter) animator.SetBool(MovingParameter, safeSpeed > 0.05f);
        }

        public void SetSimulationPaused(bool paused)
        {
            if (animator != null) animator.speed = paused ? 0f : 1f;
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            CacheAnimatorParameters();
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
    }
}
