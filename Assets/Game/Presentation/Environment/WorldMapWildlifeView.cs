using Game.Domain;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Disposable identity label for one Tier1 animal view.</summary>
    public sealed class WorldMapWildlifeView : MonoBehaviour
    {
        public StableEntityId StableId { get; private set; }
        public string SpeciesId { get; private set; }
        private LineRenderer selectionRing;
        private Material ringMaterial;
        private Animator[] animators;
        private bool[] hasMoving;
        private Transform visual;
        private Transform[] feet;

        public void ConfigureGrounding(Transform model)
        {
            visual = model;
            feet = SpeciesId == "boar" ? model.GetComponentsInChildren<Transform>(true)
                .Where(x => x.name.StartsWith("foot.") || x.name.StartsWith("front_foot.")).ToArray() : null;
        }

        // Called by the scene scheduler after animation, never an Update on every animal.
        public void GroundAnimatedFeet(float groundY)
        {
            if (visual == null || feet == null || feet.Length == 0) return;
            var bottom = feet.Min(x => x.position.y);
            visual.position += Vector3.up * (groundY + .025f - bottom);
        }

        public static void AlignVisibleGeometry(GameObject model, Vector3 origin)
        {
            // Skinned renderer bounds include the whole animation envelope (including death), not the standing hooves.
            foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.Update(0);
            var bounds = new Bounds(); var found = false;
            void Include(Vector3 point) { if (!found) { bounds = new Bounds(point,Vector3.zero); found = true; } else bounds.Encapsulate(point); }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (renderer is SkinnedMeshRenderer skin)
                {
                    var baked = new Mesh(); skin.BakeMesh(baked);
                    foreach (var vertex in baked.vertices) Include(skin.transform.TransformPoint(vertex));
                    if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked);
                }
                else if (renderer is MeshRenderer) { Include(renderer.bounds.min); Include(renderer.bounds.max); }
            }
            if (found) model.transform.position -= new Vector3(bounds.center.x-origin.x,bounds.min.y-origin.y,bounds.center.z-origin.z);
        }

        public string DisplayName => SpeciesId switch
        {
            "deer" => "Олень", "boar" => "Кабан", "hare" => "Заяц", "fox" => "Лиса",
            "wolf" => "Волк", "bear" => "Медведь", "waterfowl" => "Водоплавающая птица", _ => "Дикое животное"
        };

        public void Bind(StableEntityId stableId, string speciesId)
        {
            StableId = stableId;
            SpeciesId = speciesId;
            animators = GetComponentsInChildren<Animator>(true);
            hasMoving = new bool[animators.Length];
            for (var i = 0; i < animators.Length; i++)
                foreach (var parameter in animators[i].parameters)
                    if (parameter.name == "Moving" && parameter.type == AnimatorControllerParameterType.Bool)
                        hasMoving[i] = true;
            var bounds = new Bounds(transform.position + Vector3.up * .5f, Vector3.one);
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            }
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var body = gameObject.AddComponent<BoxCollider>();
            body.center = transform.InverseTransformPoint(bounds.center);
            var size = transform.InverseTransformVector(Vector3.Max(bounds.size, Vector3.one * .6f));
            body.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            body.isTrigger = true;
        }

        public void SetSelected(bool selected)
        {
            if (selected && selectionRing == null)
            {
                var ring = new GameObject("Wildlife selection");
                ring.transform.SetParent(transform, false);
                selectionRing = ring.AddComponent<LineRenderer>();
                selectionRing.useWorldSpace = false;
                selectionRing.loop = true;
                selectionRing.widthMultiplier = .08f;
                ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                ringMaterial.SetColor("_BaseColor", new Color(1f, .7f, .15f));
                selectionRing.sharedMaterial = ringMaterial;
                selectionRing.positionCount = 32;
                for (var i = 0; i < 32; i++)
                {
                    var angle = i * Mathf.PI * 2f / 32;
                    selectionRing.SetPosition(i, new Vector3(Mathf.Cos(angle), .12f, Mathf.Sin(angle)));
                }
            }
            if (selectionRing != null) selectionRing.enabled = selected;
        }

        public void SetLocomotion(float speed, bool paused = false)
        {
            for (var i = 0; animators != null && i < animators.Length; i++)
            {
                if (animators[i] == null || !animators[i].enabled) continue;
                if (hasMoving[i]) animators[i].SetBool("Moving", speed > .01f);
                animators[i].speed = paused ? 0f : speed > .01f ? Mathf.Clamp(speed / .9f, .5f, 2.8f) : 1f;
            }
        }

        private void OnDestroy() { if (ringMaterial != null) Destroy(ringMaterial); }
    }
}
