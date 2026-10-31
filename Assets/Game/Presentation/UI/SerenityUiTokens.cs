using System;
using UnityEngine;

namespace Game.Presentation.UI
{
    /// <summary>
    /// UI02 presentation tokens. They are immutable visual definitions and never contain game state.
    /// Keep the values aligned with SerenityRuntimeTheme.tss; tests guard the contrast floor.
    /// </summary>
    public static class SerenityUiTokens
    {
        public static readonly Color Canvas = Hex("0D0C0A");
        public static readonly Color Surface = Hex("171511", 0.96f);
        public static readonly Color SurfaceRaised = Hex("211E18", 0.98f);
        public static readonly Color SurfaceSoft = Hex("2A261E", 0.96f);
        public static readonly Color SurfaceInteractive = Hex("302B22", 0.98f);
        public static readonly Color Gold = Hex("B89A5B");
        public static readonly Color GoldBright = Hex("D2B875");
        public static readonly Color GoldMuted = Hex("786641");
        public static readonly Color Bone = Hex("EEE7D8");
        public static readonly Color BoneMuted = Hex("B8AE9B");
        public static readonly Color InkOnGold = Hex("17130D");
        public static readonly Color Success = Hex("718A72");
        public static readonly Color Warning = Hex("A78055");
        public static readonly Color Danger = Hex("A45F50");
        public static readonly Color Information = Hex("667985");

        public const float CornerSmall = 4f;
        public const float CornerMedium = 7f;
        public const float CornerLarge = 10f;
        public const float Space1 = 4f;
        public const float Space2 = 8f;
        public const float Space3 = 12f;
        public const float Space4 = 16f;
        public const float MinimumTarget = 40f;
        public const float BodySize = 14f;
        public const float CaptionSize = 12f;
        public const float DisplaySize = 24f;

        private static Color Hex(string value, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString("#" + value, out var color))
                throw new ArgumentException("Invalid UI colour token.", nameof(value));
            color.a = alpha;
            return color;
        }
    }
}
