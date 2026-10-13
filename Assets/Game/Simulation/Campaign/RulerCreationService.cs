using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Simulation.Tiers;

namespace Game.Simulation.Campaign
{
    public sealed class RulerCreationDraft
    {
        public RulerCreationDraft(long worldSeed, string rulerName, string dynastyName, CharacterSex sex,
            AppearanceProfileDefinition profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Sex != sex) throw new ArgumentException("Profile and ruler sex must agree.", nameof(profile));
            new CharacterName(rulerName);
            if (string.IsNullOrWhiteSpace(dynastyName) || dynastyName != dynastyName.Trim() || dynastyName.Length > 64)
                throw new ArgumentException("Dynasty name must be trimmed printable text up to 64 characters.", nameof(dynastyName));
            WorldSeed = worldSeed;
            RulerName = rulerName;
            DynastyName = dynastyName;
            Sex = sex;
            Profile = profile;
            Appearance = profile.CreateState(DeterministicStableId.Create(worldSeed, "player_character", 0));
        }

        public long WorldSeed { get; }
        public string RulerName { get; }
        public string DynastyName { get; }
        public CharacterSex Sex { get; }
        public AppearanceProfileDefinition Profile { get; }
        public AppearanceState Appearance { get; }

        public NewGameConfiguration ToConfiguration() => new NewGameConfiguration(WorldSeed,
            NewGameConfiguration.DefaultConfigurationId, false, RulerName, DynastyName, Sex, Profile.Id);
    }

    public static class RulerCreationService
    {
        private const uint ProfileStream = 0x50340010u;
        private const uint NameStream = 0x50340011u;
        private const uint DynastyStream = 0x50340012u;
        private static readonly string[] MaleNames = { "Арен", "Борин", "Доран", "Рован", "Тарин" };
        private static readonly string[] FemaleNames = { "Мира", "Эльда", "Нера", "Пела", "Села" };
        private static readonly string[] Dynasties = { "Дом Серенити", "Дом Каменного Берега", "Дом Ясного Огня", "Дом Северной Сосны" };

        public static RulerCreationDraft CreateDefault(long worldSeed, AppearanceDefinitionCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return new RulerCreationDraft(worldSeed, "Арен", "Дом Серенити", CharacterSex.Male,
                catalog.DefaultFor(CharacterSex.Male));
        }

        public static RulerCreationDraft Create(long worldSeed, string rulerName, string dynastyName,
            AppearanceProfileId profileId, AppearanceDefinitionCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var profile = catalog.Get(profileId);
            return new RulerCreationDraft(worldSeed, rulerName, dynastyName, profile.Sex, profile);
        }

        public static RulerCreationDraft Randomize(long worldSeed, int sequence, AppearanceDefinitionCatalog catalog)
        {
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var identity = DeterministicStableId.Create(worldSeed, "ruler_editor_random", sequence);
            var profiles = catalog.Profiles.OrderBy(x => x.Id).ToArray();
            var profileSample = DeterministicKeyedRandom.Sample64(worldSeed, identity, (ulong)sequence, ProfileStream);
            var profile = profiles[(int)(profileSample % (ulong)profiles.Length)];
            var names = profile.Sex == CharacterSex.Male ? MaleNames : FemaleNames;
            var nameSample = DeterministicKeyedRandom.Sample64(worldSeed, identity, (ulong)sequence, NameStream);
            var dynastySample = DeterministicKeyedRandom.Sample64(worldSeed, identity, (ulong)sequence, DynastyStream);
            return new RulerCreationDraft(worldSeed, names[(int)(nameSample % (ulong)names.Length)],
                Dynasties[(int)(dynastySample % (ulong)Dynasties.Length)], profile.Sex, profile);
        }
    }
}
