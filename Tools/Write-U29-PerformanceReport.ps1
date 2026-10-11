param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsDirectory,
    [string]$OutputPath,
    [string]$VerificationLogDirectory,
    [int[]]$ExpectedPopulations = @(100, 300, 500, 1000),
    [int]$ExpectedRepeats = 3,
    [switch]$DoNotFailOnVerdict
)

$ErrorActionPreference = 'Stop'
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ResultsDirectory 'U29-DIAGNOSTIC-REPORT.md'
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$jsonOutputPath = [IO.Path]::ChangeExtension($OutputPath, '.json')

$hardP95Ms = 33.33
$targetP50Ms = 16.67
$softP50Ms = 20.0
$softP99Ms = 33.33
$softAllocationBytesPerFrame = 128KB
$managedGrowthLimit = 32MB
$workingSetGrowthLimit = 128MB
$softSaveTotalMs = 500.0
$fatalLogPatterns = @(
    'error CS\d+', 'Scripts have compiler errors', 'Unhandled Exception', 'NullReferenceException',
    'AssertionException', 'There are 2 audio listeners', 'Jobs safety', 'allocator.*leak',
    'Native Collection.*leak', 'device.*lost', 'Player crash', 'timed out',
    'invalid/missing result', 'ThemeStyleSheet.*(error|failed)', 'UI.*resource.*(error|failed)'
)

$issues = [Collections.Generic.List[object]]::new()
$good = [Collections.Generic.List[string]]::new()
$repeatRows = [Collections.Generic.List[object]]::new()
$rawRows = [Collections.Generic.List[object]]::new()
$allocationRows = [Collections.Generic.List[object]]::new()
$markerRows = [Collections.Generic.List[object]]::new()
$saveRows = [Collections.Generic.List[object]]::new()
$hardwareProfiles = [Collections.Generic.List[object]]::new()
$runtimeRows = [Collections.Generic.List[object]]::new()
$isSmoke = $false
$finalSummaryReadError = $null
$finalSummaryPath = Join-Path $ResultsDirectory 'U29-final-summary.json'
if (Test-Path -LiteralPath $finalSummaryPath) {
    try {
        $inputFinalSummary = Get-Content -LiteralPath $finalSummaryPath -Raw | ConvertFrom-Json
        $smokeProperty = $inputFinalSummary.PSObject.Properties['smoke']
        if ($null -ne $smokeProperty) { $isSmoke = [bool]$smokeProperty.Value }
    } catch {
        $finalSummaryReadError = $_.Exception.Message
    }
}

function Add-Issue([string]$Severity, [string]$Location, [string]$Observed,
    [string]$Limit, [string]$Cause, [string]$Recommendation) {
    $issues.Add([pscustomobject]@{
        severity = $Severity
        location = $Location
        observed = $Observed
        limit = $Limit
        probableCause = $Cause
        recommendation = $Recommendation
    })
}

function Format-Number([double]$Value, [string]$Pattern = '0.000') {
    return $Value.ToString($Pattern, [Globalization.CultureInfo]::InvariantCulture)
}

function Format-Mib([long]$Bytes) {
    return (Format-Number ($Bytes / 1MB) '0.0') + ' MiB'
}

function Get-PropertyValue($Object, [string]$Name, $Fallback = $null) {
    if ($null -eq $Object) { return $Fallback }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $Fallback }
    return $property.Value
}

if (-not [string]::IsNullOrWhiteSpace($finalSummaryReadError)) {
    Add-Issue 'BLOCKER' 'U29-final-summary.json' $finalSummaryReadError 'valid JSON required' `
        'Final aggregate is corrupt.' 'Regenerate the result directory with the offline runner.'
}
if ($null -ne $inputFinalSummary) {
    if (-not $isSmoke) {
        if ([double](Get-PropertyValue $inputFinalSummary 'warmupSeconds' 0) -lt 60) {
            Add-Issue 'BLOCKER' 'U29-final-summary.json' 'warm-up below 60 seconds' '>= 60 seconds' `
                'Mandatory configuration was shortened.' 'Use the offline runner defaults; do not override mandatory durations.'
        }
        if ([double](Get-PropertyValue $inputFinalSummary 'measurementSeconds' 0) -lt 300) {
            Add-Issue 'BLOCKER' 'U29-final-summary.json' 'measurement below 300 seconds' '>= 300 seconds' `
                'Mandatory configuration was shortened.' 'Use the offline runner defaults; do not override mandatory durations.'
        }
        if ([int](Get-PropertyValue $inputFinalSummary 'repeatCount' 0) -lt 3) {
            Add-Issue 'BLOCKER' 'U29-final-summary.json' 'fewer than three repeats' '>= 3 repeats' `
                'Mandatory configuration was shortened.' 'Use the offline runner defaults; do not override mandatory repeat count.'
        }
    }
    foreach ($aggregate in @(Get-PropertyValue $inputFinalSummary 'populations' @())) {
        if ($null -ne $aggregate -and -not [bool](Get-PropertyValue $aggregate 'memoryTrendPassed' $false)) {
            Add-Issue 'BLOCKER' "population-$($aggregate.population)" 'cross-repeat memory trend failed' `
                'bounded/noisy plateau required' 'Managed heap or working set grew monotonically across repeats.' `
                'Compare repeat end values; inspect retained managed/native objects and cache/subscription lifecycle.'
        }
    }
    foreach ($runnerFailure in @(Get-PropertyValue $inputFinalSummary 'runnerFailures' @())) {
        if ($null -eq $runnerFailure) { continue }
        $where = if ([int]$runnerFailure.repeatIndex -gt 0) {
            "population-$($runnerFailure.population)/repeat-$($runnerFailure.repeatIndex)"
        } else { "population-$($runnerFailure.population)" }
        Add-Issue 'BLOCKER' $where ([string]$runnerFailure.message) 'runner validation required' `
            'Runner detected a missing, stale, mismatched or invalid artifact.' `
            'Open the repeat player.log/summary.json and fix the first runner validation error.'
    }
}

