# U03 — Игровое время и календарь

## Result
U03 is complete. Pure Domain/Simulation code now provides deterministic calendar and biological clocks, the five supported speeds, active pause, calendar rollover helpers and persistence-ready clock state. No U04 persistence implementation, SQL, Character/Dynasty gameplay, MonoBehaviour driver or DOTS system was added.

`GameTimeState` is canonical mutable Domain state. It stores calendar ticks, biological ticks, selected speed and pause state using primitive serializable fields. `GameClock` validates restored state and advances it from an explicit `TimeSpan` supplied by a future session-level driver. At x1, one real second advances one calendar minute, so 24 real minutes produce one calendar day. Biological time is stored and returned separately, using a configurable default 400x multiplier.

Checked integer arithmetic computes both next values before committing either one. Invalid speed, negative elapsed time and overflow are rejected without partial state mutation. The design produces identical state for one large step and equivalent smaller steps and does not reference UnityEngine, wall-clock time, camera state or `Time.timeScale`.

## Verification command
Executed from `C:\serenity_game` in PowerShell:

```powershell
./Tools/Verify-U03.ps1
```

The verifier invoked:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U03-tests.log -runTests -testPlatform EditMode -testResults C:\serenity_game\Logs\U03-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U03-build.log -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
```

## Results
- EditMode: 35/35 passed in 1.180 s, including all prior 20 tests and 15 U03 tests.
- Windows x64 / Mono / Development: exit 0; `Succeeded; errors=0; warnings=2`.
- Player: `Builds/Windows/Serenity.exe`, 667136 bytes.
- Both build warnings existed after U02. There are no U03 warnings or errors. An earlier asset recompile also reported an obsolete API in the user's untracked Hodaart asset; it is outside U03 and was not staged.
- The first sandboxed Unity launch crashed before tests because its local BuildReport REST socket could not start. A permitted normal launch then exposed one malformed new meta GUID; it was corrected, and the complete verification passed.

## U03 test coverage
- x1 maps 24 real minutes to one calendar day.
- x1, x2, x3, x5 and x10 multiply subsequent advancement correctly.
- Pause prevents both clocks from advancing; resume retains state and selected speed.
- Switching speed affects only later advancement.
- Biological elapsed time is separate and applies the configured multiplier.
- A large step equals the same duration divided into small steps.
- Reconstructed state continues deterministically.
- Day/year rollover and calendar fields are correct.
- Invalid speed, negative delta and overflow cannot partially mutate state.

## Deferred balance issue
The explicit U03 requirement says biological time is roughly 300–500x calendar time, so the default is 400x. The older FRS value of about nine real minutes per biological year implies a different coefficient. Character/Dynasty balancing must reconcile that number later. Pregnancy is a separate timer by design and was not implemented in U03.
