using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Domain;
using Game.Domain.Resources;
using Game.Domain.Production;
using Game.Simulation.Combat;
using Game.Simulation.Military;
using Game.Simulation.World;
using Game.Simulation.Production;
using Game.Simulation.Work;
using Game.Domain.Work;
using Game.Presentation.Localization;
using Game.Presentation.UI;
using Game.Presentation.Environment;
using Game.Domain.Weather;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.World
{
    /// <summary>Disposable schematic projection of U25 canonical map state.</summary>
    public sealed class GlobalMapPresentationController : MonoBehaviour
    {
        public sealed class GlobalResourceMarker : MonoBehaviour
        {
            private Renderer cachedRenderer;
            private Color baseColor;
            public ResourceId ResourceId { get; private set; }
            public IReadOnlyList<StableEntityId> SiteIds { get; private set; }
            public void Configure(ResourceId resourceId, IEnumerable<StableEntityId> siteIds, Color color)
            {
                ResourceId = resourceId;
                SiteIds = (siteIds ?? Enumerable.Empty<StableEntityId>()).OrderBy(x => x).ToArray();
                baseColor = color;
                cachedRenderer = GetComponent<Renderer>();
                SetSelected(false);
            }
            public void SetSelected(bool selected)
            {
                if (cachedRenderer == null) return;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", selected ? Color.Lerp(baseColor, Color.white, 0.58f) : baseColor);
                block.SetColor("_Color", selected ? Color.Lerp(baseColor, Color.white, 0.58f) : baseColor);
                cachedRenderer.SetPropertyBlock(block);
                transform.localScale = selected ? Vector3.one * 1.28f : Vector3.one;
            }
        }
        [SerializeField] private Camera mapCamera;
        [SerializeField] private Transform markerRoot;
        private GlobalMapService map;
        private MilitaryUnitService units;
        private BattleResolutionService battles;
        private GameModeTransitionService transitions;
        private ProductionService production;
        private DirectWorkCommandService directWork;
        private WorkPriority resourcePriority = WorkPriority.High;
        private Func<long> currentTick;
        private Action<GameModeTransitionResult> localEntered;
        private IWeatherModifierProvider weather;
        private P95NaturalAtmosphereProfile atmosphereProfile;
        private bool ownsAtmosphereProfile;
        private long displayedAtmosphereBucket = long.MinValue;
        private readonly Dictionary<WorldLocationId, GameObject> locationMarkers =
            new Dictionary<WorldLocationId, GameObject>();
        private readonly List<GameObject> transientMarkers = new List<GameObject>();
        private readonly Dictionary<MilitaryUnitId, GameObject> unitMarkers =
            new Dictionary<MilitaryUnitId, GameObject>();
        private readonly Dictionary<MilitaryUnitId, WorldTravelState> travelsByUnit =
            new Dictionary<MilitaryUnitId, WorldTravelState>();
        private WorldLocationId? selectedLocation;
        private MilitaryUnitId? selectedUnit;
        private Vector3 cameraTarget;
        private float cameraOffsetZ;
        private ulong lastDynamicHash;
        private float nextRefreshTime;
        private readonly List<GlobalResourceMarker> resourceMarkers = new List<GlobalResourceMarker>();
        private GlobalResourceMarker selectedResource;
        private GUIStyle mapPanelStyle;
        private GUIStyle mapTitleStyle;
        private GUIStyle mapLabelStyle;
        private GUIStyle mapButtonStyle;
        private GUIStyle mapSelectedButtonStyle;
        private GUIStyle mapDisabledReasonStyle;
        private GUIStyle mapLocationStyle;
        private Texture2D mapPanelTexture;
        private Texture2D mapButtonTexture;
        private Texture2D mapSelectedTexture;

        public Camera MapCamera => mapCamera;
        public int LocationMarkerCount => locationMarkers.Count;
        public int ResourceMarkerCount => resourceMarkers.Count;
        public GlobalResourceMarker SelectedResourceMarker => selectedResource;
        public bool ShowDeveloperUi { get; set; }
        public bool NaturalMapReady { get; private set; }
        public int NaturalLocationCount { get; private set; }
        public string NaturalLayoutHash => atmosphereProfile == null ? string.Empty : atmosphereProfile.ComputeLayoutHash();
        public P95AtmosphereVisualState CurrentAtmosphere { get; private set; }

        public void Configure(Camera camera, Transform root)
        {
            mapCamera = camera;
            markerRoot = root;
        }

        public void Initialize(GlobalMapService globalMap, MilitaryUnitService militaryUnits,
            BattleResolutionService battleResolution, GameModeTransitionService modeTransitions,
            ProductionService productionService, DirectWorkCommandService directWorkCommands, Func<long> tickProvider,
            Action<GameModeTransitionResult> onLocalEntered, IWeatherModifierProvider weatherProvider = null,
            P95NaturalAtmosphereProfile profile = null)
        {
            map = globalMap ?? throw new ArgumentNullException(nameof(globalMap));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            battles = battleResolution ?? throw new ArgumentNullException(nameof(battleResolution));
            transitions = modeTransitions ?? throw new ArgumentNullException(nameof(modeTransitions));
            production = productionService ?? throw new ArgumentNullException(nameof(productionService));
            directWork = directWorkCommands ?? throw new ArgumentNullException(nameof(directWorkCommands));
            currentTick = tickProvider ?? throw new ArgumentNullException(nameof(tickProvider));
            localEntered = onLocalEntered;
            cameraTarget = new Vector3(4f, 0f, 0f);
            cameraOffsetZ = mapCamera.transform.position.z;
            weather = weatherProvider;
            atmosphereProfile = profile;
            if (atmosphereProfile == null)
            {
                atmosphereProfile = ScriptableObject.CreateInstance<P95NaturalAtmosphereProfile>();
                atmosphereProfile.ConfigureDefaults();
                ownsAtmosphereProfile = true;
            }
            atmosphereProfile.ValidateConfiguration();
            BuildStaticMap();
            RefreshAtmosphere();
            RefreshDynamicMarkers();
            lastDynamicHash = DynamicHash();
        }

        private void Update()
        {
            if (map == null || mapCamera == null) return;
            RefreshAtmosphere();
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var pan = Vector3.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan += Vector3.forward;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan += Vector3.back;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan += Vector3.left;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan += Vector3.right;
            }
            cameraTarget += pan.normalized * (25f * Time.unscaledDeltaTime);
            cameraTarget.x = Mathf.Clamp(cameraTarget.x, -40f, 40f);
            cameraTarget.z = Mathf.Clamp(cameraTarget.z, -30f, 30f);
            var zoom = mouse == null ? 0f : mouse.scroll.ReadValue().y;
            var height = Mathf.Clamp(mapCamera.transform.position.y - zoom * 0.02f, 130f, 190f);
            mapCamera.transform.position = new Vector3(cameraTarget.x, height, cameraTarget.z + cameraOffsetZ);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                TrySelectResourceAt(mouse.position.ReadValue());
            UpdateTravelMarkerPositions();
            if (Time.unscaledTime >= nextRefreshTime)
            {
                nextRefreshTime = Time.unscaledTime + 0.25f;
                var hash = DynamicHash();
                if (hash != lastDynamicHash)
                {
                    RefreshDynamicMarkers();
                    lastDynamicHash = hash;
                }
            }
        }

        private void BuildStaticMap()
        {
            foreach (Transform child in markerRoot) Destroy(child.gameObject);
            locationMarkers.Clear();
            var plane = GameObject.Find("Schematic Map Plane")?.GetComponent<Renderer>();
            if (plane != null)
            {
                var mapMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mapMaterial.color = atmosphereProfile.MapWater;
                mapMaterial.SetFloat("_Smoothness", 0.72f);
                plane.sharedMaterial = mapMaterial;
                plane.transform.position = new Vector3(0f, 99.45f, 0f);
            }
            CreateLandRelief();
            foreach (var route in map.Routes)
            {
                var from = map.GetLocation(route.From);
                var to = map.GetLocation(route.To);
                var lineObject = new GameObject("Route " + route.From + " - " + route.To);
                lineObject.transform.SetParent(markerRoot, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.SetPositions(new[] { Position(from, 103.1f), Position(to, 103.1f) });
                line.startWidth = line.endWidth = 0.34f;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = line.endColor = atmosphereProfile.MapRoute;
            }
            foreach (var location in map.Locations)
            {
                var definition = atmosphereProfile.GetLocation(location.Id.Value);
                var marker = CreateNaturalMarker("Location " + location.Id,
                    location.Type == WorldLocationType.Settlement ? MarkerShape.Settlement : MarkerShape.Location,
                    location.Id == map.ActiveLocationId ? Color.Lerp(definition.GroundColor, Color.cyan, .58f) :
                    definition.GroundColor);
                marker.transform.SetParent(markerRoot, false);
                marker.transform.position = Position(location, 102f + definition.Relief);
                marker.transform.localScale = (location.Type == WorldLocationType.Settlement
                    ? new Vector3(1.25f, .72f, 1.25f) : Vector3.one * 1.12f) * definition.Scale;
                AddLocationAffordance(marker.transform, definition);
                locationMarkers.Add(location.Id, marker);
            }
            NaturalLocationCount = locationMarkers.Count;
            NaturalMapReady = NaturalLocationCount == 6 && markerRoot.Find("P9.5 Natural Land Relief") != null;
            BuildResourceMarkers();
        }

        private void RefreshDynamicMarkers()
        {
            foreach (var marker in transientMarkers) if (marker != null) Destroy(marker);
            transientMarkers.Clear();
            unitMarkers.Clear();
            travelsByUnit.Clear();
            foreach (var travel in map.Travels) travelsByUnit.Add(travel.UnitId, travel);
            foreach (var unit in units.Units.Where(x => x.Representation == MilitaryUnitRepresentationMode.Aggregate))
            {
                if (!TryUnitPosition(unit, out var position)) continue;
                var marker = CreateNaturalMarker("Unit " + unit.Id, MarkerShape.Unit,
                    unit.OwnerSideId.Value == "player" ? Color.green : Color.red);
                marker.transform.SetParent(markerRoot, false);
                marker.transform.position = position + new Vector3((unit.Id.GetHashCode() & 3) * 0.45f, 0f, 0f);
                marker.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
                transientMarkers.Add(marker);
                unitMarkers.Add(unit.Id, marker);
            }
            foreach (var battle in battles.Battles.Where(x => x.Status == CombatEncounterStatus.Active &&
                         x.Mode == BattleMode.Abstract && x.SideAUnitId.HasValue))
            {
                var unit = units.Get(battle.SideAUnitId.Value);
                try
                {
                    var location = map.GetLocation(new WorldLocationId(unit.AbstractLocationId));
                    var marker = CreateNaturalMarker("Battle " + battle.BattleId, MarkerShape.Battle,
                        new Color(1f, 0.35f, 0.05f));
                    marker.transform.SetParent(markerRoot, false);
                    marker.transform.position = Position(location, 104f);
                    marker.transform.localScale = new Vector3(1.2f, 0.3f, 1.2f);
                    transientMarkers.Add(marker);
                }
                catch (ArgumentException) { }
                catch (KeyNotFoundException) { }
            }
        }

        private void UpdateTravelMarkerPositions()
        {
            foreach (var pair in travelsByUnit)
            {
                if (!unitMarkers.TryGetValue(pair.Key, out var marker) || marker == null) continue;
                var travel = pair.Value;
                var origin = Position(map.GetLocation(travel.Origin), 102.5f);
                var destination = Position(map.GetLocation(travel.Destination), 102.5f);
                var denominator = Math.Max(1L, travel.ArrivalTick - travel.DepartureTick);
                var t = Mathf.Clamp01((float)(currentTick() - travel.DepartureTick) / denominator);
                marker.transform.position = Vector3.Lerp(origin, destination, t) +
                    new Vector3((pair.Key.GetHashCode() & 3) * 0.45f, 0f, 0f);
            }
        }

        private bool TryUnitPosition(MilitaryUnitState unit, out Vector3 position)
        {
            if (travelsByUnit.TryGetValue(unit.Id, out var travel))
            {
                position = Position(map.GetLocation(travel.Origin), 102.5f);
                return true;
            }
            try { position = Position(map.GetLocation(new WorldLocationId(unit.AbstractLocationId)), 102.5f); }
            catch (ArgumentException) { position = default; return false; }
            catch (KeyNotFoundException) { position = default; return false; }
            return true;
        }

        private void OnGUI()
        {
            if (map == null) return;
            EnsureUiStyles();
            var safe = Screen.safeArea;
            foreach (var location in map.Locations)
            {
                if (!locationMarkers.TryGetValue(location.Id, out var marker) || marker == null) continue;
                var screen = mapCamera.WorldToScreenPoint(marker.transform.position + Vector3.up * 3f);
                if (screen.z <= 0f) continue;
                var point = new Vector2(screen.x, Screen.height - screen.y);
                if (point.x < safe.xMin + 182f || point.x > safe.xMax - 362f ||
                    point.y < safe.yMin + 82f || point.y > safe.yMax - 48f) continue;
                GUI.Label(new Rect(point.x - 68f, point.y - 30f, 136f, 24f), LocationName(location.Id),
                    mapLocationStyle);
            }
            if (selectedResource != null)
            {
                GUILayout.BeginArea(new Rect(safe.xMax - 326, safe.yMin + 176, 306, 252), mapPanelStyle);
                GUILayout.Label(RussianTextCatalog.Content("resource", selectedResource.ResourceId.ToString()), mapTitleStyle);
                GUILayout.Label($"Источников на местности: {selectedResource.SiteIds.Count}", mapLabelStyle);
                var remaining = selectedResource.SiteIds.Sum(id => production.TryGetSite(id, out var site) ? site.RemainingAmount : 0L);
                GUILayout.Label($"Доступно: {remaining}", mapLabelStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Обычный", resourcePriority == WorkPriority.Normal ? mapSelectedButtonStyle : mapButtonStyle)) resourcePriority = WorkPriority.Normal;
                if (GUILayout.Button("Высокий", resourcePriority == WorkPriority.High ? mapSelectedButtonStyle : mapButtonStyle)) resourcePriority = WorkPriority.High;
                if (GUILayout.Button("Сейчас", resourcePriority == WorkPriority.Immediate ? mapSelectedButtonStyle : mapButtonStyle)) resourcePriority = WorkPriority.Immediate;
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Собирать", mapButtonStyle)) IssueSelectedResourceWork();
                if (GUILayout.Button("Перейти к источникам поселения", mapButtonStyle)) EnterSelectedResourceLocation();
                GUILayout.EndArea();
            }
            if (!ShowDeveloperUi) return;
            GUILayout.BeginArea(new Rect(safe.xMin + 12, safe.yMin + 12, 390, Mathf.Min(860, safe.height - 24)), mapPanelStyle);
            GUILayout.Label(RussianTextCatalog.Format("debug.map.title", TimeSpan.FromTicks(currentTick())), mapTitleStyle);
            GUILayout.Label(RussianTextCatalog.Format("debug.map.mode", RussianTextCatalog.Enum(map.Mode), LocationName(map.ActiveLocationId)), mapLabelStyle);
            GUILayout.Label(RussianTextCatalog.Get("debug.map.locations"), mapTitleStyle);
            foreach (var location in map.Locations)
                if (GUILayout.Button(RussianTextCatalog.Format("debug.map.location", LocationName(location.Id), RussianTextCatalog.Enum(location.Type)),
                    selectedLocation == location.Id ? mapSelectedButtonStyle : mapButtonStyle)) selectedLocation = location.Id;
            GUILayout.Label(RussianTextCatalog.Get("debug.map.units"), mapTitleStyle);
            foreach (var unit in units.Units.Where(x => x.Representation == MilitaryUnitRepresentationMode.Aggregate))
            {
                var summary = units.GetRosterSummary(unit.Id);
                var travel = map.Travels.SingleOrDefault(x => x.UnitId == unit.Id);
                var detail = travel == null ? RussianTextCatalog.Format("debug.map.stationed", LocationName(unit.AbstractLocationId)) :
                    RussianTextCatalog.Format("debug.map.travelling", LocationName(travel.Origin), LocationName(travel.Destination),
                        TimeSpan.FromTicks(Math.Max(0, travel.ArrivalTick - currentTick())));
                if (GUILayout.Button(RussianTextCatalog.Format("debug.map.unit", unit.Id,
                    unit.OwnerSideId.Value == "player" ? RussianTextCatalog.Get("ui.side.player") : RussianTextCatalog.Get("ui.side.enemy"),
                    summary.CombatCapable, detail), selectedUnit == unit.Id ? mapSelectedButtonStyle : mapButtonStyle))
                    selectedUnit = unit.Id;
            }
            if (selectedLocation.HasValue) GUILayout.Label(RussianTextCatalog.Format("debug.map.selected_location", LocationName(selectedLocation.Value)), mapLabelStyle);
            if (selectedUnit.HasValue) GUILayout.Label(RussianTextCatalog.Format("debug.map.selected_unit", selectedUnit.Value), mapLabelStyle);
            GUI.enabled = selectedLocation.HasValue && selectedUnit.HasValue;
            if (GUILayout.Button(RussianTextCatalog.Get("debug.map.move"), mapButtonStyle))
                map.IssueTravelOrder(selectedUnit.Value, selectedLocation.Value, currentTick());
            if (!GUI.enabled) GUILayout.Label("Сначала выберите место и отряд.", mapDisabledReasonStyle);
            GUI.enabled = selectedLocation.HasValue;
            if (GUILayout.Button(RussianTextCatalog.Get("debug.map.enter"), mapButtonStyle))
            {
                var result = transitions.EnterLocation(selectedLocation.Value, currentTick());
                if (result.Changed) localEntered?.Invoke(result);
            }
            GUI.enabled = true;
            var activeBattle = battles.Battles.FirstOrDefault(x => x.Status == CombatEncounterStatus.Active &&
                x.Mode == BattleMode.Abstract);
            GUI.enabled = activeBattle != null;
            if (GUILayout.Button(RussianTextCatalog.Get("debug.map.enter_battle"), mapButtonStyle))
            {
                var result = transitions.EnterBattle(activeBattle.BattleId, currentTick());
                if (result.Changed) localEntered?.Invoke(result);
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void EnsureUiStyles()
        {
            if (mapPanelStyle != null) return;
            mapPanelTexture = SolidTexture(SerenityUiDesign.Surface);
            mapButtonTexture = SolidTexture(SerenityUiDesign.SurfaceSoft);
            mapSelectedTexture = SolidTexture(new Color(0.27f, 0.31f, 0.2f, 1f));
            mapPanelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = mapPanelTexture, textColor = SerenityUiDesign.Text },
                padding = new RectOffset(14, 14, 12, 12)
            };
            mapTitleStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = SerenityUiDesign.AccentBright },
                fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true,
                margin = new RectOffset(3, 3, 5, 7)
            };
            mapLabelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = SerenityUiDesign.Text }, wordWrap = true, fontSize = 13
            };
            mapLocationStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { background = mapButtonTexture, textColor = SerenityUiDesign.AccentBright },
                alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold,
                padding = new RectOffset(5, 5, 2, 2)
            };
            mapButtonStyle = new GUIStyle(GUI.skin.button)
            {
                normal = { background = mapButtonTexture, textColor = SerenityUiDesign.Text },
                hover = { background = mapSelectedTexture, textColor = Color.white },
                active = { background = mapSelectedTexture, textColor = Color.white },
                wordWrap = true, fixedHeight = 0f, padding = new RectOffset(9, 9, 7, 7),
                margin = new RectOffset(2, 2, 3, 3)
            };
            mapSelectedButtonStyle = new GUIStyle(mapButtonStyle)
            {
                normal = { background = mapSelectedTexture, textColor = SerenityUiDesign.AccentBright },
                fontStyle = FontStyle.Bold
            };
            mapDisabledReasonStyle = new GUIStyle(mapLabelStyle)
            {
                normal = { textColor = SerenityUiDesign.TextMuted }, fontSize = 11
            };
        }

        private static Texture2D SolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color); texture.Apply();
            return texture;
        }

        private void RefreshAtmosphere()
        {
            if (weather == null || atmosphereProfile == null || currentTick == null) return;
            var tick = currentTick();
            var bucket = tick / TimeSpan.TicksPerMinute;
            if (bucket == displayedAtmosphereBucket) return;
            displayedAtmosphereBucket = bucket;
            CurrentAtmosphere = atmosphereProfile.Evaluate(tick, weather.GetWeatherAtCalendarTick(tick));
            mapCamera.backgroundColor = Color.Lerp(CurrentAtmosphere.SkyColor, CurrentAtmosphere.FogColor, .24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = CurrentAtmosphere.FogColor;
            RenderSettings.fogDensity = CurrentAtmosphere.FogDensity * .56f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(CurrentAtmosphere.AmbientColor,
                new Color(.22f, .27f, .3f), CurrentAtmosphere.IsNight ? .46f : .08f);
            RenderSettings.ambientIntensity = Mathf.Max(CurrentAtmosphere.AmbientIntensity,
                CurrentAtmosphere.IsNight ? .8f : .5f);
            foreach (var candidate in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (candidate.type != LightType.Directional || candidate.gameObject.scene != gameObject.scene) continue;
                candidate.transform.rotation = CurrentAtmosphere.LightRotation;
                candidate.color = CurrentAtmosphere.LightColor;
                candidate.intensity = Mathf.Max(CurrentAtmosphere.LightIntensity,
                    CurrentAtmosphere.IsNight ? .55f : .5f);
                candidate.shadows = LightShadows.Soft;
                candidate.shadowStrength = CurrentAtmosphere.ShadowStrength;
            }
        }

        private void CreateLandRelief()
        {
            const int columns = 13;
            const int rows = 11;
            var vertices = new Vector3[columns * rows];
            var uv = new Vector2[vertices.Length];
            for (var z = 0; z < rows; z++)
            for (var x = 0; x < columns; x++)
            {
                var px = Mathf.Lerp(-37f, 37f, x / (columns - 1f));
                var pz = Mathf.Lerp(-28f, 28f, z / (rows - 1f));
                var edge = Mathf.Clamp01(1f - Mathf.Pow(px / 39f, 4f) - Mathf.Pow(pz / 30f, 4f));
                var relief = Mathf.Sin(x * 1.73f + z * .61f) * .22f + Mathf.Cos(z * 1.37f) * .17f;
                foreach (var location in map.Locations)
                {
                    var definition = atmosphereProfile.GetLocation(location.Id.Value);
                    var distance = Vector2.Distance(new Vector2(px, pz), new Vector2(location.MapX, location.MapY));
                    relief += Mathf.Max(0f, 1f - distance / 13f) * definition.Relief;
                }
                vertices[z * columns + x] = new Vector3(px, Mathf.Lerp(-.85f, Mathf.Max(.12f, relief), edge), pz);
                uv[z * columns + x] = new Vector2(x / (columns - 1f), z / (rows - 1f));
            }
            var triangles = new int[(columns - 1) * (rows - 1) * 6];
            var index = 0;
            for (var z = 0; z < rows - 1; z++)
            for (var x = 0; x < columns - 1; x++)
            {
                var a = z * columns + x;
                triangles[index++] = a; triangles[index++] = a + columns; triangles[index++] = a + 1;
                triangles[index++] = a + 1; triangles[index++] = a + columns; triangles[index++] = a + columns + 1;
            }
            var mesh = new Mesh { name = "P9.5 Natural Global Land Relief" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var land = new GameObject("P9.5 Natural Land Relief", typeof(MeshFilter), typeof(MeshRenderer));
            land.transform.SetParent(markerRoot, false);
            land.transform.position = new Vector3(0f, 100f, 0f);
            land.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { name = "P9.5 Natural Land Material", color = atmosphereProfile.MapLand };
            material.SetFloat("_Smoothness", .08f);
            land.GetComponent<MeshRenderer>().sharedMaterial = material;

            var river = new GameObject("P9.5 River", typeof(LineRenderer));
            river.transform.SetParent(markerRoot, false);
            var line = river.GetComponent<LineRenderer>();
            line.positionCount = 5;
            line.SetPositions(new[] { new Vector3(-34f,100.45f,23f), new Vector3(-17f,100.52f,13f),
                new Vector3(2f,100.58f,4f), new Vector3(18f,100.5f,-7f), new Vector3(35f,100.42f,-17f) });
            line.startWidth = 2.4f; line.endWidth = 3.1f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = atmosphereProfile.MapWater;
        }

        private static void AddLocationAffordance(Transform root, P95MapLocationDefinition definition)
        {
            switch (definition.Kind)
            {
                case P95MapLocationKind.Settlement:
                    for (var i = 0; i < 3; i++)
                        CreateProp(root, PrimitiveType.Cylinder, "Roundhouse " + (i + 1),
                            new Vector3((i - 1) * 1.25f, .5f, i % 2 == 0 ? .8f : -.55f),
                            new Vector3(.55f, .45f, .55f), new Color(.48f,.31f,.16f));
                    break;
                case P95MapLocationKind.Forest:
                    for (var i = 0; i < 5; i++)
                        CreateProp(root, PrimitiveType.Capsule, "Pine " + (i + 1),
                            new Vector3((i % 3 - 1) * .78f, .8f, (i / 3 - .5f) * 1.1f),
                            new Vector3(.34f,.95f,.34f), new Color(.12f,.29f,.14f));
                    break;
                case P95MapLocationKind.Ford:
                    CreateProp(root, PrimitiveType.Cube, "Ford Stones", new Vector3(0f,.15f,0f),
                        new Vector3(2.4f,.16f,.42f), new Color(.55f,.58f,.5f));
                    break;
                case P95MapLocationKind.Hills:
                    for (var i = 0; i < 3; i++)
                        CreateProp(root, PrimitiveType.Sphere, "Hill " + (i + 1),
                            new Vector3((i - 1) * .85f,.35f,i == 1 ? .45f : -.3f),
                            new Vector3(1.15f,.65f,1f), new Color(.39f,.4f,.34f));
                    break;
                case P95MapLocationKind.Field:
                    for (var i = 0; i < 4; i++)
                        CreateProp(root, PrimitiveType.Cube, "Field Strip " + (i + 1),
                            new Vector3((i - 1.5f) * .48f,.12f,0f), new Vector3(.3f,.12f,2.1f),
                            i % 2 == 0 ? new Color(.58f,.52f,.25f) : new Color(.42f,.44f,.2f));
                    break;
            }
        }

        private static void CreateProp(Transform root, PrimitiveType primitive, string name, Vector3 position,
            Vector3 scale, Color color)
        {
            var prop = GameObject.CreatePrimitive(primitive);
            prop.name = name;
            prop.transform.SetParent(root, false);
            prop.transform.localPosition = position;
            prop.transform.localScale = scale;
            SetColor(prop, color);
        }

        private void OnDestroy()
        {
            if (mapPanelTexture != null) Destroy(mapPanelTexture);
            if (mapButtonTexture != null) Destroy(mapButtonTexture);
            if (mapSelectedTexture != null) Destroy(mapSelectedTexture);
            if (ownsAtmosphereProfile && atmosphereProfile != null) Destroy(atmosphereProfile);
        }

        public void SelectResourceMarker(GlobalResourceMarker marker)
        {
            if (selectedResource == marker) return;
            if (selectedResource != null) selectedResource.SetSelected(false);
            selectedResource = marker;
            if (selectedResource != null) selectedResource.SetSelected(true);
        }

        public void EnterSelectedResourceLocation()
        {
            if (selectedResource == null) return;
            var result = transitions.EnterLocation(new WorldLocationId("starter_settlement"), currentTick());
            if (result.Changed) localEntered?.Invoke(result);
        }

        public WorkOrder IssueSelectedResourceWork()
        {
            if (selectedResource == null || selectedResource.SiteIds.Count == 0)
                throw new InvalidOperationException("Resource marker is not selected.");
            var siteId = selectedResource.SiteIds.First(id => production.TryGetSite(id, out _));
            var site = production.Sites.First(x => x.Id == siteId);
            return directWork.Issue(WorkJobType.Gather,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite, site.Id, site.Position), resourcePriority);
        }

        private void TrySelectResourceAt(Vector2 screenPoint)
        {
            var ray = mapCamera.ScreenPointToRay(screenPoint);
            if (Physics.Raycast(ray, out var hit, 500f))
                SelectResourceMarker(hit.collider.GetComponentInParent<GlobalResourceMarker>());
        }

        private void BuildResourceMarkers()
        {
            resourceMarkers.Clear();
            var colors = new Dictionary<string, Color>(StringComparer.Ordinal)
            {
                ["berries"] = new Color(0.62f,0.16f,0.30f), ["plant_fiber"] = new Color(0.35f,0.62f,0.17f),
                ["wood_log"] = new Color(0.34f,0.19f,0.08f), ["stone"] = new Color(0.42f,0.44f,0.42f),
                ["iron_ore"] = new Color(0.28f,0.33f,0.36f), ["gold_ore"] = new Color(0.75f,0.48f,0.08f)
            };
            var groups = production.Sites.Where(x => x.Kind == ProductionSiteKind.NaturalSource)
                .GroupBy(x => production.Recipes.Get(x.RecipeId).Outputs[0].ResourceId).OrderBy(x => x.Key).ToArray();
            var starter = map.GetLocation(new WorldLocationId("starter_settlement"));
            for (var i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                var color = colors.TryGetValue(group.Key.ToString(), out var found) ? found : Color.white;
                var markerObject = CreateNaturalMarker("Resource " + group.Key, MarkerShape.Resource, color);
                markerObject.transform.SetParent(markerRoot, false);
                var angle = i * Mathf.PI * 2f / groups.Length;
                markerObject.transform.position = Position(starter, 102.4f) + new Vector3(Mathf.Cos(angle) * 5.2f, 0f, Mathf.Sin(angle) * 5.2f);
                var marker = markerObject.AddComponent<GlobalResourceMarker>();
                marker.Configure(group.Key, group.Select(x => x.Id), color);
                resourceMarkers.Add(marker);
            }
        }

        private enum MarkerShape { Location, Settlement, Unit, Battle, Resource }

        private static GameObject CreateNaturalMarker(string name, MarkerShape shape, Color color)
        {
            var value = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            var mesh = BuildMarkerMesh(shape);
            value.GetComponent<MeshFilter>().sharedMesh = mesh;
            value.GetComponent<MeshCollider>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Smoothness", shape == MarkerShape.Resource ? 0.48f : 0.22f);
            value.GetComponent<MeshRenderer>().sharedMaterial = material;
            SetColor(value, color);
            return value;
        }

        private static Mesh BuildMarkerMesh(MarkerShape shape)
        {
            var height = shape == MarkerShape.Battle ? 4.2f : shape == MarkerShape.Unit ? 3.2f :
                shape == MarkerShape.Settlement ? 2.5f : 2f;
            var radius = shape == MarkerShape.Settlement ? 2f : shape == MarkerShape.Resource ? 1.25f : 1f;
            var vertices = new[]
            {
                new Vector3(0,height,0), new Vector3(-radius,0,-radius*.7f), new Vector3(radius,0,-radius*.7f),
                new Vector3(radius*.8f,0,radius), new Vector3(-radius*.8f,0,radius), new Vector3(0,-.15f,0)
            };
            var triangles = new[] { 0,1,2,0,2,3,0,3,4,0,4,1,5,2,1,5,3,2,5,4,3,5,1,4 };
            var mesh = new Mesh { name = "P82 " + shape + " Marker" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Position(WorldLocationState location, float y) =>
            new Vector3(location.MapX, y, location.MapY);

        private static string LocationName(WorldLocationId id) =>
            RussianTextCatalog.TryGet("content.location." + id.Value, out var value) ? value : RussianTextCatalog.Get("ui.location.unknown");

        private static string LocationName(string id) => string.IsNullOrWhiteSpace(id)
            ? RussianTextCatalog.Get("common.none") : LocationName(new WorldLocationId(id));

        private static void SetColor(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        private ulong DynamicHash()
        {
            var hash = unchecked((ulong)map.Mode) ^ unchecked((ulong)map.ActiveLocationId.GetHashCode());
            foreach (var unit in units.Units)
                hash ^= unchecked((ulong)unit.Id.GetHashCode()) ^
                    unchecked((ulong)unit.OwnerSideId.GetHashCode() << 1) ^
                    unchecked((ulong)unit.Representation << 8) ^
                    unchecked((ulong)unit.Status << 16) ^
                    unchecked((ulong)(unit.AbstractLocationId ?? string.Empty).GetHashCode() << 2) ^
                    unchecked((ulong)(unit.ActiveEncounterId?.GetHashCode() ?? 0) << 3);
            foreach (var travel in map.Travels)
                hash ^= unchecked((ulong)travel.UnitId.GetHashCode()) ^
                    unchecked((ulong)travel.Origin.GetHashCode() << 1) ^
                    unchecked((ulong)travel.Destination.GetHashCode() << 2) ^
                    unchecked((ulong)travel.ArrivalTick);
            foreach (var battle in battles.Battles)
                hash ^= unchecked((ulong)battle.BattleId.GetHashCode()) ^
                    unchecked((ulong)battle.Status << 8) ^ unchecked((ulong)battle.Mode << 16);
            return hash;
        }
    }
}
