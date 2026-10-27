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
        public GameObject SheepBaby, Cow, Pig, Goat, GoatBaby, Duck, DuckBaby, Chicken, Horse;
        public GameObject PastureShelter;

        public GameObject Animal(string species, bool baby) => species switch
        {
            "sheep" => baby && SheepBaby != null ? SheepBaby : Sheep,
            "cow" => Cow,
            "pig" => Pig,
            "goat" => baby && GoatBaby != null ? GoatBaby : Goat,
            "duck" => baby && DuckBaby != null ? DuckBaby : Duck,
            "chicken" => Chicken,
            "horse" => Horse,
            _ => Sheep
        };

        public GameObject For(string recipe) => recipe switch
        {
            "cultivate_carrot" => Carrot,
            "cultivate_cabbage" => Cabbage,
            "cultivate_plant_fiber" => Fiber,
            _ => Berries
        };
    }
}
