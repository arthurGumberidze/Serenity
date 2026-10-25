using Game.Domain;
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

        public string DisplayName => SpeciesId switch
        {
            "deer" => "Олень", "boar" => "Кабан", "hare" => "Заяц", "fox" => "Лиса",
            "wolf" => "Волк", "bear" => "Медведь", "waterfowl" => "Водоплавающая птица", _ => "Дикое животное"
        };

        public void Bind(StableEntityId stableId, string speciesId)
        {
            StableId = stableId;
            SpeciesId = speciesId;
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

        private void OnDestroy() { if (ringMaterial != null) Destroy(ringMaterial); }
    }
}
