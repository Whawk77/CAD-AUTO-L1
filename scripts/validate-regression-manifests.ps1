$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$manifestPaths = @(
    "regression\core-cad\cases.json",
    "regression\dimension-layout\cases.json",
    "regression\hole-slot\cases.json"
)
$caseIds = @{}
$caseCount = 0

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    $stream = [System.IO.File]::OpenRead($Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($stream))).Replace("-", "")
    }
    finally {
        $sha.Dispose()
        $stream.Dispose()
    }
}

foreach ($relativeManifestPath in $manifestPaths) {
    $manifestPath = Join-Path $projectRoot $relativeManifestPath
    try {
        $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
    }
    catch {
        throw "Invalid JSON in '$relativeManifestPath': $($_.Exception.Message)"
    }

    if (-not ($manifest.PSObject.Properties.Name -contains "cases")) {
        throw "Manifest '$relativeManifestPath' has no cases property."
    }
    $cases = @($manifest.cases)
    if ($cases.Count -eq 0) {
        throw "Manifest '$relativeManifestPath' must contain at least one case."
    }

    $regressionRoot = Split-Path -Parent $manifestPath
    foreach ($case in $cases) {
        $caseId = [string]$case.id
        if ([string]::IsNullOrWhiteSpace($caseId)) {
            throw "A case in '$relativeManifestPath' has no id."
        }
        if ($caseIds.ContainsKey($caseId)) {
            throw "Duplicate case id '$caseId' in '$relativeManifestPath' and '$($caseIds[$caseId])'."
        }
        $caseIds[$caseId] = $relativeManifestPath

        if ($case.fixtureReady -isnot [bool]) {
            throw "Case '$caseId' in '$relativeManifestPath' must declare fixtureReady as a boolean."
        }

        $fixturePath = Join-Path $regressionRoot ([string]$case.fixture)
        if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
            throw "Fixture for case '$caseId' was not found: $fixturePath"
        }
        $actualHash = Get-Sha256 -Path $fixturePath
        if ($actualHash -ne [string]$case.fixtureSha256) {
            throw "Fixture hash mismatch for case '$caseId'. Expected $($case.fixtureSha256), found $actualHash."
        }

        foreach ($property in @($case.PSObject.Properties | Where-Object { $_.Name -match "Expectation$" })) {
            $expectationRelativePath = [string]$property.Value
            if ([string]::IsNullOrWhiteSpace($expectationRelativePath)) {
                throw "Case '$caseId' declares an empty $($property.Name) path."
            }
            $expectationPath = Join-Path $regressionRoot $expectationRelativePath
            if (-not (Test-Path -LiteralPath $expectationPath -PathType Leaf)) {
                throw "$($property.Name) for case '$caseId' was not found: $expectationPath"
            }
        }

        $caseCount++
    }
}

Write-Output "PASS regression manifests: manifests=$($manifestPaths.Count), cases=$caseCount"
