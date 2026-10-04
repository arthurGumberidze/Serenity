using System.Collections;
using System.Linq;
using Game.Infrastructure;
using Game.Presentation.Interaction;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class LocalGameplaySceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneHasExplicitCompositionAndCinemachineCamera()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.Input, Is.Not.Null);
            Assert.That(root.CameraController, Is.Not.Null);
            Assert.That(root.PointerRaycaster, Is.Not.Null);
            Assert.That(root.SelectionProbe, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<CinemachineBrain>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<CinemachineCamera>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<SelectableMarker>().Length, Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerRaycastAndSelectionProbeHitMarker()
        {
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var marker = Object.FindObjectsByType<SelectableMarker>()
                .OrderBy(candidate => Vector3.Distance(candidate.transform.position, Vector3.zero)).First();
            var camera = root.PointerRaycaster.WorldCamera;
            var screenPoint = camera.WorldToScreenPoint(marker.transform.position);
            Assert.That(screenPoint.z, Is.GreaterThan(0f));
            Assert.That(root.PointerRaycaster.TryGetWorldHit(screenPoint, out var hit), Is.True);
            Assert.That(hit.collider.GetComponentInParent<SelectableMarker>(), Is.SameAs(marker));
            Assert.That(root.SelectionProbe.TrySelectAt(screenPoint), Is.True);
            Assert.That(root.SelectionProbe.Selected, Is.SameAs(marker));
            Assert.That(marker.IsSelected, Is.True);
            yield return null;
        }
    }
}
