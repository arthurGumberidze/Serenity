using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using UnityEngine;

namespace Game.Infrastructure
{
    public sealed class BarracksTrainingPresentation : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private readonly HashSet<StableEntityId> travelling = new();
        public void Initialize(LocalSceneCompositionRoot session)
        {
            root = session;
        }
        private void LateUpdate()
        {
            if (root?.AiRuntime == null) return;
            var active = root.MilitaryTraining.Active.Select(x => x.CharacterId).ToHashSet();
            foreach (var id in travelling.Where(id => !active.Contains(id)).ToArray())
            {
                root.Research.Workplace.Release(id);
                travelling.Remove(id);
            }
            foreach (var record in root.MilitaryTraining.Active)
            {
                var movement = root.AiRuntime.Movements.FirstOrDefault(x => x.CharacterId == record.CharacterId);
                if (movement == null || !root.BuildingPresentations.TryGet(record.BarracksId,out _)) continue;
                travelling.Add(record.CharacterId);
                root.Research.Workplace.Travel(record.CharacterId, record.BarracksId, movement);
            }
        }
    }
}
