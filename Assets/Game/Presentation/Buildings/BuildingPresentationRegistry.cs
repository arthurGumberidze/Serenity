using System;
using System.Collections.Generic;
using Game.Domain;

namespace Game.Presentation.Buildings
{
    public sealed class BuildingPresentationRegistry
    {
        private readonly Dictionary<StableEntityId, BuildingPresenter> presenters = new Dictionary<StableEntityId, BuildingPresenter>();

        public int Count => presenters.Count;

        internal void Register(StableEntityId id, BuildingPresenter presenter)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (!presenters.TryAdd(id, presenter)) throw new InvalidOperationException("A presentation is already bound to this building.");
        }

        internal void Unregister(StableEntityId id, BuildingPresenter presenter)
        {
            if (presenters.TryGetValue(id, out var active) && ReferenceEquals(active, presenter)) presenters.Remove(id);
        }

        public bool TryGet(StableEntityId id, out BuildingPresenter presenter) => presenters.TryGetValue(id, out presenter);
    }
}
