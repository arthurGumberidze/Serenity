using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Presentation.Environment;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure.Editor
{
    public static class P3ReferenceSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/LocalGameplay.unity";
        private const string MaterialFolder = "Assets/Game/Art/Materials/P3";
        private const string MeshFolder = "Assets/Game/Art/Meshes/P3";
        private const string TreePath = "Assets/Game/Art/Prefabs/Environment/Serenity_Tree.prefab";
        private const string RockPath = "Assets/Game/Art/Prefabs/Environment/Serenity_Rock.prefab";
        private const string CampfirePath = "Assets/Game/Art/Prefabs/Props/Serenity_Campfire.prefab";
        private const string AxePath = "Assets/Game/Art/Prefabs/Weapons/Serenity_StoneAxe.prefab";
        private const string SpearPath = "Assets/Game/Art/Prefabs/Weapons/Serenity_StoneSpear.prefab";

        public static void Build()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(MeshFolder);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var composition = UnityEngine.Object.FindFirstObjectByType<LocalSceneCompositionRoot>();
            if (composition == null) throw new InvalidOperationException("LocalGameplay composition root is missing.");

            var oldWorld = GameObject.Find("P3 Reference World");
            if (oldWorld != null) UnityEngine.Object.DestroyImmediate(oldWorld);
            var oldPresentation = composition.GetComponent<StoneAgeReferenceCampPresentation>();
            if (oldPresentation != null) UnityEngine.Object.DestroyImmediate(oldPresentation);
            var oldRegion = composition.GetComponent<P5InhabitableRegionPresentation>();
            if (oldRegion != null) UnityEngine.Object.DestroyImmediate(oldRegion);

            var groundMaterial = CreateLitMaterial(MaterialFolder + "/P3_Ground.mat",
                new Color(0.19f, 0.27f, 0.16f), 0f, 0.08f);
            var sandMaterial = CreateLitMaterial(MaterialFolder + "/P3_Shore.mat",
                new Color(0.48f, 0.39f, 0.24f), 0f, 0.16f);
            var waterMaterial = CreateWaterMaterial(MaterialFolder + "/P3_Water.mat");
            var grassMaterial = CreateLitMaterial(MaterialFolder + "/P3_Grass.mat",
                new Color(0.24f, 0.38f, 0.18f), 0f, 0.04f);
            grassMaterial.SetFloat("_Cull", (float)CullMode.Off);
            var woodMaterial = CreateLitMaterial(MaterialFolder + "/P3_CharredWood.mat",
                new Color(0.16f, 0.075f, 0.035f), 0f, 0.12f);
            var stoneMaterial = CreateLitMaterial(MaterialFolder + "/P3_FireStone.mat",
                new Color(0.25f, 0.27f, 0.25f), 0f, 0.15f);
            var particleMask = CreateParticleMask(MaterialFolder + "/P3_SoftParticle.asset");
            var flameParticleMaterial = CreateTransparentUnlitMaterial(MaterialFolder + "/P3_Flame.mat",
                new Color(1f, 0.22f, 0.025f, 0.9f), particleMask);
            var smokeParticleMaterial = CreateTransparentUnlitMaterial(MaterialFolder + "/P3_Smoke.mat",
                new Color(0.22f, 0.24f, 0.25f, 0.34f), particleMask);
            var rainParticleMaterial = CreateTransparentUnlitMaterial(MaterialFolder + "/P3_Rain.mat",
                new Color(0.68f, 0.79f, 0.9f, 0.52f), particleMask);
            var shelterMaterial = CreateLitMaterial(MaterialFolder + "/P3_ShelterHide.mat",
                new Color(0.46f, 0.31f, 0.17f), 0f, 0.16f);
            var storageMaterial = CreateLitMaterial(MaterialFolder + "/P3_StorageWicker.mat",
                new Color(0.34f, 0.19f, 0.075f), 0f, 0.12f);
            var routeMaterial = CreateLitMaterial(MaterialFolder + "/P5_RouteEarth.mat",
                new Color(0.31f, 0.22f, 0.12f), 0f, 0.05f);
            var berryMaterial = CreateLitMaterial(MaterialFolder + "/P5_BerryBush.mat",
                new Color(0.36f, 0.18f, 0.26f), 0f, 0.03f);
            var fiberMaterial = CreateLitMaterial(MaterialFolder + "/P5_FiberPatch.mat",
                new Color(0.42f, 0.51f, 0.17f), 0f, 0.03f);
            var territoryMaterial = CreateLitMaterial(MaterialFolder + "/P5_RivalTerritory.mat",
                new Color(0.38f, 0.12f, 0.08f), 0f, 0.06f);
            var skyboxMaterial = CreateSkyboxMaterial(MaterialFolder + "/P3_Skybox.mat");
            var shoreMesh = CreateShoreMesh(MeshFolder + "/P3_Shoreline.asset");
            var waterMesh = CreateWaterMesh(MeshFolder + "/P3_WaterPlane.asset");
            var grassMesh = CreateGrassMesh(MeshFolder + "/P3_GrassCluster.asset");
            var routeMesh = CreateRouteMesh(MeshFolder + "/P5_RouteSegment.asset");
            var supplyMesh = CreateSupplyMesh(MeshFolder + "/P5_SupplyCache.asset");
            var totemMesh = CreateTotemMesh(MeshFolder + "/P5_TerritoryTotem.asset");

            var world = new GameObject("P3 Reference World");
            var ground = GameObject.Find("P3 Buildable Forest Clearing") ??
                GameObject.Find("P8.2 Natural Inhabitable Land") ?? GameObject.Find("Ground Placeholder");
            if (ground == null) throw new InvalidOperationException("Buildable ground is missing.");
            ground.name = "P3 Buildable Forest Clearing";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            foreach (var markerName in new[] { "Selection Marker A", "Selection Marker B", "Selection Marker C" })
            {
                var marker = GameObject.Find(markerName);
                var markerRenderer = marker != null ? marker.GetComponent<Renderer>() : null;
                if (markerRenderer != null) markerRenderer.enabled = false;
            }

            var shore = CreateMeshObject("P3 Curved Shore", world.transform, shoreMesh, sandMaterial);
            shore.transform.position = new Vector3(0f, 0.045f, 0f);
            var water = CreateMeshObject("P3 Water Edge", world.transform, waterMesh, waterMaterial);
            water.transform.position = new Vector3(0f, 0.075f, 0f);
            water.AddComponent<MeshCollider>().sharedMesh = waterMesh;
            var waterModifier = water.AddComponent<NavMeshModifier>();
            waterModifier.overrideArea = true;
            waterModifier.area = 1;
            shore.AddComponent<MeshCollider>().sharedMesh = shoreMesh;

            var treePrefab = LoadPrefab(TreePath);
            var rockPrefab = LoadPrefab(RockPath);
            var campfirePrefab = LoadPrefab(CampfirePath);
            var axePrefab = LoadPrefab(AxePath);
            var spearPrefab = LoadPrefab(SpearPath);

            var treePositions = new[]
            {
                new Vector3(-5,0,24), new Vector3(1,0,27), new Vector3(8,0,25), new Vector3(15,0,22),
                new Vector3(22,0,18), new Vector3(25,0,10), new Vector3(27,0,1), new Vector3(25,0,-9),
                new Vector3(20,0,-18), new Vector3(12,0,-24), new Vector3(3,0,-27), new Vector3(-5,0,-25),
                new Vector3(-10,0,-19), new Vector3(-8,0,18), new Vector3(18,0,27), new Vector3(30,0,-22),
                new Vector3(31,0,20), new Vector3(8,0,32), new Vector3(-4,0,9), new Vector3(11,0,34),
                new Vector3(17,0,34), new Vector3(23,0,31), new Vector3(29,0,27), new Vector3(35,0,21),
                new Vector3(37,0,13), new Vector3(38,0,4), new Vector3(37,0,-6), new Vector3(34,0,-15),
                new Vector3(28,0,-27), new Vector3(20,0,-33), new Vector3(10,0,-36), new Vector3(0,0,-37),
                new Vector3(-8,0,-34), new Vector3(-7,0,35), new Vector3(0,0,38), new Vector3(31,0,34),
                new Vector3(41,0,26), new Vector3(42,0,14), new Vector3(42,0,-2), new Vector3(39,0,-25)
            };
            var treeObjects = new List<GameObject>();
            for (var i = 0; i < treePositions.Length; i++)
            {
                var tree = InstantiatePrefab(treePrefab, world.transform, "P3 Tree " + (i + 1));
                tree.transform.position = treePositions[i];
                tree.transform.rotation = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);
                var scale = 0.82f + (i % 5) * 0.07f;
                tree.transform.localScale = Vector3.one * scale;
                treeObjects.Add(tree);
            }

            var rockPositions = new[]
            {
                new Vector3(-10.8f,0,18), new Vector3(-11.7f,0,11), new Vector3(-9.4f,0,4),
                new Vector3(-12.2f,0,-4), new Vector3(-10.5f,0,-12), new Vector3(-11.4f,0,-20),
                new Vector3(14f,0,15), new Vector3(19f,0,-12), new Vector3(8f,0,-19), new Vector3(23f,0,5),
                new Vector3(1f,0,9f), new Vector3(12f,0,19f), new Vector3(18f,0,21f),
                new Vector3(25f,0,24f), new Vector3(33f,0,18f), new Vector3(35f,0,7f),
                new Vector3(32f,0,-5f), new Vector3(27f,0,-18f), new Vector3(17f,0,-27f),
                new Vector3(5f,0,-31f), new Vector3(-6f,0,-29f), new Vector3(41f,0,-14f)
            };
            var rockObjects = new List<GameObject>();
            for (var i = 0; i < rockPositions.Length; i++)
            {
                var rock = InstantiatePrefab(rockPrefab, world.transform, "P3 Rock " + (i + 1));
                rock.transform.position = rockPositions[i];
                rock.transform.rotation = Quaternion.Euler(0f, (i * 71f) % 360f, i % 2 == 0 ? 0f : 7f);
                rock.transform.localScale = Vector3.one * (0.65f + (i % 4) * 0.16f);
                rockObjects.Add(rock);
            }

            const int grassCount = 96;
            var random = new System.Random(310033);
            for (var i = 0; i < grassCount; i++)
            {
                Vector3 position;
                do
                {
                    position = new Vector3(-8f + (float)random.NextDouble() * 48f, 0.025f,
                        -34f + (float)random.NextDouble() * 68f);
                } while (new Vector2(position.x - 1f, position.z - 3f).sqrMagnitude < 68f ||
                         (position.x > -4f && position.x < 31f && Mathf.Abs(position.z - 4f) < 3.2f));
                var grass = CreateMeshObject("P3 Grass " + (i + 1), world.transform, grassMesh, grassMaterial);
                grass.transform.position = position;
                grass.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                grass.transform.localScale = Vector3.one * (0.75f + (float)random.NextDouble() * 0.55f);
                var grassRenderer = grass.GetComponent<MeshRenderer>();
                grassRenderer.shadowCastingMode = ShadowCastingMode.Off;
                grassRenderer.receiveShadows = false;
            }

            var campfire = InstantiatePrefab(campfirePrefab, world.transform, "P3 Canonical Camp Center");
            campfire.transform.position = new Vector3(0f, 0f, 3.2f);
            campfire.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            BuildFireRing(campfire.transform, stoneMaterial, woodMaterial);

            var routeRenderers = new List<Renderer>();
            var routeAnchors = new List<Transform>();
            var routePoints = new[]
            {
                new Vector3(0f,0.035f,3f), new Vector3(8f,0.035f,4f), new Vector3(16f,0.035f,4f),
                new Vector3(24f,0.035f,5f), new Vector3(31f,0.035f,10f), new Vector3(20f,0.035f,18f),
                new Vector3(3f,0.035f,-17f)
            };
            CreateRoute("P5 Main Camp Route", routePoints[0], routePoints[1], world.transform, routeMesh,
                routeMaterial, routeRenderers, routeAnchors);
            CreateRoute("P5 Ford Route", routePoints[1], routePoints[2], world.transform, routeMesh,
                routeMaterial, routeRenderers, routeAnchors);
            CreateRoute("P5 Rival Route", routePoints[2], routePoints[3], world.transform, routeMesh,
                routeMaterial, routeRenderers, routeAnchors);
            CreateRoute("P5 East Hills Route", routePoints[3], routePoints[4], world.transform, routeMesh,
                routeMaterial, routeRenderers, routeAnchors);
            CreateRoute("P5 North Forest Route", routePoints[1], routePoints[5], world.transform, routeMesh,
                routeMaterial, routeRenderers, routeAnchors);
            CreateRoute("P5 South Meadow Route", routePoints[0], routePoints[6], world.transform, routeMesh,
                routeMaterial, routeRenderers, routeAnchors);

            var rivalTerritory = new GameObject("P5 Rival Territory enemy_settlement");
            rivalTerritory.transform.SetParent(world.transform, false);
            rivalTerritory.transform.position = new Vector3(24f, 0f, 5f);
            for (var i = 0; i < 3; i++)
            {
                var totem = CreateMeshObject("P5 Rival Boundary Totem " + (i + 1), rivalTerritory.transform,
                    totemMesh, territoryMaterial);
                totem.transform.localPosition = new Vector3(-4f + i * 4f, 0f, 4.5f);
                totem.AddComponent<BoxCollider>().size = new Vector3(0.55f, 3.4f, 0.55f);
            }
            var rivalFire = InstantiatePrefab(campfirePrefab, rivalTerritory.transform, "P5 Rival Campfire Projection");
            rivalFire.transform.localPosition = Vector3.zero;

            var buildableAnchors = new List<Transform>
            {
                CreateAnchor("P5 Player Buildable Clearing", new Vector3(0f, 0f, 0f), world.transform),
                CreateAnchor("P5 Rival Buildable Clearing", new Vector3(24f, 0f, 5f), world.transform),
                CreateAnchor("P5 South Meadow Buildable Area", new Vector3(3f, 0f, -17f), world.transform)
            };

            var pileAnchors = new List<Transform>();
            var pileData = new[]
            {
                ("Wood", new Vector3(-8f,0.08f,-2f), woodMaterial),
                ("Fiber", new Vector3(-6f,0.08f,-2f), fiberMaterial),
                ("Stone", new Vector3(-4f,0.08f,-2f), stoneMaterial),
                ("Hide", new Vector3(-2f,0.08f,-2f), shelterMaterial)
            };
            for (var i = 0; i < pileData.Length; i++)
            {
                var pile = CreateMeshObject("P5 Authored " + pileData[i].Item1 + " Supply", world.transform,
                    supplyMesh, pileData[i].Item3);
                pile.transform.position = pileData[i].Item2;
                pile.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
                pile.transform.localScale = new Vector3(0.8f, 0.65f, 0.8f);
                pile.AddComponent<BoxCollider>().size = new Vector3(1.1f, 0.8f, 1.1f);
                pileAnchors.Add(pile.transform);
            }

            var berryNodes = new[] { new Vector3(-12f,0.02f,5f), new Vector3(7f,0.02f,16f),
                new Vector3(18f,0.02f,-9f), new Vector3(29f,0.02f,14f) };
            var fiberNodes = new[] { new Vector3(-8f,0.02f,7f), new Vector3(4f,0.02f,20f),
                new Vector3(12f,0.02f,-15f), new Vector3(31f,0.02f,-8f) };
            var resourceAnchors = new List<P5InhabitableRegionPresentation.ResourceAnchor>();
            for (var i = 0; i < berryNodes.Length; i++)
            {
                var patch = CreateMeshObject("P5 Berry Thicket " + (i + 1), world.transform, grassMesh, berryMaterial);
                patch.transform.position = berryNodes[i];
                patch.transform.localScale = new Vector3(3.2f, 1.7f, 3.2f);
                patch.AddComponent<BoxCollider>().size = new Vector3(0.9f, 0.65f, 0.9f);
                resourceAnchors.Add(new P5InhabitableRegionPresentation.ResourceAnchor().Configure(
                    "gather_berries", 60 + i * 15, i == 0 ? 0 : 4 + (i - 1), patch.transform,
                    new Color(0.62f, 0.18f, 0.34f)));
            }
            for (var i = 0; i < fiberNodes.Length; i++)
            {
                var patch = CreateMeshObject("P5 Fiber Stand " + (i + 1), world.transform, grassMesh, fiberMaterial);
                patch.transform.position = fiberNodes[i];
                patch.transform.localScale = new Vector3(2.8f, 2.1f, 2.8f);
                patch.AddComponent<BoxCollider>().size = new Vector3(0.8f, 0.8f, 0.8f);
                resourceAnchors.Add(new P5InhabitableRegionPresentation.ResourceAnchor().Configure(
                    "gather_fiber", 60 + i * 15, i == 0 ? 1 : 7 + (i - 1), patch.transform,
                    new Color(0.4f, 0.68f, 0.2f)));
            }
            var woodNodeIndices = new[] { 18, 20, 24, 28 };
            for (var i = 0; i < woodNodeIndices.Length; i++)
                resourceAnchors.Add(new P5InhabitableRegionPresentation.ResourceAnchor().Configure(
                    "chop_wood", 80 + i * 20, i == 0 ? 2 : 10 + (i - 1),
                    treeObjects[woodNodeIndices[i]].transform, new Color(0.36f, 0.28f, 0.14f)));
            var stoneNodeIndices = new[] { 10, 11, 13, 16 };
            for (var i = 0; i < stoneNodeIndices.Length; i++)
                resourceAnchors.Add(new P5InhabitableRegionPresentation.ResourceAnchor().Configure(
                    "gather_stone", 80 + i * 20, i == 0 ? 3 : 13 + (i - 1),
                    rockObjects[stoneNodeIndices[i]].transform, new Color(0.5f, 0.52f, 0.5f)));

            var region = composition.gameObject.AddComponent<P5InhabitableRegionPresentation>();
            region.Configure(campfire.transform, rivalTerritory.transform, routeAnchors, buildableAnchors,
                treeObjects.Concat(rockObjects).Select(x => x.transform), pileAnchors, resourceAnchors,
                new[] { ground.GetComponent<Renderer>() }.Concat(routeRenderers),
                Enumerable.Range(0, grassCount).Select(i => GameObject.Find("P3 Grass " + (i + 1))?.GetComponent<Renderer>()),
                water.GetComponent<Renderer>(), treePositions.Length, rockPositions.Length, grassCount);

            ConfigureLighting(skyboxMaterial);
            var cameraRig = GameObject.Find("RTS Camera Rig");
            if (cameraRig != null)
            {
                cameraRig.transform.position = new Vector3(2f, 0f, 4f);
                cameraRig.transform.rotation = Quaternion.Euler(0f, -18f, 0f);
            }

            var presentation = composition.gameObject.AddComponent<StoneAgeReferenceCampPresentation>();
            presentation.Configure(campfire.transform, water.GetComponent<Renderer>(), axePrefab, spearPrefab,
                flameParticleMaterial, smokeParticleMaterial, rainParticleMaterial,
                shelterMaterial, storageMaterial,
                treePositions.Length, rockPositions.Length, grassCount);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("P5 inhabitable region generated: " + treePositions.Length + " trees, " + rockPositions.Length +
                " rocks, " + grassCount + " grass clusters, 16 canonical resource projections and rival territory.");
        }

        private static GameObject LoadPrefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) ??
            throw new FileNotFoundException("Required approved P3 prefab is missing.", path);

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic);
            return instance;
        }

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var value = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            value.transform.SetParent(parent, false);
            value.GetComponent<MeshFilter>().sharedMesh = mesh;
            value.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(value, StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic);
            return value;
        }

        private static Material CreateLitMaterial(string path, Color color, float metallic, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateWaterMaterial(string path)
        {
            var material = CreateLitMaterial(path, new Color(0.08f, 0.32f, 0.38f, 0.72f), 0.04f, 0.86f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTransparentUnlitMaterial(string path, Color color, Texture particleMask)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) throw new InvalidOperationException("URP Unlit shader is unavailable.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetTexture("_BaseMap", particleMask);
            material.SetTexture("_MainTex", particleMask);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateParticleMask(string path)
        {
            const int size = 32;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = "P3 Soft Particle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                AssetDatabase.CreateAsset(texture, path);
            }
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var normalized = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                var alpha = Mathf.SmoothStep(1f, 0f, normalized.magnitude);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Material CreateSkyboxMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader == null) throw new InvalidOperationException("Procedural skybox shader is unavailable.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_SkyTint", new Color(0.48f, 0.61f, 0.72f));
            material.SetColor("_GroundColor", new Color(0.21f, 0.19f, 0.15f));
            material.SetFloat("_AtmosphereThickness", 0.72f);
            material.SetFloat("_SunSize", 0.035f);
            material.SetFloat("_Exposure", 1.08f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh CreateShoreMesh(string path)
        {
            const int segments = 24;
            var vertices = new List<Vector3>((segments + 1) * 2);
            var triangles = new List<int>(segments * 6);
            var uvs = new List<Vector2>((segments + 1) * 2);
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var z = Mathf.Lerp(-45f, 45f, t);
                var waterEdge = -12.5f + Mathf.Sin(t * Mathf.PI * 2.3f) * 1.6f + Mathf.Sin(t * Mathf.PI * 5f) * 0.45f;
                vertices.Add(new Vector3(waterEdge, 0f, z));
                vertices.Add(new Vector3(waterEdge + 6.5f, 0f, z));
                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(1f, t));
                if (i == segments) continue;
                var a = i * 2;
                triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
            }
            return WriteMesh(path, "P3 Curved Shoreline", vertices, triangles, uvs);
        }

        private static Mesh CreateWaterMesh(string path)
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-50,0,-50), new Vector3(-10.5f,0,-50),
                new Vector3(-50,0,50), new Vector3(-10.5f,0,50)
            };
            var triangles = new List<int> { 0, 2, 1, 1, 2, 3 };
            var uvs = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            return WriteMesh(path, "P3 Water Plane", vertices, triangles, uvs);
        }

        private static Mesh CreateGrassMesh(string path)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            for (var blade = 0; blade < 7; blade++)
            {
                var angle = blade * 137.5f * Mathf.Deg2Rad;
                var distance = 0.08f + (blade % 3) * 0.11f;
                var center = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                var right = new Vector3(Mathf.Cos(angle + Mathf.PI * 0.5f), 0f,
                    Mathf.Sin(angle + Mathf.PI * 0.5f)) * (0.055f + blade % 2 * 0.018f);
                var height = 0.42f + (blade % 4) * 0.09f;
                var start = vertices.Count;
                vertices.Add(center - right);
                vertices.Add(center + right);
                vertices.Add(center - right * 0.22f + Vector3.up * height);
                vertices.Add(center + right * 0.22f + Vector3.up * height);
                uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.up); uvs.Add(Vector2.one);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            return WriteMesh(path, "P3 Grass Cluster", vertices, triangles, uvs);
        }

        private static Mesh CreateRouteMesh(string path)
        {
            return WriteMesh(path, "P5 Authored Route Segment",
                new List<Vector3>
                {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f)
                }, new List<int> { 0, 2, 1, 1, 2, 3 },
                new List<Vector2> { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
        }

        private static Mesh CreateSupplyMesh(string path)
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-0.6f,0f,-0.45f), new Vector3(0.6f,0f,-0.45f),
                new Vector3(0.6f,0f,0.45f), new Vector3(-0.6f,0f,0.45f),
                new Vector3(-0.42f,0.65f,-0.3f), new Vector3(0.42f,0.65f,-0.3f),
                new Vector3(0.42f,0.65f,0.3f), new Vector3(-0.42f,0.65f,0.3f)
            };
            var triangles = new List<int>
            {
                0,1,4, 1,5,4, 1,2,5, 2,6,5, 2,3,6, 3,7,6, 3,0,7, 0,4,7,
                4,5,6, 4,6,7, 0,3,2, 0,2,1
            };
            return WriteMesh(path, "P5 Authored Supply Cache", vertices, triangles,
                Enumerable.Repeat(Vector2.zero, vertices.Count).ToList());
        }

        private static Mesh CreateTotemMesh(string path)
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-0.22f,0f,-0.22f), new Vector3(0.22f,0f,-0.22f),
                new Vector3(0.22f,0f,0.22f), new Vector3(-0.22f,0f,0.22f),
                new Vector3(-0.13f,3.2f,-0.13f), new Vector3(0.13f,3.2f,-0.13f),
                new Vector3(0.13f,3.2f,0.13f), new Vector3(-0.13f,3.2f,0.13f)
            };
            var triangles = new List<int>
            {
                0,1,4, 1,5,4, 1,2,5, 2,6,5, 2,3,6, 3,7,6, 3,0,7, 0,4,7,
                4,5,6, 4,6,7, 0,3,2, 0,2,1
            };
            return WriteMesh(path, "P5 Rival Territory Totem", vertices, triangles,
                Enumerable.Repeat(Vector2.zero, vertices.Count).ToList());
        }

        private static Transform CreateAnchor(string name, Vector3 position, Transform parent)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.position = position;
            return anchor;
        }

        private static void CreateRoute(string name, Vector3 from, Vector3 to, Transform parent, Mesh mesh,
            Material material, ICollection<Renderer> renderers, ICollection<Transform> anchors)
        {
            var route = CreateMeshObject(name, parent, mesh, material);
            var delta = to - from;
            route.transform.position = (from + to) * 0.5f;
            route.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 0f);
            route.transform.localScale = new Vector3(2.4f, 1f, delta.magnitude);
            route.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            renderers.Add(route.GetComponent<Renderer>());
            anchors.Add(route.transform);
        }

        private static Mesh WriteMesh(string path, string name, List<Vector3> vertices, List<int> triangles,
            List<Vector2> uvs)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void BuildFireRing(Transform campfire, Material stone, Material wood)
        {
            for (var i = 0; i < 10; i++)
            {
                var pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pebble.name = "P3 Fire Ring Stone " + (i + 1);
                pebble.transform.SetParent(campfire, false);
                var angle = i * Mathf.PI * 2f / 10f;
                pebble.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.48f, 0.11f, Mathf.Sin(angle) * 0.48f);
                pebble.transform.localScale = new Vector3(0.28f, 0.18f, 0.22f);
                pebble.GetComponent<Renderer>().sharedMaterial = stone;
                UnityEngine.Object.DestroyImmediate(pebble.GetComponent<Collider>());
            }
            for (var i = 0; i < 3; i++)
            {
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "P3 Charred Log " + (i + 1);
                log.transform.SetParent(campfire, false);
                log.transform.localPosition = new Vector3(0f, 0.16f + i * 0.03f, 0f);
                log.transform.localRotation = Quaternion.Euler(90f, i * 60f, 0f);
                log.transform.localScale = new Vector3(0.12f, 0.48f, 0.12f);
                log.GetComponent<Renderer>().sharedMaterial = wood;
                UnityEngine.Object.DestroyImmediate(log.GetComponent<Collider>());
            }
        }

        private static void ConfigureLighting(Material skybox)
        {
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.39f);
            RenderSettings.reflectionIntensity = 0.72f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.45f, 0.52f, 0.52f);
            RenderSettings.fogDensity = 0.0035f;
            var sunObject = GameObject.Find("Directional Light");
            var sun = sunObject != null ? sunObject.GetComponent<Light>() : null;
            if (sun == null) return;
            sunObject.transform.rotation = Quaternion.Euler(42f, -34f, 0f);
            sun.intensity = 1.18f;
            sun.color = new Color(1f, 0.89f, 0.72f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.035f;
            RenderSettings.sun = sun;
        }
    }
}
