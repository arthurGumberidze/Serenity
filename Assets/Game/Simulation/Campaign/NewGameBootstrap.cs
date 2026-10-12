using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Dynasties;

namespace Game.Simulation.Campaign
{
    public sealed class NewGameBootstrapResult
    {
        public NewGameBootstrapResult(NewGameConfiguration configuration, IEnumerable<CharacterState> playerCharacters,
            DynastyStateSnapshot dynasty, StableEntityId monarchId, StableEntityId spouseId,
            StableEntityId firstHeirId, StableEntityId secondHeirId)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            var characters = (playerCharacters ?? throw new ArgumentNullException(nameof(playerCharacters))).ToArray();
            if (characters.Length != 10 || characters.Select(x => x.Id).Distinct().Count() != 10)
                throw new ArgumentException("Stone Age MVP requires exactly ten unique player Characters.", nameof(playerCharacters));
            Dynasty = dynasty ?? throw new ArgumentNullException(nameof(dynasty));
            if (dynasty.CurrentMonarchId != monarchId || !dynasty.MemberIds.Contains(monarchId))
                throw new ArgumentException("Bootstrap monarch must own the player dynasty.", nameof(monarchId));
            PlayerCharacters = new ReadOnlyCollection<CharacterState>(characters);
            MonarchId = monarchId;
            SpouseId = spouseId;
            FirstHeirId = firstHeirId;
            SecondHeirId = secondHeirId;
        }

        public NewGameConfiguration Configuration { get; }
        public IReadOnlyList<CharacterState> PlayerCharacters { get; }
        public DynastyStateSnapshot Dynasty { get; }
        public StableEntityId MonarchId { get; }
        public StableEntityId SpouseId { get; }
        public StableEntityId FirstHeirId { get; }
        public StableEntityId SecondHeirId { get; }
    }

    public static class NewGameBootstrap
    {
        private static readonly string[] Names =
            { "Aren", "Mira", "Cale", "Doran", "Pela", "Neris", "Tarin", "Elda", "Rovan", "Sela" };

        public static NewGameBootstrapResult Create(NewGameConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var dynastyId = DeterministicStableId.Create(configuration.WorldSeed, "player_dynasty");
            var ids = Enumerable.Range(0, 10)
                .Select(i => DeterministicStableId.Create(configuration.WorldSeed, "player_character", i)).ToArray();
            var values = new CharacterState[10];
            for (var i = 0; i < values.Length; i++)
            {
                var parents = i == 2 || i == 3
                    ? new[] { Parent(ids[0], ParentRole.Father), Parent(ids[1], ParentRole.Mother) }
                    : Array.Empty<ParentLink>();
                values[i] = new CharacterState(ids[i], new CharacterName(Names[i]),
                    i == 1 || i == 4 || i == 7 || i == 9 ? CharacterSex.Female : CharacterSex.Male,
                    0, CharacterLifeState.Alive, null, parents,
                    i == 0 ? ids[1] : i == 1 ? ids[0] : (StableEntityId?)null,
                    i <= 3 && i != 1 ? dynastyId : (StableEntityId?)null,
                    i <= 3 && i != 1 ? dynastyId : (StableEntityId?)null,
                    i >= 4 ? (ProfessionId?)new ProfessionId(i % 2 == 0 ? "worker" : "farmer") : null,
                    new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                    new[] { new SkillState(new SkillId("combat"), new SkillLevel(25 + i)),
                        new SkillState(new SkillId("research"), new SkillLevel(20 + i)) },
                    new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                    BodyHealth.Healthy, new Wealth(0), new Influence(i == 0 ? 100 : 10), null);
            }
            var dynasty = DynastyState.Create(dynastyId, "House Serenity", ids[0], ids[0],
                new[] { ids[0], ids[2], ids[3] }).CaptureState();
            return new NewGameBootstrapResult(configuration, values, dynasty, ids[0], ids[1], ids[2], ids[3]);
        }

        private static ParentLink Parent(StableEntityId id, ParentRole role) =>
            new ParentLink(id, role, ParentageKind.Biological, true);
    }
}
