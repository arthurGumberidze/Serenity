using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Time;
using Game.Presentation.UI;
using Game.Presentation.Localization;
using Game.Simulation;

namespace Game.Infrastructure
{
    /// <summary>Player-facing local slot adapter. The store remains the durable authority.</summary>
    public sealed class LocalGameplaySaveFacade : IGameplaySaveFacade
    {
        public const int SlotCount = 3;
        private readonly LocalSceneCompositionRoot root;
        private readonly SaveCoordinator coordinator;
        private readonly ISaveSlotCatalog catalog;

        public LocalGameplaySaveFacade(LocalSceneCompositionRoot compositionRoot, SaveCoordinator saveCoordinator,
            ISaveSlotCatalog slotCatalog)
        {
            root = compositionRoot ?? throw new ArgumentNullException(nameof(compositionRoot));
            coordinator = saveCoordinator ?? throw new ArgumentNullException(nameof(saveCoordinator));
            catalog = slotCatalog ?? throw new ArgumentNullException(nameof(slotCatalog));
        }

        public IReadOnlyList<GameplaySaveSlotInfo> ListSlots()
        {
            var metadata = catalog.ListSlots().ToDictionary(x => x.SlotId);
            var result = new List<GameplaySaveSlotInfo>();
            for (var index = 1; index <= SlotCount; index++)
            {
                if (!metadata.TryGetValue(SlotId(index), out var slot))
                {
                    result.Add(new GameplaySaveSlotInfo(index, RussianTextCatalog.Format("ui.save.slot", index), false, false, string.Empty));
                    continue;
                }
                var detail = slot.IsCorrupt || !slot.CalendarTicks.HasValue
                    ? RussianTextCatalog.Get("ui.save.slot_unavailable")
                    : RussianTextCatalog.Format("ui.save.slot_detail", GameCalendar.Year(slot.CalendarTicks.Value),
                      GameCalendar.DayOfYear(slot.CalendarTicks.Value),
                      slot.Mode.HasValue ? RussianTextCatalog.Enum(slot.Mode.Value) : RussianTextCatalog.Get("ui.hud.mode.local"),
                      slot.LastWriteUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.GetCultureInfo(RussianTextCatalog.Locale)));
                result.Add(new GameplaySaveSlotInfo(index, RussianTextCatalog.Format("ui.save.slot", index), true, slot.IsCorrupt, detail));
            }
            return result;
        }

        public void Save(int slotIndex) => coordinator.Save(SlotId(slotIndex));

        public void Load(int slotIndex)
        {
            coordinator.Load(SlotId(slotIndex));
            root.RebindAfterLoad(coordinator.Clock);
        }

        public static StableEntityId SlotId(int slotIndex)
        {
            if (slotIndex < 1 || slotIndex > SlotCount) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            return StableEntityId.Parse((0x2700 + slotIndex).ToString("x32"));
        }
    }
}
