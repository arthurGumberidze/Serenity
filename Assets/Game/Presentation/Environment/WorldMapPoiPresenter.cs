using System;
using Game.Domain;
using Game.Domain.World;
using UnityEngine;

namespace Game.Presentation.Environment
{
    [DisallowMultipleComponent]
    public sealed class WorldMapPoiProtectionVolume : MonoBehaviour
    {
        public StableEntityId StableId { get; private set; }
        public string PoiId { get; private set; }
        public float RadiusMeters { get; private set; }
        public bool AllowsAutomaticClearance => false;

        public void Configure(WorldMapPoiPlanEntry point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            StableId = point.StableId;
            PoiId = point.Id;
            RadiusMeters = point.ProtectionRadiusMeters;
            var trigger = GetComponent<BoxCollider>();
            if (trigger == null) trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 1.5f;
            trigger.size = new Vector3(RadiusMeters * 2f, 3f, RadiusMeters * 2f);
        }

        public bool OverlapsHorizontalBox(Vector3 center, float halfExtent)
        {
            var position = transform.position;
            var nearestX = Mathf.Clamp(position.x, center.x - halfExtent, center.x + halfExtent);
            var nearestZ = Mathf.Clamp(position.z, center.z - halfExtent, center.z + halfExtent);
            var dx = position.x - nearestX;
            var dz = position.z - nearestZ;
            return dx * dx + dz * dz <= RadiusMeters * RadiusMeters;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldMapPoiPresenter : MonoBehaviour
    {
        public WorldMapPoiPlanEntry Point { get; private set; }
        public WorldMapPoiProtectionVolume Protection { get; private set; }

        public static WorldMapPoiPresenter Create(WorldMapPoiPlanEntry point, WorldMapPoiArtCatalog.Entry art,
            Transform parent, Func<Vector3, Vector3> projectToGround = null)
        {
            if (point == null || art == null || parent == null) throw new ArgumentNullException();
            var root = new GameObject("World Map POI " + point.Id);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(point.Position.X, point.Position.Y, point.Position.Z);
            var presenter = root.AddComponent<WorldMapPoiPresenter>();
            presenter.Point = point;
            presenter.Protection = root.AddComponent<WorldMapPoiProtectionVolume>();
            presenter.Protection.Configure(point);
            CreateRing(art.PrimaryPrefab, art.PrimaryCount, art.PrimaryRadius, art.VisualScale, root.transform, 0f, projectToGround);
            if (art.AccentCount > 0)
                CreateRing(art.AccentPrefab, art.AccentCount, art.AccentRadius, 1f, root.transform, 31f, projectToGround);
            if (art.EffectPrefab != null)
            {
                var effect = Instantiate(art.EffectPrefab, root.transform);
                effect.name = "Audited POI effect";
                effect.transform.localPosition = Vector3.up * 0.2f;
            }
            return presenter;
        }

        private static void CreateRing(GameObject prefab, int count, float radius, float scale, Transform parent,
            float phaseDegrees, Func<Vector3, Vector3> projectToGround)
        {
            for (var index = 0; index < count; index++)
            {
                var angle = (phaseDegrees + index * 360f / count) * Mathf.Deg2Rad;
                var view = WorldMapScenery.Instantiate(prefab, parent);
                view.name = prefab.name + " POI view " + index;
                view.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius);
                view.transform.localRotation = Quaternion.Euler(0f, phaseDegrees + index * 360f / count + 180f, 0f);
                view.transform.localScale *= scale;
                if (projectToGround != null) view.transform.position = projectToGround(view.transform.position);
                WorldMapScenery.AddSolidFootprint(view);
                // Runtime tiled NavMesh baking cannot consume unreadable vendor MeshColliders reliably.
                foreach (var collider in view.GetComponentsInChildren<MeshCollider>(true)) collider.enabled = false;
            }
        }
    }
}
