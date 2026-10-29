using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using UnityEngine;

namespace Game.Infrastructure
{
    public sealed class BarracksTrainingPresentation : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private readonly HashSet<StableEntityId> inside = new();
        public void Initialize(LocalSceneCompositionRoot session)
        {
            root = session;
        }
        private void LateUpdate()
        {
            if (root?.AiRuntime == null) return;
            var active = root.MilitaryTraining.Active.Select(x => x.CharacterId).ToHashSet();
            foreach (var id in inside.Where(id => !active.Contains(id)).ToArray())
            {
                if (root.CharacterPresentations.TryGet(id,out var view))
                    foreach (var renderer in view.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = false;
                inside.Remove(id);
            }
            foreach (var record in root.MilitaryTraining.Active)
            {
                var movement = root.AiRuntime.Movements.FirstOrDefault(x => x.CharacterId == record.CharacterId);
                if (movement == null || !root.BuildingPresentations.TryGet(record.BarracksId,out _)) continue;
                // Training is an uninterrupted indoor activity. Do not send the trainee toward an
                // orientation-dependent guessed doorway (which visibly made them leave the barracks).
                movement.Stop(); inside.Add(record.CharacterId);
                foreach (var renderer in movement.Presenter.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = true;
            }
        }
    }
}
