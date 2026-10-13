using System;
using System.Collections.Generic;
using System.IO;
using Game.Presentation.Environment;
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
            var skyboxMaterial = CreateSkyboxMaterial(MaterialFolder + "/P3_Skybox.mat");
            var shoreMesh = CreateShoreMesh(MeshFolder + "/P3_Shoreline.asset");
            var waterMesh = CreateWaterMesh(MeshFolder + "/P3_WaterPlane.asset");
            var grassMesh = CreateGrassMesh(MeshFolder + "/P3_GrassCluster.asset");

            var world = new GameObject("P3 Reference World");
            var ground = GameObject.Find("P3 Buildable Forest Clearing") ?? GameObject.Find("Ground Placeholder");
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
                new Vector3(31,0,20), new Vector3(8,0,32)
            };
            for (var i = 0; i < treePositions.Length; i++)
            {
                var tree = InstantiatePrefab(treePrefab, world.transform, "P3 Tree " + (i + 1));
                tree.transform.position = treePositions[i];
                tree.transform.rotation = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);
                var scale = 0.82f + (i % 5) * 0.07f;
                tree.transform.localScale = Vector3.one * scale;
            }

            var rockPositions = new[]
            {
                new Vector3(-10.8f,0,18), new Vector3(-11.7f,0,11), new Vector3(-9.4f,0,4),
                new Vector3(-12.2f,0,-4), new Vector3(-10.5f,0,-12), new Vector3(-11.4f,0,-20),
                new Vector3(14f,0,15), new Vector3(19f,0,-12), new Vector3(8f,0,-19), new Vector3(23f,0,5)
            };
            for (var i = 0; i < rockPositions.Length; i++)
            {
                var rock = InstantiatePrefab(rockPrefab, world.transform, "P3 Rock " + (i + 1));
                rock.transform.position = rockPositions[i];
                rock.transform.rotation = Quaternion.Euler(0f, (i * 71f) % 360f, i % 2 == 0 ? 0f : 7f);
                rock.transform.localScale = Vector3.one * (0.65f + (i % 4) * 0.16f);
            }

            const int grassCount = 52;
            var random = new System.Random(310033);
            for (var i = 0; i < grassCount; i++)
            {
                Vector3 position;
                do
                {
                    position = new Vector3(-5f + (float)random.NextDouble() * 31f, 0.025f,
                        -22f + (float)random.NextDouble() * 44f);
                } while (new Vector2(position.x - 1f, position.z - 3f).sqrMagnitude < 68f ||
                         (position.x < 1f && position.z > 1f && position.z < 10f));
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
            Debug.Log("P3 reference camp generated: 18 trees, 10 rocks, 52 grass clusters, shore, water and campfire.");
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
