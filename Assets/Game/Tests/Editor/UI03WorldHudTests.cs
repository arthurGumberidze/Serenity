using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Work;
using Game.Presentation.UI;
using Game.Simulation.Work;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture, Category("UI03")]
    public sealed class UI03WorldHudTests
    {
        [Test]
        public void SelectionQueueProjectionReadsCanonicalOrdersWithoutOwningThem()
        {
            var characters = new CharacterRegistry();
            var first = CreateCharacter("Aren");
            var second = CreateCharacter("Mira");
            characters.Add(first);
            characters.Add(second);
            var work = new WorkManager(characters);
            var firstOrder = work.CreateMoveJob(first.Id, new WorldPosition(1, 0, 1),
                WorkPriority.High, queuePlacement: WorkQueuePlacement.Tail);
            var secondOrder = work.CreateMoveJob(second.Id, new WorldPosition(2, 0, 2),
                WorkPriority.Normal, queuePlacement: WorkQueuePlacement.Tail);

            var projection = GameplaySelectionQueueProjection.Build(new[] { second.Id, first.Id }, work, 1);

            Assert.That(projection.SelectedCount, Is.EqualTo(2));
            Assert.That(projection.BusyCount, Is.EqualTo(2));
            Assert.That(projection.TotalOrders, Is.EqualTo(2));
            Assert.That(projection.VisibleOrders, Has.Count.EqualTo(1));
            Assert.That(work.Get(firstOrder.Id), Is.SameAs(firstOrder));
            Assert.That(work.Get(secondOrder.Id), Is.SameAs(secondOrder));
            Assert.That(work.Jobs.Count(order => !order.IsTerminal), Is.EqualTo(2));
        }

        [Test]
        public void QueueProjectionRejectsInvalidLimitsAndKeepsStableSelectionDistinct()
        {
            var characters = new CharacterRegistry();
            var person = CreateCharacter("Doran");
            characters.Add(person);
            var work = new WorkManager(characters);

            Assert.That(GameplaySelectionQueueProjection.Build(new[] { person.Id, person.Id }, work).SelectedCount,
                Is.EqualTo(1));
            Assert.That(() => GameplaySelectionQueueProjection.Build(new[] { person.Id }, work, -1),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        private static Character CreateCharacter(string name) => Character.CreateNew(new CharacterCreationData(
            new CharacterName(name), CharacterSex.Male, 0, null, null, null, null, null,
            new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") }, null,
            new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
    }
}
