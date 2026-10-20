using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Presentation-only assets; resource identity and stock remain in ProductionService.</summary>
    public sealed class StarterNatureCatalog : ScriptableObject
    {
        public GameObject[] Trees;
        public GameObject[] ShoreProps;
        public TerrainLayer[] GroundLayers;
        public Texture WaterRipples;
        public float MinimumTreeHeight = 9f;
        public float MaximumTreeHeight = 13f;
    }
}
