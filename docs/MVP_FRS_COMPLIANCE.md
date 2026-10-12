# MVP FRS compliance matrix

| FRS requirement | Owning system | Player-facing path | Persistence | Automated evidence | Status | Remaining gate |
|---|---|---|---|---|---|---|
| Start a new game with 10 player Characters and dynasty | U30A `NewGameBootstrap`, Character registry, U17 | Save panel -> New Game -> confirmation | Deterministic snapshot-v15 session/IDs | U30A focused EditMode bootstrap tests; repeated New Game PlayMode test | DONE | U31 end-to-end UX pass |
| Save and load normal play | U04 `SaveCoordinator`, U27 facade/UI | Save panel slots | Snapshot v15; v1-v14 compatible | 35 persistence tests; U30A Save/Load PlayMode | DONE | U31 full journey |
| Discover rival AI state | U25 contact + U30A `CampaignProgressService` | Global map hides rival detail until contact, then notification/detail | Discovery marker/tick/audit in v15 | U30A discovery exactly-once and round-trip tests | DONE | U31 journey timing/clarity |
| AI funds construction and acts independently | U26 scheduler over U09/U15/U16 | Rival progresses through shared world systems | Existing v13 AI/economy/building state, carried in v15 | U30A-tagged Strategic AI construction tests plus critical regressions | DONE | U31 observed end-to-end path |
| Military supply and shortage | U21 `MilitaryUnitService`, U15 inventories, U25/U20 consumers | Military panel provision/resupply; travel/combat feedback | Per-unit v15 supply/checkpoints | Conservation, exact-once travel, representation, combat-shortage tests | DONE | U31 balance/UX acceptance |
| TPS aimed attack | U22 input/control -> U20 combat | Enter TPS, aim, left-click | Existing encounter/unit/health state plus v15 supply | GPU/headless TPS attack, pause, cooldown, identity tests | DONE | U31 full battle journey |
| Capture, victory and continued sandbox | U23 battle -> U26 capture/defeat -> U30A campaign | Victory banner; clock and Save/Load remain usable | Victory marker/tick/audit in v15 | U30A battle-to-capture-to-victory continuation PlayMode | DONE | U31 full journey |
| 100/300/500/1000 Character performance gate | U29 | Normal visible Windows Player benchmark | Evidence artifacts, not save state | Latest sustained 1000 control p95 38.216 ms against 33.33 ms gate | PARTIAL | Qualifying U29 matrix must pass |
| Eight-hour reliability soak | U30 | Normal gameplay composition in external Player | Verified checkpoint bundles and append-only evidence | Short preflights and U30A 30-second soak pass; no audited passing eight-hour result | IN_PROGRESS | Fresh >=28,800-second run and finalizer audit |
| MVP integration/release gate | U31 | New Game through AI defeat, victory, continuation and reload | Must exercise snapshot v15 | Not yet executed as U31 | BLOCKED | U29 DONE and U30 DONE, then U31 full gate |
