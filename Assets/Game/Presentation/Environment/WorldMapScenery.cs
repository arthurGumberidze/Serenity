using Game.Presentation.Buildings;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Environment
{
    public static class WorldMapScenery
    {
        public static void AddSolidFootprint(GameObject view,bool trunk=false)
        {
            var renderers=view.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            if(bounds.size.y<.45f)return;
            var body=new GameObject("Solid scenery footprint");
            body.transform.SetPositionAndRotation(bounds.center,Quaternion.identity);
            body.transform.SetParent(view.transform,true);
            var obstacle=body.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            if(trunk)
            {
                var solid=body.AddComponent<CapsuleCollider>();solid.radius=.4f;solid.height=Mathf.Max(1,bounds.size.y);
                obstacle.shape=UnityEngine.AI.NavMeshObstacleShape.Capsule;obstacle.radius=solid.radius;obstacle.height=solid.height;
            }
            else
            {
                var solid=body.AddComponent<BoxCollider>();solid.size=new Vector3(bounds.size.x*.8f,bounds.size.y,bounds.size.z*.8f);
                obstacle.shape=UnityEngine.AI.NavMeshObstacleShape.Box;obstacle.size=solid.size;
            }
            obstacle.carving=true;obstacle.carveOnlyStationary=true;
        }
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
