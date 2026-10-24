#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$RepairDirectory = 'Artifacts/WM15/repair-20261024',
    [string]$MarkdownPath = 'docs/world_map/WM15_REPAIR_CASES.md'
)
$ErrorActionPreference = 'Stop'
$original = @(Get-Content (Join-Path $RepairDirectory 'failed-cases.json') -Raw | ConvertFrom-Json)
$latest = @{}
foreach ($file in Get-ChildItem -LiteralPath $RepairDirectory -Filter '*.xml' -File | Sort-Object LastWriteTimeUtc) {
    [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($case in $xml.SelectNodes('//test-case')) {
        $latest[[string]$case.fullname] = [pscustomobject]@{
            result = [string]$case.result; xml = $file.FullName
            message = $(if ($case.failure) { $case.failure.message.InnerText } else { '' })
            hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
}
$notes = @{
    EveryCatchCreatesASeparateBankPileAndHaulTakesOnlyAvailableStorageSpace = 'Fishing now requires a completed camp; keep pile creation and capacity checks.'
    ApprovedForestModelsAndPrefabsAreAvailableWithoutExecutableCode = 'Approved import scope expanded; validate the Meadow subtree and retain executable-code exclusions.'
    MaterialsResolveShadersAndVendorShadersUseUrp = 'Audit referenced render materials; inert unused legacy shader text is not a runtime shader failure.'
    FishingUsesReachableShoreAndLeavesFishOnGround = 'Create the required completed fishing camp before exercising shore movement/output.'
    WorkCyclesHaveToolsAndAllStarterOutcropsAndPondAreSelectable = 'Verify fishing unavailable before the camp and available after it.'
    AllTreeLodsExcludeShaderBillboardsAndDuplicateMeshStages = 'Ignore explicitly retired disabled LOD shells; require live replacement geometry.'
    AuthoredTreesHideWhenTheirTerrainTileIsNotDrawn = 'D-158 keeps all compact tiles drawn; use strategic hiding/restoration instead of obsolete tile streaming expectations.'
    AuthoredTreesStayGroundedOnTiledTerrain = 'Admitted trees use canonical height; off-world retired roots must be inactive.'
    RealInputAttackVictoryBannerAndPostVictoryUiRemainOperational = 'Separate realtime swing debounce from simulation cooldown; assert the localized persistent victory banner rather than transient notification history.'
    ConnectedPlayerJourneyUsesUiCommandsAndCanonicalServices = 'Select the resource action row, use aggregate research for calendar jumps, and explicitly load conserved supplies before checking travel deductions.'
    AbstractBattleAttacksRespectCooldownApplyCanonicalInjuryMoraleAndOutcome = 'Use a non-lethal unarmed fixture so both participants survive to exercise cooldown.'
    PhysicalAbstractPhysicalPreservesIdentityHealthMoraleAndMidCooldownExactly = 'Use a non-lethal fixture; skill-adjusted spear damage can finish the encounter before transition.'
    NaturalMapKeepsFiniteBoundaryOnTheSupersedingTerrain = 'Validate active compact-world boundaries, not the retired regional polygon.'
    ConcurrentWorkersMayReserveOneSourceButCannotOverHarvestLastBatch = 'D-167 reserves remaining portions; a second worker cannot reserve an already fully reserved batch.'
    BuildingSelectorTests_UsesCanonicalCatalogLocksAndUnlockReasons = 'Use authored catalog entries and preserve lock/unlock assertions.'
    EveryRequiredContextActionHasAValidTargetAndIsVisibleToRules = 'Craft is now a required action; add its building target.'
    SameWorldStrategicViewShowsLocationsUnitTravelAndSettlementReturn = 'Strategic view renders canonical locations, not the historical district array.'
    BoxSelectionContextMenuQueueSaveAndTierRoundTripKeepStableIdentity = 'Use the current action enum, an adult worker, ground-projected targets and a paused deterministic queue.'
    AllVisibleResourceAffordancesAreCanonicalSelectableAndInspectable = 'Use the authored expanded resource catalog while retaining stable proxy/collider checks.'
    AnimalLakeAndIndustrialZonesHaveStableAppendOnlyIdentityAndSaveState = 'Runtime bug: starter outcrops lacked ecology registration; register finite lifecycles.'
    CameraDistanceSwitchesStableCharacterLodAndTreesNeverRotateTowardCamera = 'Test beyond the WM14 horizontal detail threshold rather than the obsolete 180m distance.'
    Mutual_consent_fade_pregnancy_and_v23_save_round_trip_are_connected = 'Assert the active world generation rather than an obsolete generation literal.'
    PauseCodesRouteRunsAllCanonicalEffectsRepeatsAndPersistsIrreversibleFlag = 'Assert StartupSaveProfile version and restore the prior isolated-save environment on teardown.'
    NormalPlayerRouteIsRussianAtMinimumResolutionAndIncreasedScale = 'Add missing resource and gathering-recipe translations.'
    AuthoredNodesBindToCanonicalFiniteProductionSites = 'Audit each admitted authored stable source; additional compact-world nodes are legitimate.'
    RegionSupportsNavMeshAndConstructionAfterArtFill = 'Use active tiled heights for navigation and an actual outside-world point for invalid construction.'
    CanonicalCommandsAdvanceRouteAndP4P5IdentitySurvivesSaveLoad = 'Runtime bug: starting stone-tools knowledge incorrectly counted as player-started research.'
    EveryLocalResourceCanBeSelectedAndMapsToCanonicalDomainState = 'Compare active bound presenters to canonical natural sources rather than a historical count of 24.'
    IronAndGoldStableIdentitySurvivesSaveLoad = 'Track admitted ore IDs through save/load and wait one frame for deferred disposal before inspecting replacement views.'
    GlobalResourceMarkerIssuesSameCanonicalGatherOrder = 'Restore resource selection and canonical gather commands in the same-world strategic view.'
    SelectedResourceCreatesCanonicalPrioritizedGroupOrder = 'Runtime teardown bug: strategic exit accessed an already destroyed camera.'
    OfficialTerrainLoadsAsTheNormalGameplayProjectionWithoutOwningSimulationState = 'Ignore retired disabled renderers whose materials were intentionally cleared.'
    TerrainHeightDrivesConstructionAndWeatherWithoutChangingCanonicalAuthority = 'Assert construction height against active tiled ground, not the superseded terrain.'
    TerrainNavMeshConnectsSettlementsAndAllResourcesKeepCanonicalStableIds = 'Use active ground and admitted resource identities; retain reachable-path and selection checks.'
    AllResourceFormationsRemainClickableAndUseCanonicalWorkAndSaveState = 'Retired off-world/water roots must remain inactive; admitted roots retain work/save identity.'
    DenseBiomeCollidersPreserveStarterRoutesOnActiveTiledNavMesh = 'Both region and biome shader audits exclude retired disabled renderer shells; retain active shaders and navigation checks.'
    GatherHaulAndBuildOrdersReceiveReadablePriorityIndicators = 'Construction also creates delivery/clearance orders; verify every displayed open order by identity.'
    SettlementNavigationWeatherAndSaveLoadKeepCanonicalIdentity = 'Sample active tiled heights for historical worksite/navigation anchors.'
    StrategicViewUsesCanonicalTerrainAndPreservesCanonicalSession = 'Validate active strategic locations and tiled navigation rather than retired district/height data.'
    IntegratedLocalWorldExposesTerrainBiomeSettlementResourcesCharactersAndWeather = 'Derive admitted source count from active world land/water admission.'
    SaveLoadAndStrategicRoundTripPreserveStableIdentityAndConnectedLayout = 'Validate active strategic locations while preserving canonical save identity checks.'
    Tier1WalksWorksAndCommitsOneCanonicalProductionOutput = 'Use the expanded authored production catalog and retain walking/work/output conservation checks.'
    LocalGameplayComposesDebugPanelLimitedKnowledgeAndTier1Research = 'Starting knowledge is catalog-driven; advance the runtime integration that owns research ticks.'
    Tier2AndTier3CommitThroughOneResearchAuthorityWithoutGameObjects = 'Stone tools are starting knowledge; exercise aggregate research with controlled fire.'
    LocalGameplayCreatesCanonicalPilesAndCharacterInventories = 'Residents use bounded CarryLoad, not personal stock; verify canonical pile mappings.'
    ShelterConfirmConsumesExactRealResources = 'Planning is unfinished and free of material consumption; test canonical carry/delivery/stage/work transactions.'
    RtsTpsRtsPreservesCanonicalStateAssignmentsMembershipAndOrder = 'Use the temporary CarryLoad inventory owner.'
    LocalGameplayBuildsNavMeshAndRegistersTier1Agents = 'The compact starter settlement now materializes all ten residents.'
    NavMeshMovementDrivesAnimationAndHaulConservesResources = 'Give the haul fixture capacity beyond nearer cargo and time for its physical route; retain conservation/animation assertions. Fix the runtime debug total omitting CarryLoad quantities.'
    PointerSelectionShowsCharacterActionTargetInventoryAndClaim = 'Debug transport inventory is CarryLoad.'
    LocalRuntimeRoundTripPreservesCanonicalStateInventoryGroupJobAndPosition = 'Temporary transport loads drop to canonical piles on transition; assert global conservation and identity.'
    SameTierRequestsDoNotCreateDuplicatePresenterOrEntity = 'Use ten initial physical residents while preserving duplicate-representation checks.'
    BuildingSelectorRejectsLockedCropAndRoutesUnlockedCropThroughExistingPlacement = 'Use the expanded authored building catalog; retain research and placement assertions.'
    NoHardcodedDemoAssignmentAndSelectedResearchCommandsReportRealActiveState = 'Runtime bug: NeedsOnly mode ignored explicitly assigned researchers.'
    PauseStopsClockAiResearchAndProductionThenResumesWithoutUiClickThrough = 'Runtime bug: explicit research assignment must execute in NeedsOnly mode.'
    TpsAttackUsesU20EncounterAndOneRequestCannotDoubleHit = 'Check attacker cooldown against a live target after a potentially lethal first hit.'
    ActiveWorkGroupRightClickReportsRemoteUnavailableWithoutTierOrOffCameraMutation = 'Restore the explicit unselected developer-group path alongside modern selected-character context commands; accept tiled TerrainCollider.'
    GroupHaulUsesCanonicalInventoryThenAgentsReturnToAutonomousAi = 'Choose an available wood pile rather than an empty industrial source.'
    GroupMoveCommandDrivesPhysicalNavMeshMovement = 'Project the destination onto active tiled ground.'
    RightClickDuringBuildingPlacementCancelsWithoutCreatingMove = 'Runtime bug: later subscribers interpreted the placement-cancel click as movement; suppress commands for the cancellation frame.'
    RightClickGroundMovesTwoSelectedNpcWithFormationAndReturnsToUtilityAi = 'Inspect the modern context-command result of the real click; preserve formation and arrival checks.'
    ShiftMultiSelectionCreatesGroupAndSurvivesPresenterRespawn = 'Runtime bug: stale presenter replacement left a duplicate scheduler state entry.'
}
$rows = foreach ($case in $original) {
    $name = $case.fullname.Split('.')[-1]
    $evidence = $latest[$case.fullname]
    [pscustomobject]@{
        fullname=$case.fullname; originalStep=$case.step; originalXml=$case.xml; originalSha256=$case.sha256
        repair=$(if ($notes.ContainsKey($name)) {$notes[$name]} else {'Investigation pending'})
        status=$(if ($evidence) {$evidence.result} else {'NOT_RETESTED'})
        latestXml=$(if ($evidence) {$evidence.xml} else {''})
        latestSha256=$(if ($evidence) {$evidence.hash} else {''})
        latestFailure=$(if ($evidence) {$evidence.message} else {''})
    }
}
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $RepairDirectory 'case-matrix.json') -Encoding UTF8
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# WM15 original failed-case repair matrix')
$lines.Add('')
$lines.Add('Generated by `Tools/Validation/Write-WM15RepairMatrix.ps1`. Proposed repairs are not PASS evidence; only the latest exact-case NUnit verdict is reported. Compilation errors without XML do not overwrite prior case verdicts. This table does not grant WM15 DONE.')
$lines.Add('')
$lines.Add('| Original failed case | Repair / investigation | Latest verdict | XML |')
$lines.Add('|---|---|---|---|')
foreach ($row in $rows) {
    $file = if ($row.latestXml) {[IO.Path]::GetFileName($row.latestXml)} else {'—'}
    $lines.Add('| `' + $row.fullname + '` | ' + $row.repair + ' | ' + $row.status + ' | ' + $file + ' |')
}
$lines | Set-Content -LiteralPath $MarkdownPath -Encoding UTF8
$rows | Group-Object status | Select-Object Name,Count | Format-Table
