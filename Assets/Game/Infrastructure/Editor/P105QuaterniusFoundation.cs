#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Infrastructure.Editor
{
    public static class P105QuaterniusFoundation
    {
        private const string Root = "Assets/Game/Art/ThirdParty/Quaternius/P105/";
        private const string OutputRoot = "Assets/Game/Art/P105/";
        private const string ControllerPath = "Assets/Game/Art/Animation/CharacterTier1.controller";
        private const string CatalogPath = "Assets/Game/Art/Config/CharacterPresentationCatalog.asset";
        private const string HodaAnimationRoot = "Assets/Hodaart/HodaartLowPolyCharacterCollection3/Animations/";

        private static readonly string[] ModelPaths =
        {
            Root + "Models/Female_Peasant.fbx", Root + "Models/Female_Ranger.fbx",
            Root + "Models/Male_Peasant.fbx", Root + "Models/Male_Ranger.fbx"
        };

        private static readonly string[] BaseModelPaths =
        {
            Root + "BaseCharacters/Female_Base.fbx", Root + "BaseCharacters/Male_Base.fbx"
        };

        private static readonly string[] AnimationPaths =
        {
            Root + "Animations/UAL1_Standard.fbx", Root + "Animations/UAL2_Standard.fbx"
        };

        private static readonly HashSet<string> LoopingClips = new HashSet<string>(StringComparer.Ordinal)
        {
            "Idle_Loop", "Walk_Loop", "Sprint_Loop", "Walk_Carry_Loop", "Farm_Harvest",
            "TreeChopping_Loop", "Fixing_Kneeling", "Idle_Rail_Loop", "Sword_Regular_Combo"
        };

        [MenuItem("Serenity/P10.5/Build Quaternius Character Content")]
        public static void Build()
        {
            foreach (var path in ModelPaths) ConfigureHumanoidImporter(path, false);
            foreach (var path in BaseModelPaths) ConfigureHumanoidImporter(path, false);
            foreach (var path in AnimationPaths) ConfigureHumanoidImporter(path, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Directory.CreateDirectory(OutputRoot + "Materials");
            Directory.CreateDirectory(OutputRoot + "Prefabs");
            Directory.CreateDirectory(OutputRoot + "Meshes");
            var worker = CreateMaterial("P105_Worker", Root + "Textures/Peasant/T_Peasant_BaseColor.png");
            var military = CreateMaterial("P105_Military", Root + "Textures/Ranger/T_Ranger_BaseColor.png");
            var femaleSkin = CreateMaterial("P105_FemaleSkin", Root + "Textures/Base/T_Regular_Female_Dark_BaseColor.png");
            var maleSkin = CreateMaterial("P105_MaleSkin", Root + "Textures/Base/T_Regular_Male_Dark_BaseColor.png");
            var femaleHead = CreateMaterial("P105_FemaleHead", Root + "Textures/Base/T_Superhero_Female_Dark_BaseColor.png");
            var maleHead = CreateMaterial("P105_MaleHead", Root + "Textures/Base/T_Superhero_Male_Dark.png");
            var femaleHair = CreateMaterial("P105_FemaleHair", Root + "Textures/Base/T_Hair_2_BaseColor.png");
            var maleHair = CreateMaterial("P105_MaleHair", Root + "Textures/Base/T_Hair_1_BaseColor.png");
            var eyes = CreateMaterial("P105_Eyes", Root + "Textures/Base/T_Eye_Brown.png");
            var controller = CreateController();

            var femaleWorker = CreateRoleVisual(ModelPaths[0], BaseModelPaths[0], "Female_Worker",
                worker, femaleSkin, femaleHead, femaleHair, eyes, controller);
            var femaleMilitary = CreateRoleVisual(ModelPaths[1], BaseModelPaths[0], "Female_Military",
                military, femaleSkin, femaleHead, femaleHair, eyes, controller);
            var maleWorker = CreateRoleVisual(ModelPaths[2], BaseModelPaths[1], "Male_Worker",
                worker, maleSkin, maleHead, maleHair, eyes, controller);
            var maleMilitary = CreateRoleVisual(ModelPaths[3], BaseModelPaths[1], "Male_Military",
                military, maleSkin, maleHead, maleHair, eyes, controller);

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(CatalogPath);
            if (catalog == null) throw new FileNotFoundException("Character presentation catalog is missing.", CatalogPath);
            catalog.ConfigureRoleVisuals(maleWorker, femaleWorker, maleMilitary, femaleMilitary);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("P105_QUATERNIUS_BUILD_OK rolePrefabs=4; authoredActions=9; license=CC0");
        }

        public static void InspectImportedAssets()
        {
            foreach (var path in ModelPaths.Concat(BaseModelPaths))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
                Debug.Log($"P105_IMPORT model={path}; animationType={importer?.animationType}; " +
                          $"avatar={(animator != null && animator.avatar != null)}");
                if (prefab != null)
                    Debug.Log("P105_BONES model=" + path + "; " +
                              string.Join("|", prefab.GetComponentsInChildren<Transform>(true).Select(x => x.name)));
                if (prefab != null)
                    foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                        Debug.Log("P105_RENDERER model=" + path + "; renderer=" + renderer.name + "; materials=" +
                                  string.Join("|", renderer.sharedMaterials.Select(x => x != null ? x.name : "<null>")));
            }
            foreach (var animationPath in AnimationPaths)
            {
                var animationImporter = AssetImporter.GetAtPath(animationPath) as ModelImporter;
                Debug.Log($"P105_IMPORT animations={animationPath}; type={animationImporter?.animationType}; clips=" +
                          string.Join("|", AssetDatabase.LoadAllAssetsAtPath(animationPath)
                              .OfType<AnimationClip>().Where(x => !x.name.StartsWith("__preview__")).Select(x => x.name)));
            }
        }

        public static void Validate()
        {
            foreach (var path in ModelPaths.Concat(BaseModelPaths))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidDataException("P10.5 source model has no valid humanoid avatar: " + path);
            }
            foreach (var name in new[] { "Female_Worker", "Female_Military", "Male_Worker", "Male_Military" })
            {
                var path = OutputRoot + "Prefabs/" + name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
                if (animator == null || !animator.isHuman || animator.runtimeAnimatorController == null)
                    throw new InvalidDataException("P10.5 role prefab is invalid: " + path);
                if (prefab.transform.Find("SourceVisual/P105_Head") == null ||
                    prefab.transform.Find("SourceVisual/P105_Eyes") == null)
                    throw new InvalidDataException("P10.5 role prefab is missing its composed head: " + path);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || controller.parameters.All(x => x.name != "ActionIntent"))
                throw new InvalidDataException("P10.5 authored action controller is missing.");
            var clipNames = controller.animationClips.Select(x => x.name).ToArray();
            foreach (var required in new[] { "Walk_Carry_Loop", "Farm_Harvest", "TreeChopping_Loop", "Fixing_Kneeling",
                         "Idle_Rail_Loop", "OverhandThrow", "Sword_Regular_Combo", "LayToIdle" })
                if (!clipNames.Any(x => x.IndexOf(required, StringComparison.OrdinalIgnoreCase) >= 0))
                    throw new InvalidDataException("Missing authored action clip: " + required +
                    "; controller=" + string.Join("|", clipNames));
        }

        private static void ConfigureHumanoidImporter(string path, bool animationsOnly)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter ??
                           throw new FileNotFoundException("P10.5 FBX importer is missing.", path);
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var importedAnimator = imported != null ? imported.GetComponentInChildren<Animator>(true) : null;
            var validAvatar = importedAnimator != null && importedAnimator.avatar != null &&
                              importedAnimator.avatar.isValid && importedAnimator.avatar.isHuman;
            if (importer.animationType == ModelImporterAnimationType.Human &&
                importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel &&
                validAvatar && (!animationsOnly || importer.clipAnimations.Length > 0))
                return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            var description = importer.humanDescription;
            description.human = CreateHumanBoneMap();
            importer.humanDescription = description;
            if (animationsOnly)
            {
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.loopTime = LoopingClips.Contains(clip.name);
                    clip.loopPose = clip.loopTime;
                    clip.keepOriginalPositionXZ = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalOrientation = true;
                }
                importer.clipAnimations = clips;
            }
            importer.SaveAndReimport();
        }

        private static HumanBone[] CreateHumanBoneMap()
        {
            return new[]
            {
                Bone(HumanBodyBones.Hips, "pelvis"),
                Bone(HumanBodyBones.LeftUpperLeg, "thigh_l"), Bone(HumanBodyBones.RightUpperLeg, "thigh_r"),
                Bone(HumanBodyBones.LeftLowerLeg, "calf_l"), Bone(HumanBodyBones.RightLowerLeg, "calf_r"),
                Bone(HumanBodyBones.LeftFoot, "foot_l"), Bone(HumanBodyBones.RightFoot, "foot_r"),
                Bone(HumanBodyBones.LeftToes, "ball_l"), Bone(HumanBodyBones.RightToes, "ball_r"),
                Bone(HumanBodyBones.Spine, "spine_01"), Bone(HumanBodyBones.Chest, "spine_02"),
                Bone(HumanBodyBones.UpperChest, "spine_03"), Bone(HumanBodyBones.Neck, "neck_01"),
                Bone(HumanBodyBones.Head, "Head"),
                Bone(HumanBodyBones.LeftShoulder, "clavicle_l"), Bone(HumanBodyBones.RightShoulder, "clavicle_r"),
                Bone(HumanBodyBones.LeftUpperArm, "upperarm_l"), Bone(HumanBodyBones.RightUpperArm, "upperarm_r"),
                Bone(HumanBodyBones.LeftLowerArm, "lowerarm_l"), Bone(HumanBodyBones.RightLowerArm, "lowerarm_r"),
                Bone(HumanBodyBones.LeftHand, "hand_l"), Bone(HumanBodyBones.RightHand, "hand_r")
            };
        }

        private static HumanBone Bone(HumanBodyBones humanBodyBone, string sourceName)
        {
            return new HumanBone
            {
                humanName = HumanTrait.BoneName[(int)humanBodyBone],
                boneName = sourceName,
                limit = new HumanLimit { useDefaultValues = true }
            };
        }

        private static Material CreateMaterial(string name, string texturePath)
        {
            var path = OutputRoot + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) ??
                          throw new FileNotFoundException("P10.5 texture is missing.", texturePath);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateRoleVisual(string sourcePath, string baseSourcePath, string name,
            Material outfit, Material skin, Material head, Material hair, Material eyes,
            RuntimeAnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) ??
                         throw new FileNotFoundException("P10.5 source model is missing.", sourcePath);
            var root = new GameObject(name);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "SourceVisual";
                visual.transform.SetParent(root.transform, false);
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(value =>
                        value != null && value.name.IndexOf("Regular", StringComparison.OrdinalIgnoreCase) >= 0
                            ? skin : outfit).ToArray();
                AddHeadPresentation(visual, baseSourcePath, name, head, hair, eyes);
                var bounds = CalculateBounds(visual);
                visual.transform.localScale = Vector3.one * (1.8f / Mathf.Max(0.01f, bounds.size.y));
                AlignToGround(visual);
                var animator = visual.GetComponentInChildren<Animator>(true) ??
                               throw new InvalidDataException("P10.5 role model has no Animator: " + sourcePath);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                var path = OutputRoot + "Prefabs/" + name + ".prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void AddHeadPresentation(GameObject targetVisual, string baseSourcePath, string roleName,
            Material headMaterial, Material hairMaterial, Material eyeMaterial)
        {
            var baseSource = AssetDatabase.LoadAssetAtPath<GameObject>(baseSourcePath) ??
                             throw new FileNotFoundException("P10.5 base Character is missing.", baseSourcePath);
            var baseInstance = (GameObject)PrefabUtility.InstantiatePrefab(baseSource);
            try
            {
                var body = baseInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.name.IndexOf("Super", StringComparison.OrdinalIgnoreCase) >= 0) ??
                           throw new InvalidDataException("P10.5 base Character body renderer is missing: " + baseSourcePath);
                var headMesh = CreateHeadOnlyMesh(body, OutputRoot + "Meshes/" + roleName + "_Head.asset");
                AddReboundRenderer(targetVisual, body, "P105_Head", headMesh, headMaterial);
                var eyebrow = baseInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.name == "Eyebrows") ??
                              throw new InvalidDataException("P10.5 eyebrows renderer is missing: " + baseSourcePath);
                var eye = baseInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.name == "Eyes") ??
                          throw new InvalidDataException("P10.5 eye renderer is missing: " + baseSourcePath);
                AddReboundRenderer(targetVisual, eyebrow, "P105_Eyebrows", eyebrow.sharedMesh, hairMaterial);
                AddReboundRenderer(targetVisual, eye, "P105_Eyes", eye.sharedMesh, eyeMaterial);
            }
            finally { UnityEngine.Object.DestroyImmediate(baseInstance); }
        }

        private static Mesh CreateHeadOnlyMesh(SkinnedMeshRenderer source, string assetPath)
        {
            var generated = UnityEngine.Object.Instantiate(source.sharedMesh);
            generated.name = Path.GetFileNameWithoutExtension(assetPath);
            var weights = generated.boneWeights;
            bool IsHeadVertex(int vertex)
            {
                if (vertex < 0 || vertex >= weights.Length) return false;
                var weight = weights[vertex];
                var total = HeadWeight(source, weight.boneIndex0, weight.weight0) +
                            HeadWeight(source, weight.boneIndex1, weight.weight1) +
                            HeadWeight(source, weight.boneIndex2, weight.weight2) +
                            HeadWeight(source, weight.boneIndex3, weight.weight3);
                return total >= 0.25f;
            }
            for (var subMesh = 0; subMesh < generated.subMeshCount; subMesh++)
            {
                var sourceTriangles = generated.GetTriangles(subMesh);
                var kept = new List<int>(sourceTriangles.Length / 5);
                for (var index = 0; index + 2 < sourceTriangles.Length; index += 3)
                {
                    var a = sourceTriangles[index]; var b = sourceTriangles[index + 1]; var c = sourceTriangles[index + 2];
                    if (!IsHeadVertex(a) || !IsHeadVertex(b) || !IsHeadVertex(c)) continue;
                    kept.Add(a); kept.Add(b); kept.Add(c);
                }
                generated.SetTriangles(kept, subMesh, false);
            }
            generated.RecalculateBounds();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static float HeadWeight(SkinnedMeshRenderer source, int boneIndex, float weight)
        {
            if (weight <= 0f || boneIndex < 0 || boneIndex >= source.bones.Length) return 0f;
            var boneName = source.bones[boneIndex] != null ? source.bones[boneIndex].name : string.Empty;
            return boneName == "Head" || boneName == "neck_01" ? weight : 0f;
        }

        private static void AddReboundRenderer(GameObject targetVisual, SkinnedMeshRenderer source,
            string name, Mesh mesh, Material material)
        {
            var bonesByName = targetVisual.GetComponentsInChildren<Transform>(true)
                .GroupBy(x => x.name).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
            var child = new GameObject(name);
            child.transform.SetParent(targetVisual.transform, false);
            child.transform.localPosition = source.transform.localPosition;
            child.transform.localRotation = source.transform.localRotation;
            child.transform.localScale = source.transform.localScale;
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.bones = source.bones.Select(x => bonesByName.TryGetValue(x.name, out var mapped)
                ? mapped : throw new InvalidDataException("P10.5 target skeleton is missing bone: " + x.name)).ToArray();
            renderer.rootBone = bonesByName.TryGetValue(source.rootBone.name, out var rootBone)
                ? rootBone : throw new InvalidDataException("P10.5 target skeleton root is missing.");
            renderer.localBounds = source.localBounds;
            renderer.updateWhenOffscreen = false;
        }

        private static AnimatorController CreateController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ??
                             AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var parameter in controller.parameters.ToArray()) controller.RemoveParameter(parameter);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("ActionIntent", AnimatorControllerParameterType.Int);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states.ToArray()) machine.RemoveState(child.state);

            var idle = AddState(machine, "Idle", Clip("Idle.anim", HodaAnimationRoot));
            var walk = AddState(machine, "Walking", Clip("Walking.anim", HodaAnimationRoot));
            var run = AddState(machine, "Running", Clip("Running.anim", HodaAnimationRoot));
            machine.defaultState = idle;
            AddFloatTransition(idle, walk, AnimatorConditionMode.Greater, 0.05f);
            AddFloatTransition(walk, idle, AnimatorConditionMode.Less, 0.05f);
            AddFloatTransition(walk, run, AnimatorConditionMode.Greater, 1.5f);
            AddFloatTransition(run, walk, AnimatorConditionMode.Less, 1.5f);

            var actions = new[]
            {
                (CharacterAnimationIntent.Carry, "Carry", Clip("Walk_Carry_Loop", AnimationPaths[1])),
                (CharacterAnimationIntent.Gather, "Gather", Clip("Farm_Harvest", AnimationPaths[1])),
                (CharacterAnimationIntent.Chop, "Chop", Clip("TreeChopping_Loop", AnimationPaths[1])),
                (CharacterAnimationIntent.Mine, "Mine", Clip("Sword_Heavy_Combo", AnimationPaths[1])),
                (CharacterAnimationIntent.Build, "Build", Clip("Fixing_Kneeling", AnimationPaths[0])),
                (CharacterAnimationIntent.Fish, "Fish", Clip("Idle_Rail_Loop", AnimationPaths[1])),
                (CharacterAnimationIntent.Hunt, "Hunt", Clip("OverhandThrow", AnimationPaths[1])),
                (CharacterAnimationIntent.Combat, "Combat", Clip("Sword_Regular_Combo", AnimationPaths[1])),
                (CharacterAnimationIntent.Sleep, "Sleep", Clip("LayToIdle", AnimationPaths[1]))
            };
            foreach (var action in actions)
            {
                var state = AddState(machine, action.Item2, action.Item3);
                var enter = machine.AddAnyStateTransition(state);
                enter.hasExitTime = false;
                enter.duration = 0.12f;
                enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.Equals, (float)action.Item1, "ActionIntent");
                var exit = state.AddTransition(idle);
                exit.hasExitTime = false;
                exit.duration = 0.12f;
                exit.AddCondition(AnimatorConditionMode.Equals, 0f, "ActionIntent");
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion)
        {
            var state = machine.AddState(name);
            state.motion = motion;
            return state;
        }

        private static void AddFloatTransition(AnimatorState from, AnimatorState to,
            AnimatorConditionMode mode, float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.AddCondition(mode, threshold, "Speed");
            transition.AddCondition(AnimatorConditionMode.Equals, 0f, "ActionIntent");
        }

        private static AnimationClip Clip(string name, string pathOrRoot)
        {
            if (pathOrRoot.EndsWith("/", StringComparison.Ordinal))
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(pathOrRoot + name) ??
                       throw new FileNotFoundException("P10.5 animation clip is missing: " + name, pathOrRoot);
            var clips = AssetDatabase.LoadAllAssetsAtPath(pathOrRoot).OfType<AnimationClip>().ToArray();
            var clip = clips.FirstOrDefault(x => x.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                                  !x.name.StartsWith("__preview__", StringComparison.Ordinal));
            return clip ?? throw new FileNotFoundException("P10.5 animation clip is missing: " + name +
                "; available=" + string.Join("|", clips.Select(x => x.name)), pathOrRoot);
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidDataException("P10.5 visual has no renderer: " + root.name);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void AlignToGround(GameObject visual)
        {
            var bounds = CalculateBounds(visual);
            visual.transform.position += Vector3.up * -bounds.min.y;
        }
    }
}
#endif
