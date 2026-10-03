# Serenity

Unity URP environment bootstrap (U00). No gameplay systems implemented.

## Open
Use **Unity 6000.6.4f1**, revision `12bfff696524`. The user approved this installed version on 2026-10-03 in place of the original 6000.3 LTS target (D-008). Add this repository directory to Unity Hub and open `Assets/Scenes/SampleScene.unity`.

Install Git LFS before cloning, then run `git lfs install` and `git lfs pull`. Windows standalone support and an activated Unity license are required. Restore the pinned packages from `Packages/manifest.json` and `Packages/packages-lock.json`; do not commit Library or local IDE files.

## Verify and build
From PowerShell in the repository root:

```powershell
./Tools/Verify-U00.ps1
```

Use `-EditorPath` if the pinned Editor is installed elsewhere. The script runs three EditMode environment tests and builds a Windows x64 **Development / Mono** player. It stops on failed tests or a failed build. Logs and NUnit XML are written to `Logs/`; the executable is `Builds/Windows/Serenity.exe`.

Clean checkout verification (choose a new empty destination):

```powershell
git clone . Validation/U00-clean
git -C Validation/U00-clean lfs fsck
./Tools/Verify-U00.ps1 -ProjectPath ./Validation/U00-clean
```

This regenerates Library from tracked files. Registry packages may use the machine's global download cache; an activated Editor and Windows module are machine prerequisites.

Read `AGENTS.md` and the state documents in `docs/` before further work. The active task is defined only by `docs/NEXT_TASK.md`.
