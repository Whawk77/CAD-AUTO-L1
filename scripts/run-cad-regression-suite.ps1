[CmdletBinding()]
param(
    [switch]$Build,
    [string]$DllDirectory,
    [ValidateRange(1, 3)]
    [int]$RepeatCount = 3,
    [string]$CoreConsolePath,
    [string]$Profile,
    [ValidateRange(30, 1800)]
    [int]$TimeoutSeconds = 180,
    [switch]$PlanOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$defaultCoreConsolePath = "D:\Program Files\Autodesk\AutoCAD 2020\accoreconsole.exe"

if ([string]::IsNullOrWhiteSpace($CoreConsolePath)) {
    $CoreConsolePath = $env:AUTOCAD_CORE_CONSOLE_PATH
}
if ([string]::IsNullOrWhiteSpace($CoreConsolePath)) {
    $CoreConsolePath = $defaultCoreConsolePath
}
if ([string]::IsNullOrWhiteSpace($Profile)) {
    $Profile = $env:AUTOCAD_PROFILE
}
if ([string]::IsNullOrWhiteSpace($Profile)) {
    $Profile = "1"
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function ConvertTo-LispString {
    param([Parameter(Mandatory = $true)][string]$Value)
    return '"' + $Value.Replace("\", "/").Replace('"', '\"') + '"'
}

function Get-Manifest {
    param([Parameter(Mandatory = $true)][string]$RelativePath)
    $path = Join-Path $projectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Manifest not found: $path"
    }
    return Get-Content -Raw -Encoding UTF8 -LiteralPath $path | ConvertFrom-Json
}

function Get-NormalizedReportHash {
    param([Parameter(Mandatory = $true)][string]$ReportPath)
    $report = Get-Content -Raw -Encoding UTF8 -LiteralPath $ReportPath | ConvertFrom-Json
    foreach ($name in @("runId", "generatedAt")) {
        if ($report.PSObject.Properties.Name -contains $name) {
            $report.PSObject.Properties.Remove($name)
        }
    }
    if ($null -ne $report.plugin) {
        foreach ($name in @("path", "lastWriteTimeUtc")) {
            if ($report.plugin.PSObject.Properties.Name -contains $name) {
                $report.plugin.PSObject.Properties.Remove($name)
            }
        }
    }
    if ($null -ne $report.drawing) {
        foreach ($name in @("fullPath", "path")) {
            if ($report.drawing.PSObject.Properties.Name -contains $name) {
                $report.drawing.PSObject.Properties.Remove($name)
            }
        }
    }
    $json = $report | ConvertTo-Json -Depth 100 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace("-", "")
    }
    finally {
        $sha.Dispose()
    }
}

function Get-NewDebugDirectory {
    $highest = 0
    foreach ($directory in @(Get-ChildItem -LiteralPath (Join-Path $projectRoot "bin") -Directory -ErrorAction SilentlyContinue)) {
        $match = [regex]::Match($directory.Name, '^Debug-v([0-9]+)$')
        if ($match.Success) {
            $version = [int]$match.Groups[1].Value
            if ($version -gt $highest) {
                $highest = $version
            }
        }
    }
    $nextVersion = $highest + 1
    $outputDirectory = Join-Path $projectRoot ("bin\Debug-v{0}" -f $nextVersion)
    if (Test-Path -LiteralPath $outputDirectory) {
        throw "Refusing to overwrite versioned output: $outputDirectory"
    }
    $intermediate = "obj-cad-nightly-v{0}\" -f $nextVersion
    Push-Location $projectRoot
    try {
        & dotnet msbuild AutoFixtureDim.csproj /t:Build /p:Configuration=Debug `
            ("/p:OutDir={0}\" -f $outputDirectory) `
            ("/p:BaseIntermediateOutputPath={0}" -f $intermediate) `
            /p:PostBuildEvent= /p:DebugType=None /p:DebugSymbols=false /v:minimal | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Plugin build failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
    return $outputDirectory
}

function Get-DllEvidence {
    param([Parameter(Mandatory = $true)][string]$Directory)
    $evidence = @()
    foreach ($name in @("AutoFixtureDim.dll", "CadAuto.Core.dll", "CadAuto.CadAdapter.dll")) {
        $path = Join-Path $Directory $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required plugin file not found: $path"
        }
        $evidence += [ordered]@{
            path = (Resolve-Path -LiteralPath $path).Path
            sha256 = Get-Sha256 -Path $path
        }
    }
    return $evidence
}

function Write-Summary {
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Summary,
        [Parameter(Mandatory = $true)][string]$BatchRoot,
        [Parameter(Mandatory = $true)][System.Collections.IEnumerable]$Records,
        [Parameter(Mandatory = $true)][System.Collections.IEnumerable]$Errors
    )
    $Summary.finishedAtUtc = [DateTime]::UtcNow.ToString("o")
    $Summary.cases = @($Records)
    $Summary.errors = @($Errors)
    $Summary.status = if (@($Errors).Count -eq 0 -and @($Records | Where-Object { $_.status -ne "Passed" -and $_.status -ne "Skipped" }).Count -eq 0) {
        "Passed"
    }
    else {
        "Failed"
    }
    $Summary | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $BatchRoot "summary.json") -Encoding UTF8

    $lines = @(
        "# CAD nightly $($Summary.batchId)",
        "",
        "- Status: $($Summary.status)",
        "- DLL directory: $($Summary.dllDirectory)",
        "- Repeat count: $($Summary.repeatCount)",
        "",
        "| Repeat | Family | Case | Status | Normalized report SHA256 |",
        "| ---: | --- | --- | --- | --- |"
    )
    foreach ($record in @($Records)) {
        $lines += "| $($record.repeat) | $($record.family) | $($record.caseId) | $($record.status) | $($record.normalizedReportSha256) |"
    }
    if (@($Errors).Count -gt 0) {
        $lines += ""
        $lines += "## Errors"
        foreach ($message in @($Errors)) {
            $lines += "- $message"
        }
    }
    $lines | Set-Content -LiteralPath (Join-Path $BatchRoot "summary.md") -Encoding UTF8
}

function Invoke-CoreConsole {
    param(
        [Parameter(Mandatory = $true)][string]$DrawingPath,
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [Parameter(Mandatory = $true)][string]$StandardOutputPath,
        [Parameter(Mandatory = $true)][string]$StandardErrorPath
    )
    $arguments = @(
        "/i", ('"{0}"' -f $DrawingPath),
        "/s", ('"{0}"' -f $ScriptPath),
        "/l", "zh-CN",
        "/p", ('"{0}"' -f $Profile)
    )
    $process = Start-Process -FilePath $CoreConsolePath -ArgumentList $arguments -WindowStyle Hidden `
        -RedirectStandardOutput $StandardOutputPath -RedirectStandardError $StandardErrorPath -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $process.Refresh()
        if ($process.HasExited) {
            break
        }
        Start-Sleep -Seconds 1
    }
    $process.Refresh()
    if (-not $process.HasExited) {
        throw "AutoCAD Core Console timed out; process was not killed. PID=$($process.Id)."
    }
    return $process.ExitCode
}

function Invoke-DimensionLayoutCase {
    param(
        [Parameter(Mandatory = $true)]$Case,
        [Parameter(Mandatory = $true)][int]$Repeat,
        [Parameter(Mandatory = $true)][string]$RepeatRoot,
        [Parameter(Mandatory = $true)][string]$PluginPath,
        [Parameter(Mandatory = $true)]$DllEvidence
    )
    $caseId = [string]$Case.id
    $side = [string]$Case.diagnosticSide
    $command = switch ($side) {
        "Bottom" { "V203BOTTOM" }
        "Top" { "V203TOP" }
        "Left" { "V203LEFT" }
        "Right" { "V203RIGHT" }
        default { throw "Unsupported DL01 side: $side" }
    }
    $detailWindow = switch ($side) {
        "Bottom" { @("-40.0 -80.0 0.0", "345.0 10.0 0.0") }
        "Top" { @("-40.0 195.0 0.0", "345.0 260.0 0.0") }
        "Left" { @("-90.0 -10.0 0.0", "5.0 215.0 0.0") }
        "Right" { @("300.0 -10.0 0.0", "345.0 215.0 0.0") }
    }

    $caseRoot = Join-Path (Join-Path $RepeatRoot "dimension-layout") $caseId
    New-Item -ItemType Directory -Path $caseRoot | Out-Null
    $fixturePath = Join-Path (Join-Path $projectRoot "regression\dimension-layout") ([string]$Case.fixture)
    $actualFixtureHash = Get-Sha256 -Path $fixturePath
    if ($actualFixtureHash -ne ([string]$Case.fixtureSha256).ToUpperInvariant()) {
        throw "Fixture hash mismatch for $caseId."
    }
    $drawingPath = Join-Path $caseRoot ([IO.Path]::GetFileName($fixturePath))
    Copy-Item -LiteralPath $fixturePath -Destination $drawingPath
    $tracePath = Join-Path $caseRoot "trace.log"
    $reportPath = Join-Path $caseRoot "report.json"
    $fullImagePath = Join-Path $caseRoot "full.png"
    $detailImagePath = Join-Path $caseRoot "detail.png"
    $scriptPath = Join-Path $caseRoot "run.scr"
    $stdoutPath = Join-Path $caseRoot "acad.stdout.log"
    $stderrPath = Join-Path $caseRoot "acad.stderr.log"
    $validationStdoutPath = Join-Path $caseRoot "validation.stdout.log"
    $validationStderrPath = Join-Path $caseRoot "validation.stderr.log"
    $resultPath = Join-Path $caseRoot "result.json"
    $lspPath = Join-Path $projectRoot "scripts\cad-v203-layout-test.lsp"

    $driver = @"
FILEDIA
0
CMDDIA
0
_.NETLOAD
$(ConvertTo-LispString $PluginPath)
(load $(ConvertTo-LispString $lspPath))
(setq *v203-trace-file* $(ConvertTo-LispString $tracePath))
(setq *v203-report-file* $(ConvertTo-LispString $reportPath))
$command
_.QSAVE
(vl-load-com)
(setq *v203-full-image-path* $(ConvertTo-LispString $fullImagePath))
(setq *v203-detail-image-path* $(ConvertTo-LispString $detailImagePath))
(vl-cmdf "_.REGEN")
(vl-cmdf "_.ZOOM" "_E")
(vl-cmdf "_.PNGOUT" *v203-full-image-path* "_ALL" "")
(setq *v203-detail-selection* (ssget "_C" '($($detailWindow[0])) '($($detailWindow[1])) '((0 . "DIMENSION"))))
(vl-cmdf "_.ZOOM" "_W" '($($detailWindow[0])) '($($detailWindow[1])))
(if *v203-detail-selection* (vl-cmdf "_.PNGOUT" *v203-detail-image-path* *v203-detail-selection* "") (princ "\nDETAIL selection empty"))
_.QUIT
_N
"@
    $driver | Set-Content -LiteralPath $scriptPath -Encoding ASCII

    $result = [ordered]@{
        schemaVersion = 1
        status = "Running"
        error = $null
        family = "DimensionLayout"
        caseId = $caseId
        direction = $side
        repeat = $Repeat
        cadExitCode = $null
        validatorExitCode = $null
        traceComplete = $false
        reportPath = $reportPath
        tracePath = $tracePath
        fullImagePath = $fullImagePath
        detailImagePath = $detailImagePath
        normalizedReportSha256 = $null
        dlls = $DllEvidence
    }
    $failure = $null
    try {
        $previousReportPath = $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH
        $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH = $reportPath
        try {
            $result.cadExitCode = Invoke-CoreConsole -DrawingPath $drawingPath -ScriptPath $scriptPath `
                -StandardOutputPath $stdoutPath -StandardErrorPath $stderrPath
        }
        finally {
            $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH = $previousReportPath
        }
        if ($result.cadExitCode -ne 0) {
            throw "AutoCAD Core Console exited $($result.cadExitCode)."
        }
        if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) {
            throw "Trace was not generated."
        }
        $trace = @(Get-Content -LiteralPath $tracePath)
        if (-not ($trace -contains "COMPLETE side=$side")) {
            throw "Trace has no COMPLETE side=$side marker."
        }
        $result.traceComplete = $true
        foreach ($requiredPath in @($reportPath, $fullImagePath, $detailImagePath)) {
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                throw "Required artifact was not generated: $requiredPath"
            }
        }

        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot "scripts\validate-dimension-layout-report.ps1") `
            -CaseId $caseId -Expectation target -ReportPath $reportPath `
            1> $validationStdoutPath 2> $validationStderrPath
        $result.validatorExitCode = $LASTEXITCODE
        if ($result.validatorExitCode -ne 0) {
            throw "Target validator failed for $caseId."
        }
        $result.normalizedReportSha256 = Get-NormalizedReportHash -ReportPath $reportPath
        $result.status = "Passed"
    }
    catch {
        $failure = $_
        $result.status = "Failed"
        $result.error = $_.Exception.Message
    }
    finally {
        $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    }
    if ($null -ne $failure) {
        throw $failure
    }
    return [pscustomobject]$result
}

function Invoke-CoreCadCase {
    param(
        [Parameter(Mandatory = $true)]$Case,
        [Parameter(Mandatory = $true)][int]$Repeat,
        [Parameter(Mandatory = $true)][string]$RepeatRoot,
        [Parameter(Mandatory = $true)][string]$ResolvedDllDirectory
    )
    $caseId = [string]$Case.id
    $sourceRoot = Join-Path (Join-Path $projectRoot "regression\core-cad\runs") $caseId
    $before = @()
    if (Test-Path -LiteralPath $sourceRoot) {
        $before = @(Get-ChildItem -LiteralPath $sourceRoot -Directory | ForEach-Object { $_.Name })
    }
    $wrapperLogRoot = Join-Path (Join-Path $RepeatRoot "core-cad") $caseId
    New-Item -ItemType Directory -Path $wrapperLogRoot | Out-Null
    $wrapperLog = Join-Path $wrapperLogRoot "runner.log"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot "scripts\run-core-cad-regression.ps1") `
        -CaseId $caseId -DllDirectory $ResolvedDllDirectory -SkipCore -Language zh-CN -Profile $Profile `
        -CoreConsolePath $CoreConsolePath -TimeoutSeconds $TimeoutSeconds 2>&1 | Out-File -LiteralPath $wrapperLog -Encoding UTF8
    $runnerExit = $LASTEXITCODE

    $sourceRun = @(Get-ChildItem -LiteralPath $sourceRoot -Directory |
        Where-Object { $before -notcontains $_.Name } |
        Sort-Object Name -Descending |
        Select-Object -First 1)
    if ($sourceRun.Count -ne 1) {
        throw "Could not identify the new Core-CAD run directory for $caseId."
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $sourceRun[0].FullName -File)) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $wrapperLogRoot $file.Name)
    }
    $resultPath = Join-Path $wrapperLogRoot "result.json"
    $reportPath = Join-Path $wrapperLogRoot "report.json"
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "Core-CAD result.json was not archived for $caseId."
    }
    $result = Get-Content -Raw -Encoding UTF8 -LiteralPath $resultPath | ConvertFrom-Json
    $normalizedHash = $null
    if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
        $normalizedHash = Get-NormalizedReportHash -ReportPath $reportPath
    }
    $status = if ($runnerExit -eq 0 -and [string]$result.status -eq "Passed") { "Passed" } else { "Failed" }
    return [pscustomobject]@{
        family = "Core-CAD"
        caseId = $caseId
        repeat = $Repeat
        status = $status
        error = [string]$result.error
        normalizedReportSha256 = $normalizedHash
        runDirectory = $wrapperLogRoot
    }
}

$coreCadManifest = Get-Manifest -RelativePath "regression\core-cad\cases.json"
$dimensionLayoutManifest = Get-Manifest -RelativePath "regression\dimension-layout\cases.json"
$holeSlotManifest = Get-Manifest -RelativePath "regression\hole-slot\cases.json"
$coreCadReady = @($coreCadManifest.cases | Where-Object { $_.fixtureReady -eq $true })
$dimensionLayoutReady = @($dimensionLayoutManifest.cases | Where-Object { $_.fixtureReady -eq $true })
$holeSlotReady = @($holeSlotManifest.cases | Where-Object { $_.fixtureReady -eq $true })

$plan = [ordered]@{
    schemaVersion = 1
    build = [bool]$Build
    dllDirectory = $DllDirectory
    repeatCount = $RepeatCount
    coreConsolePath = $CoreConsolePath
    profile = $Profile
    timeoutSeconds = $TimeoutSeconds
    ready = [ordered]@{
        coreCad = @($coreCadReady | ForEach-Object { $_.id })
        dimensionLayout = @($dimensionLayoutReady | ForEach-Object { $_.id })
        holeSlot = @($holeSlotReady | ForEach-Object { $_.id })
    }
    execution = "Core tests once; one DLL build; ready CAD cases serial; normalized reports compared across repeats."
}
if ($PlanOnly) {
    $plan | ConvertTo-Json -Depth 8
    return
}

if ($Build -and -not [string]::IsNullOrWhiteSpace($DllDirectory)) {
    throw "Use either -Build or -DllDirectory, not both."
}
if (-not $Build -and [string]::IsNullOrWhiteSpace($DllDirectory)) {
    throw "Use -Build or provide -DllDirectory."
}
if (-not (Test-Path -LiteralPath $CoreConsolePath -PathType Leaf)) {
    throw "AutoCAD Core Console not found: $CoreConsolePath"
}
if (@(Get-Process -Name "accoreconsole" -ErrorAction SilentlyContinue).Count -gt 0) {
    throw "Another AutoCAD Core Console process is active."
}

$batchId = Get-Date -Format "yyyyMMdd-HHmmssfff"
$batchRoot = Join-Path (Join-Path $projectRoot "regression\nightly\runs") $batchId
New-Item -ItemType Directory -Path $batchRoot | Out-Null
$records = New-Object System.Collections.ArrayList
$errors = New-Object System.Collections.ArrayList
$summary = [ordered]@{
    schemaVersion = 1
    batchId = $batchId
    startedAtUtc = [DateTime]::UtcNow.ToString("o")
    finishedAtUtc = $null
    status = "Running"
    repeatCount = $RepeatCount
    dllDirectory = $null
    dlls = @()
    cases = @()
    errors = @()
}

try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot "scripts\validate-remediation-matrix.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "Remediation matrix validation failed."
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot "scripts\validate-regression-manifests.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "Regression manifest validation failed."
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot "scripts\run-core-tests.ps1") -Configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Core regression failed."
    }

    $resolvedDllDirectory = if ($Build) {
        Get-NewDebugDirectory
    }
    else {
        (Resolve-Path -LiteralPath $DllDirectory).Path
    }
    $dllEvidence = @(Get-DllEvidence -Directory $resolvedDllDirectory)
    $summary.dllDirectory = $resolvedDllDirectory
    $summary.dlls = $dllEvidence
    $dllEvidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $batchRoot "dll-hashes.json") -Encoding UTF8
    $pluginPath = [string]$dllEvidence[0].path

    if ($holeSlotReady.Count -gt 0) {
        throw "Ready Hole/Slot cases exist, but no deterministic CAD executor is configured: $(@($holeSlotReady.id) -join ', ')."
    }
    $null = $records.Add([pscustomobject]@{
        family = "HoleSlot"
        caseId = "ready-cases"
        repeat = 0
        status = "Skipped"
        error = "No fixtureReady=true Hole/Slot cases."
        normalizedReportSha256 = $null
        runDirectory = $null
    })

    for ($repeat = 1; $repeat -le $RepeatCount; $repeat++) {
        $repeatRoot = Join-Path $batchRoot ("repeat-{0}" -f $repeat)
        New-Item -ItemType Directory -Path $repeatRoot | Out-Null

        foreach ($case in $coreCadReady) {
            try {
                $record = Invoke-CoreCadCase -Case $case -Repeat $repeat -RepeatRoot $repeatRoot -ResolvedDllDirectory $resolvedDllDirectory
                $null = $records.Add($record)
                if ($record.status -ne "Passed") {
                    $null = $errors.Add("$($record.family)/$($record.caseId)/repeat-$repeat failed: $($record.error)")
                }
            }
            catch {
                $null = $errors.Add("Core-CAD/$($case.id)/repeat-$repeat failed: $($_.Exception.Message)")
            }
            if (@(Get-Process -Name "accoreconsole" -ErrorAction SilentlyContinue).Count -gt 0) {
                throw "An AutoCAD Core Console process remains active; stopping without force-kill."
            }
        }

        foreach ($case in $dimensionLayoutReady) {
            try {
                $record = Invoke-DimensionLayoutCase -Case $case -Repeat $repeat -RepeatRoot $repeatRoot `
                    -PluginPath $pluginPath -DllEvidence $dllEvidence
                $null = $records.Add($record)
            }
            catch {
                $null = $errors.Add("DimensionLayout/$($case.id)/repeat-$repeat failed: $($_.Exception.Message)")
            }
            if (@(Get-Process -Name "accoreconsole" -ErrorAction SilentlyContinue).Count -gt 0) {
                throw "An AutoCAD Core Console process remains active; stopping without force-kill."
            }
        }
    }

    foreach ($group in @($records |
        Where-Object { $_.status -eq "Passed" -and $_.repeat -gt 0 } |
        Group-Object family, caseId)) {
        $hashes = @($group.Group.normalizedReportSha256 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
        if ($group.Count -ne $RepeatCount -or $hashes.Count -ne 1) {
            $null = $errors.Add("Determinism failed for $($group.Name): runs=$($group.Count), uniqueNormalizedHashes=$($hashes.Count).")
        }
    }
}
catch {
    $null = $errors.Add($_.Exception.Message)
}
finally {
    Write-Summary -Summary $summary -BatchRoot $batchRoot -Records $records -Errors $errors
}

if ($summary.status -ne "Passed") {
    throw "CAD regression suite failed. Evidence: $batchRoot"
}
Write-Output ($summary | ConvertTo-Json -Depth 30)
