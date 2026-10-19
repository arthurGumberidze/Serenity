using System;
using System.Linq;
using Game.Domain.World;
using Game.Presentation.Environment;
using Game.Simulation.World;
using Game.Simulation.Production;
using Game.Domain.Resources;
using UnityEngine;
using Unity.Cinemachine;

namespace Game.Presentation.World
{
    /// <summary>Top-down camera over the canonical terrain; it never creates a second map.</summary>
    public sealed class P102StrategicWorldView : MonoBehaviour
    {
        private Camera worldCamera;
        private P102ConnectedWorldPresentation world;
        private ConnectedWorldService discovery;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private bool savedOrthographic;
        private float savedSize;
        private float savedFarClip;
        private Rect savedViewport;
        private CinemachineBrain brain;
        private bool savedBrainEnabled;
        public static Rect MapViewport => new Rect(0.32f, 0.075f, 0.675f, 0.80f);
        private GUIStyle labelStyle;
        private ProductionService production;
        private TiledWorldDefinition tiledWorld;
        private GlobalMapService globalMap;
        private Action<bool> setStrategicOverview;
        private int resourceFilter = -1;
        private static readonly string[] ResourceIds =
        { "wood_log", "stone", "plant_fiber", "hide", "berries", "iron_ore", "gold_ore", "meat", "fish" };
        private static readonly string[] ResourceNames =
        { "Брёвна", "Камень", "Волокно", "Шкуры", "Ягоды", "Железо", "Золото", "Мясо", "Рыба" };
        public bool IsActive { get; private set; }

        public void Initialize(Camera camera, P102ConnectedWorldPresentation presentation, ConnectedWorldService service,
            ProductionService productionService = null)
        {
            worldCamera = camera ?? throw new ArgumentNullException(nameof(camera));
            world = presentation ?? throw new ArgumentNullException(nameof(presentation));
            discovery = service ?? throw new ArgumentNullException(nameof(service));
            production = productionService;
        }

        public void Initialize(Camera camera, TiledWorldDefinition definition, ConnectedWorldService service,
            GlobalMapService mapService, ProductionService productionService, Action<bool> overviewVisibility)
        {
            worldCamera = camera ?? throw new ArgumentNullException(nameof(camera));
            tiledWorld = definition ?? throw new ArgumentNullException(nameof(definition));
            discovery = service ?? throw new ArgumentNullException(nameof(service));
            globalMap = mapService ?? throw new ArgumentNullException(nameof(mapService));
            production = productionService;
            setStrategicOverview = overviewVisibility ?? throw new ArgumentNullException(nameof(overviewVisibility));
        }

        public void Enter()
        {
            if (IsActive) return;
            savedPosition = worldCamera.transform.position;
            savedRotation = worldCamera.transform.rotation;
            savedOrthographic = worldCamera.orthographic;
            savedSize = worldCamera.orthographicSize;
            savedFarClip = worldCamera.farClipPlane;
            savedViewport = worldCamera.rect;
            brain = worldCamera.GetComponent<CinemachineBrain>();
            savedBrainEnabled = brain != null && brain.enabled;
            if (brain != null) brain.enabled = false;
            setStrategicOverview?.Invoke(true);
            worldCamera.rect = MapViewport;
            worldCamera.orthographic = true;
            var width = tiledWorld?.WidthMeters ?? ConnectedWorldContract.SizeMeters;
            var depth = tiledWorld?.DepthMeters ?? ConnectedWorldContract.SizeMeters;
            var centerX = tiledWorld == null ? 0f : tiledWorld.Origin.X + width * 0.5f;
            var centerZ = tiledWorld == null ? 0f : tiledWorld.Origin.Z + depth * 0.5f;
            worldCamera.orthographicSize = Mathf.Max(depth * 0.52f, width * 0.52f / worldCamera.aspect);
            worldCamera.transform.SetPositionAndRotation(new Vector3(centerX,
                Mathf.Max(420f, worldCamera.orthographicSize + 200f), centerZ), Quaternion.Euler(90f, 0f, 0f));
            worldCamera.farClipPlane = Mathf.Max(savedFarClip, worldCamera.transform.position.y + 500f);
            IsActive = true;
        }

