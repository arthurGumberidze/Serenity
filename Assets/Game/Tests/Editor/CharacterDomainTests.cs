using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U06")]
    public sealed class CharacterDomainTests
    {
        [Test]
        public void NewCharactersReceiveValidDistinctStableIds()
        {
            var first = NewCharacter("Ada", CharacterSex.Female, 0);
            var second = NewCharacter("Borin", CharacterSex.Male, 0);

            Assert.That(first.Id.IsValid, Is.True);
            Assert.That(second.Id.IsValid, Is.True);
            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        }

        [Test]
        public void RestorePreservesExistingIdentityAndCompleteState()
        {
            var mother = StableEntityId.NewId();
            var biologicalFather = StableEntityId.NewId();
            var legalFather = StableEntityId.NewId();
            var family = StableEntityId.NewId();
            var dynasty = StableEntityId.NewId();
            var relation = StableEntityId.NewId();
            var original = Character.CreateNew(Data("Ilya", CharacterSex.Male, 42,
                new[]
                {
                    new ParentLink(mother, ParentRole.Mother, ParentageKind.Biological, true),
                    new ParentLink(biologicalFather, ParentRole.Father, ParentageKind.Biological, false),
                    new ParentLink(legalFather, ParentRole.Father, ParentageKind.Legal, true)
                }, family, dynasty, new ProfessionId("hunter"),
                new[] { new RelationshipState(relation, new RelationshipScore(31)) }));
            original.SetWealth(new Wealth(600));
            original.SetInfluence(new Influence(440));
            original.MarkDead(42 + Character.BiologicalTicksPerYear * 70);

            var state = original.CaptureState();
            var restored = Character.Restore(state);

            Assert.That(restored.Id, Is.EqualTo(original.Id));
            Assert.That(restored.Name, Is.EqualTo(original.Name));
            Assert.That(restored.Sex, Is.EqualTo(CharacterSex.Male));
            Assert.That(restored.LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(restored.BiologicalDeathTick, Is.EqualTo(state.BiologicalDeathTick));
            Assert.That(restored.FamilyId, Is.EqualTo(family));
            Assert.That(restored.DynastyId, Is.EqualTo(dynasty));
            Assert.That(restored.ProfessionId, Is.EqualTo(new ProfessionId("hunter")));
            Assert.That(restored.Parents.Count, Is.EqualTo(3));
            Assert.That(restored.Traits.Select(value => value.Value), Is.EqualTo(state.Traits.Select(value => value.Value)));
            Assert.That(restored.TryGetSkill(new SkillId("stone.knapping"), out var skill), Is.True);
            Assert.That(skill.Level.Value, Is.EqualTo(73));
            Assert.That(restored.TryGetRelationship(relation, out var relationship), Is.True);
            Assert.That(relationship.Affinity.Value, Is.EqualTo(31));
            Assert.That(restored.Wealth.Value, Is.EqualTo(600));
            Assert.That(restored.Influence.Value, Is.EqualTo(440));
        }

        [Test]
        public void EmptyRestoredIdentityIsRejected()
        {
            var source = NewCharacter("Ada", CharacterSex.Female, 0).CaptureState();
            Assert.Throws<ArgumentException>(() => Character.Restore(Copy(source, default)));
        }

        [Test]
        public void IdentityDoesNotChangeWhenMutableStateChanges()
        {
            var character = NewCharacter("Ada", CharacterSex.Female, 0);
            var id = character.Id;

            character.Rename(new CharacterName("Ada", "North"));
            character.AssignProfession(new ProfessionId("healer"));
            character.SetSkill(new SkillState(new SkillId("medicine"), new SkillLevel(88)));
            character.SetHealth(new BodyHealth(new HealthValue(90), new HealthValue(80), new HealthValue(70),
                new HealthValue(60), new HealthValue(50), new HealthValue(40)));
            character.SetWealth(new Wealth(12));
            character.SetInfluence(new Influence(34));

            Assert.That(character.Id, Is.EqualTo(id));
            Assert.That(character.CaptureState().Id, Is.EqualTo(id));
        }

        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase(" Ada", "")]
        [TestCase("Ada ", "")]
        [TestCase("Ada", " North")]
        public void InvalidNamesAreRejected(string givenName, string familyName)
        {
            Assert.Throws<ArgumentException>(() => new CharacterName(givenName, familyName));
        }

        [Test]
        public void InvalidSexAndBirthTickAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Character.CreateNew(Data("Ada", (CharacterSex)99, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Character.CreateNew(Data("Ada", CharacterSex.Female, -1)));
        }

        [Test]
        public void AgeUsesOnlyBiologicalTicksAndExactYearBoundaries()
        {
            var birth = TimeSpan.FromDays(123).Ticks;
            var character = NewCharacter("Ada", CharacterSex.Female, birth);

            Assert.That(character.AgeInCompletedYears(birth), Is.Zero);
            Assert.That(character.AgeInCompletedYears(birth + Character.BiologicalTicksPerYear - 1), Is.Zero);
            Assert.That(character.AgeInCompletedYears(birth + Character.BiologicalTicksPerYear), Is.EqualTo(1));
            Assert.That(character.AgeInCompletedYears(birth + Character.BiologicalTicksPerYear * 16), Is.EqualTo(16));
            Assert.Throws<ArgumentOutOfRangeException>(() => character.AgeInCompletedYears(birth - 1));
        }

        [Test]
        public void AgeHandlesLargestBiologicalTickWithoutOverflow()
        {
            var character = NewCharacter("Old", CharacterSex.Male, 0);
            Assert.That(character.AgeInCompletedYears(long.MaxValue), Is.GreaterThan(29000));
        }

        [Test]
        public void DeathUsesBiologicalTickAndFreezesAge()
        {
            var character = NewCharacter("Ada", CharacterSex.Female, 0);
            var death = Character.BiologicalTicksPerYear * 63 + Character.BiologicalTicksPerYear / 2;

            character.MarkDead(death);

            Assert.That(character.AgeInCompletedYears(death + Character.BiologicalTicksPerYear * 10), Is.EqualTo(63));
            Assert.Throws<InvalidOperationException>(() => character.MarkDead(death + 1));
        }

        [Test]
        public void SelfParentIsRejectedAndParentsAreOptional()
        {
            var character = NewCharacter("Ada", CharacterSex.Female, 0);
            Assert.That(character.Parents, Is.Empty);

            Assert.Throws<ArgumentException>(() => character.ReplaceParents(new[]
            {
                new ParentLink(character.Id, ParentRole.Mother, ParentageKind.Biological, true)
            }));
        }

        [Test]
        public void BiologicalAndLegalFatherCanDifferAndSurviveRestore()
        {
            var biological = StableEntityId.NewId();
            var legal = StableEntityId.NewId();
            var character = NewCharacter("Ada", CharacterSex.Female, 0);
            character.ReplaceParents(new[]
            {
                new ParentLink(biological, ParentRole.Father, ParentageKind.Biological, false),
                new ParentLink(legal, ParentRole.Father, ParentageKind.Legal, true)
            });

            var restored = Character.Restore(character.CaptureState());

            Assert.That(restored.Parents.Count, Is.EqualTo(2));
            Assert.That(restored.Parents.Single(parent => parent.Kind == ParentageKind.Biological).IsKnownToCharacter, Is.False);
            Assert.That(restored.Parents.Single(parent => parent.Kind == ParentageKind.Legal).ParentId, Is.EqualTo(legal));
        }

        [Test]
        public void DuplicateParentRoleAndKindIsRejected()
        {
            Assert.Throws<ArgumentException>(() => Character.CreateNew(Data("Ada", CharacterSex.Female, 0,
                new[]
                {
                    new ParentLink(StableEntityId.NewId(), ParentRole.Father, ParentageKind.Biological, true),
                    new ParentLink(StableEntityId.NewId(), ParentRole.Father, ParentageKind.Biological, false)
                })));
        }

        [Test]
        public void RegistryRejectsDuplicateActiveIdentity()
        {
            var original = NewCharacter("Ada", CharacterSex.Female, 0);
            var duplicate = Character.Restore(original.CaptureState());
            var registry = new CharacterRegistry();
            registry.Add(original);

            Assert.Throws<InvalidOperationException>(() => registry.Add(duplicate));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void RegistryLinksSpousesSymmetricallyAndRejectsSelfMarriage()
        {
            var first = NewCharacter("Ada", CharacterSex.Female, 0);
            var second = NewCharacter("Borin", CharacterSex.Male, 0);
            var registry = new CharacterRegistry();
            registry.Add(first);
            registry.Add(second);

            registry.LinkSpouses(first.Id, second.Id);

            Assert.That(first.SpouseId, Is.EqualTo(second.Id));
            Assert.That(second.SpouseId, Is.EqualTo(first.Id));
            Assert.Throws<ArgumentException>(() => registry.LinkSpouses(first.Id, first.Id));
            registry.UnlinkSpouses(first.Id, second.Id);
            Assert.That(first.SpouseId, Is.Null);
            Assert.That(second.SpouseId, Is.Null);
        }

        [Test]
        public void ChildrenAreDerivedFromCanonicalChildParentLinks()
        {
            var parent = NewCharacter("Ada", CharacterSex.Female, 0);
            var child = Character.CreateNew(Data("Cora", CharacterSex.Female, Character.BiologicalTicksPerYear * 20,
                new[] { new ParentLink(parent.Id, ParentRole.Mother, ParentageKind.Biological, true) }));
            var unrelated = NewCharacter("Daro", CharacterSex.Male, 0);
            var registry = new CharacterRegistry();
            registry.Add(unrelated);
            registry.Add(child);
            registry.Add(parent);

            Assert.That(registry.GetChildren(parent.Id).Select(value => value.Id), Is.EqualTo(new[] { child.Id }));
        }

        [Test]
        public void FullNpcRequiresFourOrFiveUniqueTraits()
        {
            var tooFew = Data("Ada", CharacterSex.Female, 0, traits: new[]
                { new TraitId("calm"), new TraitId("brave"), new TraitId("kind") });
            var duplicate = Data("Ada", CharacterSex.Female, 0, traits: new[]
                { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("calm") });

            Assert.Throws<ArgumentException>(() => Character.CreateNew(tooFew));
            Assert.Throws<ArgumentException>(() => Character.CreateNew(duplicate));
        }

        [TestCase("Mining")]
        [TestCase("stone knapping")]
        [TestCase("")]
        public void DefinitionIdsRejectNonCanonicalKeys(string value)
        {
            Assert.Throws<ArgumentException>(() => new SkillId(value));
            Assert.Throws<ArgumentException>(() => new TraitId(value));
            Assert.Throws<ArgumentException>(() => new ProfessionId(value));
        }

        [Test]
        public void SkillsAreBoundedDataDrivenValuesAndCapturedDeterministically()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillLevel(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkillLevel(101));
            var character = NewCharacter("Ada", CharacterSex.Female, 0);
            character.SetSkill(new SkillState(new SkillId("medicine"), new SkillLevel(44)));

            var keys = character.CaptureState().Skills.Select(skill => skill.Id.Value).ToArray();

            Assert.That(keys, Is.Ordered.Using<string>(StringComparer.Ordinal));
            Assert.That(character.TryGetSkill(new SkillId("medicine"), out var skill), Is.True);
            Assert.That(skill.Level.Value, Is.EqualTo(44));
        }

        [Test]
        public void DuplicateSkillAndRelationshipEntriesAreRejectedOnRestore()
        {
            var id = StableEntityId.NewId();
            var relation = StableEntityId.NewId();
            var duplicateSkills = new CharacterState(id, new CharacterName("Ada"), CharacterSex.Female, 0,
                CharacterLifeState.Alive, null, null, null, null, null, null, Traits(),
                new[]
                {
                    new SkillState(new SkillId("medicine"), new SkillLevel(1)),
                    new SkillState(new SkillId("medicine"), new SkillLevel(2))
                }, Attributes(), BodyHealth.Healthy, new Wealth(0), new Influence(0), null);
            var duplicateRelationships = new CharacterState(id, new CharacterName("Ada"), CharacterSex.Female, 0,
                CharacterLifeState.Alive, null, null, null, null, null, null, Traits(),
                new[] { new SkillState(new SkillId("medicine"), new SkillLevel(1)) },
                Attributes(), BodyHealth.Healthy, new Wealth(0), new Influence(0),
                new[]
                {
                    new RelationshipState(relation, new RelationshipScore(1)),
                    new RelationshipState(relation, new RelationshipScore(2))
                });

            Assert.Throws<ArgumentException>(() => Character.Restore(duplicateSkills));
            Assert.Throws<ArgumentException>(() => Character.Restore(duplicateRelationships));
        }

        [Test]
        public void SelfRelationshipAndOutOfRangeScoreAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelationshipScore(-101));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelationshipScore(101));
            var character = NewCharacter("Ada", CharacterSex.Female, 0);
            Assert.Throws<ArgumentException>(() => character.SetRelationship(default));
            Assert.Throws<ArgumentException>(() => character.SetRelationship(
                new RelationshipState(character.Id, new RelationshipScore(0))));
        }

        [Test]
        public void HealthAttributesWealthAndInfluenceUseDocumentedBounds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthValue(101));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterAttributeValue(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wealth(1001));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Influence(-1));
            var health = BodyHealth.Healthy;
            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
                Assert.That(health.Get(part).Value, Is.EqualTo(100));
        }

        [Test]
        public void FamilyDynastyAndProfessionAreOptionalStableReferences()
        {
            var character = NewCharacter("Ada", CharacterSex.Female, 0);
            Assert.That(character.FamilyId, Is.Null);
            Assert.That(character.DynastyId, Is.Null);
            Assert.That(character.ProfessionId, Is.Null);

            var family = StableEntityId.NewId();
            var dynasty = StableEntityId.NewId();
            character.SetFamily(family, dynasty);
            character.AssignProfession(new ProfessionId("gatherer"));

            Assert.That(character.CaptureState().FamilyId, Is.EqualTo(family));
            Assert.That(character.CaptureState().DynastyId, Is.EqualTo(dynasty));
            Assert.That(character.CaptureState().ProfessionId.Value.Value, Is.EqualTo("gatherer"));
        }

        [Test]
        public void CreationAndCapturedStateDetachCollectionInputs()
        {
            var traits = Traits();
            var data = Data("Ada", CharacterSex.Female, 0, traits: traits);
            traits[0] = new TraitId("changed.before.create");
            var character = Character.CreateNew(data);
            var captured = character.CaptureState();
            traits[1] = new TraitId("changed.after.capture");

            Assert.That(character.Traits.Select(value => value.Value), Does.Not.Contain("changed.before.create"));
            Assert.That(captured.Traits.Select(value => value.Value), Does.Not.Contain("changed.after.capture"));
        }

        private static Character NewCharacter(string name, CharacterSex sex, long birthTick)
        {
            return Character.CreateNew(Data(name, sex, birthTick));
        }

        private static CharacterCreationData Data(string name, CharacterSex sex, long birthTick,
            ParentLink[] parents = null, StableEntityId? family = null, StableEntityId? dynasty = null,
            ProfessionId? profession = null, RelationshipState[] relationships = null, TraitId[] traits = null)
        {
            return new CharacterCreationData(new CharacterName(name), sex, birthTick, parents, null, family,
                dynasty, profession, traits ?? Traits(), new[]
                {
                    new SkillState(new SkillId("stone.knapping"), new SkillLevel(73)),
                    new SkillState(new SkillId("foraging"), new SkillLevel(41))
                }, Attributes(), BodyHealth.Healthy, new Wealth(0), new Influence(0), relationships);
        }

        private static TraitId[] Traits()
        {
            return new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") };
        }

        private static CharacterAttributes Attributes()
        {
            return new CharacterAttributes(new CharacterAttributeValue(64), new CharacterAttributeValue(57));
        }

        private static CharacterState Copy(CharacterState source, StableEntityId id)
        {
            return new CharacterState(id, source.Name, source.Sex, source.BiologicalBirthTick,
                source.LifeState, source.BiologicalDeathTick, source.Parents, source.SpouseId, source.FamilyId,
                source.DynastyId, source.ProfessionId, source.Traits, source.Skills, source.Attributes,
                source.Health, source.Wealth, source.Influence, source.Relationships);
        }
    }
}
