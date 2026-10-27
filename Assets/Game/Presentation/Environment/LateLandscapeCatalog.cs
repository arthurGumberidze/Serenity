using UnityEngine;
namespace Game.Presentation.Environment
{
    public sealed class LateLandscapeCatalog : ScriptableObject
    {
        public GameObject SnowTree;
        public GameObject[] SnowRocks, Flowers, MarshPlants;
        public GameObject IceFloe, FallingLeaves, Fireflies;
        public Texture2D Grass, GrassNormal, ForestFloor, ForestNormal, Pebbles, PebblesNormal;
        public Texture2D WaterWaves;
        public Texture2D SnowAlbedo, SnowNormal, SnowHeight;
    }
}
