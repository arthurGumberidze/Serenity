using System.Collections;
using Game.Infrastructure;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P81")]
    public sealed class P81VisualPrototypePlayModeTests
    {
        [UnityTest]
        public IEnumerator ProductionRegionLoadsWithStableUmaAppearanceAndVisibleCharacter()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.RealisticGameplayRegion, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<P81VisualPrototypePresentation>(), Is.Null);
            Assert.That(root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var presenter), Is.True);
            Assert.That(presenter.Appearance.CharacterId, Is.EqualTo(root.MaleDemoCharacter.Id));
            Assert.That(presenter.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            Assert.That(presenter.Appearance.ProfileId.Value, Does.StartWith("uma_human_"));
            yield return null;
        }
    }
}
