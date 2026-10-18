# P10.6 handoff — Home, sleep, adult relationships and P10 Save/Load

Status: **DONE** on 2026-10-18. `NEXT_TASK.md` advances to P10.7. No P10.7 military/capture rule or P10.8 diagnostic code was implemented.

## Delivered

- `DomesticService` is the single engine-free authority for eligible homes, indoor closed/habitable bounds, owner/resident access, beds, assignment and occupancy.
- Tier1 sleep walks to the assigned bed and recovers energy only after arrival. Invalid/outdoor, incomplete/open/uninhabitable, occupied, foreign and unassigned cases have explicit denial reasons. Safe P10.3 work cancellation requeues; protected carrying/direct control is not interrupted.
- Adult family interaction requires two independent consent decisions, living adults, mutual spouse links, relationship and health eligibility. It reuses U18 relationship and pregnancy state and presents only an implicit fade with «Пропустить».
- Snapshot schema advances monotonically from v22 to v23. The appended payload round-trips domestic state and every managed Character's tier, position, needs and off-camera continuity; established appearance/role/age, work queue, health/pregnancy and connected-world payloads remain authoritative.
- P10 gameplay requires v20+ and `p10-connected-world-v1`. Primary slots live under `Serenity/P10/Saves`; P9 v1–19 are listed from the old directory only as read-only metadata and cannot be Continue/migrate/delete/overwrite targets.
- Normal UI has a player-facing «Дом и семья» route for bed status, sleep, the two consents, fade/skip and pregnancy status. No developer-only family control exists.

## Automated evidence

Unity executable for every command: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.

- Focused EditMode: `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P106\focused-edit.log -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.P106DomesticAndSaveTests -testResults C:\serenity_game\Artifacts\P106\focused-edit.xml` — **5/5 passed**.
- Focused PlayMode: `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P106\focused-play.log -runTests -testPlatform PlayMode -testCategory P106 -testResults C:\serenity_game\Artifacts\P106\focused-play.xml` — **2/2 passed**.
- Affected P10.3–P10.6: `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P106\affected-play.log -runTests -testPlatform PlayMode -testCategory "P103|P104|P105|P106" -testResults C:\serenity_game\Artifacts\P106\affected-play.xml` — **10/10 passed**.
- U29 v23 save observation: `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P106\u29-save-play.log -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.U29PerformanceGatePlayModeTests.SaveObservationRoundTripsEveryNormalGameplayPayload -testResults C:\serenity_game\Artifacts\P106\u29-save-play.xml` — **1/1 passed**.

Broad diagnostics are retained without overstating them:

- `Artifacts/P106/accepted-edit.xml`: 552 passed and one existing P8.2 `NaturalMapKeepsFiniteBoundaryOnTheSupersedingTerrain` geography failure. The same failure reproduces in the isolated P8.2 focused run; P10.6 changes no scene/geography asset.
- `Artifacts/P106/full-play.xml`: 144/167 passed. It includes the U31 GPU test that explicitly rejects `-nographics` and long-standing cross-test scene/static-state isolation failures. The only new v23-specific failure was U29's observer coordinator missing the new services; it was fixed and its exact test then passed 1/1.

## Player evidence

The accepted Development Player ran windowed with forced D3D11 and exact 1920×1080. The smoke uses the ordinary Main Menu/New Game and UI Toolkit buttons for «Дом и семья», sleep, both consents, family event, fade skip, Save and Load.

`Artifacts/P106/p106-final-pass/p106-player-smoke.json` records `passed=true`; all 13 gates are true, including outdoor-bed denial, one-sided-consent denial, mutual consent, pregnancy, v23, generation ID and Save/Load continuity of domestic, pregnancy, Tier/needs, appearance and work state. The accepted log is `Artifacts/P106/p106-final-pass.log` and contains `P106_PLAYER_SMOKE PASS`.

Screenshots, visually inspected at 1920×1080:

- `Artifacts/P106/p106-final-pass/p106-domestic-sleep.png`
- `Artifacts/P106/p106-final-pass/p106-family-fade.png`
- `Artifacts/P106/p106-final-pass/p106-after-load.png`

## Build

Final command:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -executeMethod U00Build.WindowsDevelopment -serenityBuildPath C:\serenity_game\Builds\P106-final\Serenity.exe -logFile C:\serenity_game\Artifacts\P106\final-build.log -quit`

Result: **Succeeded; errors=0; warnings=44**. Player: `Builds/P106-final/Serenity.exe`.

- Unity: `6000.6.4f1`
- Build GUID: `c3d51b9c7faf4efb984907959031228c`
- Source commit at build start: `81b59b1f664ed7e29f3ec7f9e6fc0af34ed6ab14`
- Source fingerprint SHA-256: `FCC9D77988714711527436D4481C4F4DB01A19562B533DAF8386F5BE5A12B8A2`
- Created UTC: `2026-10-18T15:27:02.4937317Z`
- Provenance: `Builds/P106-final/build-provenance.json`; before/after source manifests matched.

## Preserved debt

P10.3 and P10.4 remain PARTIAL exactly as recorded in D-141/D-143 and their handoffs. This task does not claim the missing manual Shift/drag/action/Tier routes or P10.4's inherited physical-walk and Player-evidence gates. P10.2 p95/route-strip debt remains for P10.9. P8.6 remains PARTIAL, U29 PARTIAL, U30 IN_PROGRESS and U31 BLOCKED.
