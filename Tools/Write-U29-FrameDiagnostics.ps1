param([Parameter(Mandatory=$true)][string]$RepeatDirectory)
$ErrorActionPreference='Stop'
$RepeatDirectory=(Resolve-Path -LiteralPath $RepeatDirectory).Path
$frames=@(Import-Csv -LiteralPath (Join-Path $RepeatDirectory 'diagnostic-frames.csv'))
if($frames.Count -eq 0){throw 'No diagnostic frames.'}
$metadata=Get-Content -LiteralPath (Join-Path $RepeatDirectory 'diagnostic-metadata.txt') -Raw
$inventory=@(Import-Csv -LiteralPath (Join-Path $RepeatDirectory 'diagnostic-marker-inventory.csv'))
$summary=Get-Content -LiteralPath (Join-Path $RepeatDirectory 'summary.json') -Raw|ConvertFrom-Json
$required=@('unity_frame','realtime_ns','frame_ns','focused','gc_collections_since_sample','simulation_frame','process_cpu_permille_one_core','sample_frame_gap')
foreach($column in $required){if($column -notin $frames[0].PSObject.Properties.Name){throw "Missing diagnostic column: $column"}}
$unfocused=@($frames|Where-Object focused -ne '1').Count
$gaps=@($frames|Where-Object sample_frame_gap -ne '1').Count
$misaligned=@($frames|Where-Object {$_.simulation_frame -ne $_.unity_frame}).Count
$slow=@($frames|Where-Object {[long]$_.frame_ns -gt 33330000})
$healthy=@($frames|Where-Object {[long]$_.frame_ns -le 33330000})
$ci=[Globalization.CultureInfo]::InvariantCulture
function F([double]$v){$v.ToString('0.000',$ci)}
function Stats($items,[string]$column){
    $values=@($items|ForEach-Object {[long]$_.$column}|Where-Object {$_ -ge 0})
    if($values.Count -eq 0){return [pscustomobject]@{Count=0;Mean=0;Max=0}}
    $m=$values|Measure-Object -Average -Maximum
    [pscustomobject]@{Count=$values.Count;Mean=$m.Average;Max=$m.Maximum}
}
$columns=@('tier2_ns','tier3_research_ns','workers_weather_ns','canonical_services_ns','presentation_tiers_ns')
$columns+=@($inventory|Where-Object {$_.recorded -eq '1' -and $_.unit -eq 'TimeNanoseconds'}|ForEach-Object {'profiler:'+$_.name})
$rows=@(foreach($column in $columns){
    $a=Stats $slow $column; $b=Stats $healthy $column
    [pscustomobject]@{Metric=$column;SlowSamples=$a.Count;HealthySamples=$b.Count;SlowMs=$a.Mean/1e6;HealthyMs=$b.Mean/1e6;SlowMaxMs=$a.Max/1e6}
})
# Persistent core markers must cover the run, not just a single startup frame.
$coverageFailures=@(foreach($core in @('profiler:PlayerLoop','profiler:Update.ScriptRunBehaviourUpdate')){
    if($core -notin $frames[0].PSObject.Properties.Name -or (Stats $frames $core).Count -lt [int]($frames.Count*.95)){$core}
})
$updateTiming=Stats $frames 'profiler:Update.ScriptRunBehaviourUpdate'
$tier1Timing=Stats $frames 'profiler:Serenity.Tier1.Advance'
$truncatedScopes=$updateTiming.Count -gt 0 -and $tier1Timing.Count -gt 0 -and ($updateTiming.Mean+500000) -lt ($tier1Timing.Mean*.8)
$out=[Collections.Generic.List[string]]::new()
$out.Add('# U29: диагностика медленных кадров')
$out.Add('')
$out.Add('Это диагностический запуск, он не закрывает U29. Отсутствие просадок означает, что проблему пока не воспроизвели.')
$out.Add('')
$out.Add("Build: $($summary.buildGuid). Population: $($summary.population). p95: $(F $summary.frame.p95Ms) ms. p99: $(F $summary.frame.p99Ms) ms.")
$out.Add("Диагностических кадров: $($frames.Count); выше 33.33 ms: $($slow.Count). Потеря фокуса: $unfocused. Пропуски кадров сборщика: $gaps. Несовпадение кадра simulation: $misaligned.")
$out.Add("Общий summary содержит $($summary.frame.sampleCount) кадров. Первый переходный кадр после подготовки измерения исключён из покадровой диагностики; его пик может остаться в общем summary и не иметь детальной разбивки.")
$out.Add("Недостаточное покрытие обязательных маркеров: $($coverageFailures.Count).")
$out.Add("Признак обрезанных внешних profiler scopes: $truncatedScopes.")
$out.Add('')
if($metadata -match 'allocation_probe_supported=False'){
    $out.Add('Счётчик GC.GetAllocatedBytesForCurrentThread НЕ прошёл проверку выделением 64 КиБ. Нули в прежнем allocation breakdown не доказывают отсутствие выделений. В новом CSV такие значения равны -1. Общий GC Allocated In Frame измеряется другим счётчиком.')
}else{$out.Add('Счётчик выделения памяти прошёл проверку 64 КиБ. Секции отражают только текущий поток; прочие потоки и работа движка не включены.')}
$out.Add('')
$out.Add('## Измеренные затраты')
$out.Add('')
$out.Add('Средние ниже рассчитаны по доступным образцам. -1 исключён; число образцов показано. Вложенные маркеры и времена разных потоков нельзя складывать. Это связь с медленными кадрами, а не доказательство причины.')
$out.Add('')
$out.Add('| Участок | slow samples | healthy samples | slow avg ms | healthy avg ms | slow max ms |')
$out.Add('|---|---:|---:|---:|---:|---:|')
foreach($r in ($rows|Sort-Object SlowMs -Descending)){$out.Add("| $($r.Metric) | $($r.SlowSamples) | $($r.HealthySamples) | $(F $r.SlowMs) | $(F $r.HealthyMs) | $(F $r.SlowMaxMs) |")}
$out.Add('')
$out.Add('## Память и загрузка процесса')
$out.Add('')
$out.Add('| Счётчик | Единица | slow samples | healthy samples | slow avg | healthy avg | slow max |')
$out.Add('|---|---|---:|---:|---:|---:|---:|')
$counters=@([pscustomobject]@{Name='gc_collections_since_sample';Unit='collections'},[pscustomobject]@{Name='process_cpu_permille_one_core';Unit='permille of one core'})
$counters+=@('tier2','tier3_research','workers_weather','canonical_services','presentation_tiers')|ForEach-Object {[pscustomobject]@{Name=$_+'_allocated_bytes';Unit='bytes'}}
if('profiler:GC Allocated In Frame' -in $frames[0].PSObject.Properties.Name){$counters+=[pscustomobject]@{Name='profiler:GC Allocated In Frame';Unit='bytes'}}
foreach($counter in $counters){
    $a=Stats $slow $counter.Name; $b=Stats $healthy $counter.Name
    $out.Add("| $($counter.Name) | $($counter.Unit) | $($a.Count) | $($b.Count) | $(F $a.Mean) | $(F $b.Mean) | $(F $a.Max) |")
}
$out.Add('')
$out.Add('## Интервалы по 30 секунд')
$out.Add('')
$out.Add('| Секунды | Кадров | Медленных | p95 ms |')
$out.Add('|---|---:|---:|---:|')
$start=[long]$frames[0].realtime_ns
$bins=$frames|Group-Object {[int][math]::Floor(([long]$_.realtime_ns-$start)/30e9)}
foreach($bin in ($bins|Sort-Object {[int]$_.Name})){
    $vals=@($bin.Group|ForEach-Object {[long]$_.frame_ns}|Sort-Object)
    $p95=$vals[[int][math]::Ceiling($vals.Count*.95)-1]/1e6
    $bad=@($vals|Where-Object {$_ -gt 33330000}).Count
    $out.Add("| $([int]$bin.Name*30)–$(([int]$bin.Name+1)*30) | $($vals.Count) | $bad | $(F $p95) |")
}
$out.Add('')
$out.Add('## Следующий шаг по результату')
$out.Add('')
if($slow.Count -eq 0){$out.Add('Просадки не воспроизвелись. Не объявлять исправление и не менять порог. Сохранить этот контроль; для следующего диагностического запуска увеличить длительность при том же workload.')}
else{
    $candidate=$rows|Where-Object {$_.Metric -notlike 'profiler:*' -and $_.SlowSamples -gt 0}|Sort-Object SlowMs -Descending|Select-Object -First 1
    $callbackMs=($rows|Where-Object {$_.Metric -notlike 'profiler:*'}|Measure-Object SlowMs -Sum).Sum
    $slowFrameMs=(Stats $slow 'frame_ns').Mean/1e6
    $out.Add("Крупнейшая измеренная секция callback в медленных кадрах: $($candidate.Metric), $(F $candidate.SlowMs) ms. Все пять последовательных секций: $(F $callbackMs) ms при среднем медленном кадре $(F $slowFrameMs) ms.")
    if($callbackMs -lt $slowFrameMs*.25){
        $out.Add('Callback-секции занимают менее четверти времени медленного кадра и сами не объясняют основную задержку. Следующий приоритет — остальная работа главного потока, рендеринг/GPU и ожидания; текущих данных недостаточно, чтобы назвать конкретного виновника. Не начинать переписывание AI только потому, что одна его секция больше соседних.')
    }else{$out.Add('Разделить крупнейшую callback-секцию дополнительными маркерами и сопоставить её рост с реальным GC и native animation, затем оптимизировать доказанный источник.')}
}
$out.Add('Частые сборки и высокий p99 сами по себе не доказывают GC-паузы. Проверить реальные временные маркеры GC; gc_collections_since_sample показывает только изменения счётчика между вызовами наблюдателя.')
$out.Add('Время автоматической анимации следует искать в Animators.Update и вложенных Animator/Animators-маркерах; названия PlayerLoop-стадий DirectorUpdateAnimationBegin/End доступны не во всех сборках. Serenity.Tier1.AnimationPresentation измеряет только обслуживание отключения idle Animator.')
$out.Add('Частоты и температуры здесь не измеряются: заключение о перегреве по этому отчёту делать нельзя. process_cpu_permille_one_core: 1000 = одно занятое логическое ядро, данные обновляются раз в секунду.')
$out.Add('')
$out.Add('## Куда смотреть в коде после воспроизведения')
$out.Add('')
$out.Add('- tier2: Tier2Runtime.Step; tier3_research: Tier3Adapter и продвижение исследований. Проверить стоимость обходов и размер реально активного набора, сохранив детерминизм.')
$out.Add('- workers_weather: LocalSceneCompositionRoot.OnSimulationAdvanced — сбор/сортировка работников и Weather.AdvanceHealthExposureTo. Если здесь растёт время, разделить эти операции дополнительными маркерами; рассматривать кеширование неизменных составов, не урезание канонических тиков.')
$out.Add('- canonical_services: StrategicAi, Events, HealthFamily, BuildingDamage и Production. Профилировать каждый сервис отдельно, затем убирать доказанные повторные обходы/выделения.')
$out.Add('- presentation_tiers: Refresh производственных представлений/погоды и TierPolicy.Evaluate. Проверить повторные native-вызовы и обновление неизменных данных.')
$out.Add('- Рост реального GC.Collect/CollectIncremental на медленных кадрах: искать источники аллокаций отдельным allocation-профилем. Неработающий per-thread counter не использовать для выбора виновного сервиса.')
$out.Add('- Рост Animators.Update: проверить число активных движущихся Animator и culling. Не возвращать редкое ручное обновление ходьбы, вызывавшее скольжение.')
$out.Add('- Рост ожиданий при небольшом времени callback/анимации: сопоставить main/render/GPU и загрузку ОС. Не переписывать AI только на основании большого времени ожидания; для перегрева нужны отдельные частоты/температуры.')
$out.Add('')
$out.Add('## Доступность маркеров')
$out.Add('')
foreach($pattern in @('GC.Collect','*GarbageCollector*','Animators.Update','*DirectorUpdateAnimation*','*WaitForTargetFPS*')){
    $found=@($inventory|Where-Object {$_.recorded -eq '1' -and $_.name -like $pattern})
    $out.Add("- $pattern : $($found.Count) записанных маркеров. Полный список: diagnostic-marker-inventory.csv.")
}
$out.Add('')
$out.Add('## Проверка сборщика')
$out.Add('')
$out.Add('```text'); $out.Add($metadata.Trim()); $out.Add('```')
$out|Set-Content -LiteralPath (Join-Path $RepeatDirectory 'U29-FRAME-DIAGNOSTICS.md') -Encoding UTF8
$rows|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $RepeatDirectory 'U29-FRAME-DIAGNOSTICS.json') -Encoding UTF8
Write-Output "Detailed report: $RepeatDirectory\U29-FRAME-DIAGNOSTICS.md"
if($unfocused -gt 0 -or $gaps -gt 0 -or $misaligned -gt 0 -or $coverageFailures.Count -gt 0 -or $truncatedScopes){throw 'Diagnostic capture is not comparable: see focus/frame alignment/marker coverage/scope consistency counters in report.'}