function Get-LongestStreak([bool[]]$Flags) {
    $current = 0
    $longest = 0
    foreach ($flag in $Flags) {
        if ($flag) {
            $current++
            if ($current -gt $longest) { $longest = $current }
        } else {
            $current = 0
        }
    }
    return $longest
}

function Get-RawDiagnostics([string]$RawPath) {
    if (-not (Test-Path -LiteralPath $RawPath)) { return $null }
    $frames = @(Import-Csv -LiteralPath $RawPath)
    if ($frames.Count -eq 0) { return $null }
    $values = @($frames | ForEach-Object { [double]::Parse($_.frame_ms, [Globalization.CultureInfo]::InvariantCulture) })
    $overTarget = @($values | Where-Object { $_ -gt $targetP50Ms }).Count
    $overGate = @($values | Where-Object { $_ -gt $hardP95Ms }).Count
    $gateFlags = @($values | ForEach-Object { $_ -gt $hardP95Ms })
    return [pscustomobject]@{
        sampleCount = $values.Count
        overTargetCount = $overTarget
        overTargetPercent = 100.0 * $overTarget / $values.Count
        overGateCount = $overGate
        overGatePercent = 100.0 * $overGate / $values.Count
        longestOverGateStreak = Get-LongestStreak $gateFlags
        topSpikesMs = @($values | Sort-Object -Descending | Select-Object -First 10)
    }
}

function Get-FailureAdvice([string]$Reason) {
    switch -Regex ($Reason) {
        'p95_threshold_breach' { return @('Устойчивое превышение CPU/GPU frame budget либо фоновые процессы.', 'Сравнить main/render/GPU и marker breakdown; оптимизировать крупнейший hot path и повторить тот же workload.') }
        'runaway_managed_growth' { return @('Удерживаемые managed-объекты растут в обеих половинах окна.', 'Снять Memory Profiler snapshots start/mid/end; искать растущие коллекции, подписки и кэши без eviction.') }
        'runaway_working_set_growth' { return @('Процесс монотонно резервирует native/GPU память.', 'Сравнить hierarchy/entity counts и native allocations; проверить RenderTexture, Mesh, Material, Entity и NativeContainer lifecycle.') }
        'recurring_major_gc_storm' { return @('Частые сборки совпали с плохой tail latency.', 'Сортировать allocation breakdown, убрать LINQ/ToArray/string/UI rebuild из тиков, затем проверить p99.') }
        'unfocused_or_background_samples' { return @('Player терял foreground; Windows мог применить background throttling.', 'Не использовать ПК во время матрицы; запускать offline runner, отключить блокировку экрана и закрыть всплывающие приложения.') }
        'actual_population_or_representation_mismatch' { return @('Tier churn или materialization нарушили счётчики.', 'Проверить TierManager, Tier1 presenters, Tier2 entities и Tier3 registry на симметричные add/remove.') }
        'save_round_trip_failed|save.*identity|canonical.*round' { return @('Save/load потерял identity, tier либо canonical state.', 'Сравнить before/after hashes и секции snapshot; исправить restore/rebind без benchmark-only обходов.') }
        'insufficient_frame_samples|short_measurement|short_warmup|insufficient_repeats' { return @('Прогон оборван либо конфигурация короче обязательного протокола.', 'Повторить полную матрицу: 60 s warm-up, 300 s measurement, 3 repeats.') }
        'headless_or_missing_graphics|ui_disabled|camera_inactive' { return @('Запущен невалидный presentation path.', 'Использовать Windows Development Player без -batchmode/-nographics; проверить U27 UI и Main Camera.') }
        'missing_visual_evidence|missing_raw_evidence' { return @('Артефакт не создан, устарел или не прошёл hash check.', 'Проверить права на output directory, свободное место и ScreenCapture module/link.xml.') }
        'harness_exception' { return @('Benchmark завершился исключением.', 'Открыть player.log/failure.json, исправить первое исключение и повторить этот population.') }
        default { return @('Нарушен обязательный invariant или runner validation.', 'Открыть summary.json и player.log указанного repeat; исправить первопричину, не ослабляя gate.') }
    }
}

