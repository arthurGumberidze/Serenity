using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.World;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using UnityEngine;

namespace Game.Infrastructure
{
    // One bounded presentation scheduler for saved domestic animals. Never owns health or births.
    public sealed class LivestockPresentation : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private GameObject sheep;
        private float nextRefresh;
        private readonly Dictionary<StableEntityId, GameObject> views = new();
        public void Initialize(LocalSceneCompositionRoot session)
        { root = session; sheep = Resources.Load<CropArtCatalog>("World/CropArt")?.Sheep; }

        private void Update()
        {
            if (root?.Production == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .15f;
            var states = root.Production.Animals.States.Where(x => x.Domestic && !x.Harvested).ToArray();
            var ids = new HashSet<StableEntityId>(states.Select(x => x.Id));
            foreach (var id in views.Keys.Where(x => !ids.Contains(x)).ToArray())
            { Destroy(views[id]); views.Remove(id); }
            foreach (var group in states.GroupBy(x => x.Home))
            {
                CropPlotPresentation pen = null;
                if (root.BuildingPresentations.TryGet(group.Key, out var building))
                    pen = building.GetComponent<CropPlotPresentation>();
                pen?.Refresh();
                var slot = 0;
                foreach (var animal in group.OrderBy(x => x.Id))
                {
                    if (!root.Production.TryGetSite(animal.Id, out var site) || sheep == null) continue;
                    if (!views.TryGetValue(animal.Id, out var view))
                    {
                        view = new GameObject("Domestic sheep " + animal.Id); view.transform.SetParent(transform, false);
                        var model = WorldMapScenery.Instantiate(sheep, view.transform); model.SetActive(true);
                        var renderers = model.GetComponentsInChildren<Renderer>();
                        if (renderers.Length > 0)
                        {
                            var bounds = renderers[0].bounds;
                            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                            var scale = 1f / Mathf.Max(.1f, bounds.size.y);
                            model.transform.localScale *= scale;
                            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                        }
                        var collider = view.AddComponent<CapsuleCollider>();
                        collider.radius = .4f; collider.height = 1.2f; collider.center = Vector3.up * .6f;
                        var obstacle = view.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
                        obstacle.center = Vector3.up * .6f; obstacle.radius = .4f; obstacle.height = 1.2f;
                        view.AddComponent<ProductionSourcePresenter>().Bind(site, Color.white, true);
                        view.AddComponent<AnimalHealthPresentation>().Bind(root.Production.Animals, animal.Id, model.transform);
                        views.Add(animal.Id, view);
                    }
                    var grazingSlot = slot++;
                    if (animal.Alive && pen != null)
                    {
                        var position = pen.GrazingPosition(grazingSlot);
                        position = root.TiledWorld.ProjectToGround(position, .02f);
                        site.MoveAnimal(new WorldPosition(position.x, position.y, position.z));
                    }
                    view.transform.position = new Vector3(site.Position.X, site.Position.Y, site.Position.Z);
                    var source = view.GetComponent<ProductionSourcePresenter>();
                    if (!ReferenceEquals(source.Site, site)) source.Bind(site, Color.white, true);
                    view.GetComponent<AnimalHealthPresentation>().Refresh();
                }
            }
        }
        private void OnDestroy()
        { foreach (var view in views.Values) if (view != null) Destroy(view); }
    }
}
