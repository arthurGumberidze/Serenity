using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Health;
using Game.Presentation.Interaction;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Time;
using UnityEngine;

namespace Game.Presentation.Health
{
    public sealed class HealthFamilyDebugOverlay : MonoBehaviour, IWorldPointerUiBlocker
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 330f;
        private HealthAndFamilyService service;
        private CharacterRegistry characters;
        private FamilyTreeQuery family;
        private SelectionProbe selection;
        private GameClock clock;
        private WorldPointerRaycaster raycaster;
        private string message = "Select a character";

        public void Initialize(HealthAndFamilyService healthFamily, CharacterRegistry registry,
            FamilyTreeQuery familyQuery, SelectionProbe selectionProbe, GameClock gameClock,
            WorldPointerRaycaster worldRaycaster)
        {
            service = healthFamily ?? throw new ArgumentNullException(nameof(healthFamily));
            characters = registry ?? throw new ArgumentNullException(nameof(registry));
            family = familyQuery ?? throw new ArgumentNullException(nameof(familyQuery));
            selection = selectionProbe ?? throw new ArgumentNullException(nameof(selectionProbe));
            clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
            raycaster?.UnregisterUiBlocker(this);
            raycaster = worldRaycaster ?? throw new ArgumentNullException(nameof(worldRaycaster));
            raycaster.RegisterUiBlocker(this);
        }

        public bool TryGetUiBlockingRect(out Rect guiRect)
        {
            guiRect = GetPanelRect();
            return service != null && (Application.isEditor || Debug.isDebugBuild);
        }

        private void OnEnable() => raycaster?.RegisterUiBlocker(this);
        private void OnDisable() => raycaster?.UnregisterUiBlocker(this);

        private void OnGUI()
        {
            if (service == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            GUILayout.BeginArea(GetPanelRect(), GUI.skin.box);
            GUILayout.Label("Character Health / Family (U18 debug)");
            if (!selection.SelectedCharacterId.HasValue ||
                !characters.TryGet(selection.SelectedCharacterId.Value, out var selected))
            {
                GUILayout.Label(message);
                GUILayout.EndArea();
                return;
            }
            var capabilities = service.GetCapabilities(selected.Id);
            GUILayout.Label($"{selected.Name.DisplayName} | age {selected.AgeInCompletedYears(clock.State.BiologicalTicks)} | {selected.LifeState}");
            GUILayout.Label($"Work {capabilities.WorkPerThousand / 10f:0}% | Research {capabilities.ResearchPerThousand / 10f:0}% | " +
                            $"Move {capabilities.MovementPerThousand / 10f:0}% | Incapacitated {capabilities.IsIncapacitated}");
            var active = service.Conditions.Where(x => x.TargetId == selected.Id &&
                x.State != HealthConditionState.Recovered).ToArray();
            GUILayout.Label("Conditions: " + (active.Length == 0 ? "none" : string.Join(", ", active.Select(x =>
                x.DefinitionId + " " + x.Severity + "/" + x.State + " step " + x.ProgressionSteps))));
            var pregnancy = service.Pregnancies.LastOrDefault(x => x.MotherId == selected.Id && x.Status == PregnancyStatus.Active);
            GUILayout.Label(pregnancy == null ? "Pregnancy: none" :
                $"Pregnancy: due bio {pregnancy.DueBiologicalTick}, status {pregnancy.Status}");
            var spouse = family.GetSpouse(selected.Id);
            GUILayout.Label("Spouse: " + (spouse == null ? "none" : spouse.Name.DisplayName));
            GUILayout.Label("Parents: " + Join(family.GetParents(selected.Id)));
            GUILayout.Label("Children: " + Join(family.GetChildren(selected.Id)));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add minor injury")) Execute(() => service.ApplyCondition(selected.Id,
                new HealthConditionId("minor_cut"), clock.State.CalendarTicks), "Minor injury added");
            if (GUILayout.Button("Add severe injury")) Execute(() => service.ApplyCondition(selected.Id,
                new HealthConditionId("serious_wound"), clock.State.CalendarTicks), "Severe injury added");
            if (GUILayout.Button("Add disease")) Execute(() => service.ApplyCondition(selected.Id,
                new HealthConditionId("infection"), clock.State.CalendarTicks), "Disease added");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Recover selected")) Execute(() => service.RecoverAll(selected.Id), "Recovery applied");
            if (GUILayout.Button("Start pregnancy with spouse") && spouse != null)
            {
                var mother = selected.Sex == CharacterSex.Female ? selected : spouse;
                var father = selected.Sex == CharacterSex.Male ? selected : spouse;
                Execute(() => service.StartPregnancy(mother.Id, father.Id, clock.State.CalendarTicks,
                    clock.State.BiologicalTicks), "Pregnancy started; advance game time for birth");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private void Execute(Action action, string success)
        {
            try { action(); message = success; }
            catch (Exception exception) { message = exception.Message; }
        }

        private static string Join(System.Collections.Generic.IReadOnlyList<Character> values) => values.Count == 0
            ? "none" : string.Join(", ", values.Select(x => x.Name.DisplayName));
        private static Rect GetPanelRect() => new Rect(12f, 430f, PanelWidth, PanelHeight);
    }
}
