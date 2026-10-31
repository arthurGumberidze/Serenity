using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Presentation.UI
{
    public enum GameplayUiRouteId
    {
        Settlement,
        People,
        WorkGroups,
        Construction,
        Knowledge,
        Dynasty,
        Military,
        Realm,
        Attention
    }

    /// <summary>Disposable route metadata for the UI02 shell; GameplayPanel remains the legacy command surface.</summary>
    public sealed class GameplayUiRoute
    {
        public GameplayUiRoute(GameplayUiRouteId id, string label, string iconKey, GameplayPanel panel,
            string stableElementName, string shortcut)
        {
            Id = id;
            Label = label;
            IconKey = iconKey;
            Panel = panel;
            StableElementName = stableElementName;
            Shortcut = shortcut;
        }

        public GameplayUiRouteId Id { get; }
        public string Label { get; }
        public string IconKey { get; }
        public GameplayPanel Panel { get; }
        public string StableElementName { get; }
        public string Shortcut { get; }
    }

    public static class GameplayUiRoutes
    {
        private static readonly GameplayUiRoute[] ordered =
        {
            new(GameplayUiRouteId.Settlement, "Поселение", "settlement", GameplayPanel.Economy, "nav-economy", "1"),
            new(GameplayUiRouteId.People, "Люди", "population", GameplayPanel.Domestic, "nav-domestic", "2"),
            new(GameplayUiRouteId.WorkGroups, "Рабочие группы", "work", GameplayPanel.Work, "nav-work", "3"),
            new(GameplayUiRouteId.Construction, "Строительство", "build", GameplayPanel.Build, "nav-build", "4"),
            new(GameplayUiRouteId.Knowledge, "Знания", "research", GameplayPanel.Research, "nav-research", "5"),
            new(GameplayUiRouteId.Dynasty, "Династия", "dynasty", GameplayPanel.Dynasty, "nav-dynasty", "6"),
            new(GameplayUiRouteId.Military, "Военные", "military", GameplayPanel.Military, "nav-military", "7"),
            new(GameplayUiRouteId.Realm, "Держава", "world", GameplayPanel.Global, "nav-global", "8"),
            new(GameplayUiRouteId.Attention, "Центр внимания", "alerts", GameplayPanel.Events, "nav-events", "9")
        };

        public static IReadOnlyList<GameplayUiRoute> Ordered => ordered;

        public static GameplayUiRoute Get(GameplayUiRouteId id) => ordered.Single(route => route.Id == id);

        public static bool TryForPanel(GameplayPanel panel, out GameplayUiRoute route)
        {
            route = ordered.FirstOrDefault(value => value.Panel == panel);
            return route != null;
        }
    }
}
