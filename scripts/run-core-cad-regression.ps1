[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CaseId,

    [string]$DllDirectory,

    [switch]$Build,

    [switch]$SkipCore,

    [switch]$AllowNotReady,

    [switch]$PlanOnly,

    [ValidateRange(30, 1800)]
    [int]$TimeoutSeconds = 240
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$regressionRoot = Join-Path $projectRoot "regression\core-cad"
$casesPath = Join-Path $regressionRoot "cases.json"

function ConvertTo-LispString {
    param([string]$Value)
    return '"' + $Value.Replace("\", "/").Replace('"', '\"') + '"'
}

function Get-Sha256 {
    param([string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace("-", "") }
        finally { $sha.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Get-LatestDebugDirectory {
    $directories = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot "bin") -Directory -Filter "Debug-v*" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^Debug-v([0-9]+)$' } |
        ForEach-Object { [pscustomobject]@{ Path = $_.FullName; Version = [int]$Matches[1] } })
    if ($directories.Count -eq 0) { return $null }
    return ($directories | Sort-Object Version -Descending | Select-Object -First 1)
}

function Build-NewPluginVersion {
    $latest = Get-LatestDebugDirectory
    $nextVersion = if ($null -eq $latest) { 1 } else { [int]$latest.Version + 1 }
    $outputDirectory = Join-Path $projectRoot ("bin\Debug-v{0}" -f $nextVersion)
    if (Test-Path -LiteralPath $outputDirectory) {
        throw "Refusing to overwrite versioned output: $outputDirectory"
    }
    $intermediate = "obj-core-cad-v{0}\" -f $nextVersion
    $arguments = @(
        "msbuild", "AutoFixtureDim.csproj", "/t:Build", "/p:Configuration=Debug",
        ("/p:OutDir={0}\" -f $outputDirectory),
        ("/p:BaseIntermediateOutputPath={0}" -f $intermediate),
        "/p:PostBuildEvent=", "/p:DebugType=None", "/p:DebugSymbols=false", "/v:minimal"
    )
    Push-Location $projectRoot
    try {
        & dotnet @arguments | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Plugin build failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
    return [pscustomobject]@{ Path = $outputDirectory; Version = $nextVersion }
}

if (-not (Test-Path -LiteralPath $casesPath -PathType Leaf)) {
    throw "Core-CAD case manifest not found: $casesPath"
}
$manifest = Get-Content -LiteralPath $casesPath -Raw -Encoding UTF8 | ConvertFrom-Json
$caseMatches = @($manifest.cases | Where-Object { $_.id -eq $CaseId })
if ($caseMatches.Count -ne 1) {
    throw "Core-CAD case '$CaseId' was not found or is duplicated."
}
$case = $caseMatches[0]
$fixturePath = Join-Path $regressionRoot ([string]$case.fixture)

$plan = [ordered]@{
    caseId = $CaseId
    description = [string]$case.description
    coreTests = @($case.coreTests)
    fixture = $fixturePath
    fixtureReady = [bool]$case.fixtureReady
    build = [bool]$Build
    requestedDllDirectory = $DllDirectory
    timeoutSeconds = $TimeoutSeconds
}
if ($PlanOnly) {
    $plan | ConvertTo-Json -Depth 6
    return
}

if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
    throw "Fixture not found. Run extract-core-cad-fixture.ps1 first: $fixturePath"
}
if ($case.fixtureReady -ne $true -and -not $AllowNotReady) {
    throw "Case '$CaseId' has fixtureReady=false. Use -AllowNotReady for bring-up."
}
$fixtureHash = Get-Sha256 -Path $fixturePath
if ([string]::IsNullOrWhiteSpace([string]$case.fixtureSha256) -or
    $fixtureHash -ne ([string]$case.fixtureSha256).ToUpperInvariant()) {
    throw "Fixture hash mismatch. Expected '$($case.fixtureSha256)', found '$fixtureHash'."
}

$coreConsoleExe = "D:\Program Files\Autodesk\AutoCAD 2020\accoreconsole.exe"
if (-not (Test-Path -LiteralPath $coreConsoleExe -PathType Leaf)) {
    throw "AutoCAD Core Console 2020 not found: $coreConsoleExe"
}
$existingConsole = @(Get-Process -Name "accoreconsole" -ErrorAction SilentlyContinue)
if ($existingConsole.Count -gt 0) {
    $pids = ($existingConsole | ForEach-Object { $_.Id }) -join ", "
    throw "Close existing AutoCAD Core Console instances before regression. Active PID(s): $pids"
}

if (-not $SkipCore) {
    foreach ($testName in @($case.coreTests)) {
        & (Join-Path $PSScriptRoot "run-core-tests.ps1") -Configuration Release -Filter ([string]$testName)
        if ($LASTEXITCODE -ne 0) {
            throw "Mapped Core test failed: $testName"
        }
    }
}

if ($Build) {
    $buildResult = Build-NewPluginVersion
    $dllRoot = $buildResult.Path
}
elseif (-not [string]::IsNullOrWhiteSpace($DllDirectory)) {
    $dllRoot = (Resolve-Path -LiteralPath $DllDirectory -ErrorAction Stop).Path
}
else {
    $latest = Get-LatestDebugDirectory
    if ($null -eq $latest) {
        throw "No bin\Debug-vN build exists. Use -Build or -DllDirectory."
    }
    $dllRoot = $latest.Path
}

$requiredDllNames = @("AutoFixtureDim.dll", "CadAuto.Core.dll", "CadAuto.CadAdapter.dll")
$dlls = [ordered]@{}
foreach ($name in $requiredDllNames) {
    $path = Join-Path $dllRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required plugin file not found: $path"
    }
    $dlls[$name] = [ordered]@{
        path = (Resolve-Path -LiteralPath $path).Path
        sha256 = Get-Sha256 -Path $path
    }
}

$runDirectory = Join-Path (Join-Path $regressionRoot "runs\$CaseId") (Get-Date -Format "yyyyMMdd-HHmmssfff")
$null = New-Item -ItemType Directory -Path $runDirectory -Force
$workingDwg = Join-Path $runDirectory ([IO.Path]::GetFileName($fixturePath))
$tracePath = Join-Path $runDirectory "trace.log"
$reportPath = Join-Path $runDirectory "report.json"
$lspPath = Join-Path $runDirectory "run.lsp"
$driverPath = Join-Path $runDirectory "run.scr"
$stdoutPath = Join-Path $runDirectory "core-console.stdout.log"
$stderrPath = Join-Path $runDirectory "core-console.stderr.log"
$resultPath = Join-Path $runDirectory "result.json"
Copy-Item -LiteralPath $fixturePath -Destination $workingDwg

$scopeKeyword = switch ([string]$case.commandScope) {
    "All" { "A" }
    "OutlineOnly" { "O" }
    "HoleOnly" { "H" }
    "CornerOnly" { "C" }
    default { throw "Unsupported commandScope: $($case.commandScope)" }
}
$sideKeyword = switch ([string]$case.diagnosticSide) {
    "All" { "A" }
    "Top" { "T" }
    "Bottom" { "B" }
    "Left" { "L" }
    "Right" { "R" }
    default { throw "Unsupported diagnosticSide: $($case.diagnosticSide)" }
}
$expectedCount = [int]$case.selection.expectedCount
$entityType = [string]$case.selection.entityType
$insUnits = [int]$case.environment.insUnits
$dimScale = ([double]$case.environment.dimScale).ToString("0.############", [Globalization.CultureInfo]::InvariantCulture)
$dimTextHeight = ([double]$case.environment.dimTextHeight).ToString("0.############", [Globalization.CultureInfo]::InvariantCulture)
$dimArrowSize = ([double]$case.environment.dimArrowSize).ToString("0.############", [Globalization.CultureInfo]::InvariantCulture)

$lsp = @"
(vl-load-com)
(setq *ccr-trace* $(ConvertTo-LispString $tracePath))
(setq *ccr-report* $(ConvertTo-LispString $reportPath))
(setq *ccr-expected-count* $expectedCount)

(defun ccr-trace (message / stream)
  (setq stream (open *ccr-trace* "a"))
  (if stream (progn (write-line message stream) (close stream)))
)

(defun c:RUNCORECAD (/ oldCmdecho oldOsmode selection)
  (setq oldCmdecho (getvar "CMDECHO"))
  (setq oldOsmode (getvar "OSMODE"))
  (setvar "CMDECHO" 1)
  (setvar "OSMODE" 0)
  (setvar "INSUNITS" $insUnits)
  (setvar "DIMSCALE" $dimScale)
  (setvar "DIMTXT" $dimTextHeight)
  (setvar "DIMASZ" $dimArrowSize)
  (ccr-trace "START")
  (setq selection (ssget "_X" '((0 . "$entityType") (410 . "Model"))))
  (cond
    ((null selection) (ccr-trace "ERROR selectionEmpty"))
    ((/= (sslength selection) *ccr-expected-count*)
      (ccr-trace (strcat "ERROR selectionCount=" (itoa (sslength selection)))))
    (T
      (ccr-trace (strcat "INPUT selection=" (itoa (sslength selection))))
      ;; First empty input ends outline selection; second skips manual Circle selection.
      (vl-cmdf "ASDREPRO" "$scopeKeyword" "$sideKeyword" selection "" "")
      (ccr-trace "COMPLETE")
    )
  )
  (setvar "OSMODE" oldOsmode)
  (setvar "CMDECHO" oldCmdecho)
  (princ)
)
(princ)
"@
Set-Content -LiteralPath $lspPath -Value $lsp -Encoding ASCII

$driver = @"
(setvar "FILEDIA" 0)
(setvar "CMDDIA" 0)
(command "_.NETLOAD" $(ConvertTo-LispString ([string]$dlls["AutoFixtureDim.dll"].path)))
(load $(ConvertTo-LispString $lspPath))
(c:RUNCORECAD)
_.QUIT
_N
"@
Set-Content -LiteralPath $driverPath -Value $driver -Encoding ASCII

$result = [ordered]@{
    status = "Running"
    error = $null
    caseId = $CaseId
    startedAtUtc = $null
    finishedAtUtc = $null
    processId = $null
    runDirectory = $runDirectory
    fixture = $fixturePath
    fixtureSha256 = $fixtureHash
    workingDrawing = $workingDwg
    dlls = $dlls
    reportPath = $reportPath
    tracePath = $tracePath
    standardOutputPath = $stdoutPath
    standardErrorPath = $stderrPath
    executionHost = $coreConsoleExe
}
$failure = $null
try {
    $startedAt = [DateTime]::UtcNow
    $result.startedAtUtc = $startedAt.ToString("o", [Globalization.CultureInfo]::InvariantCulture)
    $previousReportPath = $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH
    $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH = $reportPath
    try {
        $arguments = @("/i", ('"{0}"' -f $workingDwg), "/s", ('"{0}"' -f $driverPath), "/l", "en-US")
        $process = Start-Process -FilePath $coreConsoleExe -ArgumentList $arguments -WindowStyle Hidden `
            -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    }
    finally {
        $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH = $previousReportPath
    }
    $result.processId = $process.Id
    $deadline = $startedAt.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $process.Refresh()
        if ($process.HasExited) { break }
        Start-Sleep -Seconds 2
    }
    $process.Refresh()
    if (-not $process.HasExited) {
        $result.status = "TimedOut"
        throw "AutoCAD Core Console regression timed out; process was not killed. PID=$($process.Id)."
    }
    if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) {
        throw "AutoCAD produced no trace."
    }
    $trace = @(Get-Content -LiteralPath $tracePath)
    $errorLine = $trace | Where-Object { $_ -like "ERROR *" } | Select-Object -First 1
    if ($null -ne $errorLine) { throw "CAD trace failure: $errorLine" }
    if ($trace -notcontains "COMPLETE") { throw "CAD trace has no COMPLETE marker." }
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw "Diagnostic report was not generated." }

    & (Join-Path $PSScriptRoot "validate-core-cad-report.ps1") `
        -CaseId $CaseId `
        -ReportPath $reportPath `
        -RunStartedAt $startedAt `
        -AllowNotReady:$AllowNotReady
    if ($LASTEXITCODE -ne 0) { throw "Core-CAD report validation failed." }

    if ($case.fixtureReady -ne $true) {
        $case.fixtureReady = $true
        $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $casesPath -Encoding UTF8
    }
    $result.status = "Passed"
}
catch {
    $failure = $_
    if ($result.status -eq "Running") { $result.status = "Failed" }
    $result.error = $_.Exception.Message
}
finally {
    $result.finishedAtUtc = [DateTime]::UtcNow.ToString("o", [Globalization.CultureInfo]::InvariantCulture)
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultPath -Encoding UTF8
}

if ($null -ne $failure) { throw $failure }
$result | ConvertTo-Json -Depth 12
