using System;
using Game.Domain.AI;
using Game.Domain.Combat;
using Game.Domain.World;
using Game.Domain.Characters;

namespace Game.Domain.Campaign
{
    public static class DeterministicStableId
    {
        public static StableEntityId Create(long worldSeed, string purpose, int index = 0)
        {
            if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Purpose is required.", nameof(purpose));
            ulong first = 14695981039346656037UL;
            ulong second = 1099511628211UL;
            Add(ref first, worldSeed.ToString());
            Add(ref first, purpose.Trim());
            Add(ref second, purpose.Trim());
            Add(ref second, index.ToString());
            Add(ref second, worldSeed.ToString());
            return StableEntityId.FromDeterministicHash(first, second);
        }

        private static void Add(ref ulong hash, string value)
        {
            for (var i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 1099511628211UL;
            hash = (hash ^ 255UL) * 1099511628211UL;
        }
    }

    public sealed class NewGameConfiguration
    {
        public const string DefaultConfigurationId = "stone_age_mvp_v1";

        public NewGameConfiguration(long worldSeed, string configurationId = DefaultConfigurationId,
            bool rivalKnownAtStart = false, string rulerName = "Aren", string dynastyName = "House Serenity",
            CharacterSex rulerSex = CharacterSex.Male, AppearanceProfileId? rulerAppearanceProfileId = null)
        {
            if (string.IsNullOrWhiteSpace(configurationId) || configurationId != configurationId.Trim() ||
                configurationId.Length > 64)
                throw new ArgumentException("Configuration ID must be trimmed and at most 64 characters.", nameof(configurationId));
            WorldSeed = worldSeed;
            ConfigurationId = configurationId;
            RivalKnownAtStart = rivalKnownAtStart;
            RulerName = new CharacterName(rulerName).GivenName;
            if (string.IsNullOrWhiteSpace(dynastyName) || dynastyName != dynastyName.Trim() || dynastyName.Length > 64)
                throw new ArgumentException("Dynasty name must be trimmed and at most 64 characters.", nameof(dynastyName));
            for (var i = 0; i < dynastyName.Length; i++)
                if (char.IsControl(dynastyName[i])) throw new ArgumentException("Dynasty name contains control characters.", nameof(dynastyName));
            if (!Enum.IsDefined(typeof(CharacterSex), rulerSex)) throw new ArgumentOutOfRangeException(nameof(rulerSex));
            DynastyName = dynastyName;
            RulerSex = rulerSex;
            RulerAppearanceProfileId = rulerAppearanceProfileId ??
                (rulerSex == CharacterSex.Male ? StoneAgeAppearanceProfiles.AdultA : StoneAgeAppearanceProfiles.AdultB);
        }

        public long WorldSeed { get; }
        public string ConfigurationId { get; }
        public bool RivalKnownAtStart { get; }
        public string RulerName { get; }
        public string DynastyName { get; }
        public CharacterSex RulerSex { get; }
        public AppearanceProfileId RulerAppearanceProfileId { get; }
        public StableEntityId SessionId => DeterministicStableId.Create(WorldSeed, ConfigurationId + ":session");
    }

    public sealed class CampaignProgressSaveState
    {
        public CampaignProgressSaveState(long worldSeed, string configurationId, CombatSideId playerSideId,
            AiStateId rivalStateId, SettlementId rivalHomeSettlementId, bool rivalDiscovered,
            long? discoveredTick, bool militaryVictory, long? victoryTick, AiStateId? defeatedStateId,
            SettlementId? capturedSettlementId, StableEntityId? sourceBattleId, ulong eventSequence,
            StableEntityId? sourceOccupationId = null)
        {
            if (string.IsNullOrWhiteSpace(configurationId) || configurationId != configurationId.Trim() ||
                configurationId.Length > 64)
                throw new ArgumentException("Configuration ID is invalid.", nameof(configurationId));
            if (!playerSideId.IsValid) throw new ArgumentException("Player side is required.", nameof(playerSideId));
            if (!rivalStateId.IsValid) throw new ArgumentException("Rival state is required.", nameof(rivalStateId));
            if (!rivalHomeSettlementId.IsValid) throw new ArgumentException("Rival settlement is required.", nameof(rivalHomeSettlementId));
            if (rivalDiscovered != discoveredTick.HasValue || discoveredTick < 0)
                throw new ArgumentException("Discovery marker and tick must agree.", nameof(discoveredTick));
            if (militaryVictory != victoryTick.HasValue || victoryTick < 0 ||
                militaryVictory != defeatedStateId.HasValue || militaryVictory != capturedSettlementId.HasValue ||
                militaryVictory != (sourceBattleId.HasValue || sourceOccupationId.HasValue) ||
                sourceBattleId.HasValue && sourceOccupationId.HasValue)
                throw new ArgumentException("Victory marker and audit fields must agree.", nameof(victoryTick));
            if (sourceOccupationId.HasValue && !sourceOccupationId.Value.IsValid)
                throw new ArgumentException("Occupation source must be valid.", nameof(sourceOccupationId));
            if (militaryVictory && !rivalDiscovered)
                throw new ArgumentException("Military victory requires prior contact.", nameof(militaryVictory));
            if (eventSequence != (ulong)((rivalDiscovered ? 1 : 0) + (militaryVictory ? 1 : 0)))
                throw new ArgumentException("Campaign event sequence is invalid.", nameof(eventSequence));
            WorldSeed = worldSeed;
            ConfigurationId = configurationId;
            PlayerSideId = playerSideId;
            RivalStateId = rivalStateId;
            RivalHomeSettlementId = rivalHomeSettlementId;
            RivalDiscovered = rivalDiscovered;
            DiscoveredTick = discoveredTick;
            MilitaryVictory = militaryVictory;
            VictoryTick = victoryTick;
            DefeatedStateId = defeatedStateId;
            CapturedSettlementId = capturedSettlementId;
            SourceBattleId = sourceBattleId;
            SourceOccupationId = sourceOccupationId;
            EventSequence = eventSequence;
        }

        public long WorldSeed { get; }
        public string ConfigurationId { get; }
        public CombatSideId PlayerSideId { get; }
        public AiStateId RivalStateId { get; }
        public SettlementId RivalHomeSettlementId { get; }
        public bool RivalDiscovered { get; }
        public long? DiscoveredTick { get; }
        public bool MilitaryVictory { get; }
        public long? VictoryTick { get; }
        public AiStateId? DefeatedStateId { get; }
        public SettlementId? CapturedSettlementId { get; }
        public StableEntityId? SourceBattleId { get; }
        public StableEntityId? SourceOccupationId { get; }
        public ulong EventSequence { get; }
    }
}
