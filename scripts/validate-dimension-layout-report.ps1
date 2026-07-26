param(
    [Parameter(Mandatory = $true)]
    [string]$CaseId,

    [ValidateSet("baseline", "target")]
    [string]$Expectation = "target",

    [string]$ReportPath,

    [double]$CoordinateTolerance = 0.001,

    [datetime]$RunStartedAt = [datetime]::MinValue
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$regressionRoot = Join-Path $projectRoot "regression\dimension-layout"
$casesPath = Join-Path $regressionRoot "cases.json"

function Test-HasProperty {
    param($Object, [string]$Name)
    return $null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name
}

function Test-DimensionSelector {
    param($Dimension, $Selector)

    foreach ($property in $Selector.PSObject.Properties) {
        if ($property.Name -eq "valueApprox" -or $property.Name -eq "valueTolerance") {
            continue
        }
        if (-not (Test-HasProperty -Object $Dimension -Name $property.Name)) {
            return $false
        }
        if ([string]$Dimension.($property.Name) -ne [string]$property.Value) {
            return $false
        }
    }

    if (Test-HasProperty -Object $Selector -Name "valueApprox") {
        if (-not (Test-HasProperty -Object $Dimension -Name "value")) {
            return $false
        }
        $tolerance = if (Test-HasProperty -Object $Selector -Name "valueTolerance") {
            [double]$Selector.valueTolerance
        }
        else {
            0.001
        }
        if ([Math]::Abs([double]$Dimension.value - [double]$Selector.valueApprox) -gt $tolerance) {
            return $false
        }
    }

    return $true
}

function Get-PhysicalRank {
    param($Dimension, [string]$Side)

    if (-not (Test-HasProperty -Object $Dimension -Name "resolvedDimLineCoordinate") -or $null -eq $Dimension.resolvedDimLineCoordinate) {
        throw "Dimension $($Dimension.id) has no resolvedDimLineCoordinate."
    }

    $coordinate = [double]$Dimension.resolvedDimLineCoordinate
    switch ($Side) {
        "Top" { return $coordinate }
        "Right" { return $coordinate }
        "Bottom" { return -$coordinate }
        "Left" { return -$coordinate }
        default { throw "Unsupported diagnostic side '$Side'." }
    }
}

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
$fixtureHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $fixturePath).Hash
if ($fixtureHash -ne [string]$case.fixtureSha256) {
    throw "Fixture hash mismatch. Expected $($case.fixtureSha256), found $fixtureHash."
}

$expectationRelativePath = if ($Expectation -eq "baseline") {
    [string]$case.baselineExpectation
}
else {
    [string]$case.targetExpectation
}
if ([string]::IsNullOrWhiteSpace($expectationRelativePath)) {
    throw "Expectation '$Expectation' is not configured for case '$CaseId'."
}
$expectationPath = Join-Path $regressionRoot $expectationRelativePath
$expected = Get-Content -Raw -Encoding UTF8 -LiteralPath $expectationPath | ConvertFrom-Json
if ([string]$expected.caseId -ne $CaseId) {
    throw "Expectation case id '$($expected.caseId)' does not match '$CaseId'."
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $projectRoot "diagnostics\last-run.json"
}
elseif (-not [IO.Path]::IsPathRooted($ReportPath)) {
    $ReportPath = Join-Path $projectRoot $ReportPath
}
if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
    throw "Diagnostic report was not found: $ReportPath"
}

$report = Get-Content -Raw -Encoding UTF8 -LiteralPath $ReportPath | ConvertFrom-Json

