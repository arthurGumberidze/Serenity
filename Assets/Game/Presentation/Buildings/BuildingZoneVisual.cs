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
                instance.name = "Garden cell visual";
                instances.Add(instance);
            }
            var centerX = (ordered.Min(x => x.X) + ordered.Max(x => x.X)) * .5f;
            var centerZ = (ordered.Min(x => x.Z) + ordered.Max(x => x.Z)) * .5f;
            for (var i = 0; i < instances.Count; i++)
            {
                var active = i < ordered.Length;
                instances[i].SetActive(active);
                if (!active) continue;
                instances[i].transform.localPosition = cellTemplate.transform.localPosition +
                    new Vector3((ordered[i].X - centerX) * gridSize, 0f, (ordered[i].Z - centerZ) * gridSize);
            }
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            var size = box.size;
            size.x = (ordered.Max(x => x.X) - ordered.Min(x => x.X) + 1) * gridSize;
            size.z = (ordered.Max(x => x.Z) - ordered.Min(x => x.Z) + 1) * gridSize;
            box.size = size;
        }
    }
}
