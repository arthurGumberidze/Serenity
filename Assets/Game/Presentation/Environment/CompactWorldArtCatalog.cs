using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Reviewed disposable art only; no resource quantities, identity or save state.</summary>
    public sealed class CompactWorldArtCatalog : ScriptableObject
    {
        public GameObject HuntingDeer;
        public GameObject StoneOutcrop;
        public GameObject FieldFence;
        public Texture FishPattern;
    }
}
