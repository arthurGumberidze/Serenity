using System.IO;
using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    public sealed class SR02AssetAuditTests
    {
        [Test] public void CaptureReusedSampleDependenciesAndMeshBudgets()
        {
            const string output="Artifacts/SR02/assets";
            Directory.CreateDirectory(output);
            var world=TiledWorldDefinitionLoader.LoadActive();
            var sampler=new TiledWorldHeightSampler(world,1380062136);
            var plan=new WorldMapVegetationPlanner(world,sampler,1380062136).Create();
            var catalog=Resources.Load<StarterNatureCatalog>("World/StarterNature");
            catalog.RuntimeLate=Resources.Load<LateLandscapeCatalog>("World/LateLandscape");
            catalog.RuntimeWorldToDesign=new Vector4(570,120,1,1);
            catalog.RuntimeSnowRegion=new Vector4(850,840,200,190);
            var prefabs=plan.Instances.Where(v=>world.Landscape.Contains(v.Position.X,v.Position.Z))
                .Select(v=>WorldMapVegetationRenderer.Prefab(v,catalog)).Distinct().ToArray();
            Assert.That(prefabs.All(p=>p!=null),Is.True);
            var paths=prefabs.Select(AssetDatabase.GetAssetPath).Concat(catalog.WorldMapGroundLayers.Select(AssetDatabase.GetAssetPath)).ToArray();
            var dependencies=AssetDatabase.GetDependencies(paths,true).Where(File.Exists).OrderBy(p=>p).ToArray();
            File.WriteAllLines(output+"/dependency-paths.txt",dependencies);
            File.WriteAllLines(output+"/meshes.tsv",new[]{"prefab\tmesh\tvertices\ttriangles\tlocalBounds"}.Concat(prefabs.SelectMany(p=>
                p.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh!=null).Select(m=>
                    AssetDatabase.GetAssetPath(p)+"\t"+m.sharedMesh.name+"\t"+m.sharedMesh.vertexCount+"\t"+
                    Enumerable.Range(0,m.sharedMesh.subMeshCount).Sum(i=>(long)m.sharedMesh.GetIndexCount(i)/3)+"\t"+m.sharedMesh.bounds))));
            File.WriteAllLines(output+"/textures.tsv",new[]{"path\twidth\theight\tformat"}.Concat(dependencies.Select(p=>new{path=p,tex=AssetDatabase.LoadAssetAtPath<Texture2D>(p)})
                .Where(x=>x.tex!=null).Select(x=>x.path+"\t"+x.tex.width+"\t"+x.tex.height+"\t"+x.tex.format)));
            File.WriteAllLines(output+"/layers.tsv",catalog.WorldMapGroundLayers.Select(p=>AssetDatabase.GetAssetPath(p)+"\t"+p.tileSize));
        }
    }
}
