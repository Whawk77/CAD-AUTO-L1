param(
    [Parameter(Mandatory = $true)]
    [string]$CaseId,

    [string]$ReportPath,

    [double]$GeometryTolerance = 0.001,

    [datetime]$RunStartedAt = [datetime]::MinValue,

    [switch]$AllowNotReady
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$casesPath = Join-Path $projectRoot "regression\hole-slot\cases.json"
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $projectRoot "diagnostics\last-run.json"
}

$cases = Get-Content -Raw -Encoding UTF8 -LiteralPath $casesPath | ConvertFrom-Json
$case = @($cases.cases | Where-Object { $_.id -eq $CaseId })
if ($case.Count -ne 1) {
    throw "Regression case '$CaseId' was not found or is duplicated in $casesPath."
}
$case = $case[0]

if ($case.fixtureReady -ne $true -and -not $AllowNotReady) {
    throw "Case '$CaseId' has fixtureReady=false - it has never passed a real run. Use -AllowNotReady for the bring-up run, and only set fixtureReady=true after a genuine pass."
}

$fixturePath = Join-Path (Join-Path $projectRoot "regression\hole-slot") ([string]$case.fixture)
if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
    throw "Fixture was not found: $fixturePath"
}
$fixtureHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fixturePath).Hash
if ($fixtureHash -ne [string]$case.fixtureSha256) {
    throw "Fixture hash mismatch for '$CaseId'. Expected $($case.fixtureSha256), found $fixtureHash."
}

if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
    throw "Diagnostic report was not found: $ReportPath"
}
$report = Get-Content -Raw -Encoding UTF8 -LiteralPath $ReportPath | ConvertFrom-Json

# Run-identity guards (same contract as validate-dimension-layout-report.ps1).
if (-not ($report.PSObject.Properties.Name -contains "runId") -or [string]::IsNullOrWhiteSpace([string]$report.runId)) {
    throw "Report has no runId - not produced by a current run."
}
if ($report.PSObject.Properties.Name -contains "error") {
    throw "Report records a FAILED run: $($report.error.type): $($report.error.message)"
}
if (-not ($report.PSObject.Properties.Name -contains "drawing") -or [string]$report.drawing.sha256 -ne [string]$case.fixtureSha256) {
    throw "Report drawing sha256 '$($report.drawing.sha256)' does not match fixture hash - report came from a different drawing."
}
if ($RunStartedAt -ne [datetime]::MinValue) {
    $generatedAt = [datetime]::Parse([string]$report.generatedAt, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    if ($generatedAt -lt $RunStartedAt) {
        throw "Report generatedAt '$generatedAt' predates run start '$RunStartedAt' - stale report."
    }
}
$requiredFields = @(
    "firstPointX", "firstPointY", "secondPointX", "secondPointY",
    "measurementMinimum", "measurementMaximum", "isSelected",
    "isAttachmentValid", "decisionStatus", "decisionReason"
)

foreach ($candidate in @($report.dimensionCandidates)) {
    foreach ($field in $requiredFields) {
        if (-not ($candidate.PSObject.Properties.Name -contains $field)) {
            throw "Candidate $($candidate.id) is missing required field '$field'."
        }
    }
}

foreach ($dimension in @($report.finalDimensions)) {
    if (-not $dimension.isSelected -or $dimension.decisionStatus -ne "Selected") {
        throw "Final dimension $($dimension.id) is not marked Selected."
    }
    if (-not $dimension.isAttachmentValid) {
        throw "Final dimension $($dimension.id) has an invalid attachment."
    }
    if (($dimension.measurementMaximum - $dimension.measurementMinimum) -le $GeometryTolerance) {
        throw "Final dimension $($dimension.id) has zero or sub-tolerance length."
    }
}

foreach ($skipped in @($report.dimensionCandidates | Where-Object { $_.decisionStatus -eq "Skipped" })) {
    if ($skipped.isAttachmentValid) {
        throw "Skipped dimension $($skipped.id) incorrectly claims a valid attachment."
    }
    if ([string]::IsNullOrWhiteSpace([string]$skipped.decisionReason)) {
        throw "Skipped dimension $($skipped.id) has no decision reason."
    }
}

foreach ($required in @($case.requiredFinal)) {
    $matches = @($report.finalDimensions | Where-Object { $_.debugRole -eq $required.debugRole })
    $minimum = if ($null -eq $required.minCount) { 1 } else { [int]$required.minCount }
    if ($matches.Count -lt $minimum) {
        throw "Case '$CaseId' expected at least $minimum final '$($required.debugRole)' dimensions, found $($matches.Count)."
    }
    if ($null -ne $required.maxCount -and $matches.Count -gt [int]$required.maxCount) {
        throw "Case '$CaseId' expected at most $($required.maxCount) final '$($required.debugRole)' dimensions, found $($matches.Count)."
    }
}

$skippedCount = @($report.dimensionCandidates | Where-Object { $_.decisionStatus -eq "Skipped" }).Count
Write-Output "PASS ${CaseId}: final=$(@($report.finalDimensions).Count), skipped=$skippedCount, report=$ReportPath"
Write-Output "Manual checks:"
foreach ($check in @($case.manualChecks)) {
    Write-Output "- $check"
}
