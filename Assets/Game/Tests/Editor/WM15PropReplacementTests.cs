using System.Linq;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    public sealed class WM15PropReplacementTests
    {
        private static WorldPropCatalog Catalog => AssetDatabase.LoadAssetAtPath<WorldPropCatalog>("Assets/Game/Resources/World/WorldProps.asset");
        [Test] public void EveryCanonicalResourceHasAnImportedView()
        {
            var data=new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Game/Art/Config/ResourceCatalog.asset"));
            var resources=data.FindProperty("entries");Assert.That(Catalog,Is.Not.Null);
            for(var i=0;i<resources.arraySize;i++)
            {
                var id=resources.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
                Assert.That(Catalog.Find("pile_"+id),Is.Not.Null,id);
            }
            Assert.That(Catalog.Entries.Select(x=>x.Key).Distinct().Count(),Is.EqualTo(Catalog.Entries.Length));
        }
        [Test] public void ViewsHaveRealMeshesAndValidUrpMaterialsWithoutVendorLogicOrCollision()
        {
            Assert.That(Catalog.Find("meat").GetComponentInChildren<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap"),
                Is.Not.Null,"Preserve the original Synty atlas after pipeline conversion");
            foreach(var prefab in Catalog.Entries.Select(x=>x.Prefab).Distinct())
            {
                Assert.That(prefab,Is.Not.Null,"Every catalog entry must reference a saved prefab");
                Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty,prefab.name);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true),Is.Empty,prefab.name);
                foreach(var mesh in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    Assert.That(mesh.sharedMesh,Is.Not.Null,prefab.name);
                    Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh),Does.StartWith("Assets/"),prefab.name);
                }
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)
                {
                    Assert.That(material,Is.Not.Null,prefab.name);
                    Assert.That(material.shader,Is.Not.Null,material.name);
                    Assert.That(material.shader.name,Does.StartWith("Universal Render Pipeline/"),material.name);
                }
            }
        }
        [TestCase("rain")][TestCase("snow")][TestCase("fire")][TestCase("smoke")]
        public void ScheduledWeatherAndFireHaveRootParticleSystems(string key)
        {Assert.That(Catalog.Find(key).GetComponent<ParticleSystem>(),Is.Not.Null,key);}
        [Test] public void RodGripAndPierFootprintMatchTheirInteractionAdapters()
        {
            foreach(var key in new[]{"fishing_rod","pier"})
            {
                var instance=Object.Instantiate(Catalog.Find(key));
                try
                {
                    var renderers=instance.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;
                    foreach(var renderer in renderers.Skip(1))b.Encapsulate(renderer.bounds);
                    if(key=="fishing_rod") {Assert.That(b.min.y,Is.EqualTo(0).Within(.02));Assert.That(b.max.y,Is.EqualTo(2.1f).Within(.05));}
                    else {Assert.That(b.size.x,Is.EqualTo(2.5f).Within(.02));Assert.That(b.size.z,Is.EqualTo(9f).Within(.02));Assert.That(b.min.y,Is.LessThan(0)); /* Deck contact is checked by raycast in WM15AfternoonFeedbackTests. */}
                }
                finally{Object.DestroyImmediate(instance);}
            }
        }
    }
}
