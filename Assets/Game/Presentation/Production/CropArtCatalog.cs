using UnityEngine;

namespace Game.Presentation.Production
{
    public sealed class CropArtCatalog : ScriptableObject
    {
        public GameObject Carrot;
        public GameObject Cabbage;
        public GameObject Fiber;
        public GameObject Fence;
        public GameObject Berries;
        public GameObject Sheep;
        public GameObject PastureShelter;

        public GameObject For(string recipe) => recipe switch
        {
            "cultivate_carrot" => Carrot,
            "cultivate_cabbage" => Cabbage,
            "cultivate_plant_fiber" => Fiber,
            _ => Berries
        };
    }
}
