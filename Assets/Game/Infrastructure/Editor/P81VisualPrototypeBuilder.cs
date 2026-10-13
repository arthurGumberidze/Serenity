using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure.Editor
{
    public static class P81VisualPrototypeBuilder
    {
        private const string ScenePath = "Assets/Scenes/LocalGameplay.unity";
        private const string MaterialFolder = "Assets/Game/Art/Materials/P81";
        private const string TextureFolder = "Assets/Game/Art/Textures/P81";
        private const string MeshFolder = "Assets/Game/Art/Meshes/P81";
        private const string RootName = "P8.1 Visual Prototype";

        [MenuItem("Serenity/P8.1/Build Visual Prototype")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(TextureFolder);
            Directory.CreateDirectory(MeshFolder);
            EnsureProjectTag("UMAIgnore");
            RebuildUmaLibrary();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var old = GameObject.Find(RootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);

            var grass = CreateNaturalMaterial("P81_Meadow", new Color(0.20f, 0.28f, 0.13f), 0.02f, 0.18f, 11);
            var sand = CreateNaturalMaterial("P81_Shore", new Color(0.48f, 0.38f, 0.23f), 0.01f, 0.26f, 29);
            var bark = CreateNaturalMaterial("P81_BrownBark", new Color(0.20f, 0.095f, 0.038f), 0f, 0.12f, 43);
            var leaf = CreateNaturalMaterial("P81_ForestCanopy", new Color(0.16f, 0.26f, 0.095f), 0f, 0.22f, 71);
            var stone = CreateNaturalMaterial("P81_Stone", new Color(0.30f, 0.31f, 0.28f), 0.03f, 0.34f, 97);
            var skin = CreateNaturalMaterial("P81_Skin", new Color(0.50f, 0.31f, 0.20f), 0f, 0.32f, 131);
            var cloth = CreateNaturalMaterial("P81_Cloth", new Color(0.27f, 0.20f, 0.11f), 0f, 0.16f, 157);
            var hair = CreateNaturalMaterial("P81_Hair", new Color(0.11f, 0.055f, 0.026f), 0f, 0.20f, 181);
            var water = CreateWaterMaterial();

            var root = new GameObject(RootName);
            root.transform.position = new Vector3(-27f, 0.08f, 24f);
            var environment = new List<Renderer>();
            var trunks = new List<Renderer>();

            var meadowMesh = CreateLandscapeMesh(MeshFolder + "/P81_Meadow.asset", -10f, 10f, -10f, 10f,
                (x, z) => 0.16f * Mathf.Sin(x * 0.55f) + 0.12f * Mathf.Cos(z * 0.43f) + 0.025f * x);
            environment.Add(CreateMeshObject("P81 Meadow Land", root.transform, meadowMesh, grass).GetComponent<Renderer>());

            var shoreMesh = CreateLandscapeMesh(MeshFolder + "/P81_Shore.asset", -14f, -8f, -10f, 10f,
                (x, z) => -0.18f + 0.035f * (x + 14f) + 0.07f * Mathf.Sin(z * 0.42f));
            environment.Add(CreateMeshObject("P81 Natural Shore", root.transform, shoreMesh, sand).GetComponent<Renderer>());

            var waterObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterObject.name = "P81 Calm Water";
            waterObject.transform.SetParent(root.transform, false);
            waterObject.transform.localPosition = new Vector3(-15.5f, -0.24f, 0f);
            waterObject.transform.localScale = new Vector3(1.35f, 1f, 2.25f);
            waterObject.GetComponent<Renderer>().sharedMaterial = water;
            UnityEngine.Object.DestroyImmediate(waterObject.GetComponent<Collider>());
            environment.Add(waterObject.GetComponent<Renderer>());

            var treePositions = new[]
            {
                new Vector3(4.8f,0.15f,-6.2f), new Vector3(7.2f,0.08f,-2.5f),
                new Vector3(5.7f,0.12f,2.1f), new Vector3(8.5f,0.22f,6.0f),
                new Vector3(2.8f,0.1f,7.4f)
            };
            for (var i = 0; i < treePositions.Length; i++)
                BuildTree(root.transform, treePositions[i], 0.9f + i * 0.07f, bark, leaf, environment, trunks);

            var rockPositions = new[]
            {
                new Vector3(-7.6f,0.08f,-6.4f), new Vector3(-8.7f,0.04f,-1.8f),
                new Vector3(-7.9f,0.12f,3.6f), new Vector3(-6.8f,0.05f,7.0f)
            };
            for (var i = 0; i < rockPositions.Length; i++)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "P81 Shore Stone " + (i + 1);
                rock.transform.SetParent(root.transform, false);
                rock.transform.localPosition = rockPositions[i];
                rock.transform.localRotation = Quaternion.Euler(12f + i * 9f, i * 51f, 5f - i * 3f);
                rock.transform.localScale = new Vector3(1.2f + i * 0.18f, 0.65f + i * 0.09f, 0.9f + i * 0.12f);
                rock.GetComponent<Renderer>().sharedMaterial = stone;
                environment.Add(rock.GetComponent<Renderer>());
            }

            var center = new GameObject("P81 Benchmark Camera Focus").transform;
            center.SetParent(root.transform, false);
            center.localPosition = new Vector3(-0.5f, 1.2f, 0f);
            var fallback = BuildFallbackCharacter(root.transform, skin, cloth, hair);
            fallback.transform.localPosition = new Vector3(-1.0f, 0.22f, 0f);
            fallback.transform.localRotation = Quaternion.Euler(0f, 205f, 0f);

            var uma = InstantiatePackagePrefab(
                "Assets/Game/Art/ThirdParty/UMA3/Getting Started/UMADynamicCharacterAvatar-NoPhysics_NoLOD.prefab",
                root.transform, "P81 UMA Character Prototype");
            if (uma != null)
            {
                uma.transform.localPosition = fallback.transform.localPosition;
                uma.transform.localRotation = fallback.transform.localRotation;
            }
            BuildLighting(root.transform);
            var presentation = root.AddComponent<P81VisualPrototypePresentation>();
            presentation.Configure(center, uma, fallback, environment, trunks);
            presentation.ValidateConfiguration();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"P81_VISUAL_PROTOTYPE_BUILT provider={(uma != null ? "UMA3" : "fallback")}; renderers={environment.Count}");
        }

        private static void EnsureProjectTag(string tag)
        {
            var manager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            if (manager == null) throw new InvalidOperationException("Unity TagManager asset is unavailable.");
            var serialized = new SerializedObject(manager);
            var tags = serialized.FindProperty("tags");
            for (var i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void BuildTree(Transform parent, Vector3 position, float scale, Material bark, Material leaf,
            ICollection<Renderer> environment, ICollection<Renderer> trunks)
        {
            var tree = new GameObject("P81 Natural Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = position;
            tree.transform.localRotation = Quaternion.Euler(0f, position.x * 17f + position.z * 11f, 0f);
            tree.transform.localScale = Vector3.one * scale;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Brown Bark Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            trunk.transform.localScale = new Vector3(0.34f, 2.15f, 0.34f);
            trunk.GetComponent<Renderer>().sharedMaterial = bark;
            trunks.Add(trunk.GetComponent<Renderer>());
            environment.Add(trunk.GetComponent<Renderer>());
            for (var j = 0; j < 4; j++)
            {
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "Irregular Forest Canopy";
                crown.transform.SetParent(tree.transform, false);
                crown.transform.localPosition = new Vector3((j - 1.5f) * 0.55f, 4.2f + (j % 2) * 0.55f,
                    (j % 2 == 0 ? -0.45f : 0.5f));
                crown.transform.localScale = new Vector3(2.0f, 1.7f + (j % 2) * 0.25f, 1.85f);
                crown.GetComponent<Renderer>().sharedMaterial = leaf;
                environment.Add(crown.GetComponent<Renderer>());
            }
        }

        private static GameObject BuildFallbackCharacter(Transform parent, Material skin, Material cloth, Material hair)
        {
            var root = new GameObject("P81 Project Character Fallback");
            root.transform.SetParent(parent, false);
            AddPart(root.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, 1.15f, 0f),
                new Vector3(0.52f, 0.68f, 0.38f), cloth);
            AddPart(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 2.18f, 0f),
                new Vector3(0.48f, 0.56f, 0.46f), skin);
            AddPart(root.transform, PrimitiveType.Sphere, "Short Brown Hair", new Vector3(0f, 2.48f, -0.01f),
                new Vector3(0.49f, 0.25f, 0.47f), hair);
            AddPart(root.transform, PrimitiveType.Capsule, "Left Arm", new Vector3(-0.48f, 1.28f, 0f),
                new Vector3(0.18f, 0.56f, 0.18f), skin, new Vector3(0f, 0f, -7f));
            AddPart(root.transform, PrimitiveType.Capsule, "Right Arm", new Vector3(0.48f, 1.28f, 0f),
                new Vector3(0.18f, 0.56f, 0.18f), skin, new Vector3(0f, 0f, 7f));
            AddPart(root.transform, PrimitiveType.Capsule, "Left Leg", new Vector3(-0.2f, 0.43f, 0f),
                new Vector3(0.21f, 0.48f, 0.22f), cloth);
            AddPart(root.transform, PrimitiveType.Capsule, "Right Leg", new Vector3(0.2f, 0.43f, 0f),
                new Vector3(0.21f, 0.48f, 0.22f), cloth);
            return root;
        }

        private static void AddPart(Transform parent, PrimitiveType primitive, string name, Vector3 position,
            Vector3 scale, Material material, Vector3 rotation = default)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(rotation);
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static void BuildLighting(Transform parent)
        {
            var key = new GameObject("P81 Warm Key Light").AddComponent<Light>();
            key.transform.SetParent(parent, false);
            key.transform.localPosition = new Vector3(-2f, 12f, -4f);
            key.type = LightType.Spot;
            key.color = new Color(1f, 0.82f, 0.62f);
            key.intensity = 220f;
            key.range = 32f;
            key.spotAngle = 105f;
            key.transform.localRotation = Quaternion.Euler(58f, 18f, 0f);
            key.shadows = LightShadows.Soft;
            var probe = new GameObject("P81 Reflection Probe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(parent, false);
            probe.transform.localPosition = new Vector3(-1f, 3f, 0f);
            probe.size = new Vector3(24f, 10f, 22f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        }

        private static GameObject InstantiatePackagePrefab(string path, Transform parent, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            return instance;
        }

        private static void RebuildUmaLibrary()
        {
            var slotPath = "Assets/Game/Art/ThirdParty/UMA3/Races/Slots/UMA30_Body/" +
                "UMA30_Body_UDIM1001_slot.asset";
            var slotObject = AssetDatabase.LoadMainAssetAtPath(slotPath);
            var meshData = slotObject?.GetType().GetField("meshData", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(slotObject);
            var vertexCount = meshData?.GetType().GetField("vertexCount", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(meshData);
            Debug.Log($"P81_UMA_CONTENT_AUDIT slots={AssetDatabase.FindAssets("t:SlotDataAsset").Length}; " +
                $"races={AssetDatabase.FindAssets("t:RaceData").Length}; sampleType={slotObject?.GetType().FullName ?? "null"}; " +
                $"sampleVertices={vertexCount ?? "null"}");
            var type = Type.GetType("UMA.UMAAssetIndexer, UMA_Core");
            var property = type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var indexer = property?.GetValue(null);
            var rebuild = type?.GetMethod("RebuildLibrary", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (indexer == null || rebuild == null)
                throw new InvalidOperationException("UMA Global Library API is unavailable.");
            rebuild.Invoke(indexer, null);
            var forceAdd = type.GetMethod("EvilAddAsset", BindingFlags.Public | BindingFlags.Instance);
            var forced = 0;
            foreach (var filter in new[] { "SlotDataAsset", "OverlayDataAsset", "RaceData", "UMATextRecipe",
                         "UMAWardrobeRecipe", "UMAMaterial", "DynamicUMADnaAsset", "DNAConverterController",
                         "UMABonePose", "MeshHideAsset" })
            foreach (var guid in AssetDatabase.FindAssets("t:" + filter))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/Game/Art/ThirdParty/UMA3/", StringComparison.Ordinal)) continue;
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                try
                {
                    forceAdd?.Invoke(indexer, new object[] { asset.GetType(), asset });
                    forced++;
                }
                catch (TargetInvocationException exception) when (exception.InnerException is KeyNotFoundException)
                {
                    // Not every discoverable helper asset is an indexed UMA runtime type.
                }
            }
            var addReferences = type.GetMethod("AddReferences", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            addReferences?.Invoke(indexer, null);
            Debug.Log("P81_UMA_LIBRARY_FORCED_REFERENCES " + forced);
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var value = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            value.transform.SetParent(parent, false);
            value.GetComponent<MeshFilter>().sharedMesh = mesh;
            value.GetComponent<MeshRenderer>().sharedMaterial = material;
            value.GetComponent<MeshCollider>().sharedMesh = mesh;
            return value;
        }

        private static Mesh CreateLandscapeMesh(string path, float minX, float maxX, float minZ, float maxZ,
            Func<float, float, float> height)
        {
            const int resolution = 32;
            var vertices = new Vector3[(resolution + 1) * (resolution + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[resolution * resolution * 6];
            for (var z = 0; z <= resolution; z++)
            for (var x = 0; x <= resolution; x++)
            {
                var px = Mathf.Lerp(minX, maxX, x / (float)resolution);
                var pz = Mathf.Lerp(minZ, maxZ, z / (float)resolution);
                var index = z * (resolution + 1) + x;
                vertices[index] = new Vector3(px, height(px, pz), pz);
                uvs[index] = new Vector2(x / 4f, z / 4f);
            }
            var t = 0;
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var a = z * (resolution + 1) + x;
                var b = a + 1;
                var c = a + resolution + 1;
                var d = c + 1;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateNaturalMaterial(string name, Color color, float metallic,
            float smoothness, int seed)
        {
            var albedo = CreateNoiseTexture(TextureFolder + "/" + name + "_Albedo.asset", color, seed, false);
            var normal = CreateNoiseTexture(TextureFolder + "/" + name + "_Normal.asset", Color.white, seed + 1, true);
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.color = color;
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", new Vector2(3.5f, 3.5f));
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.42f);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateNoiseTexture(string path, Color color, int seed, bool normal)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(128, 128, TextureFormat.RGBA32, true) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(texture, path);
            }
            var random = new System.Random(seed);
            var pixels = new Color[128 * 128];
            for (var y = 0; y < 128; y++)
            for (var x = 0; x < 128; x++)
            {
                var broad = Mathf.PerlinNoise((x + seed) * 0.045f, (y + seed) * 0.045f);
                var grain = (float)random.NextDouble();
                if (normal)
                {
                    var nx = Mathf.Clamp01(0.5f + (broad - 0.5f) * 0.28f + (grain - 0.5f) * 0.10f);
                    var ny = Mathf.Clamp01(0.5f + (Mathf.PerlinNoise(y * 0.06f, x * 0.06f) - 0.5f) * 0.28f);
                    pixels[y * 128 + x] = new Color(nx, ny, 1f, 1f);
                }
                else
                {
                    var value = 0.76f + broad * 0.24f + (grain - 0.5f) * 0.07f;
                    // The material owns the tint. Keeping albedo neutral avoids multiplying a dark
                    // natural colour by itself and preserves readable mid-tones in the Player.
                    pixels[y * 128 + x] = new Color(value, value, value, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            texture.Apply(true, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Material CreateWaterMaterial()
        {
            var path = MaterialFolder + "/P81_CalmWater.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "P81_CalmWater" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.color = new Color(0.10f, 0.27f, 0.29f, 0.78f);
            material.SetFloat("_Smoothness", 0.88f);
            material.SetFloat("_Metallic", 0.04f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
