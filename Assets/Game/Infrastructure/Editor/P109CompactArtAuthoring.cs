using System;
using System.IO;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class P109CompactArtAuthoring
{
    private const string Output = "Assets/Game/Art/CompactWorld";
    public static void Generate()
    {
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        var catalog = ScriptableObject.CreateInstance<CompactWorldArtCatalog>();
        catalog.HuntingDeer = Prepare("Assets/ithappy/Animals_FREE/Prefabs/Deer_001.prefab", "HuntingDeer", 1.8f);
        catalog.StoneOutcrop = Prepare("Assets/Polytope Studio/Lowpoly_Environments/Sources/Meshes/Rocks/PT_Ore_Rock_01.fbx", "StoneOutcrop", 1.6f);
        catalog.FieldFence = Prepare("Assets/Toon Farm Pack/Prefabs/Props/Fence Kits/TFP_Fence_04A_Module_1.prefab", "FieldFence", 1.2f);
        catalog.WoodlandDetails = new[]
        {
            Prepare("Assets/3D Low Poly Nature Pack -assets model/Prefabs/Models/FallenLog_01.001.prefab",
                "WoodlandFallenLog", 0.65f),
            Prepare("Assets/3D Low Poly Nature Pack -assets model/Prefabs/Models/Mushroom_Red_01.001.prefab",
                "WoodlandMushroom", 0.32f),
            Prepare("Assets/3D Low Poly Nature Pack -assets model/Prefabs/Models/Cattail.001.prefab",
                "WoodlandCattail", 0.9f)
        };
        catalog.FishPattern = AssetDatabase.LoadAssetAtPath<Texture>(
            "Assets/PolygonNatureBiomes/PNB_Tropical_Jungle/FX/FX_Textures/Fish_01.png");
        if (catalog.FishPattern == null) throw new InvalidOperationException("Reviewed Synty fish art missing");
        catalog.LightningTexture = Require<Texture2D>("Assets/LightningBolt/Textures/LightningBoltTexture.png");
        catalog.ForestAmbientDay = Require<AudioClip>(
            "Assets/Medieval Action - FX Pack 2.0/Forest ambient (loop).wav");
        catalog.ForestAmbientNight = Require<AudioClip>(
            "Assets/Medieval Action - FX Pack 2.0/Forest at night (loop).wav");
        catalog.HuntingBow = Require<AudioClip>("Assets/Medieval Action - FX Pack 2.0/Bow.wav");
        catalog.HuntingArrow = Require<AudioClip>("Assets/Medieval Action - FX Pack 2.0/Arrow 1.wav");
        const string target = "Assets/Game/Resources/World/CompactWorldArt.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CompactWorldArtCatalog>(target);
        if (existing == null) AssetDatabase.CreateAsset(catalog, target);
        else { EditorUtility.CopySerialized(catalog, existing); EditorUtility.SetDirty(existing); }
        AssetDatabase.SaveAssets();
        Debug.Log("P109_COMPACT_ART_READY: deer, selectable outcrop, field fences, fish pattern");
    }

    private static T Require<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("Missing reviewed art: " + path);
        return asset;
    }

    private static GameObject Prepare(string source, string name, float height)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (prefab == null) throw new InvalidOperationException("Missing art: " + source);
        var root = new GameObject(name);
        var visual = UnityEngine.Object.Instantiate(prefab, root.transform);
        visual.name = "Reviewed art";
        foreach (var item in visual.GetComponentsInChildren<Transform>(true)) item.gameObject.isStatic = false;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
        foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(lod);
        var meshes = visual.GetComponentsInChildren<Renderer>(true);
        if (meshes.Length == 0) throw new InvalidOperationException("No renderers: " + source);
        var bounds = meshes[0].bounds;
        foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
        visual.transform.localScale *= height / Mathf.Max(0.01f, bounds.size.y);
        bounds = meshes[0].bounds;
        foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/ithappy/Animals_FREE/Meshes/Deer_001.fbx")
                .OfType<AnimationClip>().Where(x => !x.name.StartsWith("__preview__")).ToArray();
            var idle = clips.FirstOrDefault(x => x.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0);
            if (idle == null) throw new InvalidOperationException("Deer idle animation missing");
            var walk = clips.FirstOrDefault(x => x.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0);
            if (walk == null) throw new InvalidOperationException("Deer walk animation missing");
            var path = Output + "/DeerIdle.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ??
                AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            controller.parameters = new[] { new AnimatorControllerParameter { name = "Moving", type = AnimatorControllerParameterType.Bool } };
            var idleState = machine.AddState("Idle"); idleState.motion = idle;
            var walkState = machine.AddState("Walk"); walkState.motion = walk;
            machine.defaultState = idleState;
            var start = idleState.AddTransition(walkState); start.hasExitTime = false; start.duration = .12f;
            start.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            var stop = walkState.AddTransition(idleState); stop.hasExitTime = false; stop.duration = .12f;
            stop.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            EditorUtility.SetDirty(controller);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }
}
