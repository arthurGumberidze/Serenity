using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Presentation.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure.Editor
{
    public static class U05AAssetFoundation
    {
        public const string ValidationScenePath = "Assets/Scenes/Development/StoneAgeAssetGallery.unity";

        public static readonly string[] RequiredPrefabPaths =
        {
            "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_A_Tier1.prefab",
            "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_B_Tier1.prefab",
            "Assets/Game/Art/Prefabs/Characters/Serenity_Humanoid_Tier2.prefab",
            "Assets/Game/Art/Prefabs/Animals/Serenity_Boar.prefab",
            "Assets/Game/Art/Prefabs/Buildings/Serenity_PrimitiveShelter.prefab",
            "Assets/Game/Art/Prefabs/Environment/Serenity_Tree.prefab",
            "Assets/Game/Art/Prefabs/Environment/Serenity_Rock.prefab",
            "Assets/Game/Art/Prefabs/Props/Serenity_Campfire.prefab",
            "Assets/Game/Art/Prefabs/Props/Serenity_StorageBasket.prefab",
            "Assets/Game/Art/Prefabs/Tools/Serenity_Torch.prefab",
            "Assets/Game/Art/Prefabs/Weapons/Serenity_StoneAxe.prefab",
            "Assets/Game/Art/Prefabs/Weapons/Serenity_StoneSpear.prefab"
        };

        private const string CharacterA = "Assets/Hodaart/HodaartLowPolyCharacterCollection3/Prefabs/Character 01.prefab";
        private const string CharacterB = "Assets/Hodaart/HodaartLowPolyCharacterCollection3/Prefabs/Character 02.prefab";
        private const string Tree = "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageNature/Models/Tree_01.fbx";
        private const string Rock = "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageNature/Models/Rock_01.fbx";
        private const string Campfire = "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageNature/Models/Campfire_01.fbx";
        private const string Axe = "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageWeapons/Models/StoneAxe.fbx";
        private const string Spear = "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageWeapons/Models/StoneSpear.fbx";
        private const string Torch = "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageWeapons/Models/Torch.fbx";
        private const string Boar = "Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageWildHunt/Models/Boar_Animations_V4.fbx";
        private const string HumanoidAnimationRoot = "Assets/Hodaart/HodaartLowPolyCharacterCollection3/Animations/";

        private static readonly ModelRule[] ModelRules =
        {
            new ModelRule(Tree, 6f, Metric.Height, ModelImporterAnimationType.None),
            new ModelRule(Rock, 1.4f, Metric.MaxDimension, ModelImporterAnimationType.None),
            new ModelRule(Campfire, 0.8f, Metric.MaxDimension, ModelImporterAnimationType.None),
            new ModelRule(Axe, 0.7f, Metric.MaxDimension, ModelImporterAnimationType.None),
            new ModelRule(Spear, 1.8f, Metric.MaxDimension, ModelImporterAnimationType.None),
            new ModelRule(Torch, 0.8f, Metric.MaxDimension, ModelImporterAnimationType.None),
            new ModelRule(Boar, 1.8f, Metric.MaxDimension, ModelImporterAnimationType.Generic)
        };

        [MenuItem("Serenity/U05A/Build Asset Foundation")]
        public static void Build()
        {
            EnsureRequiredSources();
            EnsureDirectories();
            foreach (var rule in ModelRules)
                NormalizeImporter(rule);

            var tier2 = CreateMaterial("Assets/Game/Art/Materials/Character_Tier2.mat", new Color(0.46f, 0.33f, 0.22f));
            var foliage = CreateMaterial("Assets/Game/Art/Materials/Nature_Foliage.mat", new Color(0.20f, 0.38f, 0.17f));
            var stone = CreateMaterial("Assets/Game/Art/Materials/Nature_Stone.mat", new Color(0.34f, 0.36f, 0.35f));
            var wood = CreateMaterial("Assets/Game/Art/Materials/Primitive_Wood.mat", new Color(0.32f, 0.19f, 0.10f));
            var fire = CreateMaterial("Assets/Game/Art/Materials/Campfire_Ember.mat", new Color(0.72f, 0.23f, 0.05f), true);
            var hide = CreateMaterial("Assets/Game/Art/Materials/Primitive_Hide.mat", new Color(0.34f, 0.20f, 0.12f));
            var boar = CreateMaterial("Assets/Game/Art/Materials/Animal_Boar.mat", new Color(0.25f, 0.16f, 0.11f));
            var ground = CreateMaterial("Assets/Game/Art/Materials/Gallery_Ground.mat", new Color(0.29f, 0.34f, 0.22f));

            CreateHumanoidPrefab(CharacterA, RequiredPrefabPaths[0]);
            CreateHumanoidPrefab(CharacterB, RequiredPrefabPaths[1]);
            CreateTier2Humanoid(RequiredPrefabPaths[2], tier2);
            CreateAnimalPrefab(Boar, RequiredPrefabPaths[3], boar);
            CreateShelter(RequiredPrefabPaths[4], wood, hide);
            CreateModelPrefab(Tree, RequiredPrefabPaths[5], foliage, PivotMode.Ground, true);
            CreateModelPrefab(Rock, RequiredPrefabPaths[6], stone, PivotMode.Ground, true);
            CreateModelPrefab(Campfire, RequiredPrefabPaths[7], fire, PivotMode.Ground, false);
            CreateStorage(RequiredPrefabPaths[8], wood);
            CreateModelPrefab(Torch, RequiredPrefabPaths[9], wood, PivotMode.Center, false);
            CreateModelPrefab(Axe, RequiredPrefabPaths[10], stone, PivotMode.Center, false);
            CreateModelPrefab(Spear, RequiredPrefabPaths[11], stone, PivotMode.Center, false);
            if (AssetDatabase.LoadAssetAtPath<Game.Presentation.Buildings.BuildingPresentationCatalog>(
                    U05LocalSceneBuilder.BuildingCatalogPath) != null)
                U08BuildingFoundation.RefreshCatalogAndPrefabs();
            BuildValidationScene(ground);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteValidationReport();
            Debug.Log("U05A asset foundation generated successfully.");
        }

        public static void Validate()
        {
            EnsureRequiredSources();
            foreach (var path in RequiredPrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    throw new FileNotFoundException("Required U05A prefab is missing.", path);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ValidationScenePath) == null)
                throw new FileNotFoundException("U05A validation scene is missing.", ValidationScenePath);

            WriteValidationReport();
            Debug.Log("U05A asset validation completed successfully.");
        }

        public static void CaptureValidationScene()
        {
            var scene = EditorSceneManager.OpenScene(ValidationScenePath, OpenSceneMode.Single);
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if (camera == null)
                throw new InvalidOperationException("Validation scene has no camera.");

            Directory.CreateDirectory("Logs");
            CaptureView(camera, "Logs/U05A-gallery-overview.png", new Vector3(17f, 12f, -20f), new Vector3(0f, 1.5f, 2f));
            CaptureView(camera, "Logs/U05A-gallery-characters.png", new Vector3(-7f, 4.5f, -11f), new Vector3(-5f, 1.1f, 0f));
            CaptureView(camera, "Logs/U05A-gallery-environment.png", new Vector3(14f, 8f, -11f), new Vector3(6f, 1.8f, 0f));
            Debug.Log("U05A validation gallery captured to Logs/U05A-gallery-*.png.");
        }

        private static void CaptureView(Camera camera, string path, Vector3 position, Vector3 target)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
            var renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

        }

        private static void EnsureRequiredSources()
        {
            foreach (var path in ModelRules.Select(rule => rule.Path).Concat(new[] { CharacterA, CharacterB }))
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    throw new FileNotFoundException("Required source asset is missing or not imported.", path);
            }
        }

        private static void EnsureDirectories()
        {
            var directories = RequiredPrefabPaths.Select(Path.GetDirectoryName)
                .Concat(new[] { "Assets/Game/Art/Materials", Path.GetDirectoryName(ValidationScenePath) })
                .Distinct();
            foreach (var directory in directories)
                Directory.CreateDirectory(directory);
        }

        private static void NormalizeImporter(ModelRule rule)
        {
            var importer = AssetImporter.GetAtPath(rule.Path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Expected a ModelImporter for " + rule.Path);

            importer.importAnimation = rule.AnimationType != ModelImporterAnimationType.None;
            importer.animationType = rule.AnimationType;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = rule.AnimationType != ModelImporterAnimationType.None;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.useFileScale = true;
            importer.SaveAndReimport();

            var current = MeasureModel(rule.Path, rule.Metric);
            if (current <= 0.0001f)
                throw new InvalidDataException("Imported model has no measurable render bounds: " + rule.Path);

            var adjusted = importer.globalScale * rule.TargetSize / current;
            if (Mathf.Abs(adjusted - importer.globalScale) > 0.0001f)
            {
                importer.globalScale = adjusted;
                importer.SaveAndReimport();
            }
        }

        private static float MeasureModel(string path, Metric metric)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = UnityEngine.Object.Instantiate(asset);
            try
            {
                var bounds = CalculateBounds(instance);
                return metric == Metric.Height ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Material CreateMaterial(string path, Color color, bool emission = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit shader is unavailable.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.18f);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.4f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateHumanoidPrefab(string sourcePath, string prefabPath)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "SourceVisual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                var sourceBounds = CalculateBounds(visual);
                visual.transform.localScale = Vector3.one * (1.8f / sourceBounds.size.y);
                AlignToGround(visual);
                var bounds = CalculateBounds(root);
                var collider = root.AddComponent<CapsuleCollider>();
                collider.height = Mathf.Max(0.5f, bounds.size.y);
                collider.radius = Mathf.Max(0.15f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.45f);
                collider.center = new Vector3(0f, collider.height * 0.5f, 0f);

                var animator = visual.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidDataException("Humanoid wrapper requires a valid humanoid Animator and Avatar: " + sourcePath);
                animator.runtimeAnimatorController = CreateHumanoidController();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var rightHand = CreateSocket(animator, HumanBodyBones.RightHand, "RightHandSocket", root.transform);
                var leftHand = CreateSocket(animator, HumanBodyBones.LeftHand, "LeftHandSocket", root.transform);
                var presenter = root.AddComponent<CharacterPresenter>();
                presenter.Configure(animator, rightHand, leftHand);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform CreateSocket(Animator animator, HumanBodyBones bone, string name, Transform fallback)
        {
            var parent = animator.GetBoneTransform(bone) ?? fallback;
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            return socket;
        }

        private static RuntimeAnimatorController CreateHumanoidController()
        {
            const string controllerPath = "Assets/Game/Art/Animation/CharacterTier1.controller";
            Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            foreach (var parameter in controller.parameters.ToArray())
                controller.RemoveParameter(parameter);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;
            foreach (var childState in stateMachine.states.ToArray())
                stateMachine.RemoveState(childState.state);

            var idle = stateMachine.AddState("Idle");
            idle.motion = RequireAnimationClip("Idle.anim");
            var walking = stateMachine.AddState("Walking");
            walking.motion = RequireAnimationClip("Walking.anim");
            var running = stateMachine.AddState("Running");
            running.motion = RequireAnimationClip("Running.anim");
            stateMachine.defaultState = idle;

            AddSpeedTransition(idle, walking, AnimatorConditionMode.Greater, 0.05f);
            AddSpeedTransition(walking, idle, AnimatorConditionMode.Less, 0.05f);
            AddSpeedTransition(walking, running, AnimatorConditionMode.Greater, 1.5f);
            AddSpeedTransition(running, walking, AnimatorConditionMode.Less, 1.5f);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip RequireAnimationClip(string fileName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HumanoidAnimationRoot + fileName);
            if (clip == null) throw new FileNotFoundException("Required humanoid animation is missing.", fileName);
            return clip;
        }

        private static void AddSpeedTransition(AnimatorState from, AnimatorState to,
            AnimatorConditionMode mode, float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.AddCondition(mode, threshold, "Speed");
        }

        private static void CreateTier2Humanoid(string prefabPath, Material material)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var body = CreatePrimitiveChild(root, PrimitiveType.Capsule, "SingleMeshBody", new Vector3(0f, 0.9f, 0f), new Vector3(0.55f, 0.9f, 0.55f), material);
                body.transform.localRotation = Quaternion.identity;
                var collider = root.AddComponent<CapsuleCollider>();
                collider.height = 1.8f;
                collider.radius = 0.3f;
                collider.center = new Vector3(0f, 0.9f, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateModelPrefab(string sourcePath, string prefabPath, Material material, PivotMode pivot, bool colliderRequired)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "SourceVisual";
                visual.transform.SetParent(root.transform, false);
                if (pivot == PivotMode.Ground)
                    AlignToGround(visual);
                else
                    CenterAtOrigin(visual);
                AssignMaterial(visual, material);
                if (colliderRequired)
                {
                    var bounds = CalculateBounds(root);
                    var collider = root.AddComponent<BoxCollider>();
                    collider.center = bounds.center;
                    collider.size = bounds.size;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateAnimalPrefab(string sourcePath, string prefabPath, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "SourceVisual";
                visual.transform.SetParent(root.transform, false);
                AlignToGround(visual);
                AssignMaterial(visual, material);

                var animator = visual.GetComponent<Animator>();
                if (animator == null)
                    animator = visual.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = CreateAnimalController(sourcePath);
                animator.applyRootMotion = false;

                var bounds = CalculateBounds(root);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RuntimeAnimatorController CreateAnimalController(string sourcePath)
        {
            const string controllerPath = "Assets/Game/Art/Animation/Boar.controller";
            Directory.CreateDirectory(Path.GetDirectoryName(controllerPath));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            var stateMachine = controller.layers[0].stateMachine;
            foreach (var childState in stateMachine.states.ToArray())
                stateMachine.RemoveState(childState.state);

            var clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .OrderBy(clip => clip.name).ToArray();
            AnimatorState defaultState = null;
            foreach (var clip in clips)
            {
                var state = stateMachine.AddState(clip.name);
                state.motion = clip;
                if (defaultState == null || clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)
                    defaultState = state;
            }
            stateMachine.defaultState = defaultState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void CreateShelter(string prefabPath, Material wood, Material hide)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                CreatePrimitiveChild(root, PrimitiveType.Cube, "Back Wall", new Vector3(0f, 1.25f, 1.5f), new Vector3(4f, 2.5f, 0.25f), hide);
                CreatePrimitiveChild(root, PrimitiveType.Cube, "Left Wall", new Vector3(-1.9f, 1.25f, 0f), new Vector3(0.25f, 2.5f, 3f), hide);
                CreatePrimitiveChild(root, PrimitiveType.Cube, "Right Wall", new Vector3(1.9f, 1.25f, 0f), new Vector3(0.25f, 2.5f, 3f), hide);
                var roofA = CreatePrimitiveChild(root, PrimitiveType.Cube, "Roof A", new Vector3(-1.05f, 2.8f, 0f), new Vector3(2.6f, 0.18f, 3.5f), wood);
                roofA.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
                var roofB = CreatePrimitiveChild(root, PrimitiveType.Cube, "Roof B", new Vector3(1.05f, 2.8f, 0f), new Vector3(2.6f, 0.18f, 3.5f), wood);
                roofB.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateStorage(string prefabPath, Material material)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                CreatePrimitiveChild(root, PrimitiveType.Cube, "Basket Body", new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.9f, 0.8f), material);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.45f, 0f);
                collider.size = new Vector3(1.1f, 0.9f, 0.8f);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePrimitiveChild(GameObject root, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            var child = GameObject.CreatePrimitive(type);
            child.name = name;
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            var collider = child.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            AssignMaterial(child, material);
            return child;
        }

        private static void BuildValidationScene(Material groundMaterial)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "StoneAgeAssetGallery";

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Gallery Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 2.2f);
            AssignMaterial(ground, groundMaterial);

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.48f);

            var cameraObject = new GameObject("Gallery Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.fieldOfView = 48f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(17f, 12f, -20f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.5f, 2f));

            var positions = new[]
            {
                new Vector3(-10f, 0f, -3f), new Vector3(-7f, 0f, -3f), new Vector3(-4f, 0f, -3f),
                new Vector3(0f, 0f, -3f), new Vector3(6f, 0f, 1f), new Vector3(10f, 0f, 2f),
                new Vector3(7f, 0f, -5f), new Vector3(3f, 0f, -5f), new Vector3(0f, 0f, 3f),
                new Vector3(-3f, 0.8f, 3f), new Vector3(-6f, 0.8f, 3f), new Vector3(-9f, 0.8f, 3f)
            };
            for (var i = 0; i < RequiredPrefabPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RequiredPrefabPaths[i]);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = positions[i];
            }

            EditorSceneManager.SaveScene(scene, ValidationScenePath);
        }

        private static void WriteValidationReport()
        {
            Directory.CreateDirectory("Logs");
            var lines = new List<string>
            {
                "U05A asset validation report",
                "Generated: " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                "Unity units: 1 unit = 1 meter",
                "Validation scene: " + ValidationScenePath,
                string.Empty
            };

            foreach (var path in RequiredPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                var meshes = prefab.GetComponentsInChildren<MeshFilter>(true).Select(item => item.sharedMesh)
                    .Concat(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(item => item.sharedMesh))
                    .Where(mesh => mesh != null).Distinct().ToArray();
                var triangles = meshes.Sum(mesh => mesh.triangles.Length / 3);
                var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().Count();
                var animator = prefab.GetComponentInChildren<Animator>(true);
                var avatar = animator != null ? animator.avatar : null;
                var instance = UnityEngine.Object.Instantiate(prefab);
                var bounds = CalculateBounds(instance);
                UnityEngine.Object.DestroyImmediate(instance);
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} | size={1:0.###}x{2:0.###}x{3:0.###} renderers={4} meshes={5} triangles={6} materials={7} animator={8} avatarValid={9} avatarHuman={10} missingScripts={11}",
                    path, bounds.size.x, bounds.size.y, bounds.size.z,
                    renderers.Length, meshes.Length, triangles, materials, animator != null,
                    avatar != null && avatar.isValid, avatar != null && avatar.isHuman, CountMissingScripts(prefab)));
            }

            var boarClips = AssetDatabase.LoadAllAssetsAtPath(Boar).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .Select(clip => clip.name).OrderBy(name => name);
            lines.Add(string.Empty);
            lines.Add("Boar clips: " + string.Join(", ", boarClips));

            File.WriteAllLines("Logs/U05A-asset-report.txt", lines);
        }

        private static int CountMissingScripts(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
        }

        private static void AssignMaterial(GameObject root, Material material)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var count = Math.Max(1, renderer.sharedMaterials.Length);
                renderer.sharedMaterials = Enumerable.Repeat(material, count).ToArray();
            }
        }

        private static void AlignToGround(GameObject visual)
        {
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            var bounds = CalculateBounds(visual);
            visual.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        }

        private static void CenterAtOrigin(GameObject visual)
        {
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            var bounds = CalculateBounds(visual);
            visual.transform.localPosition = -bounds.center;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private enum Metric
        {
            Height,
            MaxDimension
        }

        private enum PivotMode
        {
            Ground,
            Center
        }

        private readonly struct ModelRule
        {
            public ModelRule(string path, float targetSize, Metric metric, ModelImporterAnimationType animationType)
            {
                Path = path;
                TargetSize = targetSize;
                Metric = metric;
                AnimationType = animationType;
            }

            public string Path { get; }
            public float TargetSize { get; }
            public Metric Metric { get; }
            public ModelImporterAnimationType AnimationType { get; }
        }
    }
}
