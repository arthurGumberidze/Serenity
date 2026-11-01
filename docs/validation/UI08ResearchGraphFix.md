# UI08 Research Graph Readability Fix

Date: 2026-11-01
Unity: 6000.6.4f1
Candidate: `Builds/UI08ResearchGraphFix/Serenity.exe`

## Scope

- Render canonical research dependencies as behind-card branch rails with visible node spacing.
- Keep the five specialization columns and shared capstone readable at 1920x1080.
- Preserve every canonical prerequisite; list secondary cross-branch requirements in the fixed detail card.
- Remove technical empty-list labels and the future squad-limit hint from Military.
- Replace white character progress controls with the existing Black and Gold tokens.
- Do not merge or migrate old research saves.

## Persistent-runner evidence

All Unity work used `Tools/UnityRunner.ps1` and one persistent Editor process.

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath 'Artifacts\UI08ResearchGraphFix\compile-readable-graph' -TimeoutSeconds 900
```

Succeeded: 0 errors, 0 warnings.

```powershell
& .\Tools\UnityRunner.ps1 test -TestMode EditMode `
  -TestNames 'Game.Tests.Editor.ResearchFrameworkTests','Game.Tests.Editor.MilitaryUnitTests','Game.Tests.Editor.UI08UserFeedbackTests' `
  -OutputPath 'Artifacts\UI08ResearchGraphFix\editmode-readable-graph' -TimeoutSeconds 1200
```

Succeeded: 34/34.

```powershell
& .\Tools\UnityRunner.ps1 test -TestMode PlayMode `
  -TestNames 'Game.Tests.PlayMode.GameplayUiPlayModeTests.UserFeedbackWorkspacesRenderTreesRecruitmentAndBlockTheWorldPointer' `
  -OutputPath 'Artifacts\UI08ResearchGraphFix\playmode-readable-graph' -TimeoutSeconds 1200
```

Succeeded: 1/1. Verified connector projection, locked/completed state classes, 1920x1080 branch/capstone bounds,
Military copy cleanup, and pointer blocking.

```powershell
& .\Tools\UnityRunner.ps1 test -TestMode PlayMode `
  -TestNames 'Game.Tests.PlayMode.GameplayUiPlayModeTests.SelectionInspectorAndDeveloperToggleRemainAdapters' `
  -OutputPath 'Artifacts\UI08ResearchGraphFix\playmode-character-readable' -TimeoutSeconds 1200
```

Succeeded: 1/1. Verified dark canvas/gold fill progress styling in the live inspector.

After adding visible inter-node spacing:

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath 'Artifacts\UI08ResearchGraphFix\compile-final-spacing' -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 test -TestMode PlayMode `
  -TestNames 'Game.Tests.PlayMode.GameplayUiPlayModeTests.UserFeedbackWorkspacesRenderTreesRecruitmentAndBlockTheWorldPointer' `
  -OutputPath 'Artifacts\UI08ResearchGraphFix\playmode-final-spacing' -TimeoutSeconds 1200
```

Succeeded: compile 0/0 and PlayMode 1/1; the full tree still fits 1920x1080.

## Build and visual review

The first build after each Unity import correctly failed the immutable-source gate when the deterministic UMA
AssetIndexer changed. Immediate stable rebuilds succeeded. Final command:

```powershell
& .\Tools\UnityRunner.ps1 build `
  -BuildPath 'Builds\UI08ResearchGraphFix\Serenity.exe' `
  -OutputPath 'Artifacts\UI08ResearchGraphFix\build-final-spacing-stable' -TimeoutSeconds 1800
```

Succeeded: 0 errors, 0 warnings.

The exact built Player was opened through Main Menu -> New Game -> Research. Visual review rejected the initial
all-edge foreground rendering because it produced crossing lines. The corrected build showed the common root,
clean branch-leading rails behind readable cards and five endpoint lines converging at the capstone. A final
2-pixel node margin was then covered by the final 1920x1080 PlayMode pass and immutable build above. Attempting
Continue demonstrated the expected old-research-save incompatibility; it was not migrated or merged, and visual
review continued through a clean new game.
