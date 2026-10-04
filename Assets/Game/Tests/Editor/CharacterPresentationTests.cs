using System;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("U07")]
    public sealed class CharacterPresentationTests
    {
        private const string MalePath = "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_A_Tier1.prefab";
        private const string FemalePath = "Assets/Game/Art/Prefabs/Characters/Serenity_Adult_B_Tier1.prefab";

        [Test]
        public void BindExposesDomainIdentityAndUnbindAllowsReplacementPresenter()
        {
            var character = NewCharacter("Aren", CharacterSex.Male);
            var id = character.Id;
            var registry = new CharacterPresentationRegistry();
            var first = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MalePath))
                .GetComponent<CharacterPresenter>();
            var second = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MalePath))
                .GetComponent<CharacterPresenter>();
            try
            {
                first.Bind(character, registry);
                Assert.That(first.IsBound, Is.True);
                Assert.That(first.Character, Is.SameAs(character));
                Assert.That(first.CharacterId, Is.EqualTo(id));
                Assert.That(registry.TryGet(id, out var active), Is.True);
                Assert.That(active, Is.SameAs(first));

                Assert.Throws<InvalidOperationException>(() => second.Bind(character, registry));
                first.Unbind();
                Assert.That(first.IsBound, Is.False);
                Assert.That(registry.TryGet(id, out _), Is.False);

                second.Bind(character, registry);
                Assert.That(second.CharacterId, Is.EqualTo(id));
                Assert.That(character.Id, Is.EqualTo(id));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                UnityEngine.Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void UnbindUnregistersWithoutChangingOrDeletingDomainCharacter()
        {
            var character = NewCharacter("Mira", CharacterSex.Female);
            var id = character.Id;
            var domainRegistry = new CharacterRegistry();
            domainRegistry.Add(character);
            var presentationRegistry = new CharacterPresentationRegistry();
            var presenter = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FemalePath))
                .GetComponent<CharacterPresenter>();
            presenter.Bind(character, presentationRegistry);

            presenter.Unbind();
            UnityEngine.Object.DestroyImmediate(presenter.gameObject);

            Assert.That(presentationRegistry.TryGet(id, out _), Is.False);
            Assert.That(domainRegistry.Get(id), Is.SameAs(character));
            Assert.That(domainRegistry.Get(id).Id, Is.EqualTo(id));
        }

        [Test]
        public void CatalogMapsSexDeterministicallyAndRejectsInvalidConfiguration()
        {
            var male = AssetDatabase.LoadAssetAtPath<GameObject>(MalePath);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(FemalePath);
            var catalog = ScriptableObject.CreateInstance<CharacterPresentationCatalog>();
            var missing = ScriptableObject.CreateInstance<CharacterPresentationCatalog>();
            try
            {
                catalog.Configure(male, female);
                Assert.That(catalog.Resolve(CharacterSex.Male), Is.SameAs(male));
                Assert.That(catalog.Resolve(CharacterSex.Female), Is.SameAs(female));
                Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Resolve((CharacterSex)99));
                Assert.Throws<InvalidOperationException>(() => missing.Resolve(CharacterSex.Male));
                missing.Configure(null, female);
                Assert.Throws<InvalidOperationException>(() => missing.Resolve(CharacterSex.Male));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(missing);
            }
        }

        [Test]
        public void TierOnePrefabsHavePresentationFoundation()
        {
            foreach (var path in new[] { MalePath, FemalePath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var presenter = prefab.GetComponent<CharacterPresenter>();
                Assert.That(presenter, Is.Not.Null, path);
                Assert.That(presenter.Animator, Is.Not.Null, path);
                Assert.That(presenter.Animator.avatar, Is.Not.Null, path);
                Assert.That(presenter.Animator.avatar.isValid, Is.True, path);
                Assert.That(presenter.Animator.runtimeAnimatorController, Is.Not.Null, path);
                Assert.That(presenter.Animator.runtimeAnimatorController.animationClips.Length, Is.GreaterThanOrEqualTo(3), path);
                Assert.That(prefab.GetComponent<CapsuleCollider>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, path);
                Assert.That(presenter.RightHandSocket, Is.Not.Null, path);
                Assert.That(presenter.LeftHandSocket, Is.Not.Null, path);
            }
        }

        private static Character NewCharacter(string name, CharacterSex sex)
        {
            return Character.CreateNew(new CharacterCreationData(new CharacterName(name), sex, 0,
                null, null, null, null, null,
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                null,
                new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
        }
    }
}
