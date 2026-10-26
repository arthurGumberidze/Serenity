using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Buildings;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    /// <summary>Repeats an authored visual once per cell of a variable-size zone.</summary>
    public sealed class BuildingZoneVisual : MonoBehaviour
    {
        [SerializeField] private GameObject cellTemplate;
        [SerializeField, Min(1)] private int maximumVisualCells = 64;
        private readonly List<GameObject> instances = new List<GameObject>();

        public void Configure(GameObject template, int maximumCells = 64)
        {
            cellTemplate = template != null ? template : throw new ArgumentNullException(nameof(template));
            maximumVisualCells = Mathf.Max(1, maximumCells);
            cellTemplate.SetActive(false);
        }

        public void Render(IReadOnlyList<GridCoordinate> cells, float gridSize)
        {
            if (cellTemplate == null || cells == null || cells.Count == 0) return;
            var ordered = cells.Take(maximumVisualCells).OrderBy(x => x.X).ThenBy(x => x.Z).ToArray();
            while (instances.Count < ordered.Length)
            {
                var instance = Instantiate(cellTemplate, cellTemplate.transform.parent);
                instance.name = "Placement cell visual";
                instances.Add(instance);
            }
            var centerX = (cells.Min(x => x.X) + cells.Max(x => x.X)) * .5f;
            var centerZ = (cells.Min(x => x.Z) + cells.Max(x => x.Z)) * .5f;
            for (var i = 0; i < instances.Count; i++)
            {
                var active = i < ordered.Length;
                instances[i].SetActive(active);
                if (!active) continue;
                var offset = new Vector3((ordered[i].X - centerX) * gridSize, 0f, (ordered[i].Z - centerZ) * gridSize);
                instances[i].transform.localPosition = cellTemplate.transform.localPosition +
                    Quaternion.Inverse(transform.rotation) * offset;
                if (maximumVisualCells == 32 && ordered.Length > 1)
                    instances[i].transform.rotation = Quaternion.Euler(0f,
                        ordered[0].X == ordered[ordered.Length - 1].X ? 90f : 0f, 0f);
            }
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            var size = box.size;
            size.x = (cells.Max(x => x.X) - cells.Min(x => x.X) + 1) * gridSize;
            size.z = (cells.Max(x => x.Z) - cells.Min(x => x.Z) + 1) * gridSize;
            box.size = size;
            if(maximumVisualCells==32 && !box.isTrigger)
            {
                // A single bounding box would seal openings cut for gates.
                box.enabled=false;
                var bound=GetComponent<BuildingPresenter>()?.IsBound==true;
                foreach(var instance in instances)
                {
                    var cell=instance.GetComponent<BoxCollider>() ?? instance.AddComponent<BoxCollider>();
                    cell.center=new Vector3(0,size.y*.5f,0);cell.size=new Vector3(gridSize,size.y,.45f);
                    cell.enabled=bound;
                    if(!bound)continue;
                    var obstacle=instance.GetComponent<UnityEngine.AI.NavMeshObstacle>() ?? instance.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                    obstacle.shape=UnityEngine.AI.NavMeshObstacleShape.Box;
                    obstacle.center=cell.center;obstacle.size=cell.size;obstacle.carving=true;
                    obstacle.carveOnlyStationary=false;
                }
            }

        }
    }
}
