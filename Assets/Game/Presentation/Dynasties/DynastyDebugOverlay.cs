using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Dynasties;
using Game.Presentation.Interaction;
using Game.Presentation.Localization;
using Game.Simulation.Dynasties;
using Game.Simulation.Time;
using UnityEngine;

namespace Game.Presentation.Dynasties
{
    public sealed class DynastyDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private const float PanelWidth = 398f;
        private const float PanelHeight = 300f;
        private SuccessionService succession;
        private CharacterRegistry characters;
        private SelectionProbe selection;
        private GameClock clock;
        private WorldPointerRaycaster raycaster;

        public void Initialize(SuccessionService successionService, CharacterRegistry characterRegistry,
            SelectionProbe selectionProbe, GameClock gameClock, WorldPointerRaycaster worldRaycaster)
        {
            succession = successionService ?? throw new ArgumentNullException(nameof(successionService));
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = GetPanelRect();
            return succession != null && (Application.isEditor || Debug.isDebugBuild);
        }

        public SuccessionResult KillCurrentMonarch()
        {
            if (succession.Dynasty.Status != DynastyStatus.Active || !succession.Dynasty.CurrentMonarchId.HasValue)
                return default;
            return succession.ProcessDeath(succession.Dynasty.CurrentMonarchId.Value,
                clock.State.BiologicalTicks, clock.State.CalendarTicks);
        }

        public SuccessionResult KillSelectedCharacter()
        {
            if (!selection.SelectedCharacterId.HasValue) return default;
            return succession.ProcessDeath(selection.SelectedCharacterId.Value,
                clock.State.BiologicalTicks, clock.State.CalendarTicks);
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);

        private void OnGUI()
        {
            if (succession == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            var dynasty = succession.Dynasty;
            GUILayout.Label($"Династия: {RussianTextCatalog.DynastyName(dynasty.Name)} [{Short(dynasty.Id)}]");
            GUILayout.Label("Состояние: " + RussianTextCatalog.Enum(dynasty.Status));
            if (dynasty.CurrentMonarchId.HasValue)
            {
                var monarch = characters.Get(dynasty.CurrentMonarchId.Value);
                GUILayout.Label($"Правитель: {RussianTextCatalog.PersonName(monarch.Name.DisplayName)} [{Short(monarch.Id)}] · {RussianTextCatalog.Enum(monarch.LifeState)}");
                var spouse = succession.Family.GetSpouse(monarch.Id);
                GUILayout.Label("Супруг или супруга: " + (spouse == null ? "нет" : Label(spouse)));
                var children = succession.Family.GetChildren(monarch.Id);
                GUILayout.Label("Дети: " + (children.Count == 0 ? "нет" : string.Join(", ", children.Select(Label))));
                var candidates = succession.Family.GetOrderedSuccessionCandidates(dynasty, monarch.Id);
                GUILayout.Label("Текущий наследник: " + (candidates.Count == 0 ? "нет" : Label(candidates[0])));
                GUILayout.Label("Первые пять наследников: " + (candidates.Count == 0 ? "нет" :
                    string.Join(" > ", candidates.Take(5).Select(Label))));
            }
            else
            {
                GUILayout.Label("Правитель: нет");
                GUILayout.Label("Игра окончена: " + RussianTextCatalog.Enum(succession.GameRun.GameOverReason));
            }

            var last = dynasty.History.LastOrDefault();
            GUILayout.Label(dynasty.History.Count == 0 ? "Последняя смена власти: нет" :
                $"Смена власти №{last.SequenceNumber}: {Short(last.PreviousMonarchId)} → " +
                (last.NewMonarchId.HasValue ? Short(last.NewMonarchId.Value) : "династия пресеклась") +
                $" · такт {last.CalendarTick}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Убить текущего правителя")) KillCurrentMonarch();
            if (GUILayout.Button("Убить выбранного персонажа")) KillSelectedCharacter();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static Rect GetPanelRect() => new Rect(Screen.width - PanelWidth - 12f, 280f, PanelWidth, PanelHeight);
        private static string Label(Character value) => RussianTextCatalog.PersonName(value.Name.DisplayName) + " [" + Short(value.Id) + "]";
        private static string Short(StableEntityId value)
        {
            var text = value.ToString();
            return text.Length <= 8 ? text : text.Substring(0, 8);
        }
    }
}
