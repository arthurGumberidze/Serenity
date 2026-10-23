using System.Collections;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM01")]
    public sealed class WM01WorldMapPackageSmokeTests
    {
        [UnityTest]
        public IEnumerator ProposedAndLegacyPackagesLoadSideBySideWithoutChangingTheActiveGeneration()
        {
            var legacy = TiledWorldDefinitionLoader.LoadReference();
            var proposed = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            Assert.That(legacy.GenerationId, Is.EqualTo(TiledWorldDefinition.ReferenceGenerationId));
            Assert.That(proposed.GenerationId, Is.EqualTo(TiledWorldDefinition.ProposedWorldMapGenerationId));
            Assert.That(legacy.MapData, Is.Null);
            Assert.That(proposed.MapData, Is.Not.Null);
            yield return null;
        }
    }
}
