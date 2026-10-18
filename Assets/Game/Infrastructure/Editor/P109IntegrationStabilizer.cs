using System;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure.Editor
{
    /// <summary>
    /// One-shot P10.9 migration for presentation data authored before the connected world grew to 512 m.
    /// Runtime authorities and stable IDs are deliberately untouched.
    /// </summary>
    public static class P109IntegrationStabilizer
    {
        private const string ScenePath = "Assets/Scenes/LocalGameplay.unity";

        [MenuItem("Serenity/P10.9/Stabilize Connected World Presentation")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var world = UnityEngine.Object.FindFirstObjectByType<P102ConnectedWorldPresentation>();
            var biome = UnityEngine.Object.FindFirstObjectByType<P93PopulatedBiomeWorld>();
            if (world == null || world.Terrain == null || biome == null)
                throw new InvalidOperationException("P10.2/P9.3 connected world presentation is required.");

            StabilizeRoutes(world.Terrain);
            RestoreStarterResourceWalkability(biome, world.Terrain);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("P109_INTEGRATION_STABILIZED routes=13; width=0.75; sampleStep<=3m; starterSources=4");
        }

        private static void StabilizeRoutes(Terrain terrain)
        {
            var routes = UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                .Where(x => x.name.StartsWith("P10.2 Physical Route ", StringComparison.Ordinal))
                .OrderBy(x => x.name).ToArray();
            if (routes.Length != 13) throw new InvalidOperationException("Expected exactly 13 connected-world routes.");

            foreach (var line in routes)
            {
                if (line.positionCount < 2) throw new InvalidOperationException(line.name + " has no route span.");
                var a = line.GetPosition(0);
                var b = line.GetPosition(line.positionCount - 1);
                var flat = Vector3.ProjectOnPlane(b - a, Vector3.up);
                var distance = flat.magnitude;
                if (distance <= 8f) throw new InvalidOperationException(line.name + " is too short to stabilize.");
                var direction = flat / distance;
                var trim = Mathf.Min(3.5f, distance * 0.08f);
                a += direction * trim;
                b -= direction * trim;
                var samples = Mathf.Max(3, Mathf.CeilToInt(Vector3.Distance(a, b) / 3f) + 1);

                line.widthMultiplier = 0.75f;
                line.numCornerVertices = 3;
                line.numCapVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.textureMode = LineTextureMode.Tile;
                line.positionCount = samples;
                for (var index = 0; index < samples; index++)
                {
                    var point = Vector3.Lerp(a, b, index / (samples - 1f));
                    point.y = terrain.SampleHeight(point) + terrain.transform.position.y + 0.045f;
                    line.SetPosition(index, point);
                }
                EditorUtility.SetDirty(line);
            }
        }

        private static void RestoreStarterResourceWalkability(P93PopulatedBiomeWorld biome, Terrain terrain)
        {
            var points = new[]
            {
                new Vector2(-12f, 5f), new Vector2(-8f, 7f),
                new Vector2(-4f, 9f), new Vector2(1f, 9f)
            };
            var formations = biome.ResourceFormations.OrderBy(x => x.StableIndex).ToArray();
            if (formations.Length < points.Length || !formations.Take(points.Length)
                    .Select(x => x.StableIndex).SequenceEqual(Enumerable.Range(0, points.Length)))
                throw new InvalidOperationException("Starter resource indices 0-3 are required.");

            for (var index = 0; index < points.Length; index++)
            {
                var point = new Vector3(points[index].x, 0f, points[index].y);
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y + 0.05f;
                formations[index].ClickableRoot.position = point;
                EditorUtility.SetDirty(formations[index].ClickableRoot);
            }
            EditorUtility.SetDirty(biome);
        }
    }
}
