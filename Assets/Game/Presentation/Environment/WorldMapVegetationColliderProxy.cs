using Game.Domain;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Disposable collider identity for one nearby interactive tree; it owns no resource state.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldMapVegetationColliderProxy : MonoBehaviour
    {
        public StableEntityId StableId { get; private set; }

        public void Bind(StableEntityId stableId)
        {
            StableId = stableId;
            name = "WM05 tree collider " + stableId;
        }
    }
}
