using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Presentation.CameraControl;
using Game.Presentation.Input;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Infrastructure
{
    [Serializable]
    public sealed class UserSettingsData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public float masterVolume = 0.8f;
        public int resolutionWidth = 1600;
        public int resolutionHeight = 900;
        public int fullScreenMode = (int)FullScreenMode.Windowed;
        public int qualityLevel = 1;
        public float uiScale = 1f;
        public float cameraSpeed = 1f;
        public string cameraUp = "w";
        public string cameraDown = "s";
        public string cameraLeft = "a";
        public string cameraRight = "d";
        public string pause = "space";
        public string build = "b";
        public string controlMode = "t";

        public UserSettingsData Clone() => JsonUtility.FromJson<UserSettingsData>(JsonUtility.ToJson(this));

        public void Validate()
        {
            if (version != CurrentVersion) throw new NotSupportedException("Unsupported user settings version.");
            if (resolutionWidth < 1024 || resolutionWidth > 7680 || resolutionHeight < 720 || resolutionHeight > 4320)
                throw new InvalidDataException("Invalid display resolution.");
            if (!Enum.IsDefined(typeof(FullScreenMode), fullScreenMode))
                throw new InvalidDataException("Invalid window mode.");
            if (qualityLevel < 0 || qualityLevel > 32) throw new InvalidDataException("Invalid quality level.");
            if (masterVolume < 0f || masterVolume > 1f || uiScale < 1f || uiScale > 1.5f ||
                cameraSpeed < 0.5f || cameraSpeed > 2f)
                throw new InvalidDataException("Invalid settings range.");

            var keys = PrimaryKeys().ToArray();
            if (keys.Any(string.IsNullOrWhiteSpace) || keys.Any(key => !IsSupportedKey(key)))
                throw new InvalidDataException("Invalid key binding.");
            if (keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
                throw new InvalidDataException("Primary key bindings must be unique.");
        }

        public IEnumerable<string> PrimaryKeys()
        {
            yield return cameraUp;
            yield return cameraDown;
            yield return cameraLeft;
            yield return cameraRight;
            yield return pause;
            yield return build;
            yield return controlMode;
        }

        public static bool IsSupportedKey(string key) => UserSettingsRuntime.SupportedKeyNames.Contains(
            key ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    public sealed class UserSettingsLoadResult
    {
        public UserSettingsLoadResult(UserSettingsData settings, bool wasCorrupt)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            WasCorrupt = wasCorrupt;
        }

        public UserSettingsData Settings { get; }
        public bool WasCorrupt { get; }
    }

    public sealed class UserSettingsStore
    {
        private readonly string path;

        public UserSettingsStore(string settingsPath)
        {
            path = System.IO.Path.GetFullPath(settingsPath ?? throw new ArgumentNullException(nameof(settingsPath)));
        }

        public string Path => path;

        public UserSettingsLoadResult Load()
        {
            if (!File.Exists(path)) return new UserSettingsLoadResult(new UserSettingsData(), false);
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var settings = JsonUtility.FromJson<UserSettingsData>(json);
                if (settings == null) throw new InvalidDataException("Settings payload is empty.");
                settings.Validate();
                return new UserSettingsLoadResult(settings, false);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                              exception is ArgumentException || exception is InvalidDataException ||
                                              exception is NotSupportedException)
            {
                return new UserSettingsLoadResult(new UserSettingsData(), true);
            }
        }

        public void Save(UserSettingsData settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            var directory = System.IO.Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Settings path has no directory.");
            Directory.CreateDirectory(directory);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(settings, true), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static UserSettingsStore CreateDefault()
        {
            var configured = Environment.GetEnvironmentVariable("SERENITY_SETTINGS_PATH");
            var settingsPath = string.IsNullOrWhiteSpace(configured)
                ? System.IO.Path.Combine(Application.persistentDataPath, "Serenity", "Settings", "user-settings.json")
                : configured;
            return new UserSettingsStore(settingsPath);
        }
    }

    public static class UserSettingsRuntime
    {
        public static readonly string[] SupportedKeyNames =
        {
            "w", "a", "s", "d", "q", "e", "r", "t", "f", "g", "z", "x", "c", "v", "b", "n", "m",
            "upArrow", "downArrow", "leftArrow", "rightArrow", "space", "enter", "leftShift", "leftCtrl"
        };

        public static void ApplySystem(UserSettingsData settings, bool applyDisplay = true)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            AudioListener.volume = settings.masterVolume;
            var maxQuality = Math.Max(0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(Mathf.Clamp(settings.qualityLevel, 0, maxQuality), true);
            if (applyDisplay && !Application.isBatchMode)
                Screen.SetResolution(settings.resolutionWidth, settings.resolutionHeight,
                    (FullScreenMode)settings.fullScreenMode);
        }

        public static void ApplyGameplay(UserSettingsData settings, LocalGameplayInputSource input,
            RtsCameraController camera, GameplayUiController ui)
        {
            ApplySystem(settings);
            if (input != null && input.Actions != null) ApplyBindings(settings, input.Actions);
            if (camera != null)
            {
                camera.Settings.MoveSpeed = 18f * settings.cameraSpeed;
                camera.Settings.EdgeScrollSpeed = 18f * settings.cameraSpeed;
                camera.Settings.DragPanSpeed = 0.8f * settings.cameraSpeed;
                camera.Settings.Validate();
            }
            ui?.SetUiScale(settings.uiScale);
        }

        public static void ApplyBindings(UserSettingsData settings, InputActionAsset actions)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            settings.Validate();
            actions.RemoveAllBindingOverrides();
            OverrideCompositePart(actions, "Camera", "Move", "up", settings.cameraUp);
            OverrideCompositePart(actions, "Camera", "Move", "down", settings.cameraDown);
            OverrideCompositePart(actions, "Camera", "Move", "left", settings.cameraLeft);
            OverrideCompositePart(actions, "Camera", "Move", "right", settings.cameraRight);
            OverrideFirstKeyboardBinding(actions, "Time", "TogglePause", settings.pause);
            OverrideFirstKeyboardBinding(actions, "Building", "ToggleMode", settings.build);
            OverrideFirstKeyboardBinding(actions, "Mode", "ToggleControl", settings.controlMode);
        }

        private static void OverrideCompositePart(InputActionAsset asset, string map, string actionName,
            string partName, string key)
        {
            var action = asset.FindActionMap(map, true).FindAction(actionName, true);
            for (var index = 0; index < action.bindings.Count; index++)
            {
                var binding = action.bindings[index];
                if (binding.isPartOfComposite && string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase) &&
                    binding.path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase))
                {
                    action.ApplyBindingOverride(index, "<Keyboard>/" + key);
                    return;
                }
            }
            throw new InvalidOperationException("Required composite key binding was not found.");
        }

        private static void OverrideFirstKeyboardBinding(InputActionAsset asset, string map, string actionName,
            string key)
        {
            var action = asset.FindActionMap(map, true).FindAction(actionName, true);
            for (var index = 0; index < action.bindings.Count; index++)
            {
                if (!action.bindings[index].path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase)) continue;
                action.ApplyBindingOverride(index, "<Keyboard>/" + key);
                return;
            }
            throw new InvalidOperationException("Required key binding was not found.");
        }
    }
}
