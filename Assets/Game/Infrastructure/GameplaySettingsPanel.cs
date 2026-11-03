using System;
using System.Collections.Generic;
using System.Linq;
using Game.Presentation.CameraControl;
using Game.Presentation.Input;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Infrastructure
{
    internal static class GameplaySettingsPanel
    {
        public static void Draw(VisualElement parent, Action close, LocalGameplayInputSource input,
            RtsCameraController camera, GameplayUiController ui)
        {
            var store = UserSettingsStore.CreateDefault();
            var draft = store.Load().Settings.Clone();
            parent.Add(new Label("Настройки"));
            SliderField("Громкость", "settings-volume", 0, 1, draft.masterVolume, v => draft.masterVolume = v);
            SliderField("Масштаб интерфейса", "settings-ui-scale", 1, 1.5f, draft.uiScale, v => draft.uiScale = v);
            SliderField("Скорость камеры", "settings-camera-speed", .5f, 2, draft.cameraSpeed, v => draft.cameraSpeed = v);
            var resolutions = new List<string> { "1280 × 720", "1600 × 900", "1920 × 1080", "2560 × 1440" };
            var current = draft.resolutionWidth + " × " + draft.resolutionHeight;
            if (!resolutions.Contains(current)) resolutions.Add(current);
            var resolution = new DropdownField("Разрешение", resolutions, current) { name = "settings-resolution" };
            SerenityUiDesign.StyleField(resolution);
            resolution.RegisterValueChangedCallback(evt =>
            {
                var parts = evt.newValue.Split('×');
                draft.resolutionWidth = int.Parse(parts[0].Trim()); draft.resolutionHeight = int.Parse(parts[1].Trim());
            });
            parent.Add(resolution);
            var fullscreen = new Toggle("Полный экран") { name = "settings-fullscreen",
                value = (FullScreenMode)draft.fullScreenMode != FullScreenMode.Windowed };
            SerenityUiDesign.StyleField(fullscreen);
            fullscreen.RegisterValueChangedCallback(evt => draft.fullScreenMode =
                (int)(evt.newValue ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed));
            parent.Add(fullscreen);
            var qualities = QualitySettings.names.Select((_, i) => "Уровень " + (i + 1)).ToList();
            if (qualities.Count > 0)
            {
                var quality = new DropdownField("Качество графики", qualities,
                    Mathf.Clamp(draft.qualityLevel, 0, qualities.Count - 1)) { name = "settings-quality" };
                SerenityUiDesign.StyleField(quality);
                quality.RegisterValueChangedCallback(_ => draft.qualityLevel = quality.index);
                parent.Add(quality);
            }
            var status = new Label { name = "settings-result" }; parent.Add(status);
            ButtonField("Применить", "settings-apply", () =>
            {
                try
                {
                    draft.Validate();
                    UserSettingsRuntime.ApplyGameplay(draft, input, camera, ui);
                    store.Save(draft);
                    status.text = "Настройки применены";
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    status.text = "Не удалось применить настройки. Попробуйте другое значение.";
                }
            });
            ButtonField("Назад", "settings-back", close);

            void SliderField(string title, string name, float min, float max, float value, Action<float> change)
            {
                var field = new Slider(title, min, max) { name = name, value = value, showInputField = true };
                SerenityUiDesign.StyleField(field);
                field.RegisterValueChangedCallback(evt => change(evt.newValue)); parent.Add(field);
            }
            void ButtonField(string title, string name, Action action)
            {
                var button = new Button(action) { text = title, name = name };
                SerenityUiDesign.StyleButton(button); parent.Add(button);
            }
        }
    }
}
