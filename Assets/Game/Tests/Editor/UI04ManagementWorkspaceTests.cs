using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Work;
using Game.Simulation.Work;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    [TestFixture, Category("UI04")]
    public sealed class UI04ManagementWorkspaceTests
    {
        private static readonly long AdultTick = Character.BiologicalTicksPerYear * 20L;

        [Test]
        public void StartingAndNewAdultsReceiveProfessionAlignedGroupsWithGeneralFallback()
        {
            var characters = new CharacterRegistry();
            var builder = Create("Builder", "builder", 0);
            var unknown = Create("Unknown", null, 0);
            var child = Create("Child", "hunter", Character.BiologicalTicksPerYear * 10L);
            characters.Add(builder);
            characters.Add(unknown);
            characters.Add(child);
            var work = new WorkManager(characters);
            var service = new ProfessionGroupService(characters, work);

            Assert.That(service.ReconcileAdults(AdultTick), Is.EqualTo(2));
            Assert.That(work.Groups.GetFor(builder.Id).Role, Is.EqualTo(WorkGroupRole.Builders));
            Assert.That(work.Groups.GetFor(unknown.Id).Role, Is.EqualTo(WorkGroupRole.General));
            Assert.That(unknown.ProfessionId.Value.Value, Is.EqualTo("worker"));
            Assert.That(work.Groups.TryGetForMember(child.Id, out _), Is.False);

            Assert.That(service.ReconcileAdults(Character.BiologicalTicksPerYear * 30L), Is.EqualTo(1));
            Assert.That(work.Groups.GetFor(child.Id).Role, Is.EqualTo(WorkGroupRole.Hunters));
        }

        [Test]
        public void ManualReassignmentChangesProfessionAndGroupInOneTransaction()
        {
            var characters = new CharacterRegistry();
            var resident = Create("Aren", "builder", 0);
            characters.Add(resident);
            var work = new WorkManager(characters);
            var service = new ProfessionGroupService(characters, work);
            service.ReconcileAdults(AdultTick);
            var hunters = service.Primary(WorkGroupRole.Hunters);

            service.Reassign(resident.Id, hunters.Id);

            Assert.That(work.Groups.GetFor(resident.Id), Is.SameAs(hunters));
            Assert.That(resident.ProfessionId.Value.Value, Is.EqualTo("hunter"));
        }

        [Test]
        public void DeletingNonEmptyGroupRequiresAndAppliesExplicitReplacement()
        {
            var characters = new CharacterRegistry();
            var resident = Create("Mira", "builder", 0);
            characters.Add(resident);
            var work = new WorkManager(characters);
            var service = new ProfessionGroupService(characters, work);
            service.ReconcileAdults(AdultTick);
            var builders = service.Primary(WorkGroupRole.Builders);
            var general = service.Primary(WorkGroupRole.General);

            Assert.Throws<InvalidOperationException>(() => work.DeleteEmptyGroup(builders.Id));
            service.DeleteGroup(builders.Id, general.Id);

            Assert.That(work.Groups.TryGet(builders.Id, out _), Is.False);
            Assert.That(work.Groups.GetFor(resident.Id), Is.SameAs(general));
            Assert.That(resident.ProfessionId.Value.Value, Is.EqualTo("worker"));
        }

        [Test]
        public void UrgentOrderIsFirstAndInterruptedEligibleWorkIsSecondWithProgressPreserved()
        {
            var characters = new CharacterRegistry();
            var builder = Create("Builder", "builder", 0);
            var gatherer = Create("Gatherer", "gatherer", 0);
            characters.Add(builder);
            characters.Add(gatherer);
            var work = new WorkManager(characters);
            var service = new ProfessionGroupService(characters, work);
            service.ReconcileAdults(AdultTick);
            var builders = service.Primary(WorkGroupRole.Builders);
            var old = work.CreateDirectJob(WorkJobType.Build, builder.Id,
                WorkTarget.ForEntity(WorkTargetKind.Building, StableEntityId.NewId()), WorkPriority.Normal,
                builders.Id);
            work.Start(old.Id);
            old.AdvanceProgress(TimeSpan.FromMinutes(7));
            var urgent = work.CreateDirectJob(WorkJobType.Build, null,
                WorkTarget.ForEntity(WorkTargetKind.Building, StableEntityId.NewId()), WorkPriority.Immediate,
                builders.Id);

            Assert.Throws<InvalidOperationException>(() => work.Assign(urgent.Id, gatherer.Id,
                WorkQueuePlacement.Front));
            work.AssignUrgentAheadOf(urgent.Id, builder.Id, old.Id);

            Assert.That(work.GetQueue(builder.Id).Select(order => order.Id),
                Is.EqualTo(new[] { urgent.Id, old.Id }));
            Assert.That(old.ProgressTicks, Is.EqualTo(TimeSpan.FromMinutes(7).Ticks));
            Assert.That(old.Status, Is.EqualTo(WorkJobStatus.Assigned));
        }

        [Test]
        public void GroupMembershipAndUrgentQueueOrderSurviveWorkStateRoundTrip()
        {
            var characters = new CharacterRegistry();
            var builder = Create("Builder", "builder", 0);
            characters.Add(builder);
            var work = new WorkManager(characters);
            var service = new ProfessionGroupService(characters, work);
            service.ReconcileAdults(AdultTick);
            var builders = service.Primary(WorkGroupRole.Builders);
            var old = work.CreateDirectJob(WorkJobType.Build, builder.Id,
                WorkTarget.ForEntity(WorkTargetKind.Building, StableEntityId.NewId()), WorkPriority.Normal,
                builders.Id);
            var urgent = work.CreateDirectJob(WorkJobType.Build, null,
                WorkTarget.ForEntity(WorkTargetKind.Building, StableEntityId.NewId()), WorkPriority.Immediate,
                builders.Id);
            work.AssignUrgentAheadOf(urgent.Id, builder.Id, old.Id);

            var restored = new WorkManager(characters);
            restored.RestoreState(work.CaptureState());

            Assert.That(restored.Groups.GetFor(builder.Id).Role, Is.EqualTo(WorkGroupRole.Builders));
            Assert.That(restored.GetQueue(builder.Id).Select(order => order.Id),
                Is.EqualTo(new[] { urgent.Id, old.Id }));
        }

        [Test]
        public void ManagementWorkspaceUsesBoundedRosterAndCanonicalCommandSurfaces()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath,
                "Game/Presentation/UI/GameplayUiController.cs"));
            StringAssert.Contains("new ListView(residents", source);
            var roster = File.ReadAllText(Path.Combine(Application.dataPath,
                "Game/Presentation/UI/GameplayUiController.WorkRoster.cs"));
            StringAssert.Contains("ProfessionGroups.Reassign", roster);
            StringAssert.Contains("SetMemberShift", roster);
            StringAssert.Contains("CollectionVirtualizationMethod.FixedHeight", roster);
            StringAssert.DoesNotContain("foreign tasks are forbidden", source);
        }

        private static Character Create(string name, string profession, long birthTick) =>
            Character.CreateNew(new CharacterCreationData(new CharacterName(name), CharacterSex.Male, birthTick,
                null, null, null, null, profession == null ? (ProfessionId?)null : new ProfessionId(profession),
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
    }

    internal static class UI04WorkGroupTestExtensions
    {
        public static WorkGroup GetFor(this WorkGroupRegistry groups, StableEntityId characterId) =>
            groups.TryGetForMember(characterId, out var group)
                ? group
                : throw new InvalidOperationException("Resident is not assigned to a work group.");
    }
}