foreach ($population in $ExpectedPopulations) {
    for ($repeat = 1; $repeat -le $ExpectedRepeats; $repeat++) {
        $location = "population-$population/repeat-$repeat"
        $repeatDirectory = Join-Path $ResultsDirectory $location
        $summaryPath = Join-Path $repeatDirectory 'summary.json'
        $failurePath = Join-Path $repeatDirectory 'failure.json'
        $playerLog = Join-Path $repeatDirectory 'player.log'
        if (-not (Test-Path -LiteralPath $summaryPath)) {
            $detail = if (Test-Path -LiteralPath $failurePath) {
                $failure = Get-Content -LiteralPath $failurePath -Raw | ConvertFrom-Json
                (@(Get-PropertyValue $failure 'failureReasons' @()) -join '; ')
            } elseif (Test-Path -LiteralPath $playerLog) {
                'Player started but summary.json is absent (interrupted/crashed run).'
            } else {
                'Repeat was not started.'
            }
            Add-Issue 'BLOCKER' $location $detail 'summary.json required' `
                'Матрица неполна.' 'Запустить новый полный offline run; не смешивать partial и final directories.'
            continue
        }

        try { $result = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json }
        catch {
            Add-Issue 'BLOCKER' $location $_.Exception.Message 'valid JSON required' `
                'summary.json повреждён или записан не полностью.' 'Проверить диск и повторить repeat.'
            continue
        }

        if ($null -ne $result.hardware) { $hardwareProfiles.Add($result.hardware) }
        $repeatRows.Add([pscustomobject]@{
            Population = [int]$result.population
            Repeat = [int]$result.repeatIndex
            P50 = [double]$result.frame.p50Ms
            P95 = [double]$result.frame.p95Ms
            P99 = [double]$result.frame.p99Ms
            Maximum = [double]$result.frame.maximumMs
            Fps = [double]$result.frame.derivedAverageFps
            MainThreadAverage = [double]$result.frame.mainThreadAverageMs
            RenderThreadAverage = [double]$result.frame.renderThreadAverageMs
            GpuAverage = [double]$result.frame.gpuAverageMs
            AllocBytesPerFrame = [double]$result.memory.allocatedBytesPerFrame
            ManagedDelta = [long]$result.memory.managedHeapEndBytes - [long]$result.memory.managedHeapStartBytes
            WorkingSetDelta = [long]$result.memory.workingSetEndBytes - [long]$result.memory.workingSetStartBytes
            Passed = [bool]$result.passed
            FailureReasons = @($result.failureReasons) -join '; '
        })
        if ($null -ne $result.countsStart -and $null -ne $result.countsEnd) {
            $runtimeRows.Add([pscustomobject]@{
                Population = [int]$result.population
                Repeat = [int]$result.repeatIndex
                Tier1 = [int]$result.countsEnd.tier1
                Tier2 = [int]$result.countsEnd.tier2
                Tier3 = [int]$result.countsEnd.tier3
                GameObjectsStart = [int]$result.countsStart.gameObjects
                GameObjectsEnd = [int]$result.countsEnd.gameObjects
                UiSubscriptionsStart = [int]$result.countsStart.uiSubscriptions
                UiSubscriptionsEnd = [int]$result.countsEnd.uiSubscriptions
                TierChurnTransitions = [int]$result.operations.tierChurnTransitions
                CanonicalHash = [string]$result.operations.canonicalStateHashHex
            })
            if ([int]$result.countsEnd.gameObjects -ne [int]$result.countsStart.gameObjects) {
                Add-Issue 'BLOCKER' $location `
                    "GameObjects $($result.countsStart.gameObjects) -> $($result.countsEnd.gameObjects)" `
                    'start/end count must remain bounded and equal after churn' `
                    'Tier churn or restore leaked/destroyed presentation objects.' `
                    'Compare Tier1 presenter materialize/dematerialize lifecycle and restore cleanup.'
            }
            if ([int]$result.countsEnd.uiSubscriptions -ne [int]$result.countsStart.uiSubscriptions) {
                Add-Issue 'BLOCKER' $location `
                    "UI subscriptions $($result.countsStart.uiSubscriptions) -> $($result.countsEnd.uiSubscriptions)" `
                    'start/end subscriptions must be equal' `
                    'UI refresh/rebind added duplicate event subscriptions.' `
                    'Make subscription ownership idempotent and unsubscribe on rebind/destroy.'
            }
        }

        $raw = Get-RawDiagnostics (Join-Path $repeatDirectory ([string]$result.rawFramesFile))
        if ($null -ne $raw) {
            $rawRows.Add([pscustomobject]@{
                Population = [int]$result.population
                Repeat = [int]$result.repeatIndex
                Over16_67Percent = [double]$raw.overTargetPercent
                Over33_33Percent = [double]$raw.overGatePercent
                LongestOver33Streak = [int]$raw.longestOverGateStreak
                TopSpikesMs = @($raw.topSpikesMs)
            })
        }

        $allocationCalibrated=$false
        $diagnosticMetadata=Join-Path $repeatDirectory 'diagnostic-metadata.txt'
        if(Test-Path -LiteralPath $diagnosticMetadata){
            $allocationCalibrated=(Get-Content -LiteralPath $diagnosticMetadata -Raw) -match 'allocation_probe_supported=True'
        }
        if ($null -ne $result.allocationBreakdown -and -not $allocationCalibrated) {
            Add-Issue 'WARN' $location 'Per-thread allocation counter is unverified or failed calibration; subsection values omitted.' `
                '64 KiB self-test required' 'Zero returned by this Mono API is not allocation-free evidence.' `
                'Use total GC Allocated In Frame; do not attribute allocations using uncalibrated subsection values.'
        }
        if ($null -ne $result.allocationBreakdown -and $allocationCalibrated) {
            $allocation = $result.allocationBreakdown
            $allocationRows.Add([pscustomobject]@{
                Population = [int]$result.population
                Repeat = [int]$result.repeatIndex
                Tier1Scheduler = [double]$allocation.tier1SchedulerBytesPerSample
                SimulationCallback = [double]$allocation.simulationCallbackBytesPerSample
                MovementPresentation = [double]$allocation.movementPresentationBytesPerSample
                Tier2 = [double]$allocation.tier2BytesPerSample
                Tier3Research = [double]$allocation.tier3AndResearchBytesPerSample
                WorkersWeather = [double]$allocation.workersAndWeatherBytesPerSample
                CanonicalServices = [double]$allocation.canonicalServicesBytesPerSample
                PresentationTierPolicy = [double]$allocation.presentationAndTierPolicyBytesPerSample
                TotalSimulation = [double]$allocation.totalSimulationBytesPerSample
            })
        }
        foreach ($marker in @($result.profilerMarkers)) {
            if ($null -ne $marker -and [bool]$marker.available) {
                $markerRows.Add([pscustomobject]@{
                    Population = [int]$result.population
                    Repeat = [int]$result.repeatIndex
                    Name = [string]$marker.name
                    AverageMs = [double]$marker.averageMs
                    MaximumMs = [double]$marker.maximumMs
                })
            }
        }

        if ($null -ne $result.save) {
            $saveTotal = [double]$result.save.captureMs + [double]$result.save.xmlSerializeMs +
                [double]$result.save.fileWriteMs + [double]$result.save.readDeserializeMs + [double]$result.save.restoreMs
            $saveRows.Add([pscustomobject]@{
                Population = [int]$result.population
                Repeat = [int]$result.repeatIndex
                PayloadBytes = [long]$result.save.payloadBytes
                CaptureMs = [double]$result.save.captureMs
                SerializeMs = [double]$result.save.xmlSerializeMs
                WriteMs = [double]$result.save.fileWriteMs
                ReadDeserializeMs = [double]$result.save.readDeserializeMs
                RestoreMs = [double]$result.save.restoreMs
                TotalMs = $saveTotal
                Identity = [bool]$result.save.exactIdentityTierRoundTrip
                Canonical = [bool]$result.save.exactCanonicalStateRoundTrip
                BeforeHash = [string]$result.save.canonicalHashBeforeRestoreHex
                AfterHash = [string]$result.save.canonicalHashAfterRestoreHex
            })
            if (-not [bool]$result.save.exactIdentityTierRoundTrip -or -not [bool]$result.save.exactCanonicalStateRoundTrip) {
                Add-Issue 'BLOCKER' $location 'Save/load hashes or identities differ' 'exact round-trip required' `
                    'Restore/rebind changed canonical state.' 'Diff snapshot sections and StableEntityId/tier maps; fix persistence before optimization.'
            } elseif ($saveTotal -gt $softSaveTotalMs) {
                Add-Issue 'WARN' $location ((Format-Number $saveTotal) + ' ms total save observation') `
                    ("diagnostic guideline <= {0} ms" -f $softSaveTotalMs) `
                    'XML serialization or restore is expensive at this population.' 'Inspect the largest save phase; reduce redundant XML/state copies without changing schema semantics.'
            }
        }

        foreach ($reason in @($result.failureReasons)) {
            $advice = Get-FailureAdvice ([string]$reason)
            Add-Issue 'BLOCKER' $location ([string]$reason) 'mandatory invariant' $advice[0] $advice[1]
        }
        if ([double]$result.frame.p95Ms -gt $hardP95Ms) {
            $mainAverage = [double]$result.frame.mainThreadAverageMs
            $gpuAverage = [double]$result.frame.gpuAverageMs
            $renderAverage = [double]$result.frame.renderThreadAverageMs
            $availableMarkers = @($result.profilerMarkers | Where-Object { $null -ne $_ -and [bool]$_.available })
            $topMarker = $availableMarkers | Sort-Object { [double]$_.averageMs } -Descending | Select-Object -First 1
            if ($mainAverage -le 0 -and $gpuAverage -le 0) {
                $bound = 'CPU/GPU recorder data unavailable; use raw spikes and allocation breakdown.'
            } elseif ($gpuAverage -gt ($mainAverage * 1.25)) {
                $bound = "Likely GPU-bound: GPU avg $(Format-Number $gpuAverage) ms vs main-thread avg $(Format-Number $mainAverage) ms."
            } elseif ($mainAverage -gt ($gpuAverage * 1.25)) {
                $bound = "Likely CPU/main-thread-bound: main avg $(Format-Number $mainAverage) ms vs GPU avg $(Format-Number $gpuAverage) ms."
            } else {
                $bound = "Likely mixed/serialized CPU-GPU pressure: main $(Format-Number $mainAverage) ms, render $(Format-Number $renderAverage) ms, GPU $(Format-Number $gpuAverage) ms."
            }
            if ($null -ne $topMarker) {
                $bound += " Largest available marker: $($topMarker.name), avg $(Format-Number $topMarker.averageMs) ms, max $(Format-Number $topMarker.maximumMs) ms."
            }
            Add-Issue 'BLOCKER' $location ((Format-Number $result.frame.p95Ms) + ' ms p95') `
                ("<= {0} ms mandatory" -f $hardP95Ms) $bound `
                'Optimize the identified dominant path under identical workload; then rerun all three repeats.'
        }
        if ([double]$result.frame.p50Ms -gt $softP50Ms) {
            Add-Issue 'WARN' $location ((Format-Number $result.frame.p50Ms) + ' ms p50') `
                ("target {0} ms; diagnostic warning > {1} ms" -f $targetP50Ms, $softP50Ms) `
                'Типичный кадр не удерживает 60 FPS.' 'Сначала определить CPU/GPU bound по recorder averages, затем оптимизировать устойчивый hot path.'
        }
        if ([double]$result.frame.p99Ms -gt $softP99Ms) {
            Add-Issue 'WARN' $location ((Format-Number $result.frame.p99Ms) + ' ms p99') `
                ("diagnostic guideline <= {0} ms" -f $softP99Ms) `
                'Редкие spikes: GC, save/churn, shader/asset work или OS interruption.' 'Сопоставить raw top spikes с GC counters, marker maxima и player.log timestamps.'
        }
        if ([double]$result.memory.allocatedBytesPerFrame -gt $softAllocationBytesPerFrame) {
            $allocationCause = 'High continuous managed allocation increases GC spike risk.'
            if ($null -ne $result.allocationBreakdown -and $allocationCalibrated) {
                $sources = [ordered]@{
                    Tier1Scheduler = [double]$result.allocationBreakdown.tier1SchedulerBytesPerSample
                    SimulationCallback = [double]$result.allocationBreakdown.simulationCallbackBytesPerSample
                    MovementPresentation = [double]$result.allocationBreakdown.movementPresentationBytesPerSample
                    Tier2 = [double]$result.allocationBreakdown.tier2BytesPerSample
                    Tier3Research = [double]$result.allocationBreakdown.tier3AndResearchBytesPerSample
                    WorkersWeather = [double]$result.allocationBreakdown.workersAndWeatherBytesPerSample
                    CanonicalServices = [double]$result.allocationBreakdown.canonicalServicesBytesPerSample
                    PresentationTierPolicy = [double]$result.allocationBreakdown.presentationAndTierPolicyBytesPerSample
                }
                $largest = $sources.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
                if ($null -ne $largest -and [double]$largest.Value -gt 0) {
                    $allocationCause += " Largest instrumented source: $($largest.Key), $(Format-Number $largest.Value '0.0') bytes/simulation sample."
                }
            }
            Add-Issue 'WARN' $location ((Format-Number ($result.memory.allocatedBytesPerFrame / 1KB) '0.0') + ' KiB/frame') `
                'diagnostic guideline <= 128 KiB/frame (not a hard gate)' `
                $allocationCause 'Remove allocations from the largest section first, then compare the identical workload.'
        }
        if ([int]$result.frame.unfocusedSampleCount -gt 0) {
            Add-Issue 'BLOCKER' $location "$($result.frame.unfocusedSampleCount) unfocused samples" '0 required' `
                'Player терял foreground.' 'Повторить offline без взаимодействия с ПК; закрыть notifications/overlays.'
        }
        if (Test-Path -LiteralPath $playerLog) {
            $fatal = @(Select-String -LiteralPath $playerLog -Pattern $fatalLogPatterns -CaseSensitive:$false)
            foreach ($match in $fatal | Select-Object -First 10) {
                Add-Issue 'BLOCKER' $location ($match.Line.Trim()) 'no fatal log diagnostics' `
                    'Unity/Player сообщил runtime/compiler/device/leak error.' 'Исправить первое сообщение и повторить repeat; последующие ошибки могут быть каскадом.'
            }
        }
    }
}

foreach ($population in $ExpectedPopulations) {
    $rows = @($repeatRows | Where-Object { $_.Population -eq $population })
    if ($rows.Count -eq $ExpectedRepeats) {
        $worst = $rows | Sort-Object P95 -Descending | Select-Object -First 1
        if ($worst.P95 -le $hardP95Ms) {
            $good.Add("Population ${population}: все $ExpectedRepeats repeat завершены; worst p95=$(Format-Number $worst.P95) ms <= $hardP95Ms ms.")
        }
        if ((@($rows | Where-Object { $_.AllocBytesPerFrame -gt $softAllocationBytesPerFrame })).Count -eq 0) {
            $good.Add("Population ${population}: allocations <= 128 KiB/frame во всех repeats (диагностический ориентир).")
        }
    }
}
if ($saveRows.Count -gt 0 -and (@($saveRows | Where-Object { -not $_.Identity -or -not $_.Canonical })).Count -eq 0) {
    $good.Add("Save/load: все $($saveRows.Count) наблюдений сохранили exact StableEntityId/tier и canonical hash.")
}

$verificationRows = [Collections.Generic.List[object]]::new()
if (-not [string]::IsNullOrWhiteSpace($VerificationLogDirectory) -and (Test-Path -LiteralPath $VerificationLogDirectory)) {
    Get-ChildItem -LiteralPath $VerificationLogDirectory -Filter 'U29-*.xml' -File | Sort-Object Name | ForEach-Object {
        try {
            [xml]$xml = Get-Content -LiteralPath $_.FullName -Raw
            $run = $xml.'test-run'
            $verificationRows.Add([pscustomobject]@{
                Name = $_.BaseName
                Result = [string]$run.result
                Passed = [int]$run.passed
                Failed = [int]$run.failed
                Skipped = [int]$run.skipped
            })
            $postgresDiscoveryOnly = $_.BaseName -eq 'U29-postgres' -and
                [int]$run.failed -eq 0 -and [int]$run.total -ge 25
            if (([string]$run.result -ne 'Passed' -and -not $postgresDiscoveryOnly) -or [int]$run.failed -ne 0) {
                Add-Issue 'BLOCKER' $_.BaseName "$($run.failed) failed" '0 failed' `
                    'NUnit regression failed.' 'Open the matching XML/log, fix the first failure, rebuild and rerun the offline suite.'
            }
        } catch {
            Add-Issue 'BLOCKER' $_.Name $_.Exception.Message 'valid NUnit XML' `
                'Test result is incomplete/corrupt.' 'Repeat verification stage.'
        }
    }
}

$complete = $repeatRows.Count -eq ($ExpectedPopulations.Count * $ExpectedRepeats)
$blockers = @($issues | Where-Object { $_.severity -eq 'BLOCKER' })
$verdict = if (-not $complete) { 'INCOMPLETE' } elseif ($blockers.Count -gt 0) { 'FAIL' } elseif ($isSmoke) { 'SMOKE_PASS' } else { 'PASS' }
foreach($row in $repeatRows){
    $repeatBlockers=@($blockers|Where-Object {$_.location -eq "population-$($row.Population)/repeat-$($row.Repeat)"})
    if($repeatBlockers.Count -gt 0){
        $row.Passed=$false
        $row.FailureReasons=(@($row.FailureReasons)+@($repeatBlockers|ForEach-Object {$_.observed})|Where-Object {$_}) -join '; '
    }
}
$generatedUtc = [DateTime]::UtcNow.ToString('O')

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# U29 offline performance diagnostic')
$lines.Add('')
$lines.Add("Generated UTC: $generatedUtc")
$lines.Add('')
$lines.Add("Verdict: **$verdict**")
$lines.Add('')
$lines.Add($(if ($isSmoke) {
    'This is a diagnostic capture, not the acceptance matrix. SMOKE_PASS validates the harness only and cannot complete U29.'
} else {
    'This is a mandatory-gate report; PASS requires the complete 100/300/500/1000 x3 matrix.'
}))
$lines.Add('')
$lines.Add("Completed repeats: $($repeatRows.Count) / $($ExpectedPopulations.Count * $ExpectedRepeats). A PASS is possible only for a complete matrix.")
$lines.Add('')
$lines.Add('## Что считается хорошим результатом')
$lines.Add('')
$lines.Add('| Показатель | Хорошо / целевое значение | Gate |')
$lines.Add('|---|---:|---:|')
$lines.Add('| p50 frame time | около или ниже 16.67 ms (60 FPS); до 20 ms помечается как допустимый диагностический диапазон | не hard gate |')
$lines.Add('| worst-repeat p95 | чем ниже, тем лучше | **<= 33.33 ms обязательно** |')
$lines.Add('| p99 | <= 33.33 ms желательно; превышение означает заметные редкие spikes | warning |')
$lines.Add('| GC allocation | <= 128 KiB/frame — ориентир для этого prototype, ниже лучше | warning; hard gate — отсутствие GC storm/runaway |')
$lines.Add('| managed heap | plateau/noisy bounded, без двух последовательных значимых ростов > 32 MiB суммарно | hard gate |')
$lines.Add('| process working set | plateau/noisy bounded, без двух последовательных значимых ростов > 128 MiB суммарно | hard gate |')
$lines.Add('| foreground samples | 100% focused | hard gate |')
$lines.Add('| population/tier | точные 25/50/25, unique StableEntityId, matching presenters/entities/records | hard gate |')
$lines.Add('| save/load | exact identity+tier и exact canonical hash | hard gate |')
$lines.Add('| save total | <= 500 ms — диагностический ориентир, не gameplay gate | warning |')
$lines.Add('')

if ($hardwareProfiles.Count -gt 0) {
    $h = $hardwareProfiles[0]
    $lines.Add('## Hardware / build')
    $lines.Add('')
    $lines.Add("- OS: $($h.operatingSystem)")
    $lines.Add("- CPU: $($h.processor), logical processors: $($h.logicalProcessorCount)")
    $lines.Add("- RAM: $($h.systemMemoryMb) MiB")
    $lines.Add("- GPU: $($h.graphicsDevice) ($($h.graphicsDeviceType)), reported VRAM: $($h.graphicsMemoryMb) MiB")
    $lines.Add("- Unity/build: $($h.unityVersion), GUID $($h.buildGuid)")
    $lines.Add("- Render: $($h.width)x$($h.height), quality $($h.qualityLevel), vSync $($h.vSyncCount), target FPS $($h.targetFrameRate)")
    $lines.Add('')
}

$lines.Add('## Frame/memory results')
$lines.Add('')
$lines.Add('| Population | Repeat | p50 ms | p95 ms | p99 ms | max ms | avg FPS | main avg | render avg | GPU avg | alloc KiB/frame | managed delta | working-set delta | result |')
$lines.Add('|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|')
foreach ($row in $repeatRows | Sort-Object Population,Repeat) {
    $lines.Add("| $($row.Population) | $($row.Repeat) | $(Format-Number $row.P50) | $(Format-Number $row.P95) | $(Format-Number $row.P99) | $(Format-Number $row.Maximum) | $(Format-Number $row.Fps '0.0') | $(Format-Number $row.MainThreadAverage) | $(Format-Number $row.RenderThreadAverage) | $(Format-Number $row.GpuAverage) | $(Format-Number ($row.AllocBytesPerFrame / 1KB) '0.0') | $(Format-Mib $row.ManagedDelta) | $(Format-Mib $row.WorkingSetDelta) | $(if ($row.Passed) {'PASS'} else {'FAIL'}) |")
}
$lines.Add('')

if ($rawRows.Count -gt 0) {
    $lines.Add('## Raw-frame tail analysis')
    $lines.Add('')
    $lines.Add('| Population | Repeat | >16.67 ms | >33.33 ms | longest >33.33 streak | top spikes ms |')
    $lines.Add('|---:|---:|---:|---:|---:|---|')
    foreach ($row in $rawRows | Sort-Object Population,Repeat) {
        $spikes = @($row.TopSpikesMs | ForEach-Object { Format-Number $_ }) -join ', '
        $lines.Add("| $($row.Population) | $($row.Repeat) | $(Format-Number $row.Over16_67Percent '0.00')% | $(Format-Number $row.Over33_33Percent '0.00')% | $($row.LongestOver33Streak) | $spikes |")
    }
    $lines.Add('')
}

if ($runtimeRows.Count -gt 0) {
    $lines.Add('## Runtime invariants and churn')
    $lines.Add('')
    $lines.Add('| Pop | Rep | Tier1/2/3 | GameObjects start/end | UI subscriptions start/end | churn transitions | canonical hash |')
    $lines.Add('|---:|---:|---|---|---|---:|---|')
    foreach ($row in $runtimeRows | Sort-Object Population,Repeat) {
        $lines.Add("| $($row.Population) | $($row.Repeat) | $($row.Tier1)/$($row.Tier2)/$($row.Tier3) | $($row.GameObjectsStart)/$($row.GameObjectsEnd) | $($row.UiSubscriptionsStart)/$($row.UiSubscriptionsEnd) | $($row.TierChurnTransitions) | $($row.CanonicalHash) |")
    }
    $lines.Add('')
}

$lines.Add('## Проблемы и способы исправления')
$lines.Add('')
if ($issues.Count -eq 0) {
    $lines.Add('Обязательных нарушений и диагностических предупреждений не обнаружено.')
} else {
    $lines.Add('| Severity | Где | Наблюдение | Порог | Вероятная причина | Что делать |')
    $lines.Add('|---|---|---|---|---|---|')
    foreach ($issue in $issues) {
        $safe = @($issue.severity, $issue.location, $issue.observed, $issue.limit, $issue.probableCause, $issue.recommendation) |
            ForEach-Object { ([string]$_).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ') }
        $lines.Add('| ' + ($safe -join ' | ') + ' |')
    }
}
$lines.Add('')

if ($good.Count -gt 0) {
    $lines.Add('## Хорошие показатели')
    $lines.Add('')
    foreach ($entry in $good) { $lines.Add("- $entry") }
    $lines.Add('')
}

if ($allocationRows.Count -gt 0) {
    $lines.Add('## Allocation source breakdown (bytes/simulation sample)')
    $lines.Add('')
    $lines.Add('| Pop | Rep | Tier1 scheduler | Simulation callback | Movement | Tier2 | Tier3/research | Workers/weather | Canonical services | Presentation/tier policy | Total simulation |')
    $lines.Add('|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|')
    foreach ($row in $allocationRows | Sort-Object Population,Repeat) {
        $lines.Add("| $($row.Population) | $($row.Repeat) | $(Format-Number $row.Tier1Scheduler '0.0') | $(Format-Number $row.SimulationCallback '0.0') | $(Format-Number $row.MovementPresentation '0.0') | $(Format-Number $row.Tier2 '0.0') | $(Format-Number $row.Tier3Research '0.0') | $(Format-Number $row.WorkersWeather '0.0') | $(Format-Number $row.CanonicalServices '0.0') | $(Format-Number $row.PresentationTierPolicy '0.0') | $(Format-Number $row.TotalSimulation '0.0') |")
    }
    $lines.Add('')
}

if ($markerRows.Count -gt 0) {
    $lines.Add('## Available profiler markers')
    $lines.Add('')
    $lines.Add('| Pop | Rep | Marker | avg ms | max ms |')
    $lines.Add('|---:|---:|---|---:|---:|')
    foreach ($row in $markerRows | Sort-Object Population,Repeat,AverageMs -Descending) {
        $lines.Add("| $($row.Population) | $($row.Repeat) | $($row.Name) | $(Format-Number $row.AverageMs) | $(Format-Number $row.MaximumMs) |")
    }
    $lines.Add('')
}

if ($saveRows.Count -gt 0) {
    $lines.Add('## Save/load observation')
    $lines.Add('')
    $lines.Add('| Pop | Rep | payload KiB | capture | serialize | write | read+deserialize | restore | total ms | identity | canonical hash |')
    $lines.Add('|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|')
    foreach ($row in $saveRows | Sort-Object Population,Repeat) {
        $hashVerdict = if ($row.Canonical) { "$($row.BeforeHash) = $($row.AfterHash)" } else { "$($row.BeforeHash) != $($row.AfterHash)" }
        $lines.Add("| $($row.Population) | $($row.Repeat) | $(Format-Number ($row.PayloadBytes / 1KB) '0.0') | $(Format-Number $row.CaptureMs) | $(Format-Number $row.SerializeMs) | $(Format-Number $row.WriteMs) | $(Format-Number $row.ReadDeserializeMs) | $(Format-Number $row.RestoreMs) | $(Format-Number $row.TotalMs) | $($row.Identity) | $hashVerdict |")
    }
    $lines.Add('')
}

if ($verificationRows.Count -gt 0) {
    $lines.Add('## NUnit verification')
    $lines.Add('')
    $lines.Add('| Suite | Result | Passed | Failed | Skipped |')
    $lines.Add('|---|---|---:|---:|---:|')
    foreach ($row in $verificationRows) {
        $lines.Add("| $($row.Name) | $($row.Result) | $($row.Passed) | $($row.Failed) | $($row.Skipped) |")
    }
    $lines.Add('')
}

$lines.Add('## Как читать отчёт')
$lines.Add('')
$lines.Add('- BLOCKER означает, что U29 нельзя считать пройденным. Порог или workload ослаблять нельзя.')
$lines.Add('- WARN — диагностический ориентир: он помогает выбрать следующую оптимизацию, но сам по себе не меняет hard verdict.')
$lines.Add('- Оптимизировать сначала устойчивую крупнейшую секцию; редкие spikes разбирать по raw CSV, GC и marker maxima.')
$lines.Add('- Перед сравнением before/after использовать тот же build/config/seed/population и не смешивать результаты разных output directories.')
$lines.Add('')
$lines.Add("Source directory: $ResultsDirectory")

$parent = Split-Path $OutputPath -Parent
if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$lines | Set-Content -LiteralPath $OutputPath -Encoding UTF8

$machineReport = [ordered]@{
    schemaVersion = 1
    generatedUtc = $generatedUtc
    verdict = $verdict
    smoke = $isSmoke
    finalGateEligible = $verdict -eq 'PASS'
    complete = $complete
    expectedRepeats = $ExpectedPopulations.Count * $ExpectedRepeats
    completedRepeats = $repeatRows.Count
    hardLimits = [ordered]@{
        maximumWorstRepeatP95Ms = $hardP95Ms
        managedGrowthBytes = $managedGrowthLimit
        workingSetGrowthBytes = $workingSetGrowthLimit
        foregroundRequired = $true
        exactPopulationTierAndSaveIdentityRequired = $true
    }
    diagnosticGuidelines = [ordered]@{
        targetP50Ms = $targetP50Ms
        warningP50Ms = $softP50Ms
        warningP99Ms = $softP99Ms
        warningAllocatedBytesPerFrame = $softAllocationBytesPerFrame
        warningSaveTotalMs = $softSaveTotalMs
    }
    repeats = @($repeatRows)
    rawFrameAnalysis = @($rawRows)
    allocationBreakdown = @($allocationRows)
    profilerMarkers = @($markerRows)
    saveObservations = @($saveRows)
    runtimeInvariants = @($runtimeRows)
    verification = @($verificationRows)
    issues = @($issues)
    goodIndicators = @($good)
}
$machineReport | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonOutputPath -Encoding UTF8

Write-Output "U29 diagnostic report: $OutputPath"
Write-Output "U29 machine report: $jsonOutputPath"
Write-Output "U29 report verdict: $verdict ($($repeatRows.Count)/$($ExpectedPopulations.Count * $ExpectedRepeats) repeats)"
if (-not $DoNotFailOnVerdict -and $verdict -ne 'PASS' -and $verdict -ne 'SMOKE_PASS') { throw "U29 diagnostic verdict is $verdict." }
