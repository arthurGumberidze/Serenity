# Gameplay UI

## Purpose and technology

U27 provides one player-facing runtime UI built with UI Toolkit. `GameplayUiController` creates one `UIDocument`, one scale-with-screen-size `PanelSettings` at a 1600×900 reference resolution and a small runtime theme. It is a projection and command adapter: canonical state remains in Domain/Simulation services and all mutations use their existing APIs.

The layout is a persistent top HUD, left navigation, contextual center panel, right selection inspector/notifications and a bottom status strip. It scales to ordinary desktop aspect ratios; panels scroll instead of expanding over the world.

## Player and developer surfaces

Player UI is enabled by default. All legacy `*Debug*` behaviours, the third-person IMGUI overlay and the global-map IMGUI controls are disabled. F10 is the single explicit developer toggle. Stable IDs, forced health/building/combat controls, hashes and AI decision internals remain developer-only.

## Player surfaces

- HUD: population, key resources, calendar, season, weather, temperature, Local/Global mode, alert count, pause and x1/x2/x3/x5/x10.
- Selection: neutral/multi-select states plus Character, Building and world-resource inspection. Character detail includes health/capability modifiers, conditions, pregnancy, inventory, research, work/job, dynasty, military and TPS eligibility. Building detail includes owner, construction, integrity, fire, storage, production and crop state.
- Settlement: stored/total resources, capacity, target-stock +/- controls, food shortage, weather production modifiers, production sites, crop/source state and active work.
- Research: the complete Stone Age catalog with Completed/Available/Locked/Active state, prerequisites, work cost, unlock preview, progress and researcher assignment.
- Work: create/select/rename groups, add/remove selected members, commander, priority, Move, Haul, job/claim lifecycle and cancel. Existing digit shortcuts remain active; text-field focus suppresses gameplay hotkeys.
- Build: catalog-backed buttons, costs, locked reasons and the existing ghost/validity/rotation/confirm/cancel path.
- Dynasty: monarch, spouse, children, heir, succession candidates and history.
- Military/combat: create player units, roster edits, captain, Idle/Move/Hold/Engage, Detailed/Aggregate, battle mode/counts/morale/outcome and physical battle entry.
- TPS: controlled Character, health/combat state and exit hint; entry eligibility is explained.
- Global: open map, inspect locations/settlement ownership, select aggregate player armies, ETA/travel, enter a settlement and enter an abstract battle. Newly created player units are bound to the current canonical location before travel.
- Save/Load: three deterministic local-file slots, human-readable date/mode/write-time metadata, corrupt-slot disabling, load confirmation and visible command status.

## Notifications

The bounded feed maps existing state/history changes for research completion, birth, death, succession, health conditions, food shortage, building ignition/destruction/capture, battle start/finish/state and army arrival. It deliberately does not add the U28 event engine.

## Input and refresh

The existing `WorldPointerRaycaster` accepts a region blocker from the UI. HUD, navigation, panels, inspector, notifications and modal block selection, Move, placement and other world clicks before physics. Text input suppresses U27 keyboard commands. Existing Space/Minus/Plus, B, selection, work shortcuts, TPS and global input stay on their established Input System paths.

The UI computes a lightweight state hash at 4 Hz and rebuilds only when the projection changes. It has one guarded subscription set (pause/slower/faster/build menu); panel changes, Local/Global transitions and Save/Load do not multiply callbacks.

## Save/Load and rebinding

`LocalGameplaySaveFacade` uses the existing `SaveCoordinator` with `FileSaveStore`; `ISaveSlotCatalog` adds metadata inspection without another save authority. Default files live under `Application.persistentDataPath/Serenity/Saves`; `SERENITY_SAVE_DIRECTORY` is an isolated test/automation override.

After load, the composition root adopts the replacement `GameClock`, rebinds the centralized runtime driver, clears selection, refreshes weather/location presentation, reconciles the additive global scene and refreshes the same UI instance. Tier 3 is a derived runtime projection, so its absolute cursors are rebased to the restored clock before the next tick; canonical persisted services remain owned by `SaveCoordinator`.

## Known limits

- Visual treatment is functional Stone Age-themed UI, not final art/localization/gamepad work.
- The notification feed is a polling adapter over current histories and events; U28 owns reusable weighted/cooldown event chains.
- The global scene remains the existing schematic U25 presentation.
- Full new-game population, AI discovery/supply and TPS attack compliance remain assigned to U30A.
