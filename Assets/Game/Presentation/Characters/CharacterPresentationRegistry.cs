using System;
using System.Collections.Generic;
using Game.Domain;

namespace Game.Presentation.Characters
{
    /// <summary>Session-owned index of active Tier 1 views. It never owns Character domain state.</summary>
    public sealed class CharacterPresentationRegistry
    {
        private readonly Dictionary<StableEntityId, CharacterPresenter> presenters =
            new Dictionary<StableEntityId, CharacterPresenter>();

        public int Count => presenters.Count;

        public void Register(StableEntityId characterId, CharacterPresenter presenter)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character ID must be valid.", nameof(characterId));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (!presenters.TryAdd(characterId, presenter))
                throw new InvalidOperationException("A Tier 1 presentation is already active for character " + characterId + ".");
        }

        public void Unregister(StableEntityId characterId, CharacterPresenter presenter)
        {
            if (!characterId.IsValid || ReferenceEquals(presenter, null)) return;
            if (presenters.TryGetValue(characterId, out var active) && ReferenceEquals(active, presenter))
                presenters.Remove(characterId);
        }

        public bool TryGet(StableEntityId characterId, out CharacterPresenter presenter)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character ID must be valid.", nameof(characterId));
            return presenters.TryGetValue(characterId, out presenter);
        }
    }
}
