using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Infrastructure;
using Game.Simulation.Work;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture]
    [Category("U27")]
    public sealed class GameplayUiTests
    {
        [Test]
        public void PlayerSlotsUseStableDeterministicIdentifiers()
        {
            var ids = Enumerable.Range(1, LocalGameplaySaveFacade.SlotCount)
                .Select(LocalGameplaySaveFacade.SlotId).ToArray();

            Assert.That(ids.Distinct().Count(), Is.EqualTo(LocalGameplaySaveFacade.SlotCount));
            Assert.That(ids.Select(x => x.ToString()), Is.Ordered);
            Assert.Throws<ArgumentOutOfRangeException>(() => LocalGameplaySaveFacade.SlotId(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => LocalGameplaySaveFacade.SlotId(4));
        }

        [Test]
        public void SaveCatalogReportsCorruptSlotsWithoutThrowingOrExposingParserDetails()
        {
            var directory = Path.Combine(Path.GetTempPath(), "serenity-u27-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var slot = LocalGameplaySaveFacade.SlotId(1);
                File.WriteAllText(Path.Combine(directory, slot + ".xml"), "not a save");

                var metadata = new FileSaveStore(directory).ListSlots().Single();

                Assert.That(metadata.SlotId, Is.EqualTo(slot));
                Assert.That(metadata.IsCorrupt, Is.True);
                Assert.That(metadata.CalendarTicks, Is.Null);
                Assert.That(metadata.Error, Is.EqualTo("Save data is unavailable or corrupt."));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void WorkGroupRenameAndCommanderCommandsMutateCanonicalGroup()
        {
            var characters = new CharacterRegistry();
            var character = Character.CreateNew(new CharacterCreationData(
                new CharacterName("Mira"), CharacterSex.Female, 0, null, null, null, null, null,
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") }, null,
                new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
            characters.Add(character);
            var work = new WorkManager(characters);
            var group = work.CreateGroup("Old name", new[] { character.Id });

            work.RenameGroup(group.Id, "Builders");
            work.SetCommander(group.Id, character.Id);

            Assert.That(group.DisplayName, Is.EqualTo("Builders"));
            Assert.That(group.CommanderId, Is.EqualTo(character.Id));
        }
    }
}
