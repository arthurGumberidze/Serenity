using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Game.Domain;
using Game.Domain.Diagnostics;
using Game.Domain.Buildings;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Research;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture, Category("P10.8")]
    public sealed class P108DiagnosticsTests
    {
        [TestCase("serenity", DiagnosticCommandKind.UnlockAll)]
        [TestCase(" RES_SERENITY ", DiagnosticCommandKind.GrantResources)]
        [TestCase("Army_Serenity", DiagnosticCommandKind.CreateArmy)]
        [TestCase("family_serenity", DiagnosticCommandKind.CreateFamily)]
        public void ParserAcceptsOnlyExplicitCodes(string raw, DiagnosticCommandKind expected)
        {
            var parsed = DiagnosticCommandParser.Parse(raw);
            Assert.That(parsed.IsValid, Is.True);
            Assert.That(parsed.Command, Is.EqualTo(expected));
        }

        [Test]
        public void ParserRejectsEmptyAndUnknownWithoutCommand()
        {
            Assert.That(DiagnosticCommandParser.Parse(" ").Failure, Is.EqualTo(DiagnosticFailure.EmptyCode));
            Assert.That(DiagnosticCommandParser.Parse("iddqd").Failure, Is.EqualTo(DiagnosticFailure.UnknownCode));
        }

        [Test]
        public void UnlockAllUsesEntireCurrentCatalogAndIsRepeatSafe()
        {
            var capabilities = new[] { new CapabilityId("one"), new CapabilityId("two"), new CapabilityId("three") };
            var definitions = capabilities.Select((capability, index) => new TechnologyDefinition(
                new TechnologyId("tech_" + index), "Tech " + index, Array.Empty<TechnologyId>(),
                TimeSpan.FromHours(1), new[] { TechnologyUnlock.Capability(capability) },
                startingKnowledge: index == 0)).ToArray();
            var catalog = new TechnologyCatalog(definitions, new TechnologyUnlockTargets(
                Array.Empty<BuildingDefinitionId>(), Array.Empty<Game.Domain.Production.ProductionRecipeId>(), capabilities));
            var research = new ResearchService(catalog);

            Assert.That(research.CompleteAllForDiagnostics(), Is.EqualTo(2));
            Assert.That(research.CompletedTechnologies.Count, Is.EqualTo(definitions.Length));
            Assert.That(capabilities.All(research.HasCapability), Is.True);
            Assert.That(research.CompleteAllForDiagnostics(), Is.Zero);
            Assert.That(research.CompletedTechnologies.Count, Is.EqualTo(definitions.Length));
        }

        [Test]
        public void InventoryCapacityExpansionAndRollbackRestoreExactCanonicalState()
        {
            var catalog = new ResourceCatalog(new[]
            {
                new ResourceDefinition(new ResourceId("a"), "A", ResourceCategory.Food),
                new ResourceDefinition(new ResourceId("b"), "B", ResourceCategory.Construction)
            });
            var inventory = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage,
                StableEntityId.NewId()), catalog, 1);
            Assert.That(inventory.TryAdd(new ResourceId("a"), new ResourceQuantity(1)), Is.True);
            var before = inventory.CaptureState();

            inventory.EnsureCapacity(20001);
            Assert.That(inventory.TryAdd(new ResourceId("a"), new ResourceQuantity(10000)), Is.True);
            Assert.That(inventory.TryAdd(new ResourceId("b"), new ResourceQuantity(10000)), Is.True);
            inventory.RestoreState(before);

            Assert.That(inventory.Capacity, Is.EqualTo(1));
            Assert.That(inventory.TotalUnits, Is.EqualTo(1));
            Assert.That(inventory.GetAmount(new ResourceId("a")).Units, Is.EqualTo(1));
            Assert.That(inventory.GetAmount(new ResourceId("b")).Units, Is.Zero);
        }

        [Test]
        public void V25CodecPersistsCheatEvidenceAndRejectsMalformedDiagnostics()
        {
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, StableEntityId.NewId(),
                new GameTimeState(123, 456, 1, true), 400,
                diagnostics: new DiagnosticSaveState(true, 9));
            var codec = new SaveXmlCodec();
            var bytes = codec.Serialize(snapshot);
            var restored = codec.Deserialize(bytes);
            Assert.That(restored.FormatVersion, Is.EqualTo(SaveSnapshot.DiagnosticVersion));
            Assert.That(restored.Diagnostics.CheatsUsed, Is.True);
            Assert.That(restored.Diagnostics.AchievementsEnabled, Is.False);
            Assert.That(restored.Diagnostics.Sequence, Is.EqualTo(9));

            XDocument document;
            using (var stream = new MemoryStream(bytes, false)) document = XDocument.Load(stream);
            document.Root.Element("diagnostics").Element("sequence").Value = "not-a-number";
            Assert.Throws<InvalidDataException>(() => codec.Deserialize(
                Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting))));
        }

        [Test]
        public void OlderV24SaveRemainsReadableWithoutInventingCheatEvidence()
        {
            var snapshot = new SaveSnapshot(SaveSnapshot.MilitaryCampaignVersion, StableEntityId.NewId(),
                new GameTimeState(), 400);
            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            Assert.That(restored.Diagnostics, Is.Null);
            Assert.That(restored.FormatVersion, Is.EqualTo(SaveSnapshot.MilitaryCampaignVersion));
        }
    }
}
