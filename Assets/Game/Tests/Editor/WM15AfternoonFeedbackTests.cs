using System.Linq;
using Game.Domain.Buildings;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Game.Domain.World;
using Game.Infrastructure;

namespace Game.Tests.Editor
{
    public sealed class WM15AfternoonFeedbackTests
    {
        [Test] public void MountainLakeHasAValidDryFishingCampFootprint()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world,1380062136);
            var start = new TiledWorldPopulationPlanner(world,heights,1380062136).Create().StartTerritory.Center;
            var origin = new Vector3(start.X,0,start.Z);
            var host = new GameObject("Shore placement regression");
            var surface = host.AddComponent<P102WorldPlacementSurface>(); surface.Configure(heights);
            var footprint = new BuildingFootprint(8,6);
            var reasons = new System.Collections.Generic.Dictionary<string,int>(); var found=false;
            try
            {
                foreach (var point in world.MapData.Hydrology.Lakes.Single(x=>x.Id=="lake_mountain").Shoreline.Points.Where((_,i)=>i%5==0))
                {
                    var p = world.MapData.CoordinateTransform.ToUnity(point);
                    for(var radius=0;radius<=12 && !found;radius++)
                    for(var dx=-radius;dx<=radius && !found;dx++)
                    for(var dz=-radius;dz<=radius && !found;dz++)
                    {
                        if(Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dz))!=radius)continue;
                        for(var turn=0;turn<4 && !found;turn++)
                        {
                            var facing=(BuildingOrientation)turn; var rotated=footprint.Rotate(facing);
                            var anchor=new GridCoordinate(Mathf.RoundToInt(p.X-origin.x)+dx-(rotated.Width-1)/2,
                                Mathf.RoundToInt(p.Z-origin.z)+dz-(rotated.Depth-1)/2);
                            if(!ShoreBuildingPlacement.HasWaterfront(heights,anchor,footprint,facing,1,origin.x,origin.z))continue;
                            var cells=footprint.EnumerateCells(anchor,facing).ToArray();
                            surface.PrepareFootprint(cells,1,origin,true,0);
                            var result=surface.EvaluateFootprint(cells,1,origin,3,false,true);
                            if(result.CanPlace && ShoreBuildingPlacement.IsValid(heights,anchor,footprint,facing,1,origin.x,origin.z))
                            { found=true; Debug.Log("Valid mountain shore anchor "+anchor+" facing "+facing); }
                            else { reasons.TryGetValue(result.BlockedReason,out var n);reasons[result.BlockedReason]=n+1; }
                        }
                    }
                    if(found)break;
                }
                Assert.That(found,Is.True,string.Join("; ",reasons.Select(x=>x.Key+"="+x.Value)));
            }
            finally { Object.DestroyImmediate(host); }
        }
        [TestCase(20261026L)][TestCase(1220639287L)][TestCase(1380062136L)]
        public void NewWorldPoiApproachesStayDryAcrossReportedWorldKeys(long seed)
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world,seed);
            var roads = new WorldMapRoadNetworkPlanner(world,heights).Create();
            var plan = new WorldMapPoiPlanner(world,heights,roads,seed).Create();
            Assert.That(plan.Points.Count,Is.EqualTo(9));
            foreach(var point in plan.Points)
            foreach(var step in point.ApproachRoute)
                Assert.That(heights.IsWater(step.X,step.Z),Is.False,point.Id);
        }

        [Test] public void AnimatedBoarHoovesDoNotUseItsOversizedSkinBoundsAsGround()
        {
            var root = new GameObject("Boar ground regression");
            try
            {
                var prefab = Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt").Entries.Single(x=>x.SpeciesId=="boar").Prefab;
                var model = Object.Instantiate(prefab,root.transform);
                WorldMapWildlifeView.AlignVisibleGeometry(model,Vector3.zero);
                var view = root.AddComponent<WorldMapWildlifeView>(); view.Bind(Game.Domain.StableEntityId.NewId(),"boar");
                view.ConfigureGrounding(model.transform);
                model.transform.position += Vector3.up*2;
                view.GroundAnimatedFeet(0);
                var feet = model.GetComponentsInChildren<Transform>().Where(x=>x.name.StartsWith("foot.") || x.name.StartsWith("front_foot."));
                Assert.That(feet.Min(x=>x.position.y),Is.EqualTo(.025f).Within(.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test] public void PierFootDatumIsItsBoardSurfaceRatherThanThePosts()
        {
            var prefab = Resources.Load<WorldPropCatalog>("World/WorldProps").Find("pier");
            var view = Object.Instantiate(prefab);
            try
            {
                var heights = new System.Collections.Generic.List<float>();
                foreach (var filter in view.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh;
                    for (var x = -.4f; x <= .4f; x += .4f)
                    for (var z = -1f; z <= 1f; z += 1f)
                        if (collider.Raycast(new Ray(new Vector3(x,5,z),Vector3.down),out var hit,10)) heights.Add(hit.point.y);
                }
                Assert.That(heights.Count,Is.GreaterThan(3)); heights.Sort();
                Assert.That(heights[heights.Count/2],Is.EqualTo(0).Within(.06f));
            }
            finally { Object.DestroyImmediate(view); }
        }
        [Test] public void ImportedWorkStatesUseHumanoidClips()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Game/Art/Animation/CharacterTier1.controller");
            foreach(var name in new[] { "Fish", "Mine", "Gather", "Build" })
            {
                var clip = controller.layers[0].stateMachine.states.Single(x=>x.state.name==name).state.motion as AnimationClip;
                Assert.That(clip,Is.Not.Null,name);
                Assert.That(clip.isHumanMotion,Is.True,name);
                Assert.That(AssetDatabase.GetAssetPath(clip),Does.Contain("WM15Afternoon/Imported/HumanCrafting"));
            }
        }
        [Test] public void EveryWildlifeSpeciesHasWalkAndIdleRatherThanAnIdleOnlyController()
        {
            foreach(var entry in Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt").Entries)
            {
                var animator = entry.Prefab.GetComponentInChildren<Animator>(true);
                Assert.That(animator,Is.Not.Null,entry.SpeciesId);
                var controller = animator.runtimeAnimatorController as AnimatorController;
                Assert.That(controller,Is.Not.Null,entry.SpeciesId);
                Assert.That(controller.parameters.Any(x=>x.name=="Moving"),Is.True,entry.SpeciesId);
                Assert.That(controller.layers[0].stateMachine.states.Select(x=>x.state.name),Does.Contain("Walk"),entry.SpeciesId);
                Assert.That(controller.layers[0].stateMachine.states.Single(x=>x.state.name=="Walk").state.motion,Is.Not.Null,entry.SpeciesId);
            }
        }
        [Test] public void LiveFishHasBonesAndSwimWhileResourcePilesRemainStatic()
        {
            var catalog = Resources.Load<WorldPropCatalog>("World/WorldProps");
            var fish = catalog.Find("fish_swimming");
            Assert.That(fish,Is.Not.Null);
            Assert.That(fish.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length,Is.GreaterThan(1));
            Assert.That(fish.GetComponentInChildren<Animator>().runtimeAnimatorController.animationClips.Single().length,Is.GreaterThan(.3f));
            Assert.That(catalog.Find("pile_fish").GetComponentInChildren<Animator>(),Is.Null);
        }
        [Test] public void OreAndHideNoLongerAliasStoneOrSacks()
        {
            var catalog = Resources.Load<WorldPropCatalog>("World/WorldProps");
            foreach(var key in new[] { "pile_iron_ore", "pile_copper_ore", "pile_gold_ore", "pile_hide", "pile_cured_hide" })
            {
                Assert.That(catalog.Find(key),Is.Not.SameAs(catalog.Find("stone")),key);
                Assert.That(catalog.Find(key),Is.Not.SameAs(catalog.Find("sack")),key);
            }
        }
        [Test] public void BarracksNoLongerSharesStarterShelterAndBuildingsHaveHumanScale()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>("Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            Assert.That(catalog.ResolvePrefab(new BuildingDefinitionId("military_barracks")),
                Is.Not.SameAs(catalog.ResolvePrefab(new BuildingDefinitionId("primitive_shelter"))));
            foreach(var id in new[] { "stone_age_hut", "healer_hut", "butcher_station", "military_barracks" })
            {
                var instance = Object.Instantiate(catalog.ResolvePrefab(new BuildingDefinitionId(id)));
                try
                {
                    var final = instance.GetComponentsInChildren<BuildingConstructionStageVisual>(true).OrderByDescending(x=>x.RequiredStage).First();
                    var meshes = final.GetComponentsInChildren<MeshRenderer>(true);
                    var bounds = meshes[0].bounds; foreach(var mesh in meshes.Skip(1))bounds.Encapsulate(mesh.bounds);
                    Assert.That(Mathf.Max(bounds.size.x,bounds.size.z),Is.GreaterThan(id=="butcher_station"?2f:id=="military_barracks"?2.7f:3f),id);
                    Assert.That(bounds.min.y,Is.EqualTo(0).Within(.05f),id);
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }
    }
}
