using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Presentation-only assets; resource identity and stock remain in ProductionService.</summary>
    public sealed class StarterNatureCatalog : ScriptableObject
    {
        public GameObject[] Trees;
        public GameObject[] ShoreProps;
        public TerrainLayer[] GroundLayers;
        public TerrainLayer[] WorldMapGroundLayers;
        public GameObject[] WorldMapTrees;
        public GameObject[] WorldMapShrubs;
        public GameObject[] WorldMapGroundCover;
        public GameObject[] WorldMapSmallProps;
        public Material WorldMapRoadMaterial;
        public GameObject WorldMapBridge;
        public GameObject WorldMapFootbridge;
        public GameObject WorldMapFordMarker;
        public GameObject WorldMapPassStep;
        public GameObject WorldMapSign;
        public GameObject WorldMapFence;
        public Texture WaterRipples;
        public float MinimumTreeHeight = 9f;
        public float MaximumTreeHeight = 13f;
    }
}
