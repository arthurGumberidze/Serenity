using System;
using System.Collections.Generic;

namespace Game.Presentation.UI
{
    public enum FirstHourStage
    {
        InspectGroup = 0,
        AssignWork = 1,
        InspectShelterAndStorage = 2,
        SecureFood = 3,
        StartResearch = 4,
        ResolveEventOrDanger = 5,
        BeginScouting = 6,
        Complete = 7
    }

    public readonly struct FirstHourSignals
    {
        public FirstHourSignals(bool characterSelected, bool workAssigned, bool settlementPanelViewed,
            bool shelterPresent, bool storagePresent, bool foodAvailable, bool foodProduced,
            bool researchStarted, bool eventOrDangerHandled, bool scoutingStarted)
        {
            CharacterSelected = characterSelected;
            WorkAssigned = workAssigned;
            SettlementPanelViewed = settlementPanelViewed;
            ShelterPresent = shelterPresent;
            StoragePresent = storagePresent;
            FoodAvailable = foodAvailable;
            FoodProduced = foodProduced;
            ResearchStarted = researchStarted;
            EventOrDangerHandled = eventOrDangerHandled;
            ScoutingStarted = scoutingStarted;
        }

        public bool CharacterSelected { get; }
        public bool WorkAssigned { get; }
        public bool SettlementPanelViewed { get; }
        public bool ShelterPresent { get; }
        public bool StoragePresent { get; }
        public bool FoodAvailable { get; }
        public bool FoodProduced { get; }
        public bool ResearchStarted { get; }
        public bool EventOrDangerHandled { get; }
        public bool ScoutingStarted { get; }
    }

    /// <summary>
    /// Presentation-only memory of what the player has already inspected. Gameplay completion signals are
    /// observed from canonical services; this object never grants resources, completes research or issues work.
    /// </summary>
    public sealed class FirstHourOnboardingProgress
    {
        private readonly HashSet<FirstHourStage> completed = new HashSet<FirstHourStage>();

        public bool Visible { get; private set; } = true;
        public int CompletedCount => completed.Count;
        public int StepCount => (int)FirstHourStage.Complete;
        public IReadOnlyCollection<FirstHourStage> Completed => completed;

        public FirstHourStage CurrentStage
        {
            get
            {
                for (var value = FirstHourStage.InspectGroup; value < FirstHourStage.Complete; value++)
                    if (!completed.Contains(value)) return value;
                return FirstHourStage.Complete;
            }
        }

        public void SetVisible(bool visible) => Visible = visible;

        public bool IsComplete(FirstHourStage stage) => stage == FirstHourStage.Complete
            ? CurrentStage == FirstHourStage.Complete
            : completed.Contains(stage);

        public bool Observe(FirstHourSignals signals)
        {
            var before = completed.Count;
            if (signals.CharacterSelected) completed.Add(FirstHourStage.InspectGroup);
            if (signals.WorkAssigned) completed.Add(FirstHourStage.AssignWork);
            if (signals.SettlementPanelViewed && signals.ShelterPresent && signals.StoragePresent)
                completed.Add(FirstHourStage.InspectShelterAndStorage);
            if (signals.FoodAvailable && signals.FoodProduced) completed.Add(FirstHourStage.SecureFood);
            if (signals.ResearchStarted) completed.Add(FirstHourStage.StartResearch);
            if (signals.EventOrDangerHandled) completed.Add(FirstHourStage.ResolveEventOrDanger);
            if (signals.ScoutingStarted) completed.Add(FirstHourStage.BeginScouting);
            return completed.Count != before;
        }
    }
}
