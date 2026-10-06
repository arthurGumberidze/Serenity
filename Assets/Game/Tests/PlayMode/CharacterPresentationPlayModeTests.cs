using System.Collections;
using System.IO;
using System.Linq;
using Game.Infrastructure;
using Game.Presentation.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U07")]
    public sealed class CharacterPresentationPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalSceneSpawnsMaleAndFemaleBoundTierOneViews()
        {
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.Characters.Count, Is.EqualTo(2));
            Assert.That(root.CharacterPresentations.Count, Is.EqualTo(2));
            Assert.That(root.DemoPresenters.Count, Is.EqualTo(2));

            foreach (var presenter in root.DemoPresenters)
            {
                Assert.That(presenter.IsBound, Is.True);
                Assert.That(root.Characters.Get(presenter.CharacterId), Is.SameAs(presenter.Character));
                Assert.That(presenter.Animator, Is.Not.Null);
                Assert.That(presenter.Animator.avatar, Is.Not.Null);
                Assert.That(presenter.Animator.avatar.isValid, Is.True);
                Assert.That(presenter.Animator.avatar.isHuman, Is.True);
                Assert.That(presenter.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
                var upperArm = presenter.Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                var hand = presenter.Animator.GetBoneTransform(HumanBodyBones.RightHand);
                Assert.That(upperArm, Is.Not.Null);
                Assert.That(hand, Is.Not.Null);
                Assert.That(hand.position.y, Is.LessThan(upperArm.position.y - 0.1f),
                    "Idle pose must lower the hand instead of leaving the model in bind pose.");
                Assert.That(presenter.GetComponent<CapsuleCollider>(), Is.Not.Null);
                Assert.That(presenter.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                Assert.That(presenter.transform.position.y, Is.EqualTo(0f).Within(0.15f),
                    "Runtime NavMesh voxelization may place the agent root slightly above the visual ground plane.");
            }
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                CaptureRuntimeValidation(root.PointerRaycaster.WorldCamera);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DespawnAndRespawnPreserveDomainCharacterAndStableId()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var character = root.MaleDemoCharacter;
            var id = character.Id;
            var first = root.DemoPresenters.Single(presenter => presenter.Character == character);

            root.CharacterSpawner.Despawn(first);
            yield return null;

            Assert.That(root.Characters.Get(id), Is.SameAs(character));
            Assert.That(root.CharacterPresentations.TryGet(id, out _), Is.False);
            var second = root.CharacterSpawner.Spawn(character, new Vector3(-3f, 0f, 0f), Quaternion.identity);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.CharacterId, Is.EqualTo(id));
            Assert.That(root.Characters.Get(id), Is.SameAs(character));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyAutomaticallyUnregistersAndAllowsSameIdRespawn()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var character = root.FemaleDemoCharacter;
            var id = character.Id;
            var presenter = root.DemoPresenters.Single(candidate => candidate.Character == character);

            Object.Destroy(presenter.gameObject);
            yield return null;

            Assert.That(root.CharacterPresentations.TryGet(id, out _), Is.False);
            Assert.That(root.Characters.Get(id), Is.SameAs(character));
            var replacement = root.CharacterSpawner.Spawn(character, new Vector3(3f, 0f, 0f), Quaternion.identity);
            Assert.That(replacement.CharacterId, Is.EqualTo(id));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerSelectionExposesBoundCharacterId()
        {
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var presenter = root.DemoPresenters[0];
            var collider = presenter.GetComponent<Collider>();
            var screenPoint = PointerSelectionTestPoint.AimCameraAtUnblockedPoint(root, collider.bounds.center);
            Assert.That(screenPoint.z, Is.GreaterThan(0f));
            Assert.That(root.SelectionProbe.TrySelectAt(screenPoint), Is.True);
            Assert.That(root.SelectionProbe.SelectedCharacterPresenter, Is.SameAs(presenter));
            Assert.That(root.SelectionProbe.SelectedCharacterId, Is.EqualTo(presenter.CharacterId));
            yield return null;
        }

        private static void CaptureRuntimeValidation(Camera camera)
        {
            camera.transform.position = new Vector3(0f, 2.8f, -6.5f);
            camera.transform.LookAt(new Vector3(0f, 1f, 0f));
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
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/U07-runtime-characters.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(image);
                renderTexture.Release();
                Object.Destroy(renderTexture);
            }
        }
    }
}
