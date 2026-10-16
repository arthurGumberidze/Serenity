using System;
using Game.Domain.World;
using Game.Presentation.Environment;
using Game.Simulation.World;
using UnityEngine;

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
        private GUIStyle labelStyle;
        public bool IsActive { get; private set; }

        public void Initialize(Camera camera, P102ConnectedWorldPresentation presentation, ConnectedWorldService service)
        {
            worldCamera = camera ?? throw new ArgumentNullException(nameof(camera));
            world = presentation ?? throw new ArgumentNullException(nameof(presentation));
            discovery = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void Enter()
        {
            if (IsActive) return;
            savedPosition = worldCamera.transform.position;
            savedRotation = worldCamera.transform.rotation;
            savedOrthographic = worldCamera.orthographic;
            savedSize = worldCamera.orthographicSize;
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 272f;
            worldCamera.transform.SetPositionAndRotation(new Vector3(0f, 420f, 0f), Quaternion.Euler(90f, 0f, 0f));
            IsActive = true;
        }

        public void Exit()
        {
            if (!IsActive) return;
            worldCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            worldCamera.orthographic = savedOrthographic;
            worldCamera.orthographicSize = savedSize;
            IsActive = false;
        }

        private void OnGUI()
        {
            if (!IsActive || worldCamera == null || world == null) return;
            labelStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 13,
                normal = { textColor = Color.white } };
            var cellPixelsX = Screen.width / 32f;
            var cellPixelsY = Screen.height / 32f;
            var fogColor = new Color(0.015f, 0.025f, 0.04f, 0.84f);
            var previous = GUI.color;
            GUI.color = fogColor;
            for (var x = 0; x < 32; x++) for (var z = 0; z < 32; z++)
            {
                var wx = ConnectedWorldContract.OriginMeters + (x + 0.5f) * ConnectedWorldContract.DiscoveryCellMeters;
                var wz = ConnectedWorldContract.OriginMeters + (z + 0.5f) * ConnectedWorldContract.DiscoveryCellMeters;
                if (!discovery.IsRevealed(wx, wz)) GUI.DrawTexture(new Rect(x * cellPixelsX,
                    Screen.height - (z + 1) * cellPixelsY, cellPixelsX + 1f, cellPixelsY + 1f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
            foreach (var district in world.Districts)
            {
                if (!discovery.IsRevealed(district.Anchor.position.x, district.Anchor.position.z)) continue;
                var point = worldCamera.WorldToScreenPoint(district.Anchor.position);
                if (point.z <= 0f) continue;
                GUI.Box(new Rect(point.x - 75f, Screen.height - point.y - 18f, 150f, 36f),
                    district.DisplayName + "\n" + district.Description, labelStyle);
            }
            GUI.Box(new Rect(12f, 12f, 330f, 48f), "Стратегический обзор — та же физическая местность\nТуман раскрывается присутствием и разведкой", labelStyle);
        }
    }
}
