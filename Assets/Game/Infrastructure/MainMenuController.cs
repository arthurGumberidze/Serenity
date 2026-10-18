using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Infrastructure.Integration;
using Game.Presentation.Characters;
using Game.Presentation.Input;
using Game.Presentation.Localization;
using Game.Presentation.UI;
using Game.Simulation.Campaign;
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
        private static readonly Color Background = SerenityUiDesign.Canvas;
        private static readonly Color Panel = SerenityUiDesign.Surface;
        private static readonly Color Accent = SerenityUiDesign.Accent;
        private static readonly Color Text = SerenityUiDesign.Text;

        [SerializeField] private InputActionAsset actionAsset;
        [SerializeField] private CharacterPresentationCatalog characterCatalog;

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
        private AppearanceDefinitionCatalog appearanceCatalog;
        private RulerCreationDraft rulerDraft;
        private int rulerRandomSequence;
        private TextField rulerSeedField;
        private TextField rulerNameField;
        private TextField dynastyNameField;
        private DropdownField appearanceProfileField;
        private VisualElement appearanceParts;
        private RenderTexture previewTexture;
        private GameObject previewRoot;
        private GameObject previewCharacter;
        private Camera previewCamera;
        private float previewYaw = 165f;
        // UMA 3.0 bodies are taller than the legacy fallback wrappers. Start far enough away to keep
        // head and feet visible; the player can still zoom in with the existing control.
        private float previewZoom = 4.2f;

        public VisualElement RootVisual => root;
        public MainMenuView CurrentView { get; private set; }
        public bool ContinueAvailable => latestUsableSlot.HasValue;
        public bool ExitRequested { get; private set; }
        public UserSettingsData Settings => settings?.Clone();
        public float UiScale => panelSettings?.scale ?? 1f;

        public RulerCreationDraft RulerDraft => rulerDraft;
        public bool HasLiveRulerPreview => previewCharacter != null && previewCamera != null && previewTexture != null;

        public void Configure(InputActionAsset actions, CharacterPresentationCatalog presentations = null)
        {
            actionAsset = actions;
            characterCatalog = presentations;
        }

        private void Awake()
        {
            if (actionAsset == null) throw new InvalidOperationException("Main menu input actions are required.");
            if (characterCatalog == null) throw new InvalidOperationException("P4 character presentation catalog is required.");
            appearanceCatalog = characterCatalog.CreateDefinitionCatalog();
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
            if (TechnicalScenarioLaunch.TryResolveWorldSeed(Environment.GetCommandLineArgs(), out var technicalSeed))
            {
                StartNewGame(technicalSeed);
                return;
            }
            P2PlayerSmokeController.AttachIfRequested(this);
            P3PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P4PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P5PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P6PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P7PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P81PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P82PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P83PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P84PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P85PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P91PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P92PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P93PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P94PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P95PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P96PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P102PlayerSmokeController.LaunchFromMenuIfRequested(this);
            P105PlayerSmokeController.LaunchFromMenuIfRequested(this);
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
            DisposeRulerPreview();
            CurrentView = MainMenuView.Main;
            CloseModal();
            RefreshSlots();
            content.Clear();
            SetMenuLayout(MainMenuView.Main);
            AddHeading(T("menu.title"), 42);
            AddMuted(T("menu.subtitle"));
            AddSpacer(18);
            var continueButton = MenuButton(T("menu.continue"), ContinueGame, "continue-button");
            continueButton.SetEnabled(ContinueAvailable);
            continueButton.EnableInClassList(SerenityUiDesign.PrimaryButtonClass, ContinueAvailable);
            content.Add(continueButton);
            var newGameButton = MenuButton(T("menu.new_game"), OpenNewGame, "new-game-button");
            newGameButton.EnableInClassList(SerenityUiDesign.PrimaryButtonClass, !ContinueAvailable);
            content.Add(newGameButton);
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
            DisposeRulerPreview();
            CurrentView = MainMenuView.NewGame;
            content.Clear();
            SetMenuLayout(MainMenuView.NewGame);
            AddHeading(T("menu.new_game"), 32);
            AddLine(T("menu.new_game.explanation"));
            rulerDraft = RulerCreationService.CreateDefault(suggestedSeed, appearanceCatalog);
            rulerRandomSequence = 0;

            var editorGrid = new VisualElement { name = "ruler-editor-grid" };
            editorGrid.AddToClassList("serenity-editor-grid");
            editorGrid.style.flexWrap = Wrap.NoWrap;
            var identityColumn = Card();
            identityColumn.name = "ruler-identity-column";
            identityColumn.AddToClassList("serenity-editor-column");
            identityColumn.style.width = new Length(50, LengthUnit.Percent);
            var appearanceColumn = Card();
            appearanceColumn.name = "ruler-appearance-column";
            appearanceColumn.AddToClassList("serenity-editor-column");
            appearanceColumn.style.width = new Length(50, LengthUnit.Percent);
            editorGrid.Add(identityColumn);
            editorGrid.Add(appearanceColumn);

            rulerSeedField = new TextField(T("menu.new_game.seed"))
                { value = suggestedSeed.ToString(), name = "world-seed" };
            rulerNameField = new TextField(T("menu.new_game.ruler_name"))
                { value = rulerDraft.RulerName, name = "ruler-name" };
            dynastyNameField = new TextField(T("menu.new_game.dynasty_name"))
                { value = rulerDraft.DynastyName, name = "dynasty-name" };
            StyleField(rulerSeedField); StyleField(rulerNameField); StyleField(dynastyNameField);
            identityColumn.Add(rulerSeedField); identityColumn.Add(rulerNameField); identityColumn.Add(dynastyNameField);

            appearanceProfileField = new DropdownField(T("menu.new_game.appearance"),
                appearanceCatalog.Profiles.Select(ProfileLabel).ToList(), ProfileLabel(rulerDraft.Profile))
                { name = "appearance-profile" };
            StyleField(appearanceProfileField);
            appearanceProfileField.RegisterValueChangedCallback(change =>
                ApplyProfile(appearanceCatalog.Profiles.First(x => ProfileLabel(x) == change.newValue)));
            identityColumn.Add(appearanceProfileField);

            var previewCard = Card();
            previewCard.name = "ruler-preview-card";
            var previewImage = new Image { name = "ruler-preview", scaleMode = ScaleMode.ScaleToFit };
            previewImage.style.height = 348;
            previewImage.AddToClassList("serenity-preview");
            previewCard.Add(previewImage);
            var previewControls = ButtonRow();
            previewControls.Add(ActionButton("↶", () => RotatePreview(-20f), "preview-rotate-left"));
            previewControls.Add(ActionButton("↷", () => RotatePreview(20f), "preview-rotate-right"));
            var zoom = new Slider(T("menu.new_game.preview_zoom"), 2.2f, 5.2f)
                { value = previewZoom, name = "preview-zoom" };
            zoom.style.flexGrow = 1f;
            zoom.RegisterValueChangedCallback(change => SetPreviewZoom(change.newValue));
            previewControls.Add(zoom);
            previewCard.Add(previewControls);
            identityColumn.Add(previewCard);

            appearanceParts = Card();
            appearanceParts.name = "appearance-parts";
            appearanceColumn.Add(appearanceParts);
            CreateRulerPreview(previewImage);
            RefreshAppearanceParts();
            AddMuted(appearanceColumn,
                "Все доступные совместимые параметры применяются к UMA в предпросмотре. Одежда пока ограничена одним проверенным комплектом.");
            content.Add(editorGrid);
            var row = ButtonRow();
            row.Add(ActionButton(T("menu.new_game.randomize"), RandomizeRuler, "randomize-ruler-button"));
            row.Add(ActionButton(T("menu.new_game.reset"), ResetRuler, "reset-ruler-button"));
            var startButton = ActionButton(T("menu.start"), () =>
            {
                TryStartEditedNewGame();
            }, "start-new-game-button");
            startButton.AddToClassList(SerenityUiDesign.PrimaryButtonClass);
            row.Add(startButton);
            row.Add(ActionButton(T("menu.back"), ShowMain, "back-button"));
            content.Add(row);
        }

        public void SetUiScale(float value)
        {
            if (panelSettings == null) throw new InvalidOperationException("Main menu UI is not initialized.");
            panelSettings.scale = Mathf.Clamp(value, 1f, 1.5f);
        }

        public void StartNewGame(long worldSeed)
        {
            if (worldSeed <= 0) throw new ArgumentOutOfRangeException(nameof(worldSeed));
            NewGameLaunchState.Request(worldSeed);
            ShowLoading(T("menu.loading.new_game"));
            SceneManager.LoadScene("LocalGameplay", LoadSceneMode.Single);
        }

        public void StartNewGame(NewGameConfiguration configuration)
        {
            NewGameLaunchState.Request(configuration ?? throw new ArgumentNullException(nameof(configuration)));
            ShowLoading(T("menu.loading.new_game"));
            SceneManager.LoadScene("LocalGameplay", LoadSceneMode.Single);
        }

        public bool TryStartEditedNewGame()
        {
            try
            {
                if (!long.TryParse(rulerSeedField?.value, out var worldSeed) || worldSeed <= 0)
                    throw new ArgumentOutOfRangeException(nameof(worldSeed));
                rulerDraft = RulerCreationService.Create(worldSeed, rulerNameField.value, dynastyNameField.value,
                    rulerDraft.Appearance, appearanceCatalog);
                StartNewGame(rulerDraft.ToConfiguration());
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                SetStatus(T("menu.new_game.editor_error"));
                return false;
            }
        }

        public void RandomizeRuler()
        {
            if (!long.TryParse(rulerSeedField?.value, out var seed) || seed <= 0)
            {
                SetStatus(T("menu.new_game.seed_error"));
                return;
            }
            rulerDraft = RulerCreationService.Randomize(seed, rulerRandomSequence++, appearanceCatalog);
            ApplyDraftToEditor();
        }

        public void ResetRuler()
        {
            if (!long.TryParse(rulerSeedField?.value, out var seed) || seed <= 0) seed = suggestedSeed;
            rulerRandomSequence = 0;
            rulerDraft = RulerCreationService.CreateDefault(seed, appearanceCatalog);
            ApplyDraftToEditor();
        }

        public void RotatePreview(float degrees)
        {
            previewYaw = Mathf.Repeat(previewYaw + degrees, 360f);
            if (previewCharacter != null) previewCharacter.transform.rotation = Quaternion.Euler(0f, previewYaw, 0f);
        }

        public void SetPreviewZoom(float distance)
        {
            previewZoom = Mathf.Clamp(distance, 2.2f, 5.2f);
            if (previewCamera != null) previewCamera.transform.position = new Vector3(1000f, 1.05f, -previewZoom);
        }

        public void OpenLoad()
        {
            CurrentView = MainMenuView.Load;
            RefreshSlots();
            content.Clear();
            SetMenuLayout(MainMenuView.Load);
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
            SetMenuLayout(MainMenuView.Settings);
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

        private void ApplyDraftToEditor()
        {
            rulerSeedField.value = rulerDraft.WorldSeed.ToString();
            rulerNameField.value = rulerDraft.RulerName;
            dynastyNameField.value = rulerDraft.DynastyName;
            appearanceProfileField.SetValueWithoutNotify(ProfileLabel(rulerDraft.Profile));
            RebuildPreviewCharacter(rulerDraft.Profile);
            RefreshAppearanceParts();
            SetStatus(string.Empty);
        }

        private void ApplyProfile(AppearanceProfileDefinition profile)
        {
            var seed = rulerDraft?.WorldSeed ?? suggestedSeed;
            rulerDraft = new RulerCreationDraft(seed, rulerNameField?.value ?? rulerDraft?.RulerName ?? "Арен",
                dynastyNameField?.value ?? rulerDraft?.DynastyName ?? "Дом Серенити", profile.Sex, profile);
            RebuildPreviewCharacter(profile);
            RefreshAppearanceParts();
        }

        private AppearanceProfileDefinition SelectedProfile()
        {
            var label = appearanceProfileField?.value;
            return appearanceCatalog.Profiles.FirstOrDefault(x => ProfileLabel(x) == label) ??
                   appearanceCatalog.DefaultFor(CharacterSex.Male);
        }

        private static string ProfileLabel(AppearanceProfileDefinition profile) =>
            profile.Sex == CharacterSex.Male ? "Человек — мужчина" : "Человек — женщина";

        private void RefreshAppearanceParts()
        {
            if (appearanceParts == null) return;
            appearanceParts.Clear();
            var profile = SelectedProfile();
            AddLine(appearanceParts, "Редактор внешности — значения 0–100% применяются без скрытых генетических параметров.");
            AddAppearanceDropdown("Тон кожи", "appearance-skin", profile.Skins, rulerDraft.Appearance.SkinId,
                value => SetAppearanceOption("skin", value));
            AddAppearanceDropdown("Причёска", "appearance-hair", profile.Hairs, rulerDraft.Appearance.HairId,
                value => SetAppearanceOption("hair", value));
            var beard = AddAppearanceDropdown("Борода", "appearance-beard", profile.Beards, rulerDraft.Appearance.BeardId,
                value => SetAppearanceOption("beard", value));
            beard.SetEnabled(profile.Beards.Count > 1);
            AddAppearanceDropdown("Цвет волос", "appearance-hair-color", profile.HairColors,
                rulerDraft.Appearance.HairColorId, value => SetAppearanceOption("hair_color", value));
            AddAppearanceDropdown("Цвет глаз", "appearance-eye-color", profile.EyeColors,
                rulerDraft.Appearance.EyeColorId, value => SetAppearanceOption("eye_color", value));
            AddAppearanceDropdown("Возраст", "appearance-age", profile.Ages, rulerDraft.Appearance.AgeId,
                value => SetAppearanceOption("age", value));
            var clothing = new DropdownField("Одежда", new List<string> { "Проверенный совместимый комплект" }, 0)
                { name = "appearance-clothing" };
            clothing.SetEnabled(false); StyleField(clothing); appearanceParts.Add(clothing);
            foreach (var parameter in profile.Parameters)
            {
                var slider = new Slider(ParameterLabel(parameter) + " (0–100%)", 0f, 100f)
                {
                    name = "appearance-" + parameter.Value,
                    value = rulerDraft.Appearance.Parameter(parameter) / 10f
                };
                slider.RegisterValueChangedCallback(change =>
                    SetAppearanceParameter(parameter, Mathf.RoundToInt(change.newValue * 10f)));
                StyleField(slider); appearanceParts.Add(slider);
            }
        }

        public bool SetAppearanceOption(string category, string stableId)
        {
            if (rulerDraft == null || string.IsNullOrEmpty(category)) return false;
            AppearancePartId value;
            try { value = new AppearancePartId(stableId); }
            catch (ArgumentException) { return false; }
            var profile = rulerDraft.Profile;
            var supported = category == "skin" ? profile.Skins : category == "hair" ? profile.Hairs :
                category == "beard" ? profile.Beards : category == "hair_color" ? profile.HairColors :
                category == "eye_color" ? profile.EyeColors : category == "age" ? profile.Ages : null;
            if (supported == null || !supported.Contains(value)) return false;
            var current = rulerDraft.Appearance;
            var changed = category == "skin" ? current.With(skinId: value) :
                category == "hair" ? current.With(hairId: value) :
                category == "beard" ? current.With(beardId: value) :
                category == "hair_color" ? current.With(hairColorId: value) :
                category == "eye_color" ? current.With(eyeColorId: value) : current.With(ageId: value);
            ReplaceAppearance(changed);
            return true;
        }

        public bool SetAppearanceParameter(AppearanceParameterId id, int value)
        {
            if (rulerDraft == null || !rulerDraft.Profile.Parameters.Contains(id) ||
                value < AppearanceParameterValue.Min || value > AppearanceParameterValue.Max) return false;
            var parameters = rulerDraft.Appearance.Parameters
                .Select(item => item.Id == id ? new AppearanceParameterValue(id, value) : item).ToArray();
            ReplaceAppearance(rulerDraft.Appearance.With(parameters: parameters));
            return true;
        }

        private void ReplaceAppearance(AppearanceState appearance)
        {
            rulerDraft = new RulerCreationDraft(rulerDraft.WorldSeed, rulerNameField?.value ?? rulerDraft.RulerName,
                dynastyNameField?.value ?? rulerDraft.DynastyName, rulerDraft.Sex, rulerDraft.Profile, appearance);
            RebuildPreviewCharacter(rulerDraft.Profile);
        }

        private DropdownField AddAppearanceDropdown(string label, string name,
            IReadOnlyList<AppearancePartId> options, AppearancePartId selected, Action<string> changed)
        {
            var choices = options.Select(option => PartLabel(option.Value)).ToList();
            var field = new DropdownField(label, choices, Math.Max(0, options.ToList().IndexOf(selected))) { name = name };
            field.RegisterValueChangedCallback(change => changed(options[choices.IndexOf(change.newValue)].Value));
            StyleField(field); appearanceParts.Add(field);
            return field;
        }

        private static string ParameterLabel(AppearanceParameterId id)
        {
            if (id == StoneAgeAppearanceProfiles.Head) return "Пропорции головы";
            if (id == StoneAgeAppearanceProfiles.Jaw) return "Форма челюсти";
            if (id == StoneAgeAppearanceProfiles.Chin) return "Форма подбородка";
            if (id == StoneAgeAppearanceProfiles.Nose) return "Форма носа";
            if (id == StoneAgeAppearanceProfiles.Eyes) return "Размер глаз";
            if (id == StoneAgeAppearanceProfiles.Brows) return "Высота бровей";
            if (id == StoneAgeAppearanceProfiles.Lips) return "Размер губ";
            if (id == StoneAgeAppearanceProfiles.Height) return "Рост";
            return "Телосложение";
        }

        private static string PartLabel(string id)
        {
            switch (id)
            {
                case "skin_light": return "Светлый"; case "skin_warm": return "Тёплый"; case "skin_deep": return "Тёмный";
                case "hair_very_short": return "Очень короткая"; case "hair_bun": return "Пучок";
                case "hair_messy": return "Растрёпанная"; case "hair_none": return "Без волос";
                case "beard_none": return "Без бороды"; case "beard_trimmed": return "Короткая";
                case "beard_goatee": return "Эспаньолка"; case "beard_neckbeard": return "Окладистая";
                case "hair_black": return "Чёрный"; case "hair_brown": return "Каштановый";
                case "hair_auburn": return "Рыжий"; case "hair_blond": return "Светлый"; case "hair_gray": return "Седой";
                case "eye_brown": return "Карие"; case "eye_hazel": return "Ореховые"; case "eye_green": return "Зелёные";
                case "eye_blue": return "Голубые"; case "eye_gray": return "Серые";
                case "age_adult": return "Взрослый"; case "age_mature": return "Зрелый"; case "age_aged": return "Пожилой";
                default: return id;
            }
        }

        private void CreateRulerPreview(Image image)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                image.tooltip = T("menu.new_game.preview_headless");
                return;
            }
            previewTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "P4 Ruler Preview"
            };
            previewTexture.Create();
            image.image = previewTexture;

            previewRoot = new GameObject("P4 Ruler Preview Root");
            DontDestroyOnLoad(previewRoot);
            var cameraObject = new GameObject("P4 Ruler Preview Camera");
            cameraObject.transform.SetParent(previewRoot.transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.targetTexture = previewTexture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.035f, 0.05f, 0.035f, 1f);
            previewCamera.cullingMask = 1 << 31;
            previewCamera.fieldOfView = 32f;
            SetPreviewZoom(previewZoom);
            previewCamera.transform.LookAt(new Vector3(1000f, 1.05f, 0f));

            var lightObject = new GameObject("P4 Ruler Preview Light");
            lightObject.layer = 31;
            lightObject.transform.SetParent(previewRoot.transform, false);
            lightObject.transform.position = new Vector3(998.5f, 3.5f, -2f);
            lightObject.transform.LookAt(new Vector3(1000f, 1f, 0f));
            var previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = 1.5f;
            previewLight.cullingMask = 1 << 31;
            RebuildPreviewCharacter(SelectedProfile());
        }

        private void RebuildPreviewCharacter(AppearanceProfileDefinition profile)
        {
            if (previewRoot == null) return;
            if (previewCharacter != null) Destroy(previewCharacter);
            var appearance = rulerDraft.Appearance;
            var prefab = characterCatalog.Resolve(profile.Sex, appearance, out _);
            previewCharacter = Instantiate(prefab, new Vector3(1000f, 0f, 0f),
                Quaternion.Euler(0f, previewYaw, 0f), previewRoot.transform);
            previewCharacter.name = "P8.4 Ruler Preview " + profile.Id.Value;
            foreach (var behaviour in previewCharacter.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            SetLayerRecursively(previewCharacter, 31);
            if (characterCatalog.UmaAvatarPrefab != null)
                previewCharacter.AddComponent<UmaAppearanceProjection>()
                    .Configure(characterCatalog.UmaAvatarPrefab, appearance);
        }

        private static void SetLayerRecursively(GameObject value, int layer)
        {
            value.layer = layer;
            for (var i = 0; i < value.transform.childCount; i++)
                SetLayerRecursively(value.transform.GetChild(i).gameObject, layer);
        }

        private void DisposeRulerPreview()
        {
            if (previewRoot != null) Destroy(previewRoot);
            previewRoot = null;
            previewCharacter = null;
            previewCamera = null;
            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
            }
            previewTexture = null;
        }

        private void OnDestroy() => DisposeRulerPreview();

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
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            SerenityUiDesign.ApplyRoot(root, "serenity-main-menu");
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());

            var atmosphere = new VisualElement { name = "menu-atmosphere" };
            atmosphere.style.position = Position.Absolute;
            atmosphere.style.left = 0; atmosphere.style.right = 0;
            atmosphere.style.top = 0; atmosphere.style.bottom = 0;
            atmosphere.pickingMode = PickingMode.Ignore;
            atmosphere.AddToClassList("serenity-menu-atmosphere");
            root.Add(atmosphere);

            var menuScroll = new ScrollView(ScrollViewMode.Vertical) { name = "menu-content" };
            menuScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            content = menuScroll;
            content.style.width = 620;
            content.style.maxHeight = 820;
            content.style.paddingLeft = 38; content.style.paddingRight = 38;
            content.style.paddingTop = 30; content.style.paddingBottom = 30;
            content.AddToClassList("serenity-menu-shell");
            root.Add(content);

            status = new Label { name = "menu-status" };
            status.style.position = Position.Absolute;
            status.style.left = 24; status.style.right = 24; status.style.bottom = 18;
            status.style.unityTextAlign = TextAnchor.MiddleCenter;
            status.AddToClassList("serenity-status");
            root.Add(status);

            modal = new VisualElement { name = "menu-modal" };
            modal.style.position = Position.Absolute;
            modal.style.left = new Length(31, LengthUnit.Percent);
            modal.style.right = new Length(31, LengthUnit.Percent);
            modal.style.top = new Length(35, LengthUnit.Percent);
            modal.style.paddingLeft = 24; modal.style.paddingRight = 24;
            modal.style.paddingTop = 20; modal.style.paddingBottom = 20;
            modal.AddToClassList(SerenityUiDesign.ModalClass);
            modal.style.display = DisplayStyle.None;
            root.Add(modal);
            ApplySafeArea();
        }

        private void RefreshSlots() => latestUsableSlot = saveCatalog.LatestUsableSlot();

        private void ShowLoading(string message)
        {
            DisposeRulerPreview();
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
            label.style.color = SerenityUiDesign.Text;
            label.AddToClassList(SerenityUiDesign.TitleClass);
            content.Add(label);
        }

        private void AddLine(string text) => AddLine(content, text);

        private static void AddLine(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.fontSize = 16;
            label.style.marginBottom = 10;
            label.AddToClassList(SerenityUiDesign.BodyClass);
            parent.Add(label);
        }

        private void AddMuted(string text) => AddMuted(content, text);

        private static void AddMuted(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.marginBottom = 12;
            label.AddToClassList(SerenityUiDesign.MutedClass);
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
            button.AddToClassList("serenity-menu-button");
            button.style.marginRight = 0;
            button.style.height = 54;
            button.style.fontSize = 20;
            button.style.marginBottom = 10;
            return button;
        }

        private static Button ActionButton(string text, Action action, string name = null)
        {
            var button = new Button(action) { text = text, name = name ?? string.Empty };
            button.style.marginRight = 8;
            button.style.marginBottom = 8;
            SerenityUiDesign.StyleButton(button);
            return button;
        }

        private static VisualElement ButtonRow()
        {
            var row = new VisualElement();
            row.AddToClassList(SerenityUiDesign.RowClass);
            row.style.marginTop = 14;
            return row;
        }

        private static VisualElement Card()
        {
            var card = new VisualElement();
            SerenityUiDesign.StyleCard(card);
            return card;
        }

        private static void StyleField(VisualElement field)
        {
            SerenityUiDesign.StyleField(field);
        }

        private void SetMenuLayout(MainMenuView view)
        {
            if (content == null) return;
            content.style.width = view == MainMenuView.NewGame ? 1120 :
                view == MainMenuView.Main ? 580 : 760;
            content.style.maxWidth = new Length(94, LengthUnit.Percent);
            content.style.maxHeight = new Length(90, LengthUnit.Percent);
            content.EnableInClassList("serenity-menu-editor", view == MainMenuView.NewGame);
        }

        private void ApplySafeArea()
        {
            SerenityUiDesign.ApplySafeArea(root, Screen.safeArea, Screen.width, Screen.height);
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
