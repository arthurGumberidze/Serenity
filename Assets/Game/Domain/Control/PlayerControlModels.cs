using System;

namespace Game.Domain.Control
{
    public enum PlayerControlMode
    {
        Rts = 0,
        ThirdPerson = 1
    }

    /// <summary>Canonical control ownership. Presentation objects and camera transforms never enter this state.</summary>
    public sealed class PlayerControlState
    {
        public PlayerControlMode Mode { get; private set; } = PlayerControlMode.Rts;
        public StableEntityId? ControlledCharacterId { get; private set; }

        public void EnterThirdPerson(StableEntityId characterId)
        {
            if (!characterId.IsValid) throw new ArgumentException("Controlled character ID is required.", nameof(characterId));
            Mode = PlayerControlMode.ThirdPerson;
            ControlledCharacterId = characterId;
        }

        public void ReturnToRts()
        {
            Mode = PlayerControlMode.Rts;
            ControlledCharacterId = null;
        }

        public PlayerControlSaveState CaptureState() => new PlayerControlSaveState(Mode, ControlledCharacterId);

        public void Restore(PlayerControlSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Mode = state.Mode;
            ControlledCharacterId = state.ControlledCharacterId;
        }
    }

    public sealed class PlayerControlSaveState
    {
        public PlayerControlSaveState(PlayerControlMode mode, StableEntityId? controlledCharacterId)
        {
            if (!Enum.IsDefined(typeof(PlayerControlMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == PlayerControlMode.Rts && controlledCharacterId.HasValue)
                throw new ArgumentException("RTS mode cannot own a directly controlled character.", nameof(controlledCharacterId));
            if (mode == PlayerControlMode.ThirdPerson &&
                (!controlledCharacterId.HasValue || !controlledCharacterId.Value.IsValid))
                throw new ArgumentException("Third-person mode requires a valid character ID.", nameof(controlledCharacterId));
            Mode = mode;
            ControlledCharacterId = controlledCharacterId;
        }

        public PlayerControlMode Mode { get; }
        public StableEntityId? ControlledCharacterId { get; }
    }
}
