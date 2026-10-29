using UnityEngine;

namespace Game.Infrastructure
{
    [DisallowMultipleComponent]
    public sealed class WorldMapWarmupProgressOverlay : MonoBehaviour
    {
        private TiledTerrainWorld world;
        private GUIStyle labelStyle;

        public void Initialize(TiledTerrainWorld value) => world = value;

        private void OnGUI()
        {
            if (world == null || !world.BackgroundWarmupProgressVisible) return;
            GUI.depth = -9000;
            var width = Mathf.Min(430f, Screen.width - 40f);
            const float height = 34f;
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 22f, width, height);
            var previous = GUI.color;
            GUI.color = new Color(0.035f, 0.05f, 0.06f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            var inset = new Rect(rect.x + 4f, rect.y + 4f,
                (rect.width - 8f) * world.BackgroundWarmupPercent / 100f, rect.height - 8f);
            GUI.color = new Color(0.24f, 0.62f, 0.42f, 0.95f);
            GUI.DrawTexture(inset, Texture2D.whiteTexture);
            GUI.color = Color.white;
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(rect, "Подготовка карты: " + world.BackgroundWarmupPercent + "%", labelStyle);
            GUI.color = previous;
        }
    }
}
