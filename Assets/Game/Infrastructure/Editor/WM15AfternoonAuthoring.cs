using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Infrastructure.Editor
{
    public static class WM15AfternoonAuthoring
    {
        public const string Folder = "Assets/Game/Art/WM15Afternoon";
        private const string Imported = Folder + "/Imported";
        public static void Generate()
        {
            Directory.CreateDirectory(Folder + "/Views");
            Directory.CreateDirectory(Folder + "/Materials");
            AssetDatabase.Refresh();
            WM15PropAuthoring.Generate();
            var catalog = AssetDatabase.LoadAssetAtPath<WorldPropCatalog>("Assets/Game/Resources/World/WorldProps.asset");
            var entries = catalog.Entries.ToList();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Imported + "/KayKit/resource_bits_texture.png");
            var metal = Material("KayKit", Color.white, atlas);
            foreach (var pair in new[] { ("iron_ore", "Iron"), ("copper_ore", "Copper"), ("gold_ore", "Gold") })
                Replace(entries, "pile_" + pair.Item1, Model(pair.Item2, Imported + "/KayKit/" + pair.Item2 + "_Nuggets.fbx", metal, 1.2f), "KayKit Resource Bits CC0");
            var hide = Model("Hide", Imported + "/Hide/Hide_animal.obj", Material("Hide", new Color(.42f,.22f,.1f)), 1.25f);
            Replace(entries, "pile_hide", hide, "Animal Hide by Zsky, CC BY");
            Replace(entries, "pile_cured_hide", hide, "Animal Hide by Zsky, CC BY");
            // The approved free crafting pack includes a plain pole without the modern reel.
            var pole = Model("FishingPole", Imported + "/HumanCrafting/Human_FishingPole.fbx", Material("Pole", new Color(.34f,.21f,.09f)), 2.1f, true);
            Replace(entries, "fishing_rod", pole, "Human Crafting Animations FREE, Kevin Iglesias");
            Replace(entries, "fish_swimming", AnimatedFish(), "Fish by joyfulsquirrel, CC0, Swim animation");
            catalog.Entries = entries.ToArray(); EditorUtility.SetDirty(catalog);
            AuthorWorkAnimation();
            AuthorAnimals();
            ResizeBuildings();
            AssetDatabase.SaveAssets();
            Debug.Log("WM15_AFTERNOON_AUTHORED");
        }

        private static void Replace(List<WorldPropCatalog.Entry> entries, string key, GameObject prefab, string source)
        {
            entries.RemoveAll(x => x.Key == key);
            entries.Add(new WorldPropCatalog.Entry { Key = key, Prefab = prefab, Source = source });
        }
        private static Material Material(string name, Color color, Texture texture = null)
        {
            var path = Folder + "/Materials/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result,path); }
            result.SetColor("_BaseColor",color); result.SetTexture("_BaseMap",texture);
            result.SetFloat("_Smoothness",.08f); EditorUtility.SetDirty(result); return result;
        }
        private static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(x => x is MeshRenderer || x is SkinnedMeshRenderer).ToArray();
            if (renderers.Length == 0) throw new InvalidDataException("Missing render geometry: " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        private static GameObject Model(string name, string source, Material material, float extent, bool pole = false)
        {
            var root = new GameObject(name);
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source),root.transform);
            foreach (var component in model.GetComponentsInChildren<Component>(true).Reverse())
                if (component != null && !(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer)) Object.DestroyImmediate(component);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            var bounds = Bounds(root);
            if (pole)
            {
                var axis = bounds.size.x > bounds.size.y ? (bounds.size.x > bounds.size.z ? Vector3.right : Vector3.forward) :
                    bounds.size.y > bounds.size.z ? Vector3.up : Vector3.forward;
                model.transform.rotation = Quaternion.FromToRotation(axis,Vector3.up) * model.transform.rotation;
                bounds = Bounds(root);
            }
            model.transform.localScale *= extent / Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            bounds = Bounds(root);
            model.transform.position -= new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            var result = PrefabUtility.SaveAsPrefabAsset(root,Folder + "/Views/" + name + ".prefab");
            Object.DestroyImmediate(root); return result;
        }
        private static AnimationClip Clip(string name)
        {
            var path = Imported + "/HumanCrafting/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var clip in clips) { clip.loopTime = true; clip.lockRootPositionXZ = true; clip.lockRootHeightY = true; clip.keepOriginalPositionY = true; }
            importer.clipAnimations = clips; importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(x => !x.name.StartsWith("__preview__"));
        }
        private static GameObject AnimatedFish()
        {
            var path = Imported + "/Fish/Fish.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var clip in clips) { clip.name = "Swim"; clip.loopTime = true; clip.lockRootPositionXZ = true; clip.lockRootHeightY = true; }
            importer.clipAnimations = clips; importer.SaveAndReimport();
            var swim = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .First(x => !x.name.StartsWith("__preview__") && x.name.IndexOf("swim",StringComparison.OrdinalIgnoreCase) >= 0);
            var controllerPath = Folder + "/Views/Fish.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states.ToArray()) machine.RemoveState(state.state);
            var swimming = machine.AddState("Swim"); swimming.motion = swim; machine.defaultState = swimming;
            var root = new GameObject("Animated fish");
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
            var material = Material("Fish",Color.white,AssetDatabase.LoadAssetAtPath<Texture2D>(Imported + "/Fish/fish.png"));
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
            var bounds = Bounds(model);
            var axis = bounds.size.x > bounds.size.z ? Vector3.right : Vector3.forward;
            model.transform.rotation = Quaternion.FromToRotation(axis,Vector3.forward) * model.transform.rotation;
            model.transform.localScale *= .65f / Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            bounds = Bounds(model); model.transform.position -= bounds.center;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root,Folder + "/Views/Fish.prefab");
            Object.DestroyImmediate(root); EditorUtility.SetDirty(controller); return prefab;
        }
        private static void AuthorWorkAnimation()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Game/Art/Animation/CharacterTier1.controller");
            var states = controller.layers[0].stateMachine.states.Select(x => x.state).ToDictionary(x => x.name);
            states["Fish"].motion = Clip("HumanM@Fishing01 - Loop");
            states["Mine"].motion = Clip("HumanM@MiningOneHand01_R - Wall");
            states["Gather"].motion = Clip("HumanM@Gathering01");
            states["Build"].motion = Clip("HumanM@HammeringGround01_R - Loop");
            if (!controller.parameters.Any(x => x.name == "AuthoredWork"))
                controller.AddParameter(new AnimatorControllerParameter { name = "AuthoredWork", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            EditorUtility.SetDirty(controller);
        }
        private static void AuthorAnimals()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldMapWildlifeArtCatalog>("Assets/Game/Resources/World/WorldMapWildlifeArt.asset");
            var entries = new List<WorldMapWildlifeArtCatalog.Entry>();
            foreach (var entry in catalog.Entries)
            {
                var root = Object.Instantiate(entry.Prefab);
                var animator = root.GetComponentInChildren<Animator>(true);
                var clips = animator?.runtimeAnimatorController?.animationClips;
                if (entry.SpeciesId == "waterfowl")
                {
                    if (animator == null) animator = root.AddComponent<Animator>();
                    var idleSource = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Toon Farm Pack/Animations/Animal Animations/Duck/TFP_Duck_01_Idle_01_Regular.controller");
                    var walkSource = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Toon Farm Pack/Animations/Animal Animations/Duck/TFP_Duck_01_Walk_Regular.controller");
                    clips = idleSource.animationClips.Concat(walkSource.animationClips).ToArray();
                }
                var idle = clips?.FirstOrDefault(x => x.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase) >= 0);
                var walk = clips?.FirstOrDefault(x => x.name.IndexOf("walk",StringComparison.OrdinalIgnoreCase) >= 0 &&
                    x.name.IndexOf("back",StringComparison.OrdinalIgnoreCase) < 0);
                if (idle == null || walk == null)
                {
                    Debug.LogWarning("No imported locomotion pair for " + entry.SpeciesId);
                    entries.Add(entry); Object.DestroyImmediate(root); continue;
                }
                var path = Folder + "/Views/" + entry.SpeciesId + ".controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
                foreach (var parameter in controller.parameters.ToArray()) controller.RemoveParameter(parameter);
                controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);
                var machine = controller.layers[0].stateMachine;
                foreach (var state in machine.states.ToArray()) machine.RemoveState(state.state);
                var rest = machine.AddState("Idle"); rest.motion = idle;
                var move = machine.AddState("Walk"); move.motion = walk; machine.defaultState = rest;
                var go = rest.AddTransition(move); go.hasExitTime = false; go.duration = .15f; go.AddCondition(AnimatorConditionMode.If,0,"Moving");
                var stop = move.AddTransition(rest); stop.hasExitTime = false; stop.duration = .15f; stop.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root,Folder + "/Views/" + entry.SpeciesId + ".prefab");
                entries.Add(new WorldMapWildlifeArtCatalog.Entry().Configure(entry.SpeciesId,prefab,entry.Scale));
                Object.DestroyImmediate(root); EditorUtility.SetDirty(controller);
            }
            catalog.Configure(entries); EditorUtility.SetDirty(catalog);
        }
        private static void ResizeBuildings()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(U08BuildingFoundation.CatalogPath);
            foreach (var id in new[] { "stone_age_hut", "healer_hut", "butcher_station", "military_barracks" })
            {
                var entry = catalog.Entries.Single(x => x.DefinitionId == id);
                var path = AssetDatabase.GetAssetPath(entry.Prefab);
                // Barracks previously shared the starter tent asset. Give it its own presentation.
                if (id == "military_barracks")
                {
                    path = Folder + "/Views/Barracks.prefab";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(entry.Prefab),path);
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var final = root.GetComponentsInChildren<BuildingConstructionStageVisual>(true).OrderByDescending(x => x.RequiredStage).First().transform;
                    if (id == "military_barracks")
                    {
                        for (var i = final.childCount - 1; i >= 0; i--) Object.DestroyImmediate(final.GetChild(i).gameObject);
                        var barracks = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RTSToonBuildings/Prefabs/Buildings/Barracks.prefab"),final);
                        foreach (var component in barracks.GetComponentsInChildren<Component>(true).Reverse())
                            if (component != null && !(component is Transform) && !(component is MeshRenderer) && !(component is MeshFilter)) Object.DestroyImmediate(component);
                        foreach (var renderer in barracks.GetComponentsInChildren<MeshRenderer>(true))
                            renderer.sharedMaterials = renderer.sharedMaterials.Select(x => WM15PropAuthoring.OwnedMaterial(x)).ToArray();
                    }
                    var bounds = Bounds(final.gameObject);
                    var targetWidth = id == "butcher_station" ? 2.8f : 3.8f;
                    var targetDepth = id == "military_barracks" ? 2.8f : targetWidth;
                    final.localScale *= Mathf.Min(targetWidth/bounds.size.x,targetDepth/bounds.size.z);
                    bounds = Bounds(final.gameObject); final.position -= new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                    var box = root.GetComponent<BoxCollider>();
                    if (box != null) { var size = box.size; size.y = Bounds(final.gameObject).size.y; box.size = size; box.center = Vector3.up * size.y*.5f; }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                if (id == "military_barracks")
                {
                    var serialized = new SerializedObject(catalog); var list = serialized.FindProperty("entries");
                    for (var i=0;i<list.arraySize;i++)
                        if (list.GetArrayElementAtIndex(i).FindPropertyRelative("definitionId").stringValue == id)
                            list.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }
    }
}
