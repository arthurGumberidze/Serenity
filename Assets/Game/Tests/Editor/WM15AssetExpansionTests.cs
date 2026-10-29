using System;
using System.Linq;
using System.Reflection;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Military;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    public sealed class WM15AssetExpansionTests
    {
        private const string Path="Assets/Game/Art/Config/BuildingPresentationCatalog.asset";
        [Test]
        public void AuthorAndValidateExpansion()
        {
            var type=Type.GetType("Game.Infrastructure.Editor.WM15AssetExpansionAuthoring, Game.Infrastructure.Editor",true);
            type.GetMethod("Generate",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);
            EveryBaseBuildingHasARealDistinctCompatibleSecondLevel();
            UniformsShareEnlistedSuitAndDistinguishEveryOfficerRank();
            type.GetMethod("CaptureReview",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);
        }
        [Test]
        public void EveryBaseBuildingHasARealDistinctCompatibleSecondLevel()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(Path);catalog.Validate();
            var bases=catalog.Entries.Where(e=>!e.DefinitionId.EndsWith("_level2",StringComparison.Ordinal)).ToArray();
            Assert.That(bases.Length,Is.GreaterThanOrEqualTo(32));
            foreach(var entry in bases)
            {
                if(entry.DefinitionId=="clan_house")continue; // Legacy seed content; now reached through the hut upgrade.
                var first=entry.CreateDefinition();Assert.That(first.Upgrades,Is.Not.Null,entry.DefinitionId);
                var option=first.Upgrades.Options.Single();var next=catalog.ResolveDefinition(option.TargetDefinitionId);
                Assert.That(next.Id.FamilyId,Is.EqualTo(first.Id));
                Assert.That(next.Footprint.Width,Is.EqualTo(first.Footprint.Width));
                Assert.That(next.Footprint.Depth,Is.EqualTo(first.Footprint.Depth));
                Assert.That(next.MaxIntegrity,Is.GreaterThan(first.MaxIntegrity));
                var prefab=catalog.ResolvePrefab(next.Id);
                Assert.That(prefab,Is.Not.SameAs(entry.Prefab));Assert.That(prefab.GetComponent<BuildingPresenter>(),Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled),Is.True);
                Assert.That(option.Cost.Count,Is.GreaterThan(0));Assert.That(option.WorkRequiredTicks,Is.GreaterThan(0));
                if(first.Housing!=null)Assert.That(next.Housing.ResidentCapacity,Is.GreaterThan(first.Housing.ResidentCapacity));
                if(first.Storage!=null)Assert.That(next.Storage.Capacity,Is.GreaterThan(first.Storage.Capacity));
                if(first.Production!=null)CollectionAssert.AreEquivalent(first.Production.RecipeIds,next.Production.RecipeIds);
            }
            foreach(var id in new[]{"communal_house","central_storehouse","crafting_hall"})
                Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId(id)),Is.Not.Null);
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("stone_age_hut")).Housing.ResidentCapacity,Is.EqualTo(4));
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("stone_age_hut_level2")).Housing.ResidentCapacity,Is.EqualTo(6));
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("stone_age_hut_level2")).DisplayName,Is.EqualTo("Каменный дом"));
        }
        [Test]
        public void UniformsShareEnlistedSuitAndDistinguishEveryOfficerRank()
        {
            var characters=AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>("Assets/Game/Art/Config/CharacterPresentationCatalog.asset");
            Assert.That(characters.MilitaryUniforms,Is.Not.Null);
            foreach(var sex in new[]{CharacterSex.Male,CharacterSex.Female})
            {
                var catalog=characters.MilitaryUniforms;
                Assert.That(catalog.Resolve(MilitaryRank.Private,sex),Is.SameAs(catalog.Resolve(MilitaryRank.Sergeant,sex)));
                Assert.That(catalog.Resolve(MilitaryRank.Private,sex),Is.SameAs(catalog.Resolve(MilitaryRank.WarrantOfficer,sex)));
                var officers=Enumerable.Range(4,7).Select(rank=>catalog.Resolve((MilitaryRank)rank,sex)).ToArray();
                Assert.That(officers.Distinct().Count(),Is.EqualTo(7));
                foreach(var prefab in officers)
                {
                    Assert.That(prefab,Is.Not.Null);var active=prefab.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();
                    Assert.That(active.Length,Is.GreaterThanOrEqualTo(6));
                    Assert.That(active.Any(r=>r.name.Contains("cape")),Is.True,prefab.name);
                    Assert.That(prefab.GetComponentInChildren<Animator>(true).avatar.isHuman,Is.True,prefab.name);
                    Assert.That(active.All(r=>r.sharedMaterial!=null),Is.True);
                    Assert.That(active.All(r=>r.sharedMaterial.shader.name=="Serenity/MilitaryUniform"&&
                        r.sharedMaterial.GetTexture("_Texture6")!=null),Is.True,"Uniform must retain the authored colour masks");
                }
            }
        }
        [Test]
        public void MountainLakeUsesAuthoredDepthAndStableCameraSampling()
        {
            var assembly=typeof(Game.Infrastructure.LocalSceneCompositionRoot).Assembly;
            var type=assembly.GetType("Game.Infrastructure.WorldWaterProfileMaterials",true);
            var profile=assembly.GetType("Game.Infrastructure.WorldWaterProfile",true);
            var material=(Material)type.GetMethod("Create").Invoke(null,new[]{Enum.Parse(profile,"MountainLake")});
            try
            {
                Assert.That(material.GetFloat("_UseVertexDepth"),Is.EqualTo(1));
                Assert.That(material.GetFloat("_RefractionStrength"),Is.EqualTo(1));
                Assert.That(material.GetFloat("_EdgeFoamIntensity"),Is.Zero);
                Assert.That(material.shader.isSupported,Is.True);
            }
            finally{UnityEngine.Object.DestroyImmediate(material);}
            Assert.That(Shader.Find("Serenity/DeformableSnow"),Is.Not.Null);
        }
    }
}
