using System.Linq;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P94")]
    public sealed class P94StoneAgeSettlementTests
    {
        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity",
            OpenSceneMode.Single);

        [Test]
        public void SettlementIsDeterministicAndUsesOnlyApprovedSources()
        {
            var marker = Object.FindAnyObjectByType<P94StoneAgeSettlement>();
            Assert.That(marker, Is.Not.Null);
            Assert.DoesNotThrow(marker.ValidateConfiguration);
            Assert.That(marker.SourceAsset, Does.Contain("Unity URP Terrain Sample 1.0.3"));
            Assert.That(marker.SourceAsset, Does.Contain("UMA 3"));
            Assert.That(marker.LayoutHash, Is.EqualTo(marker.ComputeLayoutHash()));
            Assert.That(marker.SettlementMaterials.Count, Is.GreaterThanOrEqualTo(7));
            Assert.That(marker.SettlementMaterials.All(x =>
                AssetDatabase.GetAssetPath(x).StartsWith("Assets/Game/Art/Materials/P94/")), Is.True);
        }

        [Test]
        public void BuildingPrefabsAreDistinctDetailedAndContainNoPrimitiveMeshes()
        {
            var marker = Object.FindAnyObjectByType<P94StoneAgeSettlement>();
            var prefabs = new[] { marker.ShelterPrefab, marker.StoragePrefab, marker.CropPlotPrefab };
            Assert.That(prefabs.Distinct().Count(), Is.EqualTo(3));
            Assert.That(prefabs.Select(x => AssetDatabase.GetAssetPath(x)).Distinct().Count(), Is.EqualTo(3));
            Assert.That(marker.ShelterPrefab.GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThanOrEqualTo(18));
            Assert.That(marker.StoragePrefab.GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThanOrEqualTo(14));
            Assert.That(marker.CropPlotPrefab.GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThanOrEqualTo(12));
            foreach (var prefab in prefabs)
            {
                Assert.That(prefab.GetComponent<BuildingPresenter>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true).All(x =>
                    x.sharedMesh != null && x.sharedMesh.name.StartsWith("P94")), Is.True, prefab.name);
            }
        }

        [Test]
        public void WorksitesSuppliesAndCharacterWearRemainReadableAndInteractive()
        {
            var marker = Object.FindAnyObjectByType<P94StoneAgeSettlement>();
            Assert.That(marker.Worksites.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(marker.Worksites.Select(x => x.Kind).Distinct().Count(), Is.EqualTo(6));
            Assert.That(marker.Worksites.All(x => x.VisualRoot != null &&
                x.VisualRoot.GetComponentsInChildren<Renderer>(true).Length >= 2), Is.True);
            Assert.That(marker.SupplyPresentations.Count, Is.EqualTo(4));
            Assert.That(marker.SupplyPresentations.All(x => x.GetComponentInChildren<Collider>(true) != null &&
                x.GetComponentsInChildren<Renderer>(true).Length >= 3), Is.True);
            Assert.That(marker.CharacterPrefabs, Is.Empty,
                "P9.4 must not attach static gear to the unrelated UMA/fallback skeleton boundary.");
        }
    }
}
