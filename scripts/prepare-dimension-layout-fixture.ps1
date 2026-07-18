param(
    [Parameter(Mandatory = $true)]
    [string]$CaseId,

    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$regressionRoot = Join-Path $projectRoot "regression\dimension-layout"
$casesPath = Join-Path $regressionRoot "cases.json"

$cases = Get-Content -Raw -Encoding UTF8 -LiteralPath $casesPath | ConvertFrom-Json
$case = @($cases.cases | Where-Object { $_.id -eq $CaseId })
if ($case.Count -ne 1) {
    throw "Regression case '$CaseId' was not found or is duplicated in $casesPath."
}
$case = $case[0]

$fixturePath = Join-Path $regressionRoot ([string]$case.fixture)
if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
    throw "Fixture was not found: $fixturePath"
}

$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fixturePath).Hash
if ($actualHash -ne [string]$case.fixtureSha256) {
    throw "Fixture hash mismatch. Expected $($case.fixtureSha256), found $actualHash."
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $regressionRoot "runs"
}
elseif (-not [IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot $OutputRoot
}

$runId = Get-Date -Format "yyyyMMdd-HHmmssfff"
$runDirectory = Join-Path (Join-Path $OutputRoot $CaseId) $runId
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$workingDrawing = Join-Path $runDirectory ([IO.Path]::GetFileName([string]$case.fixture))
Copy-Item -LiteralPath $fixturePath -Destination $workingDrawing
(Get-Item -LiteralPath $workingDrawing).IsReadOnly = $false

$runMetadata = [ordered]@{
    schemaVersion = 1
    caseId = $CaseId
    createdAt = (Get-Date).ToString("o")
    sourceFixture = $fixturePath
    sourceSha256 = $actualHash
    workingDrawing = $workingDrawing
    diagnosticCommand = [string]$case.diagnosticCommand
    diagnosticSide = [string]$case.diagnosticSide
    commandScope = [string]$case.commandScope
}
$runMetadataPath = Join-Path $runDirectory "run.json"
$runMetadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $runMetadataPath -Encoding UTF8

Write-Output "Prepared immutable-fixture copy: $workingDrawing"
Write-Output "Run metadata: $runMetadataPath"