# Run-identity guards: a report that merely has the right side may still be a stale
# archive from an earlier run or a different drawing.
if (-not (Test-HasProperty -Object $report -Name "runId") -or [string]::IsNullOrWhiteSpace([string]$report.runId)) {
    throw "Report has no runId - not produced by a current ASD4 run."
}
if (Test-HasProperty -Object $report -Name "error") {
    throw "Report records a FAILED run: $($report.error.type): $($report.error.message)"
}
if (-not (Test-HasProperty -Object $report -Name "drawing") -or [string]$report.drawing.sha256 -ne [string]$case.fixtureSha256) {
    throw "Report drawing sha256 '$($report.drawing.sha256)' does not match fixture hash '$($case.fixtureSha256)' - report came from a different drawing."
}
if ($RunStartedAt -ne [datetime]::MinValue) {
    $generatedAt = [datetime]::Parse([string]$report.generatedAt, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    if ($generatedAt -lt $RunStartedAt) {
        throw "Report generatedAt '$generatedAt' predates run start '$RunStartedAt' - stale report."
    }
}

$side = [string]$case.diagnosticSide
if ([string]$report.diagnosticSide -ne $side) {
    throw "Expected diagnostic side '$side', found '$($report.diagnosticSide)'."
}

$finalDimensions = @($report.finalDimensions | Where-Object {
    $_.placementSide -eq $side -and $_.isSelected -and $_.decisionStatus -eq "Selected"
})
if ($finalDimensions.Count -eq 0) {
    throw "Report has no selected final dimensions on side '$side'."
}

foreach ($dimension in $finalDimensions) {
    foreach ($field in @($expected.requiredDimensionFields)) {
        if (-not (Test-HasProperty -Object $dimension -Name ([string]$field))) {
            throw "Dimension $($dimension.id) is missing required field '$field'."
        }
    }
    if ($expected.requirePhysicalOrderValidated -eq $true -and $dimension.physicalOrderValidated -ne $true) {
        throw "Dimension $($dimension.id) did not pass physical order validation."
    }
    if ($expected.requirePhysicalOrderValidated -eq $true -and [double]$dimension.physicalOutwardDistance -lt 0.0) {
        throw "Dimension $($dimension.id) has a negative physicalOutwardDistance."
    }
}

$blockResults = @{}
$claimedDimensionIds = @{}
foreach ($block in @($expected.blocks)) {
    $matches = @()
    foreach ($dimension in $finalDimensions) {
        $matched = $false
        foreach ($selector in @($block.selectors)) {
            if (Test-DimensionSelector -Dimension $dimension -Selector $selector) {
                $matched = $true
                break
            }
        }
        if ($matched) {
            $matches += $dimension
        }
    }

    $minimum = if (Test-HasProperty -Object $block -Name "minCount") { [int]$block.minCount } else { 1 }
    if ($matches.Count -lt $minimum) {
        throw "Block '$($block.id)' expected at least $minimum members, found $($matches.Count)."
    }
    if ((Test-HasProperty -Object $block -Name "maxCount") -and $matches.Count -gt [int]$block.maxCount) {
        throw "Block '$($block.id)' expected at most $($block.maxCount) members, found $($matches.Count)."
    }

    foreach ($dimension in $matches) {
        $identity = [string]$dimension.id
        if ($claimedDimensionIds.ContainsKey($identity)) {
            throw "Dimension $identity matched both '$($claimedDimensionIds[$identity])' and '$($block.id)'."
        }
        $claimedDimensionIds[$identity] = [string]$block.id
    }

    $ranks = @($matches | ForEach-Object { Get-PhysicalRank -Dimension $_ -Side $side })
    if ($block.requireSameCoordinate -eq $true) {
        $minimumRank = [double](($ranks | Measure-Object -Minimum).Minimum)
        $maximumRank = [double](($ranks | Measure-Object -Maximum).Maximum)
        if (($maximumRank - $minimumRank) -gt $CoordinateTolerance) {
            throw "Block '$($block.id)' was split across multiple physical coordinates."
        }
    }

    if ($block.requireSameLayoutBlock -eq $true) {
        $layoutBlockIds = @($matches | ForEach-Object { [string]$_.layoutBlockId } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
        if ($layoutBlockIds.Count -ne 1) {
            throw "Block '$($block.id)' expected one shared layoutBlockId, found $($layoutBlockIds.Count)."
        }
    }

    if (Test-HasProperty -Object $block -Name "expectedEffectiveSpan") {
        $spanTolerance = if (Test-HasProperty -Object $block -Name "effectiveSpanTolerance") {
            [double]$block.effectiveSpanTolerance
        }
        else {
            0.001
        }
        foreach ($dimension in $matches) {
            if (-not (Test-HasProperty -Object $dimension -Name "effectiveSpan")) {
                throw "Dimension $($dimension.id) is missing effectiveSpan."
            }
            if ([Math]::Abs([double]$dimension.effectiveSpan - [double]$block.expectedEffectiveSpan) -gt $spanTolerance) {
                throw "Block '$($block.id)' expected effectiveSpan $($block.expectedEffectiveSpan), found $($dimension.effectiveSpan)."
            }
        }
    }

    $blockResults[[string]$block.id] = [pscustomobject]@{
        Members = $matches
        MinimumRank = [double](($ranks | Measure-Object -Minimum).Minimum)
        MaximumRank = [double](($ranks | Measure-Object -Maximum).Maximum)
    }
}

$outwardOrder = @()
if (Test-HasProperty -Object $expected -Name "outwardOrder") {
    $outwardOrder = @($expected.outwardOrder)
}
for ($index = 0; $index -lt $outwardOrder.Count - 1; $index++) {
    $innerId = [string]$outwardOrder[$index]
    $outerId = [string]$outwardOrder[$index + 1]
    if (-not $blockResults.ContainsKey($innerId) -or -not $blockResults.ContainsKey($outerId)) {
        throw "Outward order references an unknown block: '$innerId' or '$outerId'."
    }
    $innerMaximum = [double]$blockResults[$innerId].MaximumRank
    $outerMinimum = [double]$blockResults[$outerId].MinimumRank
    if ($innerMaximum -ge ($outerMinimum - $CoordinateTolerance)) {
        throw "Physical order violation: '$innerId' must be inside '$outerId'."
    }
}

$outwardConstraints = @()
if (Test-HasProperty -Object $expected -Name "outwardConstraints") {
    $outwardConstraints = @($expected.outwardConstraints)
}
foreach ($constraint in $outwardConstraints) {
    $innerId = [string]$constraint.inner
    $outerId = [string]$constraint.outer
    if (-not $blockResults.ContainsKey($innerId) -or -not $blockResults.ContainsKey($outerId)) {
        throw "Outward constraint references an unknown block: '$innerId' or '$outerId'."
    }
    $innerMaximum = [double]$blockResults[$innerId].MaximumRank
    $outerMinimum = [double]$blockResults[$outerId].MinimumRank
    if ($innerMaximum -ge ($outerMinimum - $CoordinateTolerance)) {
        throw "Physical order violation: '$innerId' must be inside '$outerId'."
    }
}

if ($expected.requireContiguousStackingLevels -eq $true) {
    $allowMultipleCoordinatesPerLevel = (Test-HasProperty -Object $expected -Name "allowMultipleCoordinatesPerLevel") -and $expected.allowMultipleCoordinatesPerLevel -eq $true
    $levelGroups = @($finalDimensions | Group-Object stackingLevel | Sort-Object { [int]$_.Name })
    for ($index = 0; $index -lt $levelGroups.Count; $index++) {
        if ([int]$levelGroups[$index].Name -ne $index) {
            throw "Stacking levels are not contiguous from zero."
        }
    }

    $representativeRanks = @()
    foreach ($levelGroup in $levelGroups) {
        $levelRanks = @($levelGroup.Group | ForEach-Object { Get-PhysicalRank -Dimension $_ -Side $side })
        $minimumRank = [double](($levelRanks | Measure-Object -Minimum).Minimum)
        $maximumRank = [double](($levelRanks | Measure-Object -Maximum).Maximum)
        if (-not $allowMultipleCoordinatesPerLevel -and ($maximumRank - $minimumRank) -gt $CoordinateTolerance) {
            throw "Stacking level $($levelGroup.Name) maps to multiple physical coordinates."
        }
        $representativeRanks += $minimumRank
    }

    if (-not $allowMultipleCoordinatesPerLevel -and (Test-HasProperty -Object $expected -Name "expectedLayerStep")) {
        $stepTolerance = [double]$expected.layerStepTolerance
        for ($index = 0; $index -lt $representativeRanks.Count - 1; $index++) {
            $actualStep = [double]$representativeRanks[$index + 1] - [double]$representativeRanks[$index]
            if ([Math]::Abs($actualStep - [double]$expected.expectedLayerStep) -gt $stepTolerance) {
                throw "Unexpected layer step between L$index and L$($index + 1): $actualStep."
            }
        }
    }
}

Write-Output "PASS ${CaseId}/${Expectation}: side=$side, final=$($finalDimensions.Count), fixture=$fixturePath, report=$ReportPath"
Write-Output "Manual checks:"
foreach ($check in @($case.manualChecks)) {
    Write-Output "- $check"
}
