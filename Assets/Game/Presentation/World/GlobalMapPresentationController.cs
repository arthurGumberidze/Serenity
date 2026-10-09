using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Simulation.Combat;
using Game.Simulation.Military;
using Game.Simulation.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.World
{
    /// <summary>Disposable schematic projection of U25 canonical map state.</summary>
    public sealed class GlobalMapPresentationController : MonoBehaviour
    {
        [SerializeField] private Camera mapCamera;
        [SerializeField] private Transform markerRoot;
        private GlobalMapService map;
        private MilitaryUnitService units;
        private BattleResolutionService battles;
        private GameModeTransitionService transitions;
        private Func<long> currentTick;
        private Action<GameModeTransitionResult> localEntered;
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
        private ulong lastDynamicHash;
        private float nextRefreshTime;

        public Camera MapCamera => mapCamera;
        public int LocationMarkerCount => locationMarkers.Count;
        public bool ShowDeveloperUi { get; set; }

        public void Configure(Camera camera, Transform root)
        {
            mapCamera = camera;
            markerRoot = root;
        }

        public void Initialize(GlobalMapService globalMap, MilitaryUnitService militaryUnits,
            BattleResolutionService battleResolution, GameModeTransitionService modeTransitions,
            Func<long> tickProvider, Action<GameModeTransitionResult> onLocalEntered)
        {
            map = globalMap ?? throw new ArgumentNullException(nameof(globalMap));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            battles = battleResolution ?? throw new ArgumentNullException(nameof(battleResolution));
            transitions = modeTransitions ?? throw new ArgumentNullException(nameof(modeTransitions));
            currentTick = tickProvider ?? throw new ArgumentNullException(nameof(tickProvider));
            localEntered = onLocalEntered;
            BuildStaticMap();
            RefreshDynamicMarkers();
            lastDynamicHash = DynamicHash();
        }

        private void Update()
        {
            if (map == null || mapCamera == null) return;
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
            var height = Mathf.Clamp(mapCamera.transform.position.y - zoom * 0.02f, 118f, 175f);
            mapCamera.transform.position = new Vector3(cameraTarget.x, height, cameraTarget.z);
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
            foreach (var route in map.Routes)
            {
                var from = map.GetLocation(route.From);
                var to = map.GetLocation(route.To);
                var lineObject = new GameObject("Route " + route.From + " - " + route.To);
                lineObject.transform.SetParent(markerRoot, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.SetPositions(new[] { Position(from, 100.1f), Position(to, 100.1f) });
                line.startWidth = line.endWidth = 0.25f;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = line.endColor = new Color(0.45f, 0.52f, 0.58f);
            }
            foreach (var location in map.Locations)
            {
                var marker = GameObject.CreatePrimitive(location.Type == WorldLocationType.Settlement
                    ? PrimitiveType.Cube : PrimitiveType.Sphere);
                marker.name = "Location " + location.Id;
                marker.transform.SetParent(markerRoot, false);
                marker.transform.position = Position(location, 101f);
                marker.transform.localScale = location.Type == WorldLocationType.Settlement
                    ? new Vector3(3f, 1f, 3f) : Vector3.one * 1.8f;
                SetColor(marker, location.Id == map.ActiveLocationId ? Color.cyan : Color.gray);
                locationMarkers.Add(location.Id, marker);
            }
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
                var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                marker.name = "Unit " + unit.Id;
                marker.transform.SetParent(markerRoot, false);
                marker.transform.position = position + new Vector3((unit.Id.GetHashCode() & 3) * 0.45f, 0f, 0f);
                marker.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
                SetColor(marker, unit.OwnerSideId.Value == "player" ? Color.green : Color.red);
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
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    marker.name = "Battle " + battle.BattleId;
                    marker.transform.SetParent(markerRoot, false);
                    marker.transform.position = Position(location, 104f);
                    marker.transform.localScale = new Vector3(1.2f, 0.3f, 1.2f);
                    SetColor(marker, new Color(1f, 0.35f, 0.05f));
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
            if (map == null || !ShowDeveloperUi) return;
            GUILayout.BeginArea(new Rect(12, 12, 390, 860), GUI.skin.box);
            GUILayout.Label("GLOBAL MAP  |  " + TimeSpan.FromTicks(currentTick()));
            GUILayout.Label("Mode: " + map.Mode + "  Active: " + map.ActiveLocationId);
            GUILayout.Label("Locations");
            foreach (var location in map.Locations)
                if (GUILayout.Button(location.Name + "  [" + location.Type + "]")) selectedLocation = location.Id;
            GUILayout.Label("Aggregate military units");
            foreach (var unit in units.Units.Where(x => x.Representation == MilitaryUnitRepresentationMode.Aggregate))
            {
                var summary = units.GetRosterSummary(unit.Id);
                var travel = map.Travels.SingleOrDefault(x => x.UnitId == unit.Id);
                var detail = travel == null ? unit.AbstractLocationId : travel.Origin + " -> " + travel.Destination +
                    " ETA " + TimeSpan.FromTicks(Math.Max(0, travel.ArrivalTick - currentTick()));
                if (GUILayout.Button(unit.Id + " " + unit.OwnerSideId + " combat " + summary.CombatCapable + "  " + detail))
                    selectedUnit = unit.Id;
            }
            if (selectedLocation.HasValue) GUILayout.Label("Selected location: " + selectedLocation.Value);
            if (selectedUnit.HasValue) GUILayout.Label("Selected unit: " + selectedUnit.Value);
            GUI.enabled = selectedLocation.HasValue && selectedUnit.HasValue;
            if (GUILayout.Button("Move selected unit"))
                map.IssueTravelOrder(selectedUnit.Value, selectedLocation.Value, currentTick());
            GUI.enabled = selectedLocation.HasValue;
            if (GUILayout.Button("Enter selected settlement"))
            {
                var result = transitions.EnterLocation(selectedLocation.Value, currentTick());
                if (result.Changed) localEntered?.Invoke(result);
            }
            GUI.enabled = true;
            var activeBattle = battles.Battles.FirstOrDefault(x => x.Status == CombatEncounterStatus.Active &&
                x.Mode == BattleMode.Abstract);
            GUI.enabled = activeBattle != null;
            if (GUILayout.Button("Enter active battle"))
            {
                var result = transitions.EnterBattle(activeBattle.BattleId, currentTick());
                if (result.Changed) localEntered?.Invoke(result);
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private static Vector3 Position(WorldLocationState location, float y) =>
            new Vector3(location.MapX, y, location.MapY);

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
