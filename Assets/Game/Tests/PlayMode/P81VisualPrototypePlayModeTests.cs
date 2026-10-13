using System.Collections;
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
        public IEnumerator BenchmarkLoadsWithStableAppearanceAndVisibleCharacter()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            var prototype = Object.FindAnyObjectByType<P81VisualPrototypePresentation>();
            Assert.That(prototype, Is.Not.Null);
            Assert.DoesNotThrow(prototype.ValidateConfiguration);
            Assert.That(prototype.CanonicalAppearance.CharacterId.ToString(),
                Is.EqualTo("81000000000000000000000000000001"));
            var deadline = Time.realtimeSinceStartup + 12f;
            while (!prototype.UsesUma && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(prototype.HasVisibleCharacter, Is.True);
            Assert.That(prototype.ActiveProvider, Is.EqualTo("uma3"),
                "Official UMA content should generate; fallback is retained for missing-content saves.");
        }
    }
}
