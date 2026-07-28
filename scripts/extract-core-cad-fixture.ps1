[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$CaseId,

    [string]$DiagnosticPath,

    [ValidateRange(30, 1800)]
    [int]$TimeoutSeconds = 300,

    [switch]$AllowSourceHashChange,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$regressionRoot = Join-Path $projectRoot "regression\core-cad"
$casesPath = Join-Path $regressionRoot "cases.json"
if ([string]::IsNullOrWhiteSpace($DiagnosticPath)) {
    $DiagnosticPath = Join-Path $projectRoot "diagnostics\last-run.json"
}

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

if (-not (Test-Path -LiteralPath $casesPath -PathType Leaf)) {
    throw "Core-CAD case manifest not found: $casesPath"
}
$manifest = Get-Content -LiteralPath $casesPath -Raw -Encoding UTF8 | ConvertFrom-Json
$caseMatches = @($manifest.cases | Where-Object { $_.id -eq $CaseId })
if ($caseMatches.Count -ne 1) {
    throw "Core-CAD case '$CaseId' was not found or is duplicated."
}
$case = $caseMatches[0]

if (-not (Test-Path -LiteralPath $DiagnosticPath -PathType Leaf)) {
    throw "Diagnostic report not found: $DiagnosticPath"
}
$diagnostic = Get-Content -LiteralPath $DiagnosticPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$diagnostic.schemaVersion -ne 2 -or [string]::IsNullOrWhiteSpace([string]$diagnostic.runId)) {
    throw "Diagnostic report must be a current schema v2 report."
}
if ([string]$diagnostic.drawing.sha256 -ne [string]$case.sourceDrawingSha256) {
    throw "Diagnostic source hash does not match case sourceDrawingSha256."
}
$sourceDwg = [string]$diagnostic.drawing.path
if (-not (Test-Path -LiteralPath $sourceDwg -PathType Leaf)) {
    throw "Source DWG not found: $sourceDwg"
}
$sourceHash = Get-Sha256 -Path $sourceDwg
if ($sourceHash -ne ([string]$diagnostic.drawing.sha256).ToUpperInvariant()) {
    if (-not $AllowSourceHashChange) {
        throw "Source DWG hash changed since the diagnostic run. Use -AllowSourceHashChange only when the recorded source handles are still authoritative."
    }
    Write-Warning "Source DWG hash changed; extraction will still require every recorded handle to exist and be a LINE."
}

$handles = @($diagnostic.selection.entityHandles | ForEach-Object { [string]$_ })
$expectedCount = [int]$case.selection.expectedCount
if ($handles.Count -ne $expectedCount -or ($handles | Select-Object -Unique).Count -ne $expectedCount) {
    throw "Diagnostic handle count '$($handles.Count)' does not match expected '$expectedCount'."
}
$typeProperties = @($diagnostic.selection.entityTypeCounts.PSObject.Properties)
if ($typeProperties.Count -ne 1 -or
    [string]$typeProperties[0].Name -ne [string]$case.selection.entityType -or
    [int]$typeProperties[0].Value -ne $expectedCount) {
    throw "Diagnostic entity types do not match the fixture case."
}

$acadExe = "D:\Program Files\Autodesk\AutoCAD 2020\acad.exe"
if (-not (Test-Path -LiteralPath $acadExe -PathType Leaf)) {
    throw "AutoCAD 2020 not found: $acadExe"
}
$existingCad = @(Get-Process -Name "acad" -ErrorAction SilentlyContinue)
if ($existingCad.Count -gt 0) {
    $pids = ($existingCad | ForEach-Object { $_.Id }) -join ", "
    throw "Close all AutoCAD instances before extracting a deterministic fixture. Active PID(s): $pids"
}

$fixturePath = Join-Path $regressionRoot ([string]$case.fixture)
$fixtureDirectory = Split-Path -Parent $fixturePath
$null = New-Item -ItemType Directory -Path $fixtureDirectory -Force
if (Test-Path -LiteralPath $fixturePath) {
    if (-not $Force) {
        throw "Fixture already exists: $fixturePath. Use -Force to replace it."
    }
    Remove-Item -LiteralPath $fixturePath -Force
}

$runDirectory = Join-Path (Join-Path $regressionRoot "runs\fixture-extraction\$CaseId") (Get-Date -Format "yyyyMMdd-HHmmssfff")
$null = New-Item -ItemType Directory -Path $runDirectory -Force
$tracePath = Join-Path $runDirectory "trace.log"
$lspPath = Join-Path $runDirectory "extract.lsp"
$driverPath = Join-Path $runDirectory "extract.scr"
$handleLiteral = "(" + (($handles | ForEach-Object { ConvertTo-LispString $_ }) -join " ") + ")"

