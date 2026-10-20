using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure.Editor
{
    /// <summary>
    /// Trees are grounded at runtime and resource formations are cloned by the tile streamer.
    /// Their meshes must retain local coordinates; static batching bakes scene coordinates.
    /// Changes only the processed scene, never the authored scene or imported assets.
    /// </summary>
    public sealed class P109DynamicSceneryBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (var biome in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<P93PopulatedBiomeWorld>(true)))
            {
                var roots = biome.CanopyTrees
                    .Concat(biome.MajorRocks).Concat(biome.GroundCover)
                    .Concat(biome.BaseRegion != null ? biome.BaseRegion.Trees : new GameObject[0])
                    .Concat(biome.BaseRegion != null ? biome.BaseRegion.Rocks : new GameObject[0])
                    .Concat(biome.BaseRegion != null ? biome.BaseRegion.Foliage : new GameObject[0])
                    .Concat(biome.ResourceFormations.Select(x => x.ClickableRoot != null
                        ? x.ClickableRoot.gameObject : null))
                    .Concat(biome.ResourceFormations.SelectMany(x => x.DecorativeSatellites));
                foreach (var root in roots.Where(x => x != null).Distinct())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(child.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                        flags & ~(StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic |
                                  StaticEditorFlags.OccluderStatic));
                }
            }
        }
    }
}