        public void Exit()
        {
            if (!IsActive) return;
            if (worldCamera == null)
            {
                IsActive = false;
                return;
            }
            worldCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            worldCamera.orthographic = savedOrthographic;
            worldCamera.orthographicSize = savedSize;
            worldCamera.farClipPlane = savedFarClip;
            worldCamera.rect = savedViewport;
            if (brain != null) brain.enabled = savedBrainEnabled;
            setStrategicOverview?.Invoke(false);
            IsActive = false;
        }

        private void OnGUI()
        {
            if (!IsActive || worldCamera == null || discovery == null || world == null && tiledWorld == null) return;
            labelStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 13,
                normal = { textColor = Color.white } };
            var viewport = worldCamera.pixelRect;
            var guiViewport = new Rect(viewport.x, Screen.height - viewport.yMax, viewport.width, viewport.height);
            GUI.BeginGroup(guiViewport);
            DrawZoneOverlays();
            var fogColor = new Color(0.015f, 0.025f, 0.04f, 0.62f);
            var previous = GUI.color;
            GUI.color = fogColor;
            var layout = discovery.Layout;
            for (var x = 0; x < layout.Columns; x++) for (var z = 0; z < layout.Rows; z++)
            {
                var wx = layout.OriginX + (x + 0.5f) * layout.DiscoveryCellMeters;
                var wz = layout.OriginZ + (z + 0.5f) * layout.DiscoveryCellMeters;
                if (!discovery.IsRevealed(wx, wz))
                {
                    var half = layout.DiscoveryCellMeters * 0.5f;
                    var a = ToMapPoint(new Vector3(wx - half, 0f, wz + half));
                    var b = ToMapPoint(new Vector3(wx + half, 0f, wz - half));
                    GUI.DrawTexture(Rect.MinMaxRect(a.x, a.y, b.x + 1f, b.y + 1f), Texture2D.whiteTexture);
                }
            }
            GUI.color = previous;
            if (tiledWorld != null)
            {
                foreach (var location in globalMap.Locations)
                {
                    if (!discovery.IsRevealed(location.MapX, location.MapY)) continue;
                    var point = ToMapPoint(new Vector3(location.MapX, 0f, location.MapY));
                    if (point.z <= 0f) continue;
                    GUI.Box(new Rect(point.x - 75f, point.y - 18f, 150f, 36f), location.Name, labelStyle);
                }
            }
            else
            {
                foreach (var district in world.Districts)
                {
                    if (!discovery.IsRevealed(district.Anchor.position.x, district.Anchor.position.z)) continue;
                    var point = ToMapPoint(district.Anchor.position);
                    if (point.z <= 0f) continue;
                    GUI.Box(new Rect(point.x - 75f, point.y - 18f, 150f, 36f),
                        district.DisplayName + "\n" + district.Description, labelStyle);
                }
            }
            GUI.Box(new Rect(12f, 12f, 330f, 48f), "Стратегический обзор — 12 × 12 км\nТуман раскрывается присутствием и разведкой", labelStyle);
            DrawResourceFilters();
            GUI.EndGroup();
        }