$lsp = @"
(vl-load-com)
(setq *ccr-trace* $(ConvertTo-LispString $tracePath))
(setq *ccr-output* $(ConvertTo-LispString $fixturePath))
(setq *ccr-handles* '$handleLiteral)
(setq *ccr-expected-count* $expectedCount)

(defun ccr-trace (message / stream)
  (setq stream (open *ccr-trace* "a"))
  (if stream (progn (write-line message stream) (close stream)))
)

(defun c:EXTRACTCORECAD (/ selection entity data ok)
  (ccr-trace "START")
  (setq selection (ssadd))
  (setq ok T)
  (foreach handle *ccr-handles*
    (setq entity (handent handle))
    (if entity
      (progn
        (setq data (entget entity))
        (if (= (cdr (assoc 0 data)) "LINE")
          (ssadd entity selection)
          (progn (ccr-trace (strcat "ERROR nonLineHandle=" handle)) (setq ok nil))
        )
      )
      (progn (ccr-trace (strcat "ERROR missingHandle=" handle)) (setq ok nil))
    )
  )
  (if (and ok (= (sslength selection) *ccr-expected-count*))
    (progn
      (ccr-trace (strcat "INPUT selection=" (itoa (sslength selection))))
      (vl-cmdf "_.-WBLOCK" *ccr-output* "" '(0.0 0.0 0.0) selection "")
      (if (findfile *ccr-output*)
        (ccr-trace "COMPLETE")
        (ccr-trace "ERROR fixtureNotCreated")
      )
    )
    (ccr-trace "ERROR selectionBuildFailed")
  )
  (princ)
)
(princ)
"@
Set-Content -LiteralPath $lspPath -Value $lsp -Encoding ASCII

$driver = @"
(setvar "FILEDIA" 0)
(setvar "CMDDIA" 0)
(load $(ConvertTo-LispString $lspPath))
(c:EXTRACTCORECAD)
_.QUIT
_N
"@
Set-Content -LiteralPath $driverPath -Value $driver -Encoding ASCII

if (-not $PSCmdlet.ShouldProcess($fixturePath, "Extract $expectedCount entities from $sourceDwg")) {
    return
}

$startedAt = [DateTime]::UtcNow
$arguments = @("/nologo", ('"{0}"' -f $sourceDwg), "/b", ('"{0}"' -f $driverPath))
$process = Start-Process -FilePath $acadExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
$deadline = $startedAt.AddSeconds($TimeoutSeconds)
while ([DateTime]::UtcNow -lt $deadline) {
    $process.Refresh()
    if ($process.HasExited) { break }
    Start-Sleep -Seconds 2
}
$process.Refresh()
if (-not $process.HasExited) {
    throw "AutoCAD fixture extraction timed out; process was not killed. PID=$($process.Id)."
}
if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) {
    throw "Fixture extraction produced no trace: $tracePath"
}
$trace = @(Get-Content -LiteralPath $tracePath)
$errorLine = $trace | Where-Object { $_ -like "ERROR *" } | Select-Object -First 1
if ($null -ne $errorLine) {
    throw "Fixture extraction failed: $errorLine"
}
if ($trace -notcontains "COMPLETE" -or -not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
    throw "Fixture extraction did not complete. Trace: $tracePath"
}

$fixtureInfo = Get-Item -LiteralPath $fixturePath
$fixtureHash = Get-Sha256 -Path $fixturePath
$case.fixtureSha256 = $fixtureHash
$case.fixtureSizeBytes = [long]$fixtureInfo.Length
$case.fixtureReady = $false
$case | Add-Member -NotePropertyName extractedFromSourceSha256 -NotePropertyValue $sourceHash -Force
$case | Add-Member -NotePropertyName sourceDiagnosticRunId -NotePropertyValue ([string]$diagnostic.runId) -Force
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $casesPath -Encoding UTF8

Write-Output "EXTRACTED $CaseId"
Write-Output "Fixture: $fixturePath"
Write-Output "SHA256: $fixtureHash"
Write-Output "Size: $($fixtureInfo.Length) bytes"
Write-Output "Trace: $tracePath"
Write-Output "fixtureReady remains false until the first genuine regression pass."
