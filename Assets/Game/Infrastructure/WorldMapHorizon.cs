using System.Collections.Generic;
using Game.Presentation.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure
{
    // Loading-time scenery outside the simulation boundary. No colliders, production or NavMesh surfaces.
    public sealed class WorldMapHorizon : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new();
        private readonly List<Material> materials = new();
        public void Initialize(TiledTerrainWorld world, StarterNatureCatalog nature)
        {
            var definition = world.Definition;
            var minimum = new Vector2(definition.Origin.X, definition.Origin.Z);
            var maximum = minimum + new Vector2(definition.WidthMeters, definition.DepthMeters);
            var river = definition.MapData.Hydrology.MainRiver;
            var mouth = river.Spline.Points[river.Spline.Points.Count - 1];
            var outletZ = definition.MapData.CoordinateTransform.ToUnity(mouth.Position).Z;
            var outletHalfWidth = mouth.WidthMeters * .5f;
            var outletLevel = river.SurfaceLevelPolicy.Sample(1f);
            const int segments = 48, rings = 12, ringSize = segments * 4;
            const float distance = 650f;
            var vertices = new Vector3[(rings + 1) * ringSize];
            var colors = new Color[vertices.Length]; var triangles = new List<int>();
            for (var ring = 0; ring <= rings; ring++)
            for (var side = 0; side < 4; side++)
            for (var i = 0; i < segments; i++)
            {
                var t = i / (float)segments; var outward = ring / (float)rings;
                var low = minimum - Vector2.one * distance * outward;
                var high = maximum + Vector2.one * distance * outward;
                var p = side == 0 ? new Vector2(Mathf.Lerp(low.x, high.x, t), low.y) :
                    side == 1 ? new Vector2(high.x, Mathf.Lerp(low.y, high.y, t)) :
                    side == 2 ? new Vector2(Mathf.Lerp(high.x, low.x, t), high.y) :
                    new Vector2(low.x, Mathf.Lerp(high.y, low.y, t));
                var edge = new Vector2(Mathf.Clamp(p.x, minimum.x, maximum.x), Mathf.Clamp(p.y, minimum.y, maximum.y));
                var height = world.HeightSampler.SampleHeight(edge.x, edge.y);
                var coast = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum.y, minimum.y + 100f, edge.y));
                if (side == 1) coast = Mathf.Max(coast,
                    1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum.y + 200f, minimum.y + 350f, edge.y)));
                // South opens onto the sea; the other sides extend into distant wooded ridges.
                var ridge = 90f + Mathf.PerlinNoise(p.x * .004f, p.y * .004f) * 140f;
                var target = Mathf.Lerp(ridge, -8f, coast);
                var blend = Mathf.SmoothStep(0f, 1f, outward);
                var index = ring * ringSize + side * segments + i;
                vertices[index] = new Vector3(p.x, Mathf.Lerp(height, target, blend), p.y);
                // Continue the main river through the scenic coast instead of ending it in a hill.
                var channelZ = outletZ - outward * 100f;
                if (p.x >= maximum.x && Mathf.Abs(p.y - channelZ) < Mathf.Lerp(outletHalfWidth, 100f, outward))
                    vertices[index].y = Mathf.Min(vertices[index].y, Mathf.Lerp(outletLevel - .5f, 4.5f, outward));
                var green = Color.Lerp(new Color(.18f,.29f,.14f), new Color(.30f,.36f,.28f), outward);
                var sand = new Color(.52f,.47f,.31f);
                colors[index] = Color.Lerp(green, sand, coast * Mathf.Clamp01(outward * 4f));
                if (ring == rings) continue;
                var next = ring * ringSize + (side * segments + i + 1) % ringSize;
                triangles.Add(index); triangles.Add(next); triangles.Add(index + ringSize);
                triangles.Add(next); triangles.Add(next + ringSize); triangles.Add(index + ringSize);
            }
            var mesh = new Mesh { name = "Distant shore and ridges" };
            mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var material = new Material(Resources.Load<Shader>("World/WorldMapHorizon")); materials.Add(material);
            AddMesh("Distant land", mesh, material);
            var sea = new Mesh { name = "Southern sea" };
            // Start the scenic sea underneath the southern terrain instead of exposing a straight
            // rectangular edge 120 m beyond it. The lowered horizon shell then reveals water along
            // its actual terrain intersection; low RTS cameras near the southern lake no longer see
            // an unrelated horizontal ocean layer behind the lake surface.
            var hiddenSeaEdge = minimum.y + 48f;
            sea.vertices = new[] { new Vector3(minimum.x - 4000, 6, minimum.y - 4000),
                new Vector3(minimum.x - 4000, 6, hiddenSeaEdge),
                new Vector3(maximum.x + 4000, 6, hiddenSeaEdge),
                new Vector3(maximum.x + 4000, 6, minimum.y - 4000) };
            sea.triangles = new[] { 0, 1, 2, 0, 2, 3 }; sea.colors = new[] { Color.red, Color.red, Color.red, Color.red };
            sea.RecalculateNormals(); sea.RecalculateBounds(); meshes.Add(sea);
            var water = new Material(Resources.Load<Shader>("World/StarterWater")); materials.Add(water);
            if (nature.WaterRipples != null) water.SetTexture("_Ripples", nature.WaterRipples);
            AddMesh("Southern sea", sea, water);
            var estuary = new Mesh { name = "River estuary" };
            var outlet = outletZ;
            estuary.vertices = new[] {
                new Vector3(maximum.x, outletLevel + .02f, outlet - outletHalfWidth),
                new Vector3(maximum.x, outletLevel + .02f, outlet + outletHalfWidth),
                new Vector3(maximum.x + 650, 6.02f, outlet), new Vector3(maximum.x + 650, 6.02f, outlet - 200),
                new Vector3(maximum.x + 120, 6, minimum.y - 120), new Vector3(maximum.x + 120, 6, minimum.y + 350),
                new Vector3(maximum.x + 4000, 6, minimum.y + 350), new Vector3(maximum.x + 4000, 6, minimum.y - 120) };
            estuary.triangles = new[] { 0,1,2,0,2,3,4,5,6,4,6,7 };
            estuary.colors = new[] { Color.red,Color.red,Color.red,Color.red,Color.red,Color.red,Color.red,Color.red };
            estuary.RecalculateNormals(); estuary.RecalculateBounds(); meshes.Add(estuary);
            AddMesh("River mouth and eastern sea", estuary, water);
        }
        private void AddMesh(string label, Mesh mesh, Material material)
        {
            var view = new GameObject(label); view.transform.SetParent(transform, false);
            // Mesh coordinates are global; the world root need not be the origin.
            view.transform.position = Vector3.zero; view.transform.rotation = Quaternion.identity;
            view.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = view.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        private void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
