[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CaseId,

    [Parameter(Mandatory = $true)]
    [string]$ReportPath,

    [datetime]$RunStartedAt = [datetime]::MinValue,

    [switch]$AllowNotReady
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$regressionRoot = Join-Path $projectRoot "regression\core-cad"
$casesPath = Join-Path $regressionRoot "cases.json"

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

function Get-OptionalProperty {
    param($Value, [string]$Name, $DefaultValue = $null)
    if ($null -eq $Value) { return $DefaultValue }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $DefaultValue }
    return $property.Value
}

function Test-ExpectationMatch {
    param($Candidate, $Expectation, [double]$Tolerance)

    $role = Get-OptionalProperty $Expectation "debugRole" ""
    if (-not [string]::IsNullOrWhiteSpace([string]$role) -and
        [string]$Candidate.debugRole -ne [string]$role) {
        return $false
    }

    $valueApprox = Get-OptionalProperty $Expectation "valueApprox"
    if ($null -ne $valueApprox -and
        [Math]::Abs([double]$Candidate.value - [double]$valueApprox) -gt $Tolerance) {
        return $false
    }

    foreach ($field in @("placementSide", "orientation", "decisionStatus", "decisionReason", "suppressedReason")) {
        $expected = Get-OptionalProperty $Expectation $field
        if ($null -ne $expected -and -not [string]::IsNullOrWhiteSpace([string]$expected)) {
            $actual = Get-OptionalProperty $Candidate $field ""
            if ([string]$actual -ne [string]$expected) { return $false }
        }
    }
    return $true
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
if ($case.fixtureReady -ne $true -and -not $AllowNotReady) {
    throw "Case '$CaseId' has fixtureReady=false. Use -AllowNotReady only for bring-up."
}

$fixturePath = Join-Path $regressionRoot ([string]$case.fixture)
if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
    throw "Fixture not found: $fixturePath"
}
$fixtureHash = Get-Sha256 -Path $fixturePath
if ([string]::IsNullOrWhiteSpace([string]$case.fixtureSha256) -or
    $fixtureHash -ne ([string]$case.fixtureSha256).ToUpperInvariant()) {
    throw "Fixture hash mismatch for '$CaseId'. Expected '$($case.fixtureSha256)', found '$fixtureHash'."
}

if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
    throw "Diagnostic report not found: $ReportPath"
}
$report = Get-Content -LiteralPath $ReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$report.schemaVersion -ne 2) {
    throw "Report schemaVersion must be 2."
}
if ([string]::IsNullOrWhiteSpace([string]$report.runId)) {
    throw "Report has no runId."
}
if ($report.PSObject.Properties["error"]) {
    throw "Report records a failed run: $($report.error.message)"
}
if ([string]$report.drawing.sha256 -ne $fixtureHash) {
    throw "Report drawing hash '$($report.drawing.sha256)' does not match fixture '$fixtureHash'."
}
if ([string]$report.command -ne "ASDREPRO" -or
    [string]$report.commandScope -ne [string]$case.commandScope -or
    [string]$report.diagnosticSide -ne [string]$case.diagnosticSide) {
    throw "Report command/scope/side does not match case '$CaseId'."
}
if ($RunStartedAt -ne [datetime]::MinValue) {
    $generatedAt = [datetime]::Parse(
        [string]$report.generatedAt,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind)
    if ($generatedAt.ToUniversalTime() -lt $RunStartedAt.ToUniversalTime().AddSeconds(-2)) {
        throw "Report is stale: generatedAt '$generatedAt', run start '$RunStartedAt'."
    }
}

$tolerance = [double]$case.valueTolerance
$finalDimensions = @($report.finalDimensions)
$candidates = @($report.dimensionCandidates)

foreach ($expectation in @($case.requiredFinal)) {
    $matches = @($finalDimensions | Where-Object { Test-ExpectationMatch $_ $expectation $tolerance })
    $minimum = Get-OptionalProperty $expectation "minCount" 1
    $maximum = Get-OptionalProperty $expectation "maxCount"
    if ($matches.Count -lt [int]$minimum) {
        throw "Required final dimension missing: role=$($expectation.debugRole), value=$($expectation.valueApprox), found=$($matches.Count)."
    }
    if ($null -ne $maximum -and $matches.Count -gt [int]$maximum) {
        throw "Too many final dimensions: role=$($expectation.debugRole), value=$($expectation.valueApprox), found=$($matches.Count), max=$maximum."
    }
}

foreach ($expectation in @($case.forbiddenFinal)) {
    $matches = @($finalDimensions | Where-Object { Test-ExpectationMatch $_ $expectation $tolerance })
    if ($matches.Count -gt 0) {
        $ids = ($matches | ForEach-Object { $_.id }) -join ","
        throw "Forbidden final dimension exists: role=$($expectation.debugRole), value=$($expectation.valueApprox), ids=$ids."
    }
}

foreach ($expectation in @($case.requiredCandidates)) {
    $matches = @($candidates | Where-Object { Test-ExpectationMatch $_ $expectation $tolerance })
    $minimum = Get-OptionalProperty $expectation "minCount" 1
    $maximum = Get-OptionalProperty $expectation "maxCount"
    if ($matches.Count -lt [int]$minimum) {
        throw "Required candidate decision missing: role=$($expectation.debugRole), value=$($expectation.valueApprox), status=$($expectation.decisionStatus)."
    }
    if ($null -ne $maximum -and $matches.Count -gt [int]$maximum) {
        throw "Too many matching candidate decisions for role=$($expectation.debugRole), value=$($expectation.valueApprox)."
    }
}

Write-Output "PASS ${CaseId}: final=$($finalDimensions.Count), candidates=$($candidates.Count), report=$ReportPath"
