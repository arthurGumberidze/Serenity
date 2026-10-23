using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Presentation-only access to the project-owned 6x4 HUD atlas. Runtime sprites are disposable and
    /// deliberately never enter save data or simulation definitions.
    /// </summary>
    public static class SerenityIconLibrary
    {
        private const int Columns = 6;
        private const int Rows = 4;
        private static readonly Dictionary<string, int> Indices = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["population"] = 0, ["wood_log"] = 1, ["stone"] = 2, ["water"] = 3,
            ["meat"] = 4, ["fish"] = 5,
            ["build"] = 6, ["settlement"] = 7, ["research"] = 8, ["work"] = 9,
            ["dynasty"] = 10, ["domestic"] = 11,
            ["military"] = 12, ["save"] = 13, ["world"] = 14, ["events"] = 15,
            ["settings"] = 16, ["alerts"] = 17,
            ["gather"] = 18, ["production"] = 19, ["housing"] = 20, ["food"] = 21,
            ["storage"] = 22, ["defense"] = 23
        };

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static Texture2D atlas;

        public static bool IsReady => LoadAtlas() != null;

        public static Sprite Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !Indices.TryGetValue(key, out var index)) return null;
            if (Sprites.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            var texture = LoadAtlas();
            if (texture == null) return null;
            var cellWidth = texture.width / Columns;
            var cellHeight = texture.height / Rows;
            var column = index % Columns;
            var rowFromTop = index / Columns;
            var rect = new Rect(column * cellWidth, (Rows - rowFromTop - 1) * cellHeight, cellWidth, cellHeight);
            sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Serenity UI " + key;
            Sprites[key] = sprite;
            return sprite;
        }

        public static void Apply(Button button, string key, string tooltip = null)
        {
            if (button == null) return;
            var sprite = Get(key);
            if (sprite != null) button.style.backgroundImage = new StyleBackground(sprite);
            button.AddToClassList("serenity-icon-button");
            if (!string.IsNullOrWhiteSpace(tooltip)) button.tooltip = tooltip;
        }

        public static Image Image(string key, string tooltip = null)
        {
            var image = new Image { name = "icon-" + key, sprite = Get(key), scaleMode = ScaleMode.ScaleToFit };
            image.AddToClassList("serenity-icon");
            if (!string.IsNullOrWhiteSpace(tooltip)) image.tooltip = tooltip;
            image.pickingMode = PickingMode.Ignore;
            return image;
        }

        private static Texture2D LoadAtlas()
        {
            if (atlas == null) atlas = UnityEngine.Resources.Load<Texture2D>("UI/SerenityIconAtlas");
            return atlas;
        }
    }
}
