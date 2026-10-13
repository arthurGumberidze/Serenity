using System;
using System.Collections.Generic;
using System.Linq;
using Game.Presentation.Localization;
using Game.Presentation.UI;
using Game.Simulation;

namespace Game.Infrastructure
{
    public sealed class StartupSaveCatalog
    {
        private readonly ISaveSlotCatalog catalog;

        public StartupSaveCatalog(ISaveSlotCatalog saveCatalog)
        {
            catalog = saveCatalog ?? throw new ArgumentNullException(nameof(saveCatalog));
        }

        public IReadOnlyList<GameplaySaveSlotInfo> ListSlots()
        {
            var metadata = catalog.ListSlots().ToDictionary(x => x.SlotId);
            var result = new List<GameplaySaveSlotInfo>();
            for (var index = 1; index <= LocalGameplaySaveFacade.SlotCount; index++)
            {
                if (!metadata.TryGetValue(LocalGameplaySaveFacade.SlotId(index), out var slot))
                {
                    result.Add(new GameplaySaveSlotInfo(index, RussianTextCatalog.Format("ui.save.slot", index),
                        false, false, RussianTextCatalog.Get("ui.save.empty_slot")));
                    continue;
                }

                var detail = slot.IsCorrupt || !slot.CalendarTicks.HasValue
                    ? RussianTextCatalog.Get("ui.save.slot_unavailable")
                    : RussianTextCatalog.Format("ui.save.slot_detail",
                        Game.Domain.Time.GameCalendar.Year(slot.CalendarTicks.Value),
                        Game.Domain.Time.GameCalendar.DayOfYear(slot.CalendarTicks.Value),
                        slot.Mode.HasValue ? RussianTextCatalog.Enum(slot.Mode.Value) : RussianTextCatalog.Get("ui.hud.mode.local"),
                        slot.LastWriteUtc.ToLocalTime().ToString("g",
                            System.Globalization.CultureInfo.GetCultureInfo(RussianTextCatalog.Locale)));
                result.Add(new GameplaySaveSlotInfo(index, RussianTextCatalog.Format("ui.save.slot", index),
                    true, slot.IsCorrupt, detail, slot.WorldSeed));
            }
            return result;
        }

        public int? LatestUsableSlot()
        {
            var valid = catalog.ListSlots().Where(x => !x.IsCorrupt && x.CalendarTicks.HasValue && x.WorldSeed.HasValue)
                .OrderByDescending(x => x.LastWriteUtc).ThenBy(x => x.SlotId.ToString(), StringComparer.Ordinal)
                .FirstOrDefault();
            if (valid == null) return null;
            for (var index = 1; index <= LocalGameplaySaveFacade.SlotCount; index++)
                if (LocalGameplaySaveFacade.SlotId(index) == valid.SlotId) return index;
            return null;
        }
    }

    internal static class SessionLaunchState
    {
        private static int? requestedLoadSlot;
        private static string feedbackKey;
        private static int feedbackSlot;

        public static void RequestLoad(int slotIndex, long worldSeed)
        {
            LocalGameplaySaveFacade.SlotId(slotIndex);
            NewGameLaunchState.Request(worldSeed);
            requestedLoadSlot = slotIndex;
        }

        public static bool TryConsumeLoad(out int slotIndex)
        {
            slotIndex = requestedLoadSlot ?? 0;
            requestedLoadSlot = null;
            return slotIndex > 0;
        }

        public static void ReportLoadFailure(int slotIndex)
        {
            feedbackKey = "menu.load.failed";
            feedbackSlot = slotIndex;
        }

        public static bool TryConsumeFeedback(out string key, out int slotIndex)
        {
            key = feedbackKey;
            slotIndex = feedbackSlot;
            feedbackKey = null;
            feedbackSlot = 0;
            return !string.IsNullOrEmpty(key);
        }
    }
}
