using Game.Presentation.AI;
using Game.Presentation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P96")]
    public sealed class P96VisualIntegrationTests
    {
        [Test]
        public void DetailedUmaAndCrowdLodBudgetsStayBoundedTogether()
        {
            Assert.That(CharacterPresentationSpawner.MaximumDetailedUmaProjections,
                Is.EqualTo(Tier1AiRuntimeDriver.HighDetailCrowdBudget));
        }

        [Test]
        public void LightweightCrowdMeshIsAReadableHumanScaleSilhouette()
        {
            var mesh = Tier1AiRuntimeDriver.BuildLightweightHumanoidMesh();
            try
            {
                Assert.That(mesh.name, Does.Contain("Humanoid"));
                Assert.That(mesh.vertexCount, Is.GreaterThan(100));
                Assert.That(mesh.bounds.size.y, Is.InRange(1.65f, 1.9f));
                Assert.That(mesh.bounds.size.x, Is.InRange(0.6f, 0.8f));
                Assert.That(mesh.triangles.Length, Is.GreaterThan(300));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void CleanUmaPrefabAndStoneAgeRecipesAreImported()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Art/Prefabs/Characters/Serenity_UMA_Human30.prefab");
            Assert.That(prefab, Is.Not.Null);
            var missing = 0;
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            Assert.That(missing, Is.Zero);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(
                "Assets/Game/Art/Config/P96/F_StoneAgeHideTunic_Recipe.asset"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(
                "Assets/Game/Art/Config/P96/M_StoneAgeHideTunic_Recipe.asset"), Is.Not.Null);
        }
    }
}
