using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Shared presentation-only visual language for the shipped UI. It intentionally contains no
    /// gameplay state and can be replaced without migrating a save.
    /// </summary>
    public static class SerenityUiDesign
    {
        public static readonly Color Canvas = Hex("111710");
        public static readonly Color Surface = Hex("1B2419", 0.97f);
        public static readonly Color SurfaceRaised = Hex("253020", 0.98f);
        public static readonly Color SurfaceSoft = Hex("303A28", 0.96f);
        public static readonly Color Accent = Hex("C99B4A");
        public static readonly Color AccentBright = Hex("E1BC72");
        public static readonly Color Text = Hex("F3EAD8");
        public static readonly Color TextMuted = Hex("B9B5A5");
        public static readonly Color Success = Hex("82AE78");
        public static readonly Color Warning = Hex("E2B85F");
        public static readonly Color Danger = Hex("D77B68");

        public const string RootClass = "serenity-root";
        public const string SurfaceClass = "serenity-surface";
        public const string RaisedSurfaceClass = "serenity-surface-raised";
        public const string CardClass = "serenity-card";
        public const string ButtonClass = "serenity-button";
        public const string PrimaryButtonClass = "serenity-button-primary";
        public const string NavigationButtonClass = "serenity-nav-button";
        public const string SelectedClass = "is-selected";
        public const string TitleClass = "serenity-title";
        public const string SectionClass = "serenity-section-title";
        public const string BodyClass = "serenity-body";
        public const string MutedClass = "serenity-muted";
        public const string FieldClass = "serenity-field";
        public const string RowClass = "serenity-row";
        public const string ChipClass = "serenity-chip";
        public const string ModalClass = "serenity-modal";

        public static void ApplyRoot(VisualElement root, string variant)
        {
            if (root == null) return;
            root.AddToClassList(RootClass);
            if (!string.IsNullOrWhiteSpace(variant)) root.AddToClassList(variant);
        }

        public static void ApplySafeArea(VisualElement root, Rect safeArea, int screenWidth, int screenHeight)
        {
            if (root == null || screenWidth <= 0 || screenHeight <= 0) return;
            var width = root.resolvedStyle.width;
            var height = root.resolvedStyle.height;
            var xScale = float.IsNaN(width) || width <= 0f ? 1f : width / screenWidth;
            var yScale = float.IsNaN(height) || height <= 0f ? 1f : height / screenHeight;
            root.style.paddingLeft = Mathf.Max(0f, safeArea.xMin * xScale);
            root.style.paddingRight = Mathf.Max(0f, (screenWidth - safeArea.xMax) * xScale);
            root.style.paddingBottom = Mathf.Max(0f, safeArea.yMin * yScale);
            root.style.paddingTop = Mathf.Max(0f, (screenHeight - safeArea.yMax) * yScale);
        }

        public static void StyleSurface(VisualElement element, bool raised = false)
        {
            if (element == null) return;
            element.AddToClassList(SurfaceClass);
            if (raised) element.AddToClassList(RaisedSurfaceClass);
        }

        public static void StyleCard(VisualElement element)
        {
            if (element == null) return;
            element.AddToClassList(CardClass);
        }

        public static void StyleButton(Button button, bool primary = false, bool navigation = false)
        {
            if (button == null) return;
            button.AddToClassList(ButtonClass);
            if (primary) button.AddToClassList(PrimaryButtonClass);
            if (navigation) button.AddToClassList(NavigationButtonClass);
            button.focusable = true;
        }

        public static void SetSelected(VisualElement element, bool selected)
        {
            if (element == null) return;
            element.EnableInClassList(SelectedClass, selected);
        }

        public static void StyleField(VisualElement field)
        {
            if (field == null) return;
            field.AddToClassList(FieldClass);
        }

        public static VisualElement Card(string name = null)
        {
            var card = new VisualElement { name = name ?? string.Empty };
            StyleCard(card);
            return card;
        }

        public static float ContrastRatio(Color foreground, Color background)
        {
            var light = RelativeLuminance(foreground);
            var dark = RelativeLuminance(background);
            if (dark > light) (light, dark) = (dark, light);
            return (light + 0.05f) / (dark + 0.05f);
        }

        private static float RelativeLuminance(Color value) =>
            0.2126f * Linear(value.r) + 0.7152f * Linear(value.g) + 0.0722f * Linear(value.b);

        private static float Linear(float channel) => channel <= 0.03928f
            ? channel / 12.92f
            : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);

        private static Color Hex(string value, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString("#" + value, out var color))
                throw new ArgumentException("Invalid UI colour.", nameof(value));
            color.a = alpha;
            return color;
        }
    }
}
