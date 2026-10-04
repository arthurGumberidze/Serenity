# U05 Implementation Plan

## Scope
Implement only U05: a project-owned local gameplay scene, RTS camera navigation, centralized Input System actions, pointer-to-world raycasting, and a minimal selection probe. Do not integrate vendor assets, implement third-person control, add gameplay simulation, or change persistence.

## Requirements traced from the FRS
- FR-COMBAT-001 and FR-UI-001 require a free top-down 3D camera that can approach street level.
- FR-UI-002 keeps the global map as a separate future mode rather than an extreme local-scene zoom.
- FRS 42.3 assigns cameras and input to Presentation; Infrastructure remains the composition layer.
- FRS 42.4 selects Input System input maps and Cinemachine for RTS and future third-person cameras.
- FRS 42.2 states that camera distance is not authoritative simulation state.
- FR-TP-001..005 and U22 are future requirements: U05 prepares separate input maps/contracts but does not implement character control.

## Design
1. Add a dedicated `LocalGameplay` scene made only from Unity primitives, with a ground collider, directional light, explicit composition root, RTS camera rig, and selection markers.
2. Add a compact `LocalGameplay.inputactions` asset with Camera Move, Zoom, Rotate, Pan, PanModifier and Pointer Position, PrimaryClick, SecondaryClick actions. Bindings remain in the asset for future rebinding.
3. Keep raw device input in one Presentation component. Expose an input-state interface to the camera and interaction controllers.
4. Drive camera target translation, yaw and zoom with `Time.unscaledDeltaTime`, configurable serialized settings, normalized screen-edge thresholds, focus checks, and rectangular XZ bounds. Keep a fixed pitch for U05.
5. Use Cinemachine for the render camera while keeping navigation intent/controller independent of Cinemachine-specific input APIs.
6. Provide `TryGetWorldHit` / `TryGetWorldPoint` pointer-ray APIs with an optional EventSystem UI guard. Add only a debug selection marker using a renderer property block.
7. Add deterministic EditMode tests for navigation math and action-map contracts, PlayMode tests for scene composition/raycast/selection, and a U05 verifier that runs both suites plus the Windows development build.

## Acceptance gate
- Keyboard/edge/middle-drag pan, Q/E rotation and wheel zoom are configured and bounded.
- Navigation continues independently of simulation pause because it uses unscaled presentation time.
- Pointer raycast returns world hits and primary click selects a marker while respecting the UI guard.
- The local scene contains no vendor asset dependency and is the build scene.
- EditMode tests, PlayMode tests and Windows x64 Mono Development build pass.
