using System;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    /// <summary>Presentation-only authoring marker for a material construction stage.</summary>
    public sealed class BuildingConstructionStageVisual : MonoBehaviour
    {
        [SerializeField, Min(1)] private int requiredStage = 1;
        public int RequiredStage => requiredStage;

        public void Configure(int stage)
        {
            if (stage <= 0) throw new ArgumentOutOfRangeException(nameof(stage));
            requiredStage = stage;
        }
    }
}
