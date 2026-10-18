using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure.Editor
{
    /// <summary>Creates the project-owned realistic Stone Age settlement presentation for P9.4.</summary>
    public static class P94StoneAgeSettlementBuilder
    {
        private const string ScenePath = "Assets/Scenes/LocalGameplay.unity";
        private const string RootName = "P9.4 Stone Age Settlement";
        private const string MeshFolder = "Assets/Game/Art/Meshes/P94";
        private const string MaterialFolder = "Assets/Game/Art/Materials/P94";
        private const string TextureFolder = "Assets/Game/Art/Textures/P94";
        private const string ShelterPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_PrimitiveShelter.prefab";
        private const string StoragePath = "Assets/Game/Art/Prefabs/Props/Serenity_StorageBasket.prefab";
        private const string CropPath = "Assets/Game/Art/Prefabs/Buildings/Serenity_PrimitiveCropPlot.prefab";
        private const string IndicatorPath = "Assets/Game/Art/Prefabs/Props/Serenity_P94_WorkIndicator.prefab";
        private const string MalePath = "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_A_Tier1.prefab";
        private const string FemalePath = "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_B_Tier1.prefab";

        [MenuItem("Serenity/P9.4/Build Stone Age Settlement")]
        public static void Build()
        {
            P93PopulatedBiomeWorldBuilder.Build();
            Directory.CreateDirectory(MeshFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(TextureFolder);

            var meshes = CreateMeshes();
            var materials = CreateMaterials();
            CreateShelterPrefab(meshes, materials);
            CreateStoragePrefab(meshes, materials);
            CreateCropPlotPrefab(meshes, materials);
            CreateIndicatorPrefab(meshes, materials);
            RemoveCharacterWear(MalePath);
            RemoveCharacterWear(FemalePath);
            ConfigureBuildingCatalog();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var biome = UnityEngine.Object.FindFirstObjectByType<P93PopulatedBiomeWorld>();
            var region = UnityEngine.Object.FindFirstObjectByType<P5InhabitableRegionPresentation>();
            if (biome == null || region == null)
                throw new InvalidOperationException("P9.4 requires the completed P9.3 gameplay world.");
            DestroyByName(RootName);
            var root = new GameObject(RootName);
            root.transform.SetParent(biome.BaseRegion.transform, false);
            var worksiteRoot = Child(root.transform, "Stone Age Worksites");
            var worksites = CreateWorksites(worksiteRoot, biome.BaseRegion.Terrain, meshes, materials);
            var supplies = RebuildSupplyPresentations(region, meshes, materials);
            AddSettlementGrounding(root.transform, biome.BaseRegion.Terrain, meshes, materials);

            var marker = root.AddComponent<P94StoneAgeSettlement>();
            marker.Configure(biome, Load<GameObject>(ShelterPath), Load<GameObject>(StoragePath),
                Load<GameObject>(CropPath), Load<GameObject>(IndicatorPath), worksites, supplies,
                Array.Empty<GameObject>(), materials.Values);
            marker.ValidateConfiguration();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"P94_STONE_AGE_SETTLEMENT_BUILT worksites={worksites.Count}; supplies={supplies.Length}; " +
                      $"materials={materials.Count}; hash={marker.LayoutHash}");
        }

        private sealed class MeshSet
        {
            public Mesh Log;
            public Mesh Panel;
            public Mesh Stone;
            public Mesh Torus;
            public Mesh Soil;
            public Mesh Tuft;
            public Mesh Rope;
        }

        private static MeshSet CreateMeshes() => new MeshSet
        {
            Log = WriteMesh(MeshFolder + "/P94_TaperedLog.asset", "P94 Tapered Hand-Hewn Log", CreateLogMesh()),
            Panel = WriteMesh(MeshFolder + "/P94_HidePanel.asset", "P94 Irregular Hide Panel", CreatePanelMesh()),
            Stone = WriteMesh(MeshFolder + "/P94_FieldStone.asset", "P94 Irregular Field Stone", CreateStoneMesh()),
            Torus = WriteMesh(MeshFolder + "/P94_WovenRing.asset", "P94 Woven Ring", CreateTorusMesh(16, 5)),
            Soil = WriteMesh(MeshFolder + "/P94_SoilBed.asset", "P94 Raised Soil Bed", CreateSoilMesh()),
            Tuft = WriteMesh(MeshFolder + "/P94_ReedTuft.asset", "P94 Reed And Crop Tuft", CreateTuftMesh()),
            Rope = WriteMesh(MeshFolder + "/P94_RopeCoil.asset", "P94 Rope Coil", CreateTorusMesh(20, 4))
        };

        private static Dictionary<string, Material> CreateMaterials()
        {
            var barkColor = Load<Texture2D>(
                "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_A/Textures/Bark_Color.png");
            var barkNormal = Load<Texture2D>(
                "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_A/Textures/Bark_Normal.png");
            var hideTexture = CreateDerivedTexture(TextureFolder + "/P94_Hide_Base.png", false);
            var weaveTexture = CreateDerivedTexture(TextureFolder + "/P94_WovenFiber_Base.png", true);
            return new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                ["bark"] = Material("P94_Bark", new Color(0.42f,0.28f,0.16f), .08f, .58f, barkColor, barkNormal),
                ["darkBark"] = Material("P94_DarkBark", new Color(0.25f,0.16f,0.09f), .04f, .72f, barkColor, barkNormal),
                ["hide"] = Material("P94_Hide", new Color(0.55f,0.37f,0.21f), .03f, .82f, hideTexture, null),
                ["hideLight"] = Material("P94_HideLight", new Color(0.68f,0.49f,0.29f), .02f, .88f, hideTexture, null),
                ["wicker"] = Material("P94_Wicker", new Color(0.52f,0.38f,0.18f), .02f, .78f, weaveTexture, null),
                ["stone"] = Material("P94_Stone", new Color(0.38f,0.40f,0.37f), .02f, .66f, null, null),
                ["soil"] = Material("P94_Soil", new Color(0.23f,0.15f,0.08f), 0f, .96f, null, null),
                ["bone"] = Material("P94_Bone", new Color(0.75f,0.70f,0.55f), .04f, .54f, null, null),
                ["priority"] = Material("P94_Priority", new Color(0.78f,0.16f,0.07f), .05f, .62f, hideTexture, null),
                ["green"] = Material("P94_CropGreen", new Color(0.28f,0.43f,0.16f), 0f, .9f, null, null)
            };
        }

        private static void CreateShelterPrefab(MeshSet mesh, IReadOnlyDictionary<string, Material> mat)
        {
            var root = new GameObject("Serenity_PrimitiveShelter");
            root.AddComponent<BuildingPresenter>();
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(4.2f, 3.1f, 3.4f);
            collider.center = new Vector3(0f, 1.55f, 0.12f);

            var frame = Child(root.transform, "Hand Hewn Timber Frame");
            foreach (var x in new[] { -1.75f, 1.75f })
            foreach (var z in new[] { -1.35f, 1.35f })
                AddLog(frame, mesh.Log, mat["bark"], new Vector3(x, 0f, z), new Vector3(x, 2.15f, z), .16f,
                    $"P94 Upright {x:F1} {z:F1}");
            AddLog(frame, mesh.Log, mat["darkBark"], new Vector3(0f, 2.95f, -1.55f),
                new Vector3(0f, 2.95f, 1.55f), .18f, "P94 Roof Ridge");
            for (var i = 0; i < 5; i++)
            {
                var z = -1.42f + i * .71f;
                AddLog(frame, mesh.Log, mat["bark"], new Vector3(-1.92f, 2.12f, z),
                    new Vector3(0f, 2.98f, z), .105f, "P94 Left Rafter " + (i + 1));
                AddLog(frame, mesh.Log, mat["bark"], new Vector3(0f, 2.98f, z),
                    new Vector3(1.92f, 2.12f, z), .105f, "P94 Right Rafter " + (i + 1));
            }
            AddLog(frame, mesh.Log, mat["darkBark"], new Vector3(-1.85f, .38f, 1.38f),
                new Vector3(1.85f, .38f, 1.38f), .12f, "P94 Rear Sill");
            AddLog(frame, mesh.Log, mat["darkBark"], new Vector3(-1.85f, 1.46f, 1.38f),
                new Vector3(1.85f, 1.46f, 1.38f), .10f, "P94 Rear Rail");

            var cover = Child(root.transform, "Layered Hide And Thatch Cover");
            for (var side = -1; side <= 1; side += 2)
            for (var i = 0; i < 4; i++)
            {
                var part = Part($"P94 Roof Hide {side} {i + 1}", cover, mesh.Panel,
                    i % 2 == 0 ? mat["hide"] : mat["hideLight"]);
                part.transform.localPosition = new Vector3(side * .96f, 2.53f, -1.16f + i * .78f);
                part.transform.localRotation = Quaternion.Euler(0f, 0f, side * -24f);
                part.transform.localScale = new Vector3(2.18f, .055f, .92f);
            }
            for (var i = 0; i < 3; i++)
            {
                var rear = Part("P94 Rear Hide " + (i + 1), cover, mesh.Panel, mat["hide"]);
                rear.transform.localPosition = new Vector3(-1.18f + i * 1.18f, 1.35f, 1.42f);
                rear.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                rear.transform.localScale = new Vector3(1.32f, .06f, 2.45f);
            }

            var foundation = Child(root.transform, "Stone Footing");
            for (var i = 0; i < 14; i++)
            {
                var angle = i / 14f * Mathf.PI * 2f;
                var stone = Part("P94 Footing Stone " + (i + 1), foundation, mesh.Stone, mat["stone"]);
                stone.transform.localPosition = new Vector3(Mathf.Cos(angle) * 1.86f, .12f,
                    Mathf.Sin(angle) * 1.42f);
                stone.transform.localRotation = Quaternion.Euler(0f, i * 61f, 0f);
                stone.transform.localScale = new Vector3(.46f, .24f, .34f) * (0.86f + (i % 3) * .08f);
            }
            var bed = Part("P94 Hide Sleeping Mat", root.transform, mesh.Panel, mat["hideLight"]);
            bed.transform.localPosition = new Vector3(.62f, .12f, .36f);
            bed.transform.localScale = new Vector3(1.3f, .05f, 1.75f);
            SavePrefab(root, ShelterPath);
        }

        private static void CreateStoragePrefab(MeshSet mesh, IReadOnlyDictionary<string, Material> mat)
        {
            var root = new GameObject("Serenity_StorageBasket");
            root.AddComponent<BuildingPresenter>();
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.85f, 1.45f, 1.15f);
            collider.center = new Vector3(0f, .72f, 0f);
            var rack = Child(root.transform, "Raised Dry Storage Rack");
            foreach (var x in new[] { -.72f, .72f })
            foreach (var z in new[] { -.42f, .42f })
                AddLog(rack, mesh.Log, mat["darkBark"], new Vector3(x, 0f, z), new Vector3(x, 1.22f, z), .09f,
                    $"P94 Rack Post {x:F1} {z:F1}");
            for (var i = 0; i < 5; i++)
                AddLog(rack, mesh.Log, mat["bark"], new Vector3(-.78f, .47f, -.38f + i * .19f),
                    new Vector3(.78f, .47f, -.38f + i * .19f), .055f, "P94 Rack Slat " + (i + 1));
            AddLog(rack, mesh.Log, mat["bark"], new Vector3(-.88f, 1.12f, 0f),
                new Vector3(.88f, 1.12f, 0f), .08f, "P94 Rack Ridge");
            for (var side = -1; side <= 1; side += 2)
            {
                var roof = Part("P94 Rack Hide Roof " + side, rack, mesh.Panel, mat["hide"]);
                roof.transform.localPosition = new Vector3(side * .43f, .98f, 0f);
                roof.transform.localRotation = Quaternion.Euler(0f, 0f, side * -19f);
                roof.transform.localScale = new Vector3(1.1f, .045f, 1.22f);
            }
            var basket = Child(root.transform, "Woven Storage Basket");
            basket.localPosition = new Vector3(0f, .52f, 0f);
            for (var i = 0; i < 9; i++)
            {
                var angle = i / 9f * Mathf.PI * 2f;
                AddLog(basket, mesh.Log, mat["wicker"],
                    new Vector3(Mathf.Cos(angle) * .52f, 0f, Mathf.Sin(angle) * .34f),
                    new Vector3(Mathf.Cos(angle) * .61f, .66f, Mathf.Sin(angle) * .40f), .027f,
                    "P94 Basket Stake " + (i + 1));
            }
            for (var i = 0; i < 6; i++)
            {
                var ring = Part("P94 Basket Weave " + (i + 1), basket, mesh.Torus, mat["wicker"]);
                ring.transform.localPosition = new Vector3(0f, .08f + i * .11f, 0f);
                ring.transform.localScale = new Vector3(.58f + i * .018f, .036f, .39f + i * .012f);
            }
            SavePrefab(root, StoragePath);
        }

        private static void CreateCropPlotPrefab(MeshSet mesh, IReadOnlyDictionary<string, Material> mat)
        {
            var root = new GameObject("Serenity_PrimitiveCropPlot");
            root.AddComponent<BuildingPresenter>();
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.9f, .24f, 2.9f);
            collider.center = new Vector3(0f, .1f, 0f);
            var soil = Part("P94 Worked Soil Bed", root.transform, mesh.Soil, mat["soil"]);
            soil.transform.localPosition = new Vector3(0f, .02f, 0f);
            soil.transform.localScale = new Vector3(2.75f, .18f, 2.75f);
            for (var i = 0; i < 5; i++)
            for (var j = 0; j < 4; j++)
            {
                var tuft = Part($"P94 Crop Tuft {i + 1}-{j + 1}", root.transform, mesh.Tuft, mat["green"]);
                tuft.transform.localPosition = new Vector3(-1.05f + i * .52f, .14f, -.82f + j * .55f);
                tuft.transform.localRotation = Quaternion.Euler(0f, i * 23f + j * 31f, 0f);
                tuft.transform.localScale = new Vector3(.34f, .46f + (i + j) % 3 * .05f, .34f);
            }
            foreach (var z in new[] { -1.38f, 1.38f })
                AddLog(root.transform, mesh.Log, mat["darkBark"], new Vector3(-1.4f, .16f, z),
                    new Vector3(1.4f, .16f, z), .065f, "P94 Plot Edge " + z);
            foreach (var x in new[] { -1.38f, 1.38f })
                AddLog(root.transform, mesh.Log, mat["darkBark"], new Vector3(x, .16f, -1.4f),
                    new Vector3(x, .16f, 1.4f), .065f, "P94 Plot Edge " + x);
            var basket = Part("P94 Seed Basket", root.transform, mesh.Torus, mat["wicker"]);
            basket.transform.localPosition = new Vector3(1.05f, .25f, 1.05f);
            basket.transform.localScale = new Vector3(.28f, .12f, .28f);
            SavePrefab(root, CropPath);
        }

        private static void CreateIndicatorPrefab(MeshSet mesh, IReadOnlyDictionary<string, Material> mat)
        {
            var root = new GameObject("Serenity_P94_WorkIndicator");
            var ring = Part("P94 Priority Ground Ring", root.transform, mesh.Torus, mat["priority"]);
            ring.transform.localPosition = new Vector3(0f, .035f, 0f);
            ring.transform.localScale = new Vector3(.82f, .028f, .82f);
            var post = Part("P94 Priority Stake", root.transform, mesh.Log, mat["priority"]);
            post.transform.localPosition = new Vector3(.68f, .52f, 0f);
            post.transform.localScale = new Vector3(.07f, 1.0f, .07f);
            var flag = Part("P94 Priority Hide Flag", root.transform, mesh.Panel, mat["priority"]);
            flag.transform.localPosition = new Vector3(.48f, .85f, 0f);
            flag.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            flag.transform.localScale = new Vector3(.45f, .025f, .34f);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            SavePrefab(root, IndicatorPath);
        }

        private static List<P94StoneAgeSettlement.Worksite> CreateWorksites(Transform parent, Terrain terrain,
            MeshSet mesh, IReadOnlyDictionary<string, Material> mat)
        {
            var result = new List<P94StoneAgeSettlement.Worksite>();
            result.Add(Worksite("knapping", P94StoneAgeSettlement.WorksiteKind.Knapping,
                new Vector3(-2.4f,0f,1.2f), 18f, parent, terrain, root =>
                {
                    var seat = Part("P94 Knapping Seat", root, mesh.Stone, mat["stone"]);
                    seat.transform.localPosition = new Vector3(0f,.22f,0f);
                    seat.transform.localScale = new Vector3(.72f,.32f,.62f);
                    for (var i = 0; i < 7; i++)
                    {
                        var flake = Part("P94 Stone Flake " + (i + 1), root, mesh.Stone, mat[i % 2 == 0 ? "stone" : "bone"]);
                        flake.transform.localPosition = new Vector3(-.62f + i * .18f,.08f,.42f + (i % 2) * .14f);
                        flake.transform.localScale = new Vector3(.14f,.05f,.20f);
                    }
                }));
            result.Add(Worksite("hide_rack", P94StoneAgeSettlement.WorksiteKind.HideWorking,
                new Vector3(2.2f,0f,1.1f), -22f, parent, terrain, root =>
                {
                    AddLog(root, mesh.Log, mat["bark"], new Vector3(-.8f,0f,0f), new Vector3(-.8f,1.75f,0f), .08f, "P94 Hide Rack Left");
                    AddLog(root, mesh.Log, mat["bark"], new Vector3(.8f,0f,0f), new Vector3(.8f,1.75f,0f), .08f, "P94 Hide Rack Right");
                    AddLog(root, mesh.Log, mat["darkBark"], new Vector3(-.9f,1.67f,0f), new Vector3(.9f,1.67f,0f), .075f, "P94 Hide Rack Top");
                    var hide = Part("P94 Stretched Hide", root, mesh.Panel, mat["hideLight"]);
                    hide.transform.localPosition = new Vector3(0f,.92f,0f);
                    hide.transform.localRotation = Quaternion.Euler(90f,0f,0f);
                    hide.transform.localScale = new Vector3(1.35f,.035f,1.22f);
                }));
            result.Add(Worksite("woodworking", P94StoneAgeSettlement.WorksiteKind.WoodWorking,
                new Vector3(-.7f,0f,-3.0f), 34f, parent, terrain, root =>
                {
                    var stump = Part("P94 Chopping Stump", root, mesh.Log, mat["darkBark"]);
                    stump.transform.localPosition = new Vector3(0f,.34f,0f);
                    stump.transform.localScale = new Vector3(.62f,.68f,.62f);
                    for (var i = 0; i < 5; i++)
                        AddLog(root, mesh.Log, mat["bark"], new Vector3(-.95f + i * .16f,.11f,.5f + i * .08f),
                            new Vector3(.65f + i * .12f,.11f,.5f + i * .08f), .075f, "P94 Cut Pole " + (i + 1));
                }));
            result.Add(Worksite("drying", P94StoneAgeSettlement.WorksiteKind.Drying,
                new Vector3(3.8f,0f,-2.3f), 8f, parent, terrain, root =>
                {
                    AddLog(root, mesh.Log, mat["bark"], new Vector3(-.9f,0f,0f), new Vector3(-.9f,1.65f,0f), .07f, "P94 Drying Left");
                    AddLog(root, mesh.Log, mat["bark"], new Vector3(.9f,0f,0f), new Vector3(.9f,1.65f,0f), .07f, "P94 Drying Right");
                    for (var i = 0; i < 4; i++)
                        AddLog(root, mesh.Log, mat["wicker"], new Vector3(-.84f,.55f + i * .27f,0f),
                            new Vector3(.84f,.55f + i * .27f,0f), .025f, "P94 Drying Cord " + (i + 1));
                    for (var i = 0; i < 3; i++)
                    {
                        var strip = Part("P94 Drying Strip " + (i + 1), root, mesh.Panel, mat["hide"]);
                        strip.transform.localPosition = new Vector3(-.48f + i * .48f,.78f,0f);
                        strip.transform.localRotation = Quaternion.Euler(90f,0f,0f);
                        strip.transform.localScale = new Vector3(.25f,.025f,.75f);
                    }
                }));
            result.Add(Worksite("hauling_sled", P94StoneAgeSettlement.WorksiteKind.Hauling,
                new Vector3(-5.9f,0f,-2.9f), -12f, parent, terrain, root =>
                {
                    AddLog(root, mesh.Log, mat["darkBark"], new Vector3(-.55f,.1f,-.75f), new Vector3(-.55f,.1f,.95f), .065f, "P94 Sled Runner Left");
                    AddLog(root, mesh.Log, mat["darkBark"], new Vector3(.55f,.1f,-.75f), new Vector3(.55f,.1f,.95f), .065f, "P94 Sled Runner Right");
                    for (var i = 0; i < 5; i++)
                        AddLog(root, mesh.Log, mat["bark"], new Vector3(-.65f,.2f,-.58f + i * .28f),
                            new Vector3(.65f,.2f,-.58f + i * .28f), .05f, "P94 Sled Crossbar " + (i + 1));
                    var coil = Part("P94 Sled Rope", root, mesh.Rope, mat["wicker"]);
                    coil.transform.localPosition = new Vector3(.35f,.32f,.15f);
                    coil.transform.localScale = new Vector3(.32f,.045f,.32f);
                }));
            result.Add(Worksite("construction_frame", P94StoneAgeSettlement.WorksiteKind.Construction,
                new Vector3(6.4f,0f,5.7f), 26f, parent, terrain, root =>
                {
                    foreach (var x in new[] { -.8f,.8f })
                        AddLog(root, mesh.Log, mat["bark"], new Vector3(x,0f,0f), new Vector3(x,1.7f,0f), .08f, "P94 Construction Upright " + x);
                    AddLog(root, mesh.Log, mat["darkBark"], new Vector3(-.9f,1.65f,0f), new Vector3(.9f,1.65f,0f), .075f, "P94 Construction Beam");
                    var bundle = Part("P94 Construction Lashings", root, mesh.Rope, mat["wicker"]);
                    bundle.transform.localPosition = new Vector3(0f,.12f,.34f);
                    bundle.transform.localScale = new Vector3(.34f,.04f,.34f);
                }));
            return result;
        }

        private static P94StoneAgeSettlement.Worksite Worksite(string id,
            P94StoneAgeSettlement.WorksiteKind kind, Vector3 world, float yaw, Transform parent, Terrain terrain,
            Action<Transform> build)
        {
            var root = new GameObject("P9.4 " + id + " Worksite");
            root.transform.SetParent(parent, false);
            root.transform.position = GroundPoint(terrain, world);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            build(root.transform);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.On;
            return new P94StoneAgeSettlement.Worksite().Configure(id, kind, root);
        }

        private static Transform[] RebuildSupplyPresentations(P5InhabitableRegionPresentation region,
            MeshSet mesh, IReadOnlyDictionary<string, Material> mat)
        {
            var supplies = Enumerable.Range(0, 4).Select(region.GetPileAnchor).ToArray();
            for (var index = 0; index < supplies.Length; index++)
            {
                var root = supplies[index];
                for (var i = root.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
                if (index == 0)
                {
                    for (var i = 0; i < 9; i++)
                        AddLog(root, mesh.Log, i % 3 == 0 ? mat["darkBark"] : mat["bark"],
                            new Vector3(-.62f + (i % 3) * .62f,.11f + (i / 3) * .18f,-.25f + (i % 2) * .34f),
                            new Vector3(.18f + (i % 3) * .62f,.11f + (i / 3) * .18f,-.25f + (i % 2) * .34f), .08f,
                            "P94 Supply Log " + (i + 1));
                }
                else if (index == 1)
                {
                    for (var i = 0; i < 7; i++)
                    {
                        var bundle = Part("P94 Fiber Bundle " + (i + 1), root, mesh.Tuft, mat["wicker"]);
                        bundle.transform.localPosition = new Vector3(-.55f + (i % 3) * .52f,.12f + (i / 3) * .18f,
                            -.24f + (i % 2) * .42f);
                        bundle.transform.localScale = new Vector3(.32f,.48f,.32f);
                    }
                }
                else if (index == 2)
                {
                    for (var i = 0; i < 9; i++)
                    {
                        var stone = Part("P94 Supply Stone " + (i + 1), root, mesh.Stone, mat["stone"]);
                        stone.transform.localPosition = new Vector3(-.62f + (i % 3) * .57f,.12f + (i / 3) * .20f,
                            -.28f + (i % 2) * .48f);
                        stone.transform.localRotation = Quaternion.Euler(i * 11f,i * 47f,i * 7f);
                        stone.transform.localScale = new Vector3(.38f,.26f,.34f);
                    }
                }
                else
                {
                    for (var i = 0; i < 5; i++)
                    {
                        var hide = Part("P94 Folded Hide " + (i + 1), root, mesh.Panel,
                            i % 2 == 0 ? mat["hide"] : mat["hideLight"]);
                        hide.transform.localPosition = new Vector3(0f,.08f + i * .07f,0f);
                        hide.transform.localRotation = Quaternion.Euler(0f,i * 9f,0f);
                        hide.transform.localScale = new Vector3(1.05f,.055f,.72f);
                    }
                }
                var collider = root.GetComponent<BoxCollider>();
                if (collider == null) collider = root.gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(1.65f, .9f, 1.2f);
                collider.center = new Vector3(0f, .38f, 0f);
            }
            return supplies;
        }

        private static void AddSettlementGrounding(Transform parent, Terrain terrain, MeshSet mesh,
            IReadOnlyDictionary<string, Material> mat)
        {
            var host = Child(parent, "Settlement Grounding And Paths");
            for (var i = 0; i < 22; i++)
            {
                var angle = i / 22f * Mathf.PI * 2f;
                var radius = 8.4f + Mathf.Sin(i * 2.1f) * .7f;
                var stone = Part("P94 Camp Boundary Stone " + (i + 1), host, mesh.Stone, mat["stone"]);
                stone.transform.position = GroundPoint(terrain,
                    new Vector3(Mathf.Cos(angle) * radius,0f,3f + Mathf.Sin(angle) * radius));
                stone.transform.rotation = Quaternion.Euler(0f,i * 73f,0f);
                stone.transform.localScale = new Vector3(.30f,.16f,.24f);
            }
            for (var i = 0; i < 12; i++)
            {
                var marker = Part("P94 Trampled Path Marker " + (i + 1), host, mesh.Soil, mat["soil"]);
                marker.transform.position = GroundPoint(terrain, new Vector3(-4.8f + i * .86f,0f,2.7f + Mathf.Sin(i * .7f) * .35f), -.025f);
                marker.transform.rotation = Quaternion.Euler(0f,8f + i * 3f,0f);
                marker.transform.localScale = new Vector3(.92f,.025f,.58f);
                marker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void RemoveCharacterWear(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var old = root.transform.Find("P9.4 Stone Age Wear");
                var hadOld = old != null;
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                if (hadOld) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureBuildingCatalog()
        {
            var catalog = Load<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            catalog.Configure(new[]
            {
                new BuildingPresentationCatalog.Entry().Configure("primitive_shelter", "Primitive Shelter",
                    BuildingCategory.Shelter, 4, 3, Load<GameObject>(ShelterPath)),
                new BuildingPresentationCatalog.Entry().Configure("storage_basket", "Storage Basket",
                    BuildingCategory.Storage, 2, 1, Load<GameObject>(StoragePath)),
                new BuildingPresentationCatalog.Entry().Configure("primitive_crop_plot", "Primitive Crop Plot",
                    BuildingCategory.Structure, 3, 3, Load<GameObject>(CropPath)),
                new BuildingPresentationCatalog.Entry().Configure("military_barracks", "Military Barracks",
                    BuildingCategory.Structure, 4, 3, Load<GameObject>(ShelterPath))
            }, "primitive_shelter");
            var resources = Load<Game.Presentation.Resources.ResourceCatalogAsset>(U09ResourceFoundation.CatalogPath);
            U09ResourceFoundation.ApplyEconomy(catalog);
            catalog.ConfigureResources(resources);
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
        }

        private static GameObject Part(string name, Transform parent, Mesh mesh, Material material)
        {
            var value = new GameObject(name);
            value.transform.SetParent(parent, false);
            value.AddComponent<MeshFilter>().sharedMesh = mesh;
            value.AddComponent<MeshRenderer>().sharedMaterial = material;
            return value;
        }

        private static void AddLog(Transform parent, Mesh mesh, Material material, Vector3 from, Vector3 to,
            float radius, string name)
        {
            var delta = to - from;
            var value = Part(name, parent, mesh, material);
            value.transform.localPosition = (from + to) * .5f;
            value.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            value.transform.localScale = new Vector3(radius * 2f, delta.magnitude, radius * 2f);
        }

        private static Transform Child(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Material Material(string name, Color color, float metallic, float smoothness,
            Texture2D baseMap, Texture2D normalMap)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader is unavailable.");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateDerivedTexture(string path, bool woven)
        {
            if (!File.Exists(path))
            {
                var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false, false);
                for (var y = 0; y < texture.height; y++)
                for (var x = 0; x < texture.width; x++)
                {
                    var noise = Mathf.PerlinNoise(x * .075f + 11.3f, y * .065f + 4.7f);
                    var stripe = woven ? ((x / 9 + y / 7) % 2 == 0 ? .10f : -.06f) :
                        Mathf.Sin((x + y * .18f) * .12f) * .035f;
                    var value = Mathf.Clamp01(.42f + noise * .36f + stripe);
                    texture.SetPixel(x, y, woven
                        ? new Color(value * .72f, value * .50f, value * .22f, 1f)
                        : new Color(value * .78f, value * .52f, value * .30f, 1f));
                }
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return Load<Texture2D>(path);
        }

        private static Mesh WriteMesh(string path, string name,
            (List<Vector3> Vertices, List<int> Triangles, List<Vector2> Uvs) data)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(data.Vertices);
            mesh.SetTriangles(data.Triangles, 0);
            mesh.SetUVs(0, data.Uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static (List<Vector3>, List<int>, List<Vector2>) CreateLogMesh()
        {
            const int segments = 12;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            for (var ring = 0; ring < 4; ring++)
            {
                var y = -0.5f + ring / 3f;
                var taper = 1f - ring * .055f;
                var offset = new Vector2(Mathf.Sin(ring * 2.7f) * .025f, Mathf.Cos(ring * 1.9f) * .018f);
                for (var i = 0; i < segments; i++)
                {
                    var angle = i / (float)segments * Mathf.PI * 2f;
                    var irregular = 1f + Mathf.Sin(i * 2.4f + ring) * .055f;
                    vertices.Add(new Vector3(offset.x + Mathf.Cos(angle) * .5f * taper * irregular, y,
                        offset.y + Mathf.Sin(angle) * .5f * taper * irregular));
                    uvs.Add(new Vector2(i / (float)segments, ring / 3f));
                }
            }
            for (var ring = 0; ring < 3; ring++)
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                var a = ring * segments + i;
                var b = ring * segments + next;
                var c = (ring + 1) * segments + i;
                var d = (ring + 1) * segments + next;
                triangles.AddRange(new[] { a,c,b,b,c,d });
            }
            var bottom = vertices.Count;
            vertices.Add(new Vector3(0f,-.5f,0f)); uvs.Add(new Vector2(.5f,.5f));
            var top = vertices.Count;
            vertices.Add(new Vector3(0f,.5f,0f)); uvs.Add(new Vector2(.5f,.5f));
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                triangles.AddRange(new[] { bottom,next,i });
                triangles.AddRange(new[] { top,3 * segments + i,3 * segments + next });
            }
            return (vertices, triangles, uvs);
        }

        private static (List<Vector3>, List<int>, List<Vector2>) CreatePanelMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var outline = new[]
            {
                new Vector2(-.50f,-.44f), new Vector2(.46f,-.50f), new Vector2(.52f,.38f),
                new Vector2(.18f,.50f), new Vector2(-.45f,.46f), new Vector2(-.53f,.08f)
            };
            for (var side = 0; side < 2; side++)
            {
                var y = side == 0 ? -.5f : .5f;
                foreach (var point in outline)
                {
                    vertices.Add(new Vector3(point.x, y, point.y));
                    uvs.Add(point + Vector2.one * .5f);
                }
            }
            for (var i = 1; i < outline.Length - 1; i++) triangles.AddRange(new[] { 0,i + 1,i });
            for (var i = 1; i < outline.Length - 1; i++) triangles.AddRange(new[] { 6,6 + i,6 + i + 1 });
            for (var i = 0; i < outline.Length; i++)
            {
                var next = (i + 1) % outline.Length;
                triangles.AddRange(new[] { i,next,6 + i,next,6 + next,6 + i });
            }
            return (vertices, triangles, uvs);
        }

        private static (List<Vector3>, List<int>, List<Vector2>) CreateStoneMesh()
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-.55f,-.34f,-.42f), new Vector3(.48f,-.30f,-.46f), new Vector3(.57f,-.28f,.30f),
                new Vector3(-.44f,-.32f,.48f), new Vector3(-.38f,.31f,-.29f), new Vector3(.32f,.42f,-.34f),
                new Vector3(.44f,.28f,.27f), new Vector3(-.31f,.37f,.36f)
            };
            var triangles = new List<int>
            {
                0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7,
                4,5,6,4,6,7,0,3,2,0,2,1
            };
            return (vertices, triangles, Enumerable.Repeat(Vector2.zero, vertices.Count).ToList());
        }

        private static (List<Vector3>, List<int>, List<Vector2>) CreateTorusMesh(int majorSegments, int minorSegments)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            for (var major = 0; major < majorSegments; major++)
            for (var minor = 0; minor < minorSegments; minor++)
            {
                var a = major / (float)majorSegments * Mathf.PI * 2f;
                var b = minor / (float)minorSegments * Mathf.PI * 2f;
                var radius = .5f + Mathf.Cos(b) * .09f;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(b) * .09f, Mathf.Sin(a) * radius));
                uvs.Add(new Vector2(major / (float)majorSegments, minor / (float)minorSegments));
            }
            for (var major = 0; major < majorSegments; major++)
            for (var minor = 0; minor < minorSegments; minor++)
            {
                var nextMajor = (major + 1) % majorSegments;
                var nextMinor = (minor + 1) % minorSegments;
                var a = major * minorSegments + minor;
                var b = nextMajor * minorSegments + minor;
                var c = major * minorSegments + nextMinor;
                var d = nextMajor * minorSegments + nextMinor;
                triangles.AddRange(new[] { a,b,c,c,b,d });
            }
            return (vertices, triangles, uvs);
        }

        private static (List<Vector3>, List<int>, List<Vector2>) CreateSoilMesh()
        {
            var data = CreateStoneMesh();
            for (var i = 0; i < data.Item1.Count; i++)
            {
                var value = data.Item1[i];
                data.Item1[i] = new Vector3(value.x, value.y * .16f, value.z);
            }
            return data;
        }

        private static (List<Vector3>, List<int>, List<Vector2>) CreateTuftMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            for (var blade = 0; blade < 7; blade++)
            {
                var angle = blade / 7f * Mathf.PI * 2f;
                var right = new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle)) * .09f;
                var center = new Vector3(Mathf.Cos(angle) * .08f,0f,Mathf.Sin(angle) * .08f);
                var lean = new Vector3(Mathf.Cos(angle) * .18f,1f + (blade % 3) * .12f,Mathf.Sin(angle) * .18f);
                var start = vertices.Count;
                vertices.Add(center - right); vertices.Add(center + right);
                vertices.Add(center + lean - right * .18f); vertices.Add(center + lean + right * .18f);
                uvs.AddRange(new[] { Vector2.zero,Vector2.right,Vector2.up,Vector2.one });
                triangles.AddRange(new[] { start,start + 2,start + 1,start + 1,start + 2,start + 3,
                    start + 1,start + 2,start,start + 3,start + 2,start + 1 });
            }
            return (vertices, triangles, uvs);
        }

        private static Vector3 GroundPoint(Terrain terrain, Vector3 world, float offset = 0f)
        {
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y + offset;
            return world;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException("Missing P9.4 asset.", path);

        private static void DestroyByName(string name)
        {
            var target = GameObject.Find(name);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
