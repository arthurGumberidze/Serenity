# P10.3 validation evidence

- `p103-editmode.xml`: focused P10.3 EditMode, 17/17 passed.
- `p103-playmode.xml`: focused P10.3 PlayMode, 2/2 passed.
- `p103-editmode-ui-shield.xml`: post-visible-route context-menu click-through regression, 17/17 passed.
- `p103-playmode-ui-load.xml`: post-visible-route production UI Load continuity regression, 2/2 passed.
- `affected-editmode.xml`: queues/AI/tiers/persistence/P10.2 compatibility, 92/92 passed.
- `affected-playmode.xml`: P10.3/P10.2/P9.4/TPS compatibility, 19/19 passed.
- `full-editmode-diagnostic.xml`: broad diagnostic run, 536/538 before correcting the stale P10.2 version assertion; the other failure is the independently reproducible P8.2 geography test.
- `full-playmode-diagnostic.xml`: broad headless diagnostic run, 141/159; see `docs/P103_HANDOFF.md` for conflict categories.

The final Windows Development build from source commit `ea55dd712d9f84dc39699abe9f067eb8a289dbdd` succeeded with 0 errors and 26 warnings. `build-provenance.json` records fingerprint `C28A07678081694B4DC7305A238BE3A05B89207D9E2CAE1C9D4AC008F2A38343` and build GUID `6b60fff2e13a490d96195ffbf0ba616e`.

Visible real-mouse evidence:

- `02-move-order-selection-preserved.jpg`: ordinary Move produces one order and preserves the selected resident after the click-through fix.
- `03-edge-clamped-menu-disabled-reasons.jpg`: eight-action context menu remains reachable at the right edge and keeps disabled reasons visible.
- `04-selection-preserved-after-ui-load.jpg`: production UI Save/Load preserves the selected stable resident after the Load fix.
- `player-final3.log`: final D3D11 Player log; no exception/assertion/crash diagnostic matched.

Windows DPI scaling produced 1282×751 logical application-window JPEGs. The Player launch requested exact 1920×1080; the file dimensions are not misreported as physical 1920×1080 captures.

P10.3 remains PARTIAL because physical Shift-click/Shift-command, slow multi-frame drag, needs/protected-carry resume, Tier1→Tier2→Tier1 and the rest of the target/action matrix still require the human route in `docs/P103_HANDOFF.md`.
