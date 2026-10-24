using Game.Presentation.Buildings;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Environment
{
    public static class WorldMapScenery
    {
        // Strip simulation components and vendor mesh collision before activating imported scenery.
        public static GameObject Instantiate(GameObject prefab, Transform parent)
        {
            var staging = new GameObject("Scenery preparation");
            staging.SetActive(false);
            staging.transform.SetParent(parent, false);
            var view = Object.Instantiate(prefab, staging.transform);
            var stages = view.GetComponentsInChildren<BuildingConstructionStageVisual>(true);
            if (stages.Length > 0)
            {
                var final = stages.Max(value => value.RequiredStage);
                foreach (var stage in stages) stage.gameObject.SetActive(stage.RequiredStage == final);
            }
            foreach (var collider in view.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                if (collider is MeshCollider mesh) mesh.sharedMesh = null;
                Object.Destroy(collider);
            }
            foreach (var presenter in view.GetComponentsInChildren<BuildingPresenter>(true))
            {
                presenter.enabled = false;
                Object.Destroy(presenter);
            }
            view.transform.SetParent(parent, false);
            Object.Destroy(staging);
            return view;
        }
    }
}
