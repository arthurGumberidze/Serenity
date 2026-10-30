# Ruler strategy AI validation — 2026-10-30

## Scope

Optional aggregate ruler planning through local Ollama or an OpenAI-compatible cloud endpoint, with
deterministic execution/fallback, bounded validation, user setting and strategic save/load persistence.

## Commands and results

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 compile -OutputPath Artifacts\RulerStrategy\compile-a -TimeoutSeconds 900
```

Result: succeeded, Unity 6000.6.4f1, 0 errors / 143 inherited warnings. The first refresh imported the current
large dirty asset tree and took 90.995 s.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 compile -OutputPath Artifacts\RulerStrategy\compile-b -TimeoutSeconds 900
```

Result: succeeded, 0 errors / 7 inherited warnings, 27.517 s.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 test -CategoryNames U26 -OutputPath Artifacts\RulerStrategy\u26-tests-e -TimeoutSeconds 1200
```

Result: 29/29 EditMode tests passed, 0 failed/skipped/inconclusive, 8.245 s. This includes aggregate context,
annual request, strict plan validation, critical-action precedence, UTF-8 Ollama/cloud payloads and strategy
save/XML/load round-trip.

The first U26 run was 28/29 because the new test constructed a future plan while leaving its synthetic save
checkpoint at tick zero. The fixture was corrected to reproduce the production save barrier; the focused
rerun passed 1/1 before the final 29/29 run.

## Real local provider smoke

One UTF-8 POST to `http://localhost:11434/api/chat` with `qwen3.5:4b`, `think=false`, `stream=false` and JSON
mode succeeded. The first Russian prompt produced valid JSON but translated machine IDs and therefore would be
rejected by the game validator. The revised English contract explicitly prohibited translating IDs. Its
response kept `stabilize_economy`, `neutral`, `primitive_shelter` and `storage_basket` unchanged, kept the
five workforce shares at 100 and wrote only the summary in Russian. CPU response time: 37.8 s.

No cloud request was made and no API key was read, printed, persisted or charged during validation.

## Delivery gate

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 compile -OutputPath Artifacts\RulerStrategy\compile-delivery -TimeoutSeconds 900
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 test -CategoryNames U26 -OutputPath Artifacts\RulerStrategy\u26-delivery -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 test -CategoryNames P2 -OutputPath Artifacts\RulerStrategy\p2-delivery -TimeoutSeconds 900
pwsh -NoProfile -ExecutionPolicy Bypass -File .\Tools\UnityRunner.ps1 build -BuildPath Builds\RulerStrategyAI\Serenity.exe -OutputPath Artifacts\RulerStrategy\build-delivery -TimeoutSeconds 3600
```

- Compile: succeeded, 0 errors / 143 inherited warnings, 37.618 s.
- U26: 29/29 passed, 0 failed/skipped/inconclusive, 11.709 s.
- P2: 5/5 passed, 0 failed/skipped/inconclusive, 6.368 s.
- Windows x64 Development build: succeeded, 0 errors / 52 inherited warnings, 56.504 s.
- Build: `Builds/RulerStrategyAI/Serenity.exe`.

An attempted combined `-CategoryNames U26,P2` filter selected zero tests because the runner treats multiple
categories as an intersection; it failed closed as designed. Both categories were then run separately above.
No Player runtime smoke was used for this delivery; real provider verification was the bounded local Ollama API
smoke, while runtime composition was compiled into the successful Development build.
