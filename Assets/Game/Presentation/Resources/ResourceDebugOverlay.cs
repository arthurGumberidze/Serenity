using System;
using System.Linq;
using Game.Domain.Resources;
using UnityEngine;

namespace Game.Presentation.Resources
{
    // Development-only read model. It owns no resource quantity and performs no mutations.
    public sealed class ResourceDebugOverlay : MonoBehaviour
    {
        private ResourceInventoryRegistry inventories;
        private ResourceCatalog resources;

        public void Initialize(ResourceInventoryRegistry registry, ResourceCatalog catalog)
        {
            inventories = registry ?? throw new ArgumentNullException(nameof(registry));
            resources = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        private void OnGUI()
        {
            if (inventories == null || resources == null || (!Application.isEditor && !Debug.isDebugBuild)) return;
            var ids = new[] { new ResourceId("wood_log"), new ResourceId("stone"),
                new ResourceId("plant_fiber"), new ResourceId("hide") };
            GUILayout.BeginArea(new Rect(12, 12, 230, 145), GUI.skin.box);
            GUILayout.Label("U09 Resources (canonical total)");
            foreach (var id in ids)
                GUILayout.Label(resources.Get(id).DisplayName + ": " +
                    inventories.All.Sum(x => x.GetAmount(id).Units));
            GUILayout.EndArea();
        }
    }
}
