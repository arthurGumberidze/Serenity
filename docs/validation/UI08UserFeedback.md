# UI08 user-feedback workspace repair

Date: 2026-11-01

Unity: 6000.6.4f1

Candidate: `Builds/UI08UserFeedback/Serenity.exe`

## Scope and result

- Research now renders by prerequisite depth with visible stage connectors instead of a flat card list.
- Dynasty now renders members by parent-derived generation with visible generation connectors.
- Military has a virtualized in-workspace recruit roster and transient draft selection; the obscured world no
  longer needs to be clicked before creating a unit.
- Settlement no longer enumerates every active production site/source.
- The top HUD no longer renders one permanent card for every resident. It shows the selected resident and a
  bounded `Жители · N` route to People.
- The internal `water` resource remains compatible with saves/simulation but is excluded from player-managed
  chips, stock lists, inventories, storage filters and target editors.
- UI Toolkit picking consumes pointer input over nested menu content, so scrolling a menu cannot zoom the map.
- `cook_fish` already produced `cooked_fish` and was already configured for campfire and stone-age kitchen; an
  explicit regression test now protects both definitions.

No gameplay, selection, research, dynasty, military, resource or save authority was added.

## Commands and final evidence

All commands used the project persistent Unity runner:

```powershell
.\Tools\UnityRunner.ps1 compile -OutputPath Artifacts\UI08UserFeedback\compile-playmode-long-wait -TimeoutSeconds 900
.\Tools\UnityRunner.ps1 test -TestMode EditMode -CategoryNames UI08 -OutputPath Artifacts\UI08UserFeedback\editmode-ui08 -TimeoutSeconds 900
.\Tools\UnityRunner.ps1 test -TestMode EditMode -TestNames Game.Tests.Editor.UI03WorldHudTests -OutputPath Artifacts\UI08UserFeedback\editmode-ui03 -TimeoutSeconds 900
.\Tools\UnityRunner.ps1 test -TestMode EditMode -TestNames Game.Tests.Editor.UI04ManagementWorkspaceTests -OutputPath Artifacts\UI08UserFeedback\editmode-ui04 -TimeoutSeconds 900
.\Tools\UnityRunner.ps1 test -TestMode EditMode -CategoryNames UI05 -OutputPath Artifacts\UI08UserFeedback\editmode-ui05-rerun -TimeoutSeconds 900
.\Tools\UnityRunner.ps1 test -TestMode PlayMode -TestNames Game.Tests.PlayMode.GameplayUiPlayModeTests.UserFeedbackWorkspacesRenderTreesRecruitmentAndBlockTheWorldPointer -OutputPath Artifacts\UI08UserFeedback\playmode-feedback-final -TimeoutSeconds 1200
.\Tools\UnityRunner.ps1 build -BuildPath Builds\UI08UserFeedback\Serenity.exe -OutputPath Artifacts\UI08UserFeedback\build-stable -TimeoutSeconds 1800
```

Final results:

- compile: 0 errors; 45 inherited obsolete-API warnings;
- UI08 EditMode: 7/7;
- UI03 EditMode: 2/2;
- UI04 EditMode: 6/6;
- UI05 EditMode: 4/4;
- relevant EditMode total: 19/19;
- focused full-world PlayMode: 1/1 in 106.26 seconds;
- Windows Development build: succeeded, 0 errors / 0 warnings.

The initial UI05 run exposed an obsolete source-shape assertion and passed 4/4 after updating it to the local
recruit-selection contract. The initial PlayMode test waited only for a short fixed boot interval; after using the
project's real composition-readiness window, the same route passed. The first build correctly failed with
`Source changed during build; rebuild for immutable provenance` because Unity 6 deterministically migrated several
old material records and the UMA asset index. Those generated records were retained, and the immediate second
build passed without any further source rewrite.
