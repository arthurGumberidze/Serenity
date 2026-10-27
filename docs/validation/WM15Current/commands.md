# WM15 current feedback — commands and evidence

Working directory: `C:\serenity_game`. Unity: Unity 6000.6.4f1 at
`C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.
Accepted Unity invocations used hidden `Start-Process -PassThru -Wait` execution outside the sandbox so
the normal local Unity Personal profile was visible.

- `editor-batch-b`: the exact focused command is recorded in
  `docs/world_map/WM15_CURRENT_FEEDBACK.md`; exit 0, 9/9 passed.
- `build-batch-c`: Windows Development build, exit 0, 0 errors / 30 warnings. The build provenance copied
  beside this file records GUID and immutable source fingerprint.
- `player-a`: disposable-save WM15 October/evening route, exit 0, 32/32 passed, no runtime errors.
- No full 100/300/500 population/performance gates were run.

`author-a` is rejected evidence: it ran inside the sandbox, created a licensing client with a different
machine identity and exited 198 before executing project code. `build-batch-b` was operator-interrupted
after compilation and is also not acceptance evidence.