        private void DrawZoneOverlays()
        {
            if (tiledWorld == null) return;
            var previous = GUI.color;
            foreach (var zone in tiledWorld.Zones)
            {
                if (zone.Uses.HasFlag(WorldZoneUse.Fishing) ||
                    !discovery.IsRevealed(zone.Center.X, zone.Center.Z)) continue;
                var center = ToMapPoint(new Vector3(zone.Center.X, 0f, zone.Center.Z));
                var horizontal = ToMapPoint(new Vector3(zone.Center.X + zone.RadiusX, 0f, zone.Center.Z));
                var vertical = ToMapPoint(new Vector3(zone.Center.X, 0f, zone.Center.Z + zone.RadiusZ));
                if (center.z <= 0f) continue;
                var color = zone.Uses.HasFlag(WorldZoneUse.Settlement) ? new Color(0.72f, 0.62f, 0.42f, 0.34f) :
                    zone.Uses.HasFlag(WorldZoneUse.Ore) ? new Color(0.58f, 0.55f, 0.5f, 0.34f) :
                    zone.Uses.HasFlag(WorldZoneUse.Forest) ? new Color(0.18f, 0.55f, 0.2f, 0.34f) :
                    zone.Uses.HasFlag(WorldZoneUse.Agriculture) ? new Color(0.78f, 0.66f, 0.2f, 0.34f) :
                    zone.Uses.HasFlag(WorldZoneUse.Berries) ? new Color(0.65f, 0.25f, 0.48f, 0.34f) :
                    new Color(0.55f, 0.43f, 0.24f, 0.3f);
                GUI.color = color;
                var halfWidth = Mathf.Abs(horizontal.x - center.x);
                var halfHeight = Mathf.Abs(vertical.y - center.y);
                GUI.DrawTexture(new Rect(center.x - halfWidth, center.y - halfHeight,
                    halfWidth * 2f, halfHeight * 2f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        private Vector3 ToMapPoint(Vector3 position)
        {
            var point = worldCamera.WorldToViewportPoint(position);
            return new Vector3(point.x * worldCamera.pixelWidth,
                (1f - point.y) * worldCamera.pixelHeight, point.z);
        }

        private void OnDisable() => Exit();

        private void DrawResourceFilters()
        {
            if (production == null) return;
            GUI.Box(new Rect(12f, 66f, 330f, 92f), "Известные природные ресурсы", labelStyle);
            for (var i = 0; i < ResourceIds.Length; i++)
            {
                var row = i / 5;
                var column = i % 5;
                var old = GUI.color;
                if (resourceFilter == i) GUI.color = new Color(0.35f, 1f, 0.55f, 1f);
                if (GUI.Button(new Rect(18f + column * 63f, 92f + row * 28f, 60f, 24f), ResourceNames[i]))
                    resourceFilter = resourceFilter == i ? -1 : i;
                GUI.color = old;
            }
            if (resourceFilter < 0) return;
            var selected = new ResourceId(ResourceIds[resourceFilter]);
            foreach (var site in production.Sites)
            {
                var recipe = production.Recipes.Get(site.RecipeId);
                if (recipe.Outputs.All(x => x.ResourceId != selected) ||
                    !discovery.IsRevealed(site.Position.X, site.Position.Z)) continue;
                var point = ToMapPoint(new Vector3(site.Position.X, site.Position.Y, site.Position.Z));
                if (point.z <= 0f) continue;
                var text = site.IsDepleted ? "×" : site.RemainingAmount.ToString();
                GUI.Box(new Rect(point.x - 18f, point.y - 18f, 36f, 30f), text, labelStyle);
            }
            if (production.Industry == null) return;
            foreach (var zone in production.Industry.Zones)
            {
                var definition = production.Industry.GetDefinition(zone.DefinitionId);
                if (definition.OutputsPerCycle.All(x => x.ResourceId != selected) ||
                    !discovery.IsRevealed(zone.Position.X, zone.Position.Z)) continue;
                var point = ToMapPoint(new Vector3(zone.Position.X, zone.Position.Y, zone.Position.Z));
                if (point.z <= 0f) continue;
                var text = zone.FacilityBuilt ? zone.Remaining.ToString() : "⌂";
                GUI.Box(new Rect(point.x - 22f, point.y - 18f, 44f, 30f), text, labelStyle);
            }
        }
    }
}
