using Game.Domain;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Disposable identity label for one Tier1 animal view.</summary>
    public sealed class WorldMapWildlifeView : MonoBehaviour
    {
        public StableEntityId StableId { get; private set; }
        public string SpeciesId { get; private set; }

        public void Bind(StableEntityId stableId, string speciesId)
        {
            StableId = stableId;
            SpeciesId = speciesId;
        }
    }
}
