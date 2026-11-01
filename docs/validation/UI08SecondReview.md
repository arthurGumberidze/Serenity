# UI08 Second-Review Validation

Date: 2026-11-01
Unity: 6000.6.4f1
Candidate: `Builds/UI08SecondReview/Serenity.exe`

## Delivered scope

- Canonical 33-technology Stone Age graph: common root, Survival, Production, Military, Knowledge and Society
  branches, then a shared capstone.
- Full-screen top-down Research workspace whose complete graph and selected-detail card fit 1920x1080.
- Spouse-inclusive Dynasty projection.
- Military-only squad membership, two initial active squads and four after `military_organization`.
- Compact right-side character inspector with metric, wellbeing, activity and progressive-detail sections.
- No migration or merging of old research saves, per explicit user direction.

## Commands and results

All Unity commands used the repository-owned persistent runner and its single Editor process.

```powershell
.\Tools\UnityRunner.ps1 compile -OutputPath Artifacts\UI08SecondReview\compile-timeout-final -TimeoutSeconds 900
```

Result: succeeded; errors 0, warnings 45 inherited/import warnings.

```powershell
.\Tools\UnityRunner.ps1 test -TestMode EditMode `
  -TestNames 'Game.Tests.Editor.ResearchFrameworkTests','Game.Tests.Editor.MilitaryUnitTests','Game.Tests.Editor.UI08UserFeedbackTests' `
  -OutputPath Artifacts\UI08SecondReview\editmode-targeted-final2 -TimeoutSeconds 1800
```

Result: 34 passed, 0 failed.

```powershell
.\Tools\UnityRunner.ps1 test -TestMode PlayMode `
  -TestNames 'Game.Tests.PlayMode.GameplayUiPlayModeTests.UserFeedbackWorkspacesRenderTreesRecruitmentAndBlockTheWorldPointer' `
  -OutputPath Artifacts\UI08SecondReview\playmode-layout-1920-final -TimeoutSeconds 1800
```

Result: 1 passed, 0 failed. The test sets 1920x1080 and fails closed unless all five branch columns remain inside
the research viewport horizontally and the `stone_age_mastery` capstone remains inside it vertically. This is the
final pass after two useful intermediate failures measured the original capstone overflow at 1305.314 and then
733.677 against a 709.921 viewport boundary.

```powershell
.\Tools\UnityRunner.ps1 test -TestMode PlayMode `
  -TestNames 'Game.Tests.PlayMode.GameplayUiPlayModeTests.SelectionInspectorAndDeveloperToggleRemainAdapters' `
  -OutputPath Artifacts\UI08SecondReview\playmode-character-final2 -TimeoutSeconds 1800
```

Result: 1 passed, 0 failed. The first attempt reached the Unity Test Framework's default 180-second timeout while
the heavy scene was still loading; the assertion set was unchanged and a test-local 300-second timeout allowed
the same route to finish in 218.400 seconds.

```powershell
.\Tools\UnityRunner.ps1 build -BuildPath Builds\UI08SecondReview\Serenity.exe `
  -OutputPath Artifacts\UI08SecondReview\build-final -TimeoutSeconds 1800
.\Tools\UnityRunner.ps1 build -BuildPath Builds\UI08SecondReview\Serenity.exe `
  -OutputPath Artifacts\UI08SecondReview\build-final-stable -TimeoutSeconds 1800
```

The first pass deliberately failed the immutable-source gate after Unity wrote deterministic material keyword and
UMA index normalization. The immediate second pass made no further source changes and succeeded with 0 errors and
0 warnings. Candidate path: `Builds/UI08SecondReview/Serenity.exe`.

## Compatibility statement

The new catalog is authoritative for new games. No alias table, progress conversion, fallback completion or
combined save path was introduced for the old research graph. This matches the user's explicit instruction that
old saves are not needed.
