using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Infrastructure.Integration;
using Game.Presentation.Input;
using Game.Presentation.Localization;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure
{
    public enum MainMenuView
    {
        Main,
        NewGame,
        Load,
        Settings
    }

    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.035f, 0.045f, 0.035f, 1f);
        private static readonly Color Panel = new Color(0.075f, 0.09f, 0.065f, 0.98f);
        private static readonly Color Accent = new Color(0.68f, 0.52f, 0.26f, 1f);
        private static readonly Color Text = new Color(0.94f, 0.91f, 0.82f, 1f);

        [SerializeField] private InputActionAsset actionAsset;

        private UIDocument document;
        private PanelSettings panelSettings;
        private VisualElement root;
        private VisualElement content;
        private VisualElement modal;
        private Label status;
        private StartupSaveCatalog saveCatalog;
        private UserSettingsStore settingsStore;
        private UserSettingsData settings;
        private UserSettingsData workingSettings;
        private int? latestUsableSlot;
        private long suggestedSeed;

        public VisualElement RootVisual => root;
        public MainMenuView CurrentView { get; private set; }
        public bool ContinueAvailable => latestUsableSlot.HasValue;
        public bool ExitRequested { get; private set; }
        public UserSettingsData Settings => settings?.Clone();

        public void Configure(InputActionAsset actions) => actionAsset = actions;

        private void Awake()
        {
            if (actionAsset == null) throw new InvalidOperationException("Main menu input actions are required.");
            var saveDirectory = ResolveSaveDirectory();
            saveCatalog = new StartupSaveCatalog(new FileSaveStore(saveDirectory));
            settingsStore = UserSettingsStore.CreateDefault();
            var loadedSettings = settingsStore.Load();
            settings = loadedSettings.Settings;
            UserSettingsRuntime.ApplySystem(settings);
            UserSettingsRuntime.ApplyBindings(settings, actionAsset);
            suggestedSeed = Math.Abs(DateTime.UtcNow.Ticks % 2000000000L) + 1L;
            CreateDocument();
            ShowMain();
            if (loadedSettings.WasCorrupt) SetStatus(T("menu.settings.corrupt"));
            if (SessionLaunchState.TryConsumeFeedback(out var feedbackKey, out var feedbackSlot))
                SetStatus(F(feedbackKey, feedbackSlot));
            P2PlayerSmokeController.AttachIfRequested(this);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (modal.style.display != DisplayStyle.None) CloseModal();
            else if (CurrentView != MainMenuView.Main) ShowMain();
        }

        public void ShowMain()
        {
            CurrentView = MainMenuView.Main;
            CloseModal();
            RefreshSlots();
            content.Clear();
            AddHeading(T("menu.title"), 42);
            AddMuted(T("menu.subtitle"));
            AddSpacer(18);
            var continueButton = MenuButton(T("menu.continue"), ContinueGame, "continue-button");
            continueButton.SetEnabled(ContinueAvailable);
            content.Add(continueButton);
            content.Add(MenuButton(T("menu.new_game"), OpenNewGame, "new-game-button"));
            content.Add(MenuButton(T("menu.load"), OpenLoad, "load-button"));
            content.Add(MenuButton(T("menu.settings"), OpenSettings, "settings-button"));
            content.Add(MenuButton(T("menu.exit"), RequestExit, "exit-button"));

            if (!ContinueAvailable)
            {
                var corrupt = saveCatalog.ListSlots().Where(x => x.IsCorrupt).Select(x => x.Label).ToArray();
                SetStatus(corrupt.Length == 0 ? T("menu.continue.none") :
                    F("menu.continue.corrupt", string.Join(", ", corrupt)));
            }
        }

        public void OpenNewGame()
        {
            CurrentView = MainMenuView.NewGame;
            content.Clear();
            AddHeading(T("menu.new_game"), 32);
            AddLine(T("menu.new_game.explanation"));
            var seed = new TextField(T("menu.new_game.seed")) { value = suggestedSeed.ToString(), name = "world-seed" };
            StyleField(seed);
            content.Add(seed);
            AddMuted(T("menu.new_game.p4_note"));
            var row = ButtonRow();
            row.Add(ActionButton(T("menu.start"), () =>
            {
                if (!long.TryParse(seed.value, out var value) || value <= 0)
                {
                    SetStatus(T("menu.new_game.seed_error"));
                    return;
                }
                StartNewGame(value);
            }, "start-new-game-button"));
            row.Add(ActionButton(T("menu.back"), ShowMain, "back-button"));
            content.Add(row);
        }

        public void StartNewGame(long worldSeed)
        {
            if (worldSeed <= 0) throw new ArgumentOutOfRangeException(nameof(worldSeed));
            NewGameLaunchState.Request(worldSeed);
            ShowLoading(T("menu.loading.new_game"));
            SceneManager.LoadScene("LocalGameplay", LoadSceneMode.Single);
        }

        public void OpenLoad()
        {
            CurrentView = MainMenuView.Load;
            RefreshSlots();
            content.Clear();
            AddHeading(T("menu.load"), 32);
            AddLine(T("menu.load.explanation"));
            foreach (var slot in saveCatalog.ListSlots())
            {
                var box = Card();
                var title = new Label(slot.Label) { name = "slot-title-" + slot.Index };
                title.style.fontSize = 20;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                box.Add(title);
                AddLine(box, slot.Detail);
                if (slot.IsCorrupt)
                {
                    AddMuted(box, T("menu.load.corrupt"));
                    var explain = ActionButton(T("menu.load.explain"), () => SetStatus(T("ui.save.corrupt_slot")),
                        "corrupt-slot-" + slot.Index);
                    box.Add(explain);
                }
                else if (slot.Exists && slot.WorldSeed.HasValue)
                {
                    var captured = slot.Index;
                    box.Add(ActionButton(T("common.load"), () => LoadSlot(captured), "load-slot-" + captured));
                }
                else if (slot.Exists)
                {
                    AddMuted(box, T("menu.load.legacy_unavailable"));
                }
                else
                {
                    AddMuted(box, T("menu.load.empty"));
                }
                content.Add(box);
            }
            content.Add(ActionButton(T("menu.back"), ShowMain, "back-button"));
        }

        public void ContinueGame()
        {
            RefreshSlots();
            if (!latestUsableSlot.HasValue)
            {
                SetStatus(T("menu.continue.none"));
                return;
            }
            LoadSlot(latestUsableSlot.Value);
        }

        public void LoadSlot(int slotIndex)
        {
            var slot = saveCatalog.ListSlots().Single(x => x.Index == slotIndex);
            if (!slot.Exists)
            {
                SetStatus(T("menu.load.empty"));
                return;
            }
            if (slot.IsCorrupt)
            {
                SetStatus(T("ui.save.corrupt_slot"));
                return;
            }
            if (!slot.WorldSeed.HasValue)
            {
                SetStatus(T("menu.load.legacy_unavailable"));
                return;
            }
            SessionLaunchState.RequestLoad(slotIndex, slot.WorldSeed.Value);
            ShowLoading(F("menu.loading.slot", slotIndex));
            SceneManager.LoadScene("LocalGameplay", LoadSceneMode.Single);
        }

        public void OpenSettings()
        {
            CurrentView = MainMenuView.Settings;
            workingSettings = settings.Clone();
            DrawSettings();
        }

        public void ApplySettings()
        {
            try
            {
                workingSettings.Validate();
                settingsStore.Save(workingSettings);
                settings = workingSettings.Clone();
                UserSettingsRuntime.ApplySystem(settings);
                UserSettingsRuntime.ApplyBindings(settings, actionAsset);
                panelSettings.scale = settings.uiScale;
                SetStatus(T("menu.settings.saved"));
                ShowMain();
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is NotSupportedException ||
                                              exception is IOException || exception is UnauthorizedAccessException ||
                                              exception is InvalidOperationException || exception is ArgumentException)
            {
                Debug.LogException(exception);
                SetStatus(T("menu.settings.invalid"));
            }
        }

        public void RequestExit()
        {
            modal.Clear();
            modal.style.display = DisplayStyle.Flex;
            AddLine(modal, T("menu.exit.confirm"));
            var row = ButtonRow();
            row.Add(ActionButton(T("menu.exit"), ConfirmExit, "confirm-exit-button"));
            row.Add(ActionButton(T("common.cancel"), CloseModal, "cancel-exit-button"));
            modal.Add(row);
        }

        public void ConfirmExit()
        {
            ExitRequested = true;
            if (!Application.isEditor) Application.Quit(0);
            else CloseModal();
        }

        private void DrawSettings()
        {
            content.Clear();
            AddHeading(T("menu.settings"), 32);
            AddLine(T("menu.settings.explanation"));

            AddSlider(T("menu.settings.sound"), 0f, 1f, workingSettings.masterVolume,
                value => workingSettings.masterVolume = value);

            var resolutions = new[] { "1280 × 720", "1600 × 900", "1920 × 1080", "2560 × 1440" }.ToList();
            var currentResolution = workingSettings.resolutionWidth + " × " + workingSettings.resolutionHeight;
            if (!resolutions.Contains(currentResolution)) resolutions.Add(currentResolution);
            var resolution = new DropdownField(T("menu.settings.resolution"), resolutions, currentResolution);
            resolution.RegisterValueChangedCallback(change =>
            {
                var parts = change.newValue.Split('×');
                workingSettings.resolutionWidth = int.Parse(parts[0].Trim());
                workingSettings.resolutionHeight = int.Parse(parts[1].Trim());
            });
            StyleField(resolution); content.Add(resolution);

            var windowModes = new List<string> { T("menu.settings.windowed"), T("menu.settings.fullscreen") };
            var windowMode = new DropdownField(T("menu.settings.window_mode"), windowModes,
                (FullScreenMode)workingSettings.fullScreenMode == FullScreenMode.Windowed ? 0 : 1);
            windowMode.RegisterValueChangedCallback(change => workingSettings.fullScreenMode =
                change.newValue == windowModes[0] ? (int)FullScreenMode.Windowed : (int)FullScreenMode.FullScreenWindow);
            StyleField(windowMode); content.Add(windowMode);

            var qualityNames = Enumerable.Range(0, Math.Max(1, QualitySettings.names.Length))
                .Select(QualityName).ToList();
            workingSettings.qualityLevel = Mathf.Clamp(workingSettings.qualityLevel, 0, qualityNames.Count - 1);
            var quality = new DropdownField(T("menu.settings.quality"), qualityNames, workingSettings.qualityLevel);
            quality.RegisterValueChangedCallback(change => workingSettings.qualityLevel = qualityNames.IndexOf(change.newValue));
            StyleField(quality); content.Add(quality);

            AddSlider(T("menu.settings.ui_scale"), 1f, 1.5f, workingSettings.uiScale,
                value => workingSettings.uiScale = (float)Math.Round(value, 2));
            AddSlider(T("menu.settings.camera_speed"), 0.5f, 2f, workingSettings.cameraSpeed,
                value => workingSettings.cameraSpeed = (float)Math.Round(value, 2));

            AddHeading(T("menu.settings.keys"), 22);
            AddKeyDropdown(T("menu.settings.key.up"), workingSettings.cameraUp, value => workingSettings.cameraUp = value);
            AddKeyDropdown(T("menu.settings.key.down"), workingSettings.cameraDown, value => workingSettings.cameraDown = value);
            AddKeyDropdown(T("menu.settings.key.left"), workingSettings.cameraLeft, value => workingSettings.cameraLeft = value);
            AddKeyDropdown(T("menu.settings.key.right"), workingSettings.cameraRight, value => workingSettings.cameraRight = value);
            AddKeyDropdown(T("menu.settings.key.pause"), workingSettings.pause, value => workingSettings.pause = value);
            AddKeyDropdown(T("menu.settings.key.build"), workingSettings.build, value => workingSettings.build = value);
            AddKeyDropdown(T("menu.settings.key.control"), workingSettings.controlMode, value => workingSettings.controlMode = value);

            var row = ButtonRow();
            row.Add(ActionButton(T("menu.settings.apply"), ApplySettings, "apply-settings-button"));
            row.Add(ActionButton(T("menu.settings.reset"), () => { workingSettings = new UserSettingsData(); DrawSettings(); },
                "reset-settings-button"));
            row.Add(ActionButton(T("common.cancel"), ShowMain, "cancel-settings-button"));
            content.Add(row);
        }

        private void AddKeyDropdown(string label, string current, Action<string> set)
        {
            var choices = UserSettingsRuntime.SupportedKeyNames.Select(KeyDisplay).ToList();
            var field = new DropdownField(label, choices, KeyDisplay(current));
            field.RegisterValueChangedCallback(change => set(KeyName(change.newValue)));
            StyleField(field);
            content.Add(field);
        }

        private void AddSlider(string label, float low, float high, float value, Action<float> set)
        {
            var slider = new Slider(label, low, high) { value = value };
            slider.RegisterValueChangedCallback(change => set(change.newValue));
            StyleField(slider);
            content.Add(slider);
        }

        private void CreateDocument()
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "Serenity Main Menu Panel";
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1600, 900);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.scale = settings.uiScale;
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("SerenityRuntimeTheme");
            if (panelSettings.themeStyleSheet == null)
                throw new InvalidOperationException("SerenityRuntimeTheme.tss is required for the main menu.");

            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = 200;
            root = document.rootVisualElement;
            root.name = "main-menu-root";
            root.style.flexGrow = 1f;
            root.style.backgroundColor = Background;
            root.style.color = Text;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            content = new ScrollView(ScrollViewMode.Vertical) { name = "menu-content" };
            content.style.width = 620;
            content.style.maxHeight = 820;
            content.style.paddingLeft = 38; content.style.paddingRight = 38;
            content.style.paddingTop = 30; content.style.paddingBottom = 30;
            content.style.backgroundColor = Panel;
            root.Add(content);

            status = new Label { name = "menu-status" };
            status.style.position = Position.Absolute;
            status.style.left = 24; status.style.right = 24; status.style.bottom = 18;
            status.style.unityTextAlign = TextAnchor.MiddleCenter;
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.color = new Color(0.86f, 0.76f, 0.52f, 1f);
            root.Add(status);

            modal = new VisualElement { name = "menu-modal" };
            modal.style.position = Position.Absolute;
            modal.style.left = 520; modal.style.right = 520; modal.style.top = 330;
            modal.style.paddingLeft = 24; modal.style.paddingRight = 24;
            modal.style.paddingTop = 20; modal.style.paddingBottom = 20;
            modal.style.backgroundColor = new Color(0.09f, 0.08f, 0.055f, 1f);
            modal.style.display = DisplayStyle.None;
            root.Add(modal);
        }

        private void RefreshSlots() => latestUsableSlot = saveCatalog.LatestUsableSlot();

        private void ShowLoading(string message)
        {
            content.Clear();
            AddHeading(T("menu.loading"), 34);
            AddLine(message);
            SetStatus(string.Empty);
        }

        private void CloseModal()
        {
            if (modal == null) return;
            modal.Clear();
            modal.style.display = DisplayStyle.None;
        }

        private void AddHeading(string text, int size)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginBottom = 12;
            content.Add(label);
        }

        private void AddLine(string text) => AddLine(content, text);

        private static void AddLine(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 16;
            label.style.marginBottom = 10;
            parent.Add(label);
        }

        private void AddMuted(string text) => AddMuted(content, text);

        private static void AddMuted(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new Color(0.7f, 0.69f, 0.62f, 1f);
            label.style.marginBottom = 12;
            parent.Add(label);
        }

        private void AddSpacer(int height)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            content.Add(spacer);
        }

        private Button MenuButton(string text, Action action, string name)
        {
            var button = ActionButton(text, action, name);
            button.style.height = 54;
            button.style.fontSize = 20;
            button.style.marginBottom = 10;
            return button;
        }

        private static Button ActionButton(string text, Action action, string name = null)
        {
            var button = new Button(action) { text = text, name = name ?? string.Empty };
            button.style.minHeight = 38;
            button.style.marginRight = 8;
            button.style.marginBottom = 8;
            button.style.backgroundColor = Accent;
            button.style.color = Color.white;
            button.style.whiteSpace = WhiteSpace.Normal;
            return button;
        }

        private static VisualElement ButtonRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 14;
            return row;
        }

        private static VisualElement Card()
        {
            var card = new VisualElement();
            card.style.paddingLeft = 16; card.style.paddingRight = 16;
            card.style.paddingTop = 12; card.style.paddingBottom = 12;
            card.style.marginBottom = 10;
            card.style.backgroundColor = new Color(0.11f, 0.125f, 0.095f, 1f);
            return card;
        }

        private static void StyleField(VisualElement field)
        {
            field.style.marginTop = 6;
            field.style.marginBottom = 8;
            field.style.minHeight = 34;
            field.style.whiteSpace = WhiteSpace.Normal;
        }

        private void SetStatus(string message) => status.text = message ?? string.Empty;

        private static string QualityName(int index)
        {
            switch (index)
            {
                case 0: return T("menu.settings.quality.low");
                case 1: return T("menu.settings.quality.medium");
                case 2: return T("menu.settings.quality.high");
                default: return F("menu.settings.quality.custom", index + 1);
            }
        }

        private static string KeyDisplay(string key)
        {
            switch (key)
            {
                case "space": return T("menu.key.space");
                case "enter": return T("menu.key.enter");
                case "leftShift": return T("menu.key.shift");
                case "leftCtrl": return T("menu.key.ctrl");
                case "upArrow": return T("menu.key.up_arrow");
                case "downArrow": return T("menu.key.down_arrow");
                case "leftArrow": return T("menu.key.left_arrow");
                case "rightArrow": return T("menu.key.right_arrow");
                default: return (key ?? string.Empty).ToUpperInvariant();
            }
        }

        private static string KeyName(string display)
        {
            foreach (var key in UserSettingsRuntime.SupportedKeyNames)
                if (string.Equals(KeyDisplay(key), display, StringComparison.Ordinal)) return key;
            return string.Empty;
        }

        private static string ResolveSaveDirectory()
        {
            var configured = Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY");
            return string.IsNullOrWhiteSpace(configured)
                ? System.IO.Path.Combine(Application.persistentDataPath, "Serenity", "Saves")
                : configured;
        }

        private static string T(string key) => RussianTextCatalog.Get(key);
        private static string F(string key, params object[] values) => RussianTextCatalog.Format(key, values);
    }
}
