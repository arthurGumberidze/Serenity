using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Production;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Tests.Editor
{
    public sealed class WM15CurrentPresentationTests
    {
        [Test] public void ReadyCropHoverShowsGrowthInsteadOfNaturalSourceZeroOverZero()
        {
            var site=ProductionSite.CreateCropPlot(StableEntityId.NewId(),new ProductionRecipeId("cultivate_berries"),default,TimeSpan.FromDays(2));
            Assert.That(P104ResourceHoverLabel.CropGrowthLabel(site),Does.Contain("0%"));
            site.CompleteWork(ProductionTaskKind.Plant);
            site.AdvanceGrowth(TimeSpan.FromDays(2).Ticks);
            Assert.That(P104ResourceHoverLabel.CropGrowthLabel(site),Does.Contain("100%"));
            Assert.That(P104ResourceHoverLabel.CropGrowthLabel(site),Does.Not.Contain("0/0"));
        }

        [Test] public void BoarAnatomyFacesTravelDirectionAfterRigAdapter()
        {
            var host=new GameObject("Boar axis check");
            try
            {
                var prefab=Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt").For("boar").Prefab;
                var model=Object.Instantiate(prefab,host.transform);
                var adapter=host.AddComponent<WorldMapWildlifeView>();
                adapter.Bind(StableEntityId.NewId(),"boar");adapter.ConfigureGrounding(model.transform);
                var bones=model.GetComponentsInChildren<Transform>();
                var front=(bones.Single(x=>x.name=="front_foot.L").position+bones.Single(x=>x.name=="front_foot.R").position)*.5f;
                var back=(bones.Single(x=>x.name=="foot.L").position+bones.Single(x=>x.name=="foot.R").position)*.5f;
                var direction=front-back;direction.y=0;
                Assert.That(Vector3.Dot(direction.normalized,host.transform.forward),Is.GreaterThan(.8f));
            }
            finally {Object.DestroyImmediate(host);}
        }

        [Test] public void CurrentLivestockAndWinterHavePreparedOwnedAssets()
        {
            var art=Resources.Load<CropArtCatalog>("World/CropArt");
            foreach(var species in new[]{"sheep","cow","pig","goat","duck","chicken","horse"})
            {
                var animal=art.Animal(species,false);Assert.That(animal,Is.Not.Null,species);
                var animator=animal.GetComponentInChildren<Animator>();
                Assert.That(animator,Is.Not.Null,species);
                Assert.That(animator.runtimeAnimatorController,Is.Not.Null,species);
            }
            Assert.That(art.SheepBaby,Is.Not.Null);Assert.That(art.GoatBaby,Is.Not.Null);Assert.That(art.DuckBaby,Is.Not.Null);
            var winter=Resources.Load<LateLandscapeCatalog>("World/LateLandscape");
            Assert.That(winter.SnowAlbedo,Is.Not.Null);Assert.That(winter.SnowNormal,Is.Not.Null);Assert.That(winter.SnowHeight,Is.Not.Null);
            Assert.That(winter.SnowRocks.Length,Is.GreaterThanOrEqualTo(2));
        }
    }
}
