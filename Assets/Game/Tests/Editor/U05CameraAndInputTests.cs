using System.Linq;
using Game.Presentation.CameraControl;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tests
{
    public class U05CameraAndInputTests
    {
        [Test]
        public void EdgeDirectionUsesNormalizedResolutionIndependentThresholds()
        {
            Assert.That(RtsCameraMath.GetEdgeDirection(new Vector2(5f, 540f), new Vector2(1920f, 1080f), 0.01f),
                Is.EqualTo(Vector2.left));
            Assert.That(RtsCameraMath.GetEdgeDirection(new Vector2(2.5f, 360f), new Vector2(1280f, 720f), 0.01f),
                Is.EqualTo(Vector2.left));
            Assert.That(RtsCameraMath.GetEdgeDirection(new Vector2(960f, 540f), new Vector2(1920f, 1080f), 0.01f),
                Is.EqualTo(Vector2.zero));
            Assert.That(RtsCameraMath.GetEdgeDirection(new Vector2(-1f, 540f), new Vector2(1920f, 1080f), 0.01f),
                Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void HorizontalMotionRotatesWithoutVerticalDrift()
        {
            var motion = RtsCameraMath.HorizontalMotion(Vector2.up, 90f);
            Assert.That(motion.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(motion.y, Is.Zero.Within(0.0001f));
            Assert.That(motion.z, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void BoundsClampOnlyHorizontalPosition()
        {
            var result = RtsCameraMath.ClampToBounds(new Vector3(100f, 7f, -100f),
                new Vector2(-10f, -20f), new Vector2(10f, 20f));
            Assert.That(result, Is.EqualTo(new Vector3(10f, 7f, -20f)));
        }

        [Test]
        public void LocalActionAssetHasSmallRebindingReadyMaps()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Game/Presentation/Input/LocalGameplay.inputactions");
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.actionMaps.Select(map => map.name), Is.EquivalentTo(new[] { "Camera", "Pointer" }));
            Assert.That(asset.FindActionMap("Camera").actions.Select(action => action.name),
                Is.EquivalentTo(new[] { "Move", "Zoom", "Rotate", "Pan", "PanModifier" }));
            Assert.That(asset.FindActionMap("Pointer").actions.Select(action => action.name),
                Is.EquivalentTo(new[] { "Position", "PrimaryClick", "SecondaryClick" }));
            Assert.That(asset.FindAction("Camera/Move").bindings.Any(binding => binding.path == "<Keyboard>/w"), Is.True);
            Assert.That(asset.FindAction("Camera/Zoom").bindings.Any(binding => binding.path == "<Mouse>/scroll/y"), Is.True);
            Assert.That(asset.FindAction("Camera/Rotate").bindings.Any(binding => binding.path == "<Keyboard>/q"), Is.True);
        }

        [Test]
        public void LocalSceneIsTheBuildSceneAndHasNoVendorDependencies()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Assert.That(scenes.Select(scene => scene.path), Is.EqualTo(new[] { "Assets/Scenes/LocalGameplay.unity" }));
            var dependencies = AssetDatabase.GetDependencies(scenes[0].path, true);
            Assert.That(dependencies.Any(path => path.StartsWith("Assets/EmaceArt")
                || path.StartsWith("Assets/EmbersStorm")
                || path.StartsWith("Assets/Hodaart")
                || path.StartsWith("Assets/Medieval Fortification")
                || path.StartsWith("Assets/PolyOne")
                || path.StartsWith("Assets/Stylized Nature Environment")
                || path.StartsWith("Assets/SunboxGames")
                || path.StartsWith("Assets/ToonyTinyPeople")
                || path.StartsWith("Assets/TriForge Assets")
                || path.StartsWith("Assets/URP GanzSe")
                || path.StartsWith("Assets/Vefects")), Is.False);
        }
    }
}
