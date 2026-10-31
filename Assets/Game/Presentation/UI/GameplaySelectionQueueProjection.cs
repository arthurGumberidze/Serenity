using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Work;
using Game.Simulation.Work;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Immutable, disposable view of the canonical work queue for the current StableEntityId selection.
    /// It is rebuilt on UI refresh and never becomes a second work or selection authority.
    /// </summary>
    public sealed class GameplaySelectionQueueProjection
    {
        public GameplaySelectionQueueProjection(int selectedCount, int busyCount, int totalOrders,
            IReadOnlyList<WorkOrder> visibleOrders)
        {
            SelectedCount = selectedCount;
            BusyCount = busyCount;
            TotalOrders = totalOrders;
            VisibleOrders = visibleOrders ?? throw new ArgumentNullException(nameof(visibleOrders));
        }

        public int SelectedCount { get; }
        public int BusyCount { get; }
        public int TotalOrders { get; }
        public IReadOnlyList<WorkOrder> VisibleOrders { get; }

        public static GameplaySelectionQueueProjection Build(IEnumerable<StableEntityId> selectedIds,
            WorkManager work, int visibleLimit = 3)
        {
            if (selectedIds == null) throw new ArgumentNullException(nameof(selectedIds));
            if (work == null) throw new ArgumentNullException(nameof(work));
            if (visibleLimit < 0) throw new ArgumentOutOfRangeException(nameof(visibleLimit));

            var ids = selectedIds.Where(id => id.IsValid).Distinct().OrderBy(id => id).ToArray();
            var queues = ids.Select(id => work.GetQueue(id)).ToArray();
            var orders = queues.SelectMany(queue => queue).Distinct().OrderByDescending(order =>
                    order.Status == WorkJobStatus.Active)
                .ThenBy(order => order.QueueOrder).ThenBy(order => order.Id).ToArray();
            return new GameplaySelectionQueueProjection(ids.Length, queues.Count(queue => queue.Count > 0),
                orders.Length, Array.AsReadOnly(orders.Take(visibleLimit).ToArray()));
        }
    }
}
