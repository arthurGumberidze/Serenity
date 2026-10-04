# U05 — Local Scene / RTS Camera / Input

## Scope and requirements
Work continued from `fba7afb U04A: implement PostgreSQL persistence layer`. Before implementation, commit `fba7afb` was confirmed at HEAD, U04A was DONE, NEXT_TASK selected U05, U05 was TODO, and the full U04A baseline passed. Relevant requirements were read from both FRS documents: FR-COMBAT-001, FR-UI-001/002, FR-TP-001..005 and sections 42.2–42.5. U05 implements only the local camera/input/interaction foundation. U05A vendor assets, third-person control, Character Domain, orders and gameplay systems were not implemented.

## Scene and composition
`Assets/Scenes/LocalGameplay.unity` is the only enabled build scene. It contains a 100×100 primitive ground collider, directional light, output Camera with CinemachineBrain, RTS rig with CinemachineCamera, explicit `LocalSceneCompositionRoot`, and three primitive selection markers. There is no EventSystem because U05 has no UI. The raycaster still checks EventSystem when future UI adds one.

Infrastructure owns the composition root and scene authoring tool. Presentation owns all input/camera/interaction runtime components. Dependencies are serialized explicitly; runtime components do not call scene-wide find APIs. The editor builder is retained so the placeholder scene can be reproduced without hand-editing YAML. No vendor pack or staging asset is a scene dependency.

## Input and camera behavior
`LocalGameplay.inputactions` defines only Camera and Pointer maps. Camera actions are Move (WASD/arrows), Zoom (wheel), Rotate (Q/E), Pan (pointer delta) and PanModifier (middle mouse). Pointer actions are Position, PrimaryClick and SecondaryClick. Bindings are data, not hardcoded KeyCode checks, so later rebinding can operate on the asset.

`LocalGameplayInputSource` owns device-facing action state and emits primary-click intent. `RtsCameraController` consumes the interface in one presentation Update. Keyboard movement follows rig yaw without vertical drift. Edge scroll uses a configurable normalized screen threshold, independent speed, application focus and in-window pointer check. Middle-drag pan preserves pointer-delta magnitude. Yaw, zoom distance, fixed pitch, smoothing and XZ bounds are configurable. Translation, rotation and zoom use `Time.unscaledDeltaTime`; simulation pause and GameClock do not control navigation.

Cinemachine drives only presentation: the controller moves a camera mount/target rig while CinemachineBrain renders the active virtual camera. The controller does not use Cinemachine input callbacks, leaving room for a separate U22 third-person map/camera.

## Pointer interaction
`WorldPointerRaycaster` exposes `TryGetWorldHit` and `TryGetWorldPoint`, using an explicitly assigned Camera, layer mask, maximum distance and trigger exclusion. Pointer-over-UI blocks world hits when an EventSystem exists. `SelectionProbe` listens to primary-click intent and selects a parent `SelectableMarker`. The highlight uses a MaterialPropertyBlock, so it does not clone or mutate shared material assets. This is only a U05 integration probe.

## Validation
Primary command from `C:\serenity_game`:

```powershell
./Tools/Verify-U05.ps1 -ManagedTestCluster
```

The verifier restores pinned NuGet dependencies, starts a fresh ignored SCRAM-authenticated PostgreSQL 18 test cluster, and invokes:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U04A-core-tests.log -runTests -testPlatform EditMode -testCategory '!PostgresIntegration' -testResults C:\serenity_game\Logs\U04A-core-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U04A-postgres-tests.log -runTests -testPlatform EditMode -testCategory PostgresIntegration -testResults C:\serenity_game\Logs\U04A-postgres-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U04A-build.log -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U05-playmode-tests.log -runTests -testPlatform PlayMode -testResults C:\serenity_game\Logs\U05-playmode-tests.xml
```

Final results: EditMode 91 passed, 0 failed, 0 skipped in 1.2878448 seconds; PostgreSQL integration 25 passed, 0 failed, 0 skipped in 16.4536168 seconds; PlayMode 2 passed, 0 failed, 0 skipped in 0.1850961 seconds. Windows x64 Mono Development succeeded with errors=0 and warnings=2. Player output is `Builds/Windows/Serenity.exe` (667136 bytes). Evidence is committed in `docs/validation/U05-editmode-tests.xml`, `U05-postgres-tests.xml`, `U05-playmode-tests.xml` and `U05-diagnostics.txt`.

The first sandbox Unity scene-builder attempt reproduced the known BuildReportRestService socket crash; the approved outside-sandbox run succeeded. Two subsequent editor-builder attempts exposed and corrected an editor-time InputActionAsset configuration ordering issue before final validation. Final logs contain no U05 C# compile errors. The build retains two Unity BuildReport warnings whose individual text is not expanded in the log.

## Deferred work
U05 does not implement terrain-aware camera collision, dynamic pitch, production selection UI, command/order dispatch, rebinding UI, touch/gamepad navigation, UI screens, global-map switching or third-person control. It does not integrate downloaded assets. U05A is next and must preserve this functional scene/control foundation while introducing only inventoried, licensed and normalized content.
