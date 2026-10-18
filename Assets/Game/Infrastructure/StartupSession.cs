using System;
using System.Collections.Generic;
using System.Linq;
using Game.Presentation.Localization;
using Game.Presentation.UI;
using Game.Simulation;
using Game.Domain;
using Game.Domain.World;

namespace Game.Infrastructure
{
    public sealed class StartupSaveCatalog
    {
        private readonly ISaveSlotCatalog catalog;
        private readonly ISaveSlotCatalog legacyCatalog;

        public StartupSaveCatalog(ISaveSlotCatalog saveCatalog, ISaveSlotCatalog readOnlyLegacyCatalog = null)
        {
            catalog = saveCatalog ?? throw new ArgumentNullException(nameof(saveCatalog));
            legacyCatalog = readOnlyLegacyCatalog;
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

                var compatible = IsCompatible(slot);
                var detail = slot.IsCorrupt || !slot.CalendarTicks.HasValue
                    ? RussianTextCatalog.Get("ui.save.slot_unavailable")
                    : RussianTextCatalog.Format("ui.save.slot_detail",
                        Game.Domain.Time.GameCalendar.Year(slot.CalendarTicks.Value),
                        Game.Domain.Time.GameCalendar.DayOfYear(slot.CalendarTicks.Value),
                        slot.Mode.HasValue ? RussianTextCatalog.Enum(slot.Mode.Value) : RussianTextCatalog.Get("ui.hud.mode.local"),
                        slot.LastWriteUtc.ToLocalTime().ToString("g",
                            System.Globalization.CultureInfo.GetCultureInfo(RussianTextCatalog.Locale)));
                if (slot.CheatsUsed) detail += " · коды использованы, достижения отключены";
                if (!compatible && !slot.IsCorrupt) detail += " · архивный формат v" + (slot.FormatVersion ?? 0) + " (только чтение)";
                result.Add(new GameplaySaveSlotInfo(index, RussianTextCatalog.Format("ui.save.slot", index),
                    true, slot.IsCorrupt, detail, compatible ? slot.WorldSeed : null, slot.CheatsUsed));
            }
            return result;
        }

        public int? LatestUsableSlot()
        {
            var valid = catalog.ListSlots().Where(x => !x.IsCorrupt && x.CalendarTicks.HasValue && x.WorldSeed.HasValue && IsCompatible(x))
                .OrderByDescending(x => x.LastWriteUtc).ThenBy(x => x.SlotId.ToString(), StringComparer.Ordinal)
                .FirstOrDefault();
            if (valid == null) return null;
            for (var index = 1; index <= LocalGameplaySaveFacade.SlotCount; index++)
                if (LocalGameplaySaveFacade.SlotId(index) == valid.SlotId) return index;
            return null;
        }

        public IReadOnlyList<SaveSlotMetadata> ListLegacyMetadata() => legacyCatalog == null
            ? Array.Empty<SaveSlotMetadata>()
            : legacyCatalog.ListSlots().Where(x => x.FormatVersion.HasValue &&
                x.FormatVersion.Value < SaveSnapshot.ConnectedWorldVersion)
                .OrderByDescending(x => x.LastWriteUtc).ToArray();

        private static bool IsCompatible(SaveSlotMetadata slot) => !slot.FormatVersion.HasValue
            ? slot.WorldSeed.HasValue // Test/custom catalogs predating metadata v23; FileSaveStore always supplies a version.
            : slot.FormatVersion.Value >= SaveSnapshot.ConnectedWorldVersion &&
              string.Equals(slot.WorldGenerationId, ConnectedWorldContract.GenerationId, StringComparison.Ordinal);
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
