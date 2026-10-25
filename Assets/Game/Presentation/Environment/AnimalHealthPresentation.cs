using Game.Domain;
using Game.Domain.World;
using UnityEngine;

namespace Game.Presentation.Environment
{
    // Driven by the shared animal presenter, never by an Update on each animal.
    public sealed class AnimalHealthPresentation : MonoBehaviour
    {
        private AnimalHealthRegistry registry;
        private StableEntityId id;
        private TextMesh label;
        private Animator[] animators;
        private Animation[] legacyAnimations;
        private Transform body;
        private Quaternion standing;
        private bool dead;
        private Quaternion corpseRotation;
        public void Bind(AnimalHealthRegistry source, StableEntityId identity, Transform model = null)
        {
            registry = source; id = identity;
            body = model != null ? model : transform;
            standing = body != null ? body.localRotation : Quaternion.identity;
            animators = GetComponentsInChildren<Animator>(true);
            legacyAnimations = GetComponentsInChildren<Animation>(true);
            var text = new GameObject("Animal health");
            text.transform.SetParent(transform, false);
            text.transform.localPosition = Vector3.up * 1.8f;
            label = text.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.characterSize = .065f; label.fontSize = 32;
            Refresh();
        }
        public void Refresh()
        {
            if (registry == null || !registry.TryGet(id, out var health)) return;
            if (health.Harvested) { gameObject.SetActive(false); return; }
            label.text = health.Alive ? $"{health.Health}/{health.Maximum}" : "Туша · 0 HP";
            label.color = health.Alive ? Color.Lerp(new Color(.9f, .18f, .12f), new Color(.4f, .95f, .3f),
                (float)health.Health / health.Maximum) : new Color(.7f, .65f, .6f);
            var camera = Camera.main;
            label.transform.position = transform.position + Vector3.up * 1.8f;
            if (camera != null)
            {
                label.transform.rotation = camera.transform.rotation;
                label.gameObject.SetActive((camera.transform.position - transform.position).sqrMagnitude < 10000f);
            }
            if (dead == !health.Alive)
            {
                if (dead && body != null) body.rotation = corpseRotation;
                return;
            }
            dead = !health.Alive;
            foreach (var animator in animators) if (animator != null) animator.enabled = !dead;
            foreach (var animation in legacyAnimations) if (animation != null)
            { if (dead) animation.Stop(); animation.enabled = !dead; }
            if (body != null)
            {
                if (dead)
                {
                    var ground = transform.position.y;
                    // Roll in the authority root's world frame, independent of vendor FBX axes.
                    corpseRotation = Quaternion.AngleAxis(88f, transform.forward) * body.rotation;
                    body.rotation = corpseRotation;
                    var meshes = body.GetComponentsInChildren<Renderer>();
                    var bottom = float.PositiveInfinity;
                    foreach (var mesh in meshes)
                        if (!(mesh is LineRenderer) && mesh.GetComponent<TextMesh>() == null)
                            bottom = Mathf.Min(bottom, mesh.bounds.min.y);
                    if (!float.IsInfinity(bottom)) body.position += Vector3.up * (ground + .03f - bottom);
                }
                else body.localRotation = standing;
            }
        }
    }
}
