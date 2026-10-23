using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Dynasties;
using Game.Domain.World;
using Game.Simulation.Characters;

namespace Game.Simulation.Campaign
{
    public sealed class NewGameBootstrapResult
    {
        public NewGameBootstrapResult(NewGameConfiguration configuration, IEnumerable<CharacterState> playerCharacters,
            DynastyStateSnapshot dynasty, StableEntityId monarchId, StableEntityId spouseId,
            StableEntityId firstHeirId, StableEntityId secondHeirId, AppearanceWorldSaveState appearances)
            : this(configuration, playerCharacters, dynasty, monarchId, spouseId, firstHeirId, secondHeirId,
                appearances, null)
        {
        }

        public NewGameBootstrapResult(NewGameConfiguration configuration, IEnumerable<CharacterState> playerCharacters,
            DynastyStateSnapshot dynasty, StableEntityId monarchId, StableEntityId spouseId,
            StableEntityId firstHeirId, StableEntityId secondHeirId, AppearanceWorldSaveState appearances,
            WorldMapStartTerritoryPlan startTerritory)
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
            Appearances = appearances ?? throw new ArgumentNullException(nameof(appearances));
            if (appearances.Characters.Count != 10 || appearances.Characters.Select(x => x.CharacterId).Distinct().Count() != 10)
                throw new ArgumentException("Every starting Character requires one appearance.", nameof(appearances));
            if (startTerritory != null && startTerritory.WorldSeed != configuration.WorldSeed)
                throw new ArgumentException("Start territory and New Game must share one world seed.", nameof(startTerritory));
            StartTerritory = startTerritory;
        }

        public NewGameConfiguration Configuration { get; }
        public IReadOnlyList<CharacterState> PlayerCharacters { get; }
        public DynastyStateSnapshot Dynasty { get; }
        public StableEntityId MonarchId { get; }
        public StableEntityId SpouseId { get; }
        public StableEntityId FirstHeirId { get; }
        public StableEntityId SecondHeirId { get; }
        public AppearanceWorldSaveState Appearances { get; }
        public WorldMapStartTerritoryPlan StartTerritory { get; }
    }

    public static class NewGameBootstrap
    {
        private static readonly string[] Names =
            { "Aren", "Mira", "Cale", "Doran", "Pela", "Neris", "Tarin", "Elda", "Rovan", "Sela" };

        public static NewGameBootstrapResult Create(NewGameConfiguration configuration,
            AppearanceDefinitionCatalog appearanceCatalog = null, TiledWorldPopulationPlan populationPlan = null)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            appearanceCatalog = appearanceCatalog ?? StoneAgeAppearanceProfiles.CreateCatalog();
            var rulerProfile = appearanceCatalog.Get(configuration.RulerAppearanceProfileId);
            if (rulerProfile.Sex != configuration.RulerSex)
                throw new ArgumentException("Ruler appearance is incompatible with ruler sex.", nameof(configuration));
            var dynastyId = DeterministicStableId.Create(configuration.WorldSeed, "player_dynasty");
            var ids = Enumerable.Range(0, 10)
                .Select(i => DeterministicStableId.Create(configuration.WorldSeed, "player_character", i)).ToArray();
            var values = new CharacterState[10];
            for (var i = 0; i < values.Length; i++)
            {
                var sex = i == 0 ? configuration.RulerSex : i == 1
                    ? (configuration.RulerSex == CharacterSex.Male ? CharacterSex.Female : CharacterSex.Male)
                    : i == 4 || i == 7 || i == 9 ? CharacterSex.Female : CharacterSex.Male;
                var parentLinks = i == 2 || i == 3
                    ? new[]
                    {
                        Parent(configuration.RulerSex == CharacterSex.Male ? ids[0] : ids[1], ParentRole.Father),
                        Parent(configuration.RulerSex == CharacterSex.Female ? ids[0] : ids[1], ParentRole.Mother)
                    }
                    : Array.Empty<ParentLink>();
                var name = i == 0 ? configuration.RulerName : i == 1 && configuration.RulerSex == CharacterSex.Female
                    ? "Aren" : Names[i];
                values[i] = new CharacterState(ids[i], new CharacterName(name),
                    sex,
                    0, CharacterLifeState.Alive, null, parentLinks,
                    i == 0 ? ids[1] : i == 1 ? ids[0] : (StableEntityId?)null,
                    i <= 3 && i != 1 ? dynastyId : (StableEntityId?)null,
                    i <= 3 && i != 1 ? dynastyId : (StableEntityId?)null,
                    i >= 4 ? (ProfessionId?)new ProfessionId(i % 2 == 0 ? "worker" : "farmer") : null,
                    new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                    StartingSkills(i),
                    new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                    BodyHealth.Healthy, new Wealth(0), new Influence(i == 0 ? 100 : 10), null);
            }
            var dynasty = DynastyState.Create(dynastyId, configuration.DynastyName, ids[0], ids[0],
                new[] { ids[0], ids[2], ids[3] }).CaptureState();
            var appearanceRegistry = new CharacterRegistry();
            appearanceRegistry.RestoreStates(values);
            var appearanceService = new CharacterAppearanceService(appearanceRegistry, appearanceCatalog,
                configuration.WorldSeed);
            appearanceService.EnsureDefaults(appearanceRegistry.GetAll());
            var appearances = new List<AppearanceState>(values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                var generated = appearanceService.Get(ids[i]);
                if (i == 0 && configuration.RulerAppearance != null)
                    generated = configuration.RulerAppearance.With(characterId: ids[i]);
                else if (i == 0 && generated.ProfileId != rulerProfile.Id)
                    generated = rulerProfile.CreateState(ids[i]);
                if (i > 0)
                {
                    var profile = appearanceCatalog.Get(generated.ProfileId);
                    generated = generated.With(
                        ageId: profile.Ages[i % profile.Ages.Count],
                        clothingId: i == 8
                            ? StoneAgeAppearanceProfiles.MilitaryClothing
                            : i >= 4 ? StoneAgeAppearanceProfiles.WorkerClothing : StoneAgeAppearanceProfiles.CivilianClothing);
                }
                appearances.Add(generated);
            }
            return new NewGameBootstrapResult(configuration, values, dynasty, ids[0], ids[1], ids[2], ids[3],
                new AppearanceWorldSaveState(appearances), populationPlan?.StartTerritory);
        }

        private static ParentLink Parent(StableEntityId id, ParentRole role) =>
            new ParentLink(id, role, ParentageKind.Biological, true);

        private static IReadOnlyList<SkillState> StartingSkills(int index)
        {
            var favorite = CharacterSkillRules.StartingFavorites[index % CharacterSkillRules.StartingFavorites.Length];
            var values = new Dictionary<SkillId, int>
            {
                [CharacterSkillRules.Combat] = 18 + index,
                [CharacterSkillRules.Research] = 15 + index,
                [favorite] = 50 + index % 9
            };
            return values.OrderBy(x => x.Key.Value, StringComparer.Ordinal)
                .Select(x => new SkillState(x.Key, new SkillLevel(x.Value)))
                .Append(CharacterSkillRules.FavoriteMarker(favorite)).ToArray();
        }
    }
}
