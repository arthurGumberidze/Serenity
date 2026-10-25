using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Military;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Infrastructure
{
    public sealed class BarracksTrainingPresentation : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private readonly HashSet<StableEntityId> inside = new();
        private readonly Dictionary<StableEntityId, Vector3> entrances = new();
        public void Initialize(LocalSceneCompositionRoot session)
        {
            root = session;
            root.MilitaryTraining.IsAtBarracks = record => inside.Contains(record.CharacterId);
        }
        private void LateUpdate()
        {
            if (root?.AiRuntime == null) return;
            var active = root.MilitaryTraining.Active.Select(x => x.CharacterId).ToHashSet();
            foreach (var id in inside.Where(id => !active.Contains(id)).ToArray())
            {
                if (root.CharacterPresentations.TryGet(id,out var view))
                    foreach (var renderer in view.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = false;
                inside.Remove(id); entrances.Remove(id);
            }
            foreach (var record in root.MilitaryTraining.Active)
            {
                var movement = root.AiRuntime.Movements.FirstOrDefault(x => x.CharacterId == record.CharacterId);
                if (movement == null || !root.BuildingPresentations.TryGet(record.BarracksId,out var barracks)) continue;
                if (!entrances.TryGetValue(record.CharacterId,out var entrance))
                {
                    var renderers = barracks.GetComponentsInChildren<Renderer>();
                    var bounds = new Bounds(barracks.transform.position,Vector3.one * 4);
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    entrance = new Vector3(bounds.center.x,bounds.min.y,bounds.min.z - .8f);
                    if (!NavMesh.SamplePosition(entrance,out var hit,4f,NavMesh.AllAreas)) continue;
                    entrance = hit.position; entrances[record.CharacterId] = entrance;
                }
                var delta = movement.Presenter.transform.position-entrance; delta.y = 0;
                if (!inside.Contains(record.CharacterId) && delta.sqrMagnitude > 1f)
                {
                    if (movement.State != Game.Simulation.AI.MovementState.Moving)
                        movement.MoveTo(new WorldPosition(entrance.x,entrance.y,entrance.z),.4f);
                    continue;
                }
                movement.Stop(); inside.Add(record.CharacterId);
                foreach (var renderer in movement.Presenter.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = true;
            }
        }
    }
}
