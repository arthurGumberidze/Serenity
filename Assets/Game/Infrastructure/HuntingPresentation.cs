using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.World;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using UnityEngine;

namespace Game.Infrastructure
{
    // One shared adapter. HP and shot timing belong exclusively to Production.Animals.
    public sealed class HuntingPresentation : MonoBehaviour
    {
        private LocalSceneCompositionRoot root;
        private GameObject arrowPrefab;
        private readonly Dictionary<StableEntityId, GameObject> arrows = new();
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private Material bloodMaterial;

        public void Initialize(LocalSceneCompositionRoot session, GameObject arrow)
        {
            root = session; arrowPrefab = arrow;
            root.Production.Animals.CanHunt = id => root.CharacterPresentations.TryGet(id, out var view) &&
                view != null && view.gameObject.activeInHierarchy;
            root.Production.Animals.CanHit = CanHit;
            root.Production.Animals.Impact += OnImpact;
        }

        private bool CanHit(StableEntityId hunter, StableEntityId animal)
        {
            if (!root.CharacterPresentations.TryGet(hunter, out var person) ||
                !root.Production.TryGetSite(animal, out var site)) return false;
            var start = person.LeftHandSocket != null ? person.LeftHandSocket.position :
                person.transform.position + Vector3.up * 1.25f;
            var target = new Vector3(site.Position.X, site.Position.Y + .8f, site.Position.Z);
            var vector = target - start;
            if (vector.magnitude > root.Production.Animals.Policy.Range) return false;
            var count = Physics.RaycastNonAlloc(start, vector.normalized, hits, vector.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (var i = 0; i < count; i++)
            {
                var obstacle = hits[i].collider;
                if (obstacle.transform.IsChildOf(person.transform)) continue;
                var wild = obstacle.GetComponentInParent<WorldMapWildlifeView>();
                if (wild != null && wild.StableId == animal) continue;
                var source = obstacle.GetComponentInParent<ProductionSourcePresenter>();
                if (source != null && source.IsBound && source.Site.Id == animal) continue;
                return false;
            }
            return true;
        }

        private void Update()
        {
            if (root?.Production == null) return;
            var visible = new HashSet<StableEntityId>();
            foreach (var task in root.Production.ActiveTasks)
            {
                if (task.RecipeId.ToString() != "hunt_animal" ||
                    !root.Production.Animals.TryGet(task.SiteId, out var health) || !health.Alive ||
                    health.ShotTicks < root.Production.Animals.Policy.DrawTicks ||
                    !root.CharacterPresentations.TryGet(task.WorkerId, out var person) ||
                    !root.Production.TryGetSite(task.SiteId, out var site) || arrowPrefab == null) continue;
                visible.Add(task.SiteId);
                if (!arrows.TryGetValue(task.SiteId, out var arrow))
                {
                    arrow = new GameObject("Hunting arrow"); arrow.transform.SetParent(transform, false);
                    var model = WorldMapScenery.Instantiate(arrowPrefab, arrow.transform);
                    var filter = model.GetComponentInChildren<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                    {
                        var size = filter.sharedMesh.bounds.size;
                        var axis = size.x > size.y && size.x > size.z ? Vector3.right :
                            size.y > size.z ? Vector3.up : Vector3.forward;
                        model.transform.localRotation = Quaternion.FromToRotation(axis, Vector3.forward);
                        model.transform.localScale = Vector3.one * (.72f / Mathf.Max(.01f, Mathf.Max(size.x, Mathf.Max(size.y, size.z))));
                        model.transform.localPosition = -(model.transform.localRotation * filter.sharedMesh.bounds.center) * model.transform.localScale.x;
                    }
                    arrows.Add(task.SiteId, arrow);
                }
                var start = person.LeftHandSocket != null ? person.LeftHandSocket.position :
                    person.transform.position + Vector3.up * 1.2f;
                var target = new Vector3(site.Position.X, site.Position.Y + .8f, site.Position.Z);
                if (!root.Production.Animals.IsAccurateShot(task.SiteId, health.ShotSequence + 1))
                    target += Vector3.Cross((target - start).normalized, Vector3.up) * 1.6f;
                var facing = target - person.transform.position; facing.y = 0f;
                if (facing.sqrMagnitude > .001f) person.transform.rotation = Quaternion.LookRotation(facing);
                var fraction = (float)(health.ShotTicks - root.Production.Animals.Policy.DrawTicks) /
                    root.Production.Animals.Policy.FlightTicks;
                arrow.transform.position = Vector3.Lerp(start, target, fraction) + Vector3.up *
                    (Mathf.Sin(fraction * Mathf.PI) * .15f);
                if ((target - start).sqrMagnitude > .001f)
                    arrow.transform.rotation = Quaternion.LookRotation(target - start);
            }
            foreach (var id in arrows.Keys.Where(id => !visible.Contains(id)).ToArray())
            { Destroy(arrows[id]); arrows.Remove(id); }
        }

        private void OnImpact(HuntingImpact impact)
        {
            if (arrows.TryGetValue(impact.Animal, out var arrow)) { Destroy(arrow); arrows.Remove(impact.Animal); }
            if (impact.Damage <= 0 || !root.Production.TryGetSite(impact.Animal, out var site)) return;
            var camera = Camera.main;
            var point = new Vector3(site.Position.X, site.Position.Y + .7f, site.Position.Z);
            if (camera == null || (camera.transform.position - point).sqrMagnitude > 160f * 160f) return;
            var effect = new GameObject("Arrow impact blood");
            effect.transform.position = point;
            var particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.duration = .25f; main.startLifetime = .45f;
            main.startSpeed = 1.4f; main.startSize = .045f; main.gravityModifier = .6f;
            main.startColor = new Color(.55f, .015f, .01f); main.maxParticles = 16;
            var emission = particles.emission; emission.rateOverTime = 0;
            if (bloodMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                    Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) { Destroy(effect); return; }
                bloodMaterial = new Material(shader);
                bloodMaterial.SetColor("_BaseColor", new Color(.55f, .015f, .01f));
            }
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = bloodMaterial;
            particles.Play(); particles.Emit(12);
            Destroy(effect, 1.1f);
        }

        private void OnDestroy()
        {
            if (root?.Production != null)
            { root.Production.Animals.Impact -= OnImpact; root.Production.Animals.CanHit = null;
                root.Production.Animals.CanHunt = null; }
            foreach (var arrow in arrows.Values) if (arrow != null) Destroy(arrow);
            if (bloodMaterial != null) Destroy(bloodMaterial);
        }
    }
}
