using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace Game.Presentation.UI
{
    /// <summary>
    /// Resolves Cyrillic-complete operating-system fonts without redistributing unreviewed font files.
    /// Segoe UI/Georgia are preferred on Windows; broad Unicode fallbacks keep other editor hosts usable.
    /// </summary>
    public static class SerenityUiTypography
    {
        public static readonly string[] InterfaceFamilies =
        {
            "Segoe UI", "Arial", "Noto Sans", "DejaVu Sans", "Liberation Sans"
        };

        public static readonly string[] DisplayFamilies =
        {
            "Georgia", "Times New Roman", "Noto Serif", "DejaVu Serif", "Liberation Serif"
        };

        private static FontAsset interfaceFont;
        private static FontAsset displayFont;

        public static string InterfaceFamily { get; private set; } = string.Empty;
        public static string DisplayFamily { get; private set; } = string.Empty;

        public static void ApplyRoot(VisualElement root)
        {
            if (root == null) return;
            if (interfaceFont == null)
            {
                interfaceFont = Resolve(InterfaceFamilies, out var family);
                if (!string.IsNullOrEmpty(family)) InterfaceFamily = family;
            }
            if (interfaceFont != null) root.style.unityFontDefinition = FontDefinition.FromSDFFont(interfaceFont);
        }

        public static void ApplyDisplay(TextElement element)
        {
            if (element == null) return;
            if (displayFont == null)
            {
                displayFont = Resolve(DisplayFamilies, out var family);
                if (!string.IsNullOrEmpty(family)) DisplayFamily = family;
            }
            if (displayFont != null) element.style.unityFontDefinition = FontDefinition.FromSDFFont(displayFont);
        }

        public static bool SupportsRequiredCyrillic(FontAsset font)
        {
            if (font == null) return false;
            return "АБВГДабвгдЁёЙйЯя".All(value => font.HasCharacter(value));
        }

        private static FontAsset Resolve(string[] preferredFamilies, out string selectedFamily)
        {
            selectedFamily = string.Empty;
            var installed = Font.GetOSInstalledFontNames();
            foreach (var preferred in preferredFamilies)
            {
                var installedName = installed.FirstOrDefault(value =>
                    string.Equals(value, preferred, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(installedName)) continue;
                var candidate = FontAsset.CreateFontAsset(installedName, "Regular", 90);
                if (!SupportsRequiredCyrillic(candidate))
                {
                    if (candidate != null) UnityEngine.Object.Destroy(candidate);
                    continue;
                }
                candidate.name = "Serenity Runtime " + installedName;
                selectedFamily = installedName;
                return candidate;
            }
            return null;
        }
    }
}
