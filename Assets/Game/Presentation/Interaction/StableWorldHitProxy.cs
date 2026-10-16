using System;
using Game.Domain;
using Game.Domain.Work;
using UnityEngine;

namespace Game.Presentation.Interaction
{
    [DisallowMultipleComponent]
    public sealed class StableWorldHitProxy : MonoBehaviour
    {
        private static Material sharedRingMaterial;
        private LineRenderer ring;
        private bool hovered;
        private bool selected;

        public bool IsBound { get; private set; }
        public StableEntityId EntityId { get; private set; }
        public ContextTargetKind TargetKind { get; private set; }
        public bool IsHovered => hovered;
        public bool IsSelected => selected;

        public void Bind(StableEntityId entityId, ContextTargetKind targetKind)
        {
            if (!entityId.IsValid) throw new ArgumentException("Stable target identity required.", nameof(entityId));
            if (!Enum.IsDefined(typeof(ContextTargetKind), targetKind)) throw new ArgumentOutOfRangeException(nameof(targetKind));
            EntityId = entityId;
            TargetKind = targetKind;
            IsBound = true;
            EnsureCollider();
            RefreshRing();
        }

        public void SetHovered(bool value)
        {
            if (hovered == value) return;
            hovered = value;
            RefreshRing();
        }

        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            RefreshRing();
        }

        private void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>(true) != null) return;
            var renderers = GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(transform.position + Vector3.up, new Vector3(1.2f, 2f, 1.2f));
            var initialized = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                if (!initialized) { bounds = renderer.bounds; initialized = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.center = transform.InverseTransformPoint(bounds.center);
            var localSize = transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Max(0.6f, Mathf.Abs(localSize.x)),
                Mathf.Max(0.8f, Mathf.Abs(localSize.y)), Mathf.Max(0.6f, Mathf.Abs(localSize.z)));
        }

        private void RefreshRing()
        {
            var visible = IsBound && (hovered || selected);
            if (!visible)
            {
                if (ring != null) ring.gameObject.SetActive(false);
                return;
            }
            if (ring == null) ring = CreateRing();
            ring.gameObject.SetActive(true);
            var color = selected ? new Color(0.24f, 1f, 0.48f, 0.95f) : new Color(1f, 0.78f, 0.18f, 0.9f);
            ring.startColor = color;
            ring.endColor = color;
        }

        private LineRenderer CreateRing()
        {
            var ringObject = new GameObject("Interaction State Ring");
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localPosition = Vector3.up * 0.08f;
            var line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 40;
            line.widthMultiplier = 0.07f;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = RingMaterial();
            var radius = TargetKind == ContextTargetKind.Character ? 0.65f : 1.05f;
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = Mathf.PI * 2f * i / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            return line;
        }

        private static Material RingMaterial()
        {
            if (sharedRingMaterial != null) return sharedRingMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            sharedRingMaterial = new Material(shader) { name = "Serenity Interaction Ring" };
            return sharedRingMaterial;
        }
    }
}
