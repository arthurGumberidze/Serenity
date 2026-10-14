using System;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture, Category("P86")]
    public sealed class P86IntegrationQualificationTests
    {
        [TestCase("-u29Benchmark", "-u29Seed", "20261006", 20261006L)]
        [TestCase("-u30Soak", "-u30Seed", "3030", 3030L)]
        [TestCase("-u31Smoke", "-u31SmokeSeed", "310031", 310031L)]
        public void TechnicalScenarioUsesNormalNewGameSeedBoundary(
            string flag, string seedArgument, string seedText, long expected)
        {
            Assert.That(TechnicalScenarioLaunch.TryResolveWorldSeed(
                new[] { "Serenity.exe", flag, seedArgument, seedText }, out var seed), Is.True);
            Assert.That(seed, Is.EqualTo(expected));
        }

        [Test]
        public void OrdinaryStartupDoesNotRequestTechnicalLaunch()
        {
            Assert.That(TechnicalScenarioLaunch.TryResolveWorldSeed(
                new[] { "Serenity.exe", "-screen-width", "1280" }, out var seed), Is.False);
            Assert.That(seed, Is.Zero);
        }

        [Test]
        public void ConflictingTechnicalScenariosFailClosed()
        {
            Assert.Throws<InvalidOperationException>(() => TechnicalScenarioLaunch.TryResolveWorldSeed(
                new[] { "Serenity.exe", "-u29Benchmark", "-u29Seed", "1", "-u30Soak", "-u30Seed", "2" },
                out _));
        }

        [TestCase("-u29Benchmark", "-u29Seed", "0")]
        [TestCase("-u30Soak", "-u30Seed", "not-a-seed")]
        [TestCase("-u31Smoke", "-u31SmokeSeed", "-7")]
        public void InvalidTechnicalSeedFailsClosed(string flag, string seedArgument, string seedText)
        {
            Assert.Throws<InvalidOperationException>(() => TechnicalScenarioLaunch.TryResolveWorldSeed(
                new[] { "Serenity.exe", flag, seedArgument, seedText }, out _));
        }
    }
}
