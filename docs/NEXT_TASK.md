# NEXT_TASK.md

The active product task is P7: finish the connected Stone Age game. P1 through P6 are DONE at their implementation/evidence boundaries. Begin from the committed P6 first-hour route and make one coherent campaign from settlement economy, construction, research, weather, health/family and succession through scouting, supplied war, battle, occupation/capture, victory or defeat and continued play. Do not begin P8.

Implement the user-approved symmetric undefended-settlement occupation rule recorded in `PRODUCT_ROADMAP_RU.md` and D-106: defenders require combat; without defenders an arriving hostile army begins a canonical, data-driven occupation; arriving defenders can interrupt it; ownership changes exactly once only after completion and then evaluates the common state-defeat rule. Occupation progress and interruption must persist through Save/Load and remain identical for player and AI. Resolve duration, defender eligibility, progress reset/retention, claimant loss and same-tick defender-arrival ordering explicitly in data/tests before claiming completion; do not fabricate a battle or add UI-only state.

Balance and expose the complete first-era route: stable food/work/building progression, research unlocks, visible birth/age/succession feedback, independent AI growth and supply, discovery, RTS/TPS battle, capture, victory/defeat, continuation and menu/restart/load. Commands and outcomes must continue through existing canonical services. Preserve P4 appearance, P5 world/resource/map identity and P6 optional guidance across Save/Load. No new Asset Store dependency without approval.

Completion requires focused P7 tests, affected gameplay/persistence regressions, a Windows Development build and a real visible Player campaign route covering both defended combat and undefended occupation, interruption, Save/Load mid-occupation, victory/defeat and continuation. Record remaining visual/balance limitations and keep P3/P5 visual plus P6 usability acceptance explicitly open for the user. Update project docs and commit with a `P7:` prefix. Do not reinterpret P7 evidence as P8 performance/endurance acceptance.

## Deferred technical gates for P8

1. U29 remains PARTIAL: the retained 1000-Character sustained control is 38.216 ms p95 versus the 33.33 ms gate. Run the complete qualifying matrix only in P8.
2. U30 remains IN_PROGRESS. Preserve `C:\serenity_u30_soak_20261012_run4`; do not launch another eight-hour run without separate authorization before P8.
3. U31 remains BLOCKED on U29/U30 even though its existing integration tests pass. Finalize it only after P7 and qualifying P8 evidence.

Do not modify existing U29/U30 evidence, run U29 final qualification, launch a new eight-hour U30, or mark U31 DONE while implementing P7.
