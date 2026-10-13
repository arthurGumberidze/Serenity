using UnityEngine;

namespace Game.Infrastructure.Editor
{
    public static class P5RegionSceneBuilder
    {
        public static void Build()
        {
            P3ReferenceSceneBuilder.Build();
            Debug.Log("P5 region scene generation completed.");
        }
    }
}
