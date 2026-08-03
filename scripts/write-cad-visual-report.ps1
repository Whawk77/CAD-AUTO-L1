[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SnapshotPath,
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [Parameter(Mandatory = $true)][string]$MarkdownPath,
    [string]$CaseId,
    [ValidateSet('', 'All', 'Top', 'Bottom', 'Left', 'Right')][string]$ExpectedDirection = '',
    [string]$FullImagePath,
    [string]$DetailImagePath,
    [string]$TruthMatrixPath = '',
    [string]$GatePath = ''
)

$ErrorActionPreference = 'Stop'
if (-not $TruthMatrixPath) { $TruthMatrixPath = Join-Path $PSScriptRoot '..\regression\remediation\case-truth-matrix.json' }
if (-not $GatePath) { $GatePath = Join-Path $PSScriptRoot '..\regression\visual-inspection\visual-gate.json' }

function Get-VisualGate([string]$Path) {
    $gate = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($gate.schemaVersion -ne 1 -or $gate.mode -notin 'WarningOnly', 'Blocking' -or $null -eq $gate.blockingWarningCodes -or $gate.warningOnly -ne 'AllOtherWarnings' -or $null -eq $gate.exemptWarningCodes -or $gate.failurePolicy -ne 'FailOnBlockingWarnings' -or $null -eq $gate.rollback -or $gate.rollback.mode -ne 'WarningOnly' -or $null -eq $gate.rollback.blockingWarningCodes) { throw "Invalid visual gate: $Path" }
    return $gate
}

function Get-Sha256([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($Path)
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose() }
    }
    finally { $sha.Dispose() }
}

$visualGate = Get-VisualGate $GatePath

function Get-Number([string]$Value) {
    $number = 0.0
    if ([double]::TryParse($Value, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$number)) { return $number }
    return $null
}

function Get-Point([string[]]$Fields, [int]$Offset) {
    $x = Get-Number $Fields[$Offset]; $y = Get-Number $Fields[$Offset + 1]
    if ($null -eq $x -or $null -eq $y) { return $null }
    return [pscustomobject]@{ X = $x; Y = $y }
}

function Test-RectOverlap($A, $B) {
    return ($A.MinX -lt $B.MaxX -and $A.MaxX -gt $B.MinX -and $A.MinY -lt $B.MaxY -and $A.MaxY -gt $B.MinY)
}

function Test-HardRectOverlap($A, $B, [double]$Tolerance) {
    return ([math]::Min($A.MaxX, $B.MaxX) - [math]::Max($A.MinX, $B.MinX) -gt $Tolerance -and [math]::Min($A.MaxY, $B.MaxY) - [math]::Max($A.MinY, $B.MinY) -gt $Tolerance)
}

function Test-PointInRect($Point, $Rect) {
    return ($Point.X -ge $Rect.MinX -and $Point.X -le $Rect.MaxX -and $Point.Y -ge $Rect.MinY -and $Point.Y -le $Rect.MaxY)
}

function Get-Orientation($A, $B, $C) { return (($B.X - $A.X) * ($C.Y - $A.Y)) - (($B.Y - $A.Y) * ($C.X - $A.X)) }

function Test-SegmentsIntersect($A, $B, $C, $D) {
    $epsilon = 0.0000001
    $o1 = Get-Orientation $A $B $C; $o2 = Get-Orientation $A $B $D; $o3 = Get-Orientation $C $D $A; $o4 = Get-Orientation $C $D $B
    if (([math]::Abs($o1) -lt $epsilon) -and $C.X -ge [math]::Min($A.X, $B.X) -and $C.X -le [math]::Max($A.X, $B.X) -and $C.Y -ge [math]::Min($A.Y, $B.Y) -and $C.Y -le [math]::Max($A.Y, $B.Y)) { return $true }
    if (([math]::Abs($o2) -lt $epsilon) -and $D.X -ge [math]::Min($A.X, $B.X) -and $D.X -le [math]::Max($A.X, $B.X) -and $D.Y -ge [math]::Min($A.Y, $B.Y) -and $D.Y -le [math]::Max($A.Y, $B.Y)) { return $true }
    if (([math]::Abs($o3) -lt $epsilon) -and $A.X -ge [math]::Min($C.X, $D.X) -and $A.X -le [math]::Max($C.X, $D.X) -and $A.Y -ge [math]::Min($C.Y, $D.Y) -and $A.Y -le [math]::Max($C.Y, $D.Y)) { return $true }
    if (([math]::Abs($o4) -lt $epsilon) -and $B.X -ge [math]::Min($C.X, $D.X) -and $B.X -le [math]::Max($C.X, $D.X) -and $B.Y -ge [math]::Min($C.Y, $D.Y) -and $B.Y -le [math]::Max($C.Y, $D.Y)) { return $true }
    return (($o1 -gt 0) -ne ($o2 -gt 0)) -and (($o3 -gt 0) -ne ($o4 -gt 0))
}

function Test-SegmentRect($A, $B, $Rect) {
    if ([math]::Max($A.X, $B.X) -lt $Rect.MinX -or [math]::Min($A.X, $B.X) -gt $Rect.MaxX -or [math]::Max($A.Y, $B.Y) -lt $Rect.MinY -or [math]::Min($A.Y, $B.Y) -gt $Rect.MaxY) { return $false }
    if ((Test-PointInRect $A $Rect) -or (Test-PointInRect $B $Rect)) { return $true }
    $p1 = [pscustomobject]@{ X = $Rect.MinX; Y = $Rect.MinY }; $p2 = [pscustomobject]@{ X = $Rect.MaxX; Y = $Rect.MinY }
    $p3 = [pscustomobject]@{ X = $Rect.MaxX; Y = $Rect.MaxY }; $p4 = [pscustomobject]@{ X = $Rect.MinX; Y = $Rect.MaxY }
    return (Test-SegmentsIntersect $A $B $p1 $p2) -or (Test-SegmentsIntersect $A $B $p2 $p3) -or (Test-SegmentsIntersect $A $B $p3 $p4) -or (Test-SegmentsIntersect $A $B $p4 $p1)
}

function Get-ExtensionEnd($Feature, $DimensionLine, [double]$Allowance) {
    $dx = $DimensionLine.X - $Feature.X; $dy = $DimensionLine.Y - $Feature.Y; $length = [math]::Sqrt($dx * $dx + $dy * $dy)
    if ($length -le $Allowance) { return $null }
    $scale = ($length - $Allowance) / $length
    return [pscustomobject]@{ X = $Feature.X + $dx * $scale; Y = $Feature.Y + $dy * $scale }
}

function Add-Warning([string]$Code, [string]$Message, [string[]]$Handles = @()) {
    $handles = @($Handles | Where-Object { $_ } | Sort-Object -Unique); $key = "$Code|$($handles -join '|')"
    if ($script:warningKeys.Add($key)) { $script:warnings.Add([pscustomobject]@{ code = $Code; message = $Message; handles = $handles }) }
}

$warnings = [System.Collections.Generic.List[object]]::new()
$warningKeys = [System.Collections.Generic.HashSet[string]]::new()
$records = @{ META = @(); ENV = @(); SOURCE = @(); SEG = @(); DIM = @(); TEXT = @(); ARROW = @(); ERROR = @() }
foreach ($line in Get-Content -LiteralPath $SnapshotPath) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $fields = @($line.Split([char]9)); $kind = $fields[0]
    if (-not $records.ContainsKey($kind)) { Add-Warning 'UNKNOWN_RECORD' "Unknown TSV record: $kind"; continue }
    $records[$kind] += ,$fields
}

foreach ($kind in 'META', 'ENV', 'SOURCE', 'SEG', 'DIM', 'TEXT', 'ARROW') {
    if ($records[$kind].Count -eq 0) { Add-Warning 'SNAPSHOT_MISSING' "Missing $kind record." }
}

$meta = @{}
foreach ($row in $records.META) { if ($row.Count -ge 3) { $meta[$row[1]] = $row[2] } }
if (-not $CaseId) { $CaseId = $meta.caseId }
if (-not $ExpectedDirection) { $ExpectedDirection = $meta.expectedDirection }
if (-not $CaseId) { Add-Warning 'SNAPSHOT_MISSING' 'Missing caseId.' }
if (-not $ExpectedDirection) { Add-Warning 'SNAPSHOT_MISSING' 'Missing expectedDirection.' }
if ($ExpectedDirection -and $ExpectedDirection -notin 'All', 'Top', 'Bottom', 'Left', 'Right') { Add-Warning 'EXPECTED_DIRECTION_INVALID' "Unsupported expectedDirection: $ExpectedDirection" }
foreach ($row in $records.ERROR) { Add-Warning 'SNAPSHOT_ERROR' ($row -join ' | ') }
$environment = @{}
foreach ($row in $records.ENV) { if ($row.Count -ge 3) { $environment[$row[1]] = $row[2] } }
$dimTxt = Get-Number $environment.DIMTXT; $dimAsz = Get-Number $environment.DIMASZ
$positiveScales = @(@($dimTxt, $dimAsz) | Where-Object { $null -ne $_ -and $_ -gt 0 })
$contactTolerance = [math]::Max(0.0000001, $(if ($positiveScales.Count) { ($positiveScales | Measure-Object -Minimum).Minimum * 0.1 } else { 0 }))
$positiveDimAsz = if ($null -ne $dimAsz -and $dimAsz -gt 0) { $dimAsz } else { 0 }

$sources = foreach ($row in $records.SOURCE) {
    if ($row.Count -lt 7) { Add-Warning 'SNAPSHOT_MALFORMED' "Malformed SOURCE record: $($row -join ' | ')"; continue }
    $numbers = 3..6 | ForEach-Object { Get-Number $row[$_] }
    if ($numbers -contains $null) { Add-Warning 'SNAPSHOT_MALFORMED' "SOURCE has invalid bounds: $($row[1])"; continue }
    [pscustomobject]@{ Handle = $row[1]; MinX = [math]::Min($numbers[0], $numbers[2]); MinY = [math]::Min($numbers[1], $numbers[3]); MaxX = [math]::Max($numbers[0], $numbers[2]); MaxY = [math]::Max($numbers[1], $numbers[3]) }
}
$segments = foreach ($row in $records.SEG) {
    if ($row.Count -lt 7) { Add-Warning 'SNAPSHOT_MALFORMED' "Malformed SEG record."; continue }
    $a = Get-Point $row 3; $b = Get-Point $row 5
    if ($null -eq $a -or $null -eq $b) { Add-Warning 'SNAPSHOT_MALFORMED' "SEG has invalid coordinates: $($row[1])"; continue }
    [pscustomobject]@{ Handle = $row[1]; A = $a; B = $b; MinX = [math]::Min($a.X, $b.X); MinY = [math]::Min($a.Y, $b.Y); MaxX = [math]::Max($a.X, $b.X); MaxY = [math]::Max($a.Y, $b.Y) }
}
$dimensions = foreach ($row in $records.DIM) {
    if ($row.Count -lt 12) { Add-Warning 'SNAPSHOT_MALFORMED' 'Malformed DIM record.'; continue }
    $ext1 = Get-Point $row 3; $ext2 = Get-Point $row 5; $definition = Get-Point $row 7; $text = Get-Point $row 9
    if ($null -eq $ext1 -or $null -eq $ext2 -or $null -eq $definition -or $null -eq $text) { Add-Warning 'SNAPSHOT_MALFORMED' "DIM has invalid coordinates: $($row[1])"; continue }
    $rotation = if ($row.Count -ge 13) { Get-Number $row[12] } else { $null }
    $horizontal = if ($null -ne $rotation) { [math]::Abs([math]::Sin($rotation)) -lt [math]::Abs([math]::Cos($rotation)) } else { [math]::Abs($ext2.X - $ext1.X) -ge [math]::Abs($ext2.Y - $ext1.Y) }
    $lineA = if ($horizontal) { [pscustomobject]@{ X = $ext1.X; Y = $definition.Y } } else { [pscustomobject]@{ X = $definition.X; Y = $ext1.Y } }
    $lineB = if ($horizontal) { [pscustomobject]@{ X = $ext2.X; Y = $definition.Y } } else { [pscustomobject]@{ X = $definition.X; Y = $ext2.Y } }
    [pscustomobject]@{ Handle = $row[1]; Ext1 = $ext1; Ext2 = $ext2; Definition = $definition; TextPoint = $text; Rotation = $rotation; Horizontal = $horizontal; LineA = $lineA; LineB = $lineB }
}
$boxes = foreach ($kind in 'TEXT', 'ARROW') { foreach ($row in $records[$kind]) {
    $offset = if ($kind -eq 'TEXT') { 3 } else { 4 }
    if ($row.Count -lt ($offset + 4)) { Add-Warning 'SNAPSHOT_MALFORMED' "Malformed $kind record."; continue }
    $numbers = $offset..($offset + 3) | ForEach-Object { Get-Number $row[$_] }
    if ($numbers -contains $null) { Add-Warning 'SNAPSHOT_MALFORMED' "$kind has invalid bounds: $($row[1])"; continue }
    [pscustomobject]@{ Kind = $kind; DimensionHandle = $row[1]; Handle = $row[2]; MinX = [math]::Min($numbers[0], $numbers[2]); MinY = [math]::Min($numbers[1], $numbers[3]); MaxX = [math]::Max($numbers[0], $numbers[2]); MaxY = [math]::Max($numbers[1], $numbers[3]) }
} }

foreach ($dim in $dimensions) {
    $minX = [math]::Min($dim.LineA.X, $dim.LineB.X); $minY = [math]::Min($dim.LineA.Y, $dim.LineB.Y); $maxX = [math]::Max($dim.LineA.X, $dim.LineB.X); $maxY = [math]::Max($dim.LineA.Y, $dim.LineB.Y)
    foreach ($seg in $segments) { if ($maxX -lt $seg.MinX -or $minX -gt $seg.MaxX -or $maxY -lt $seg.MinY -or $minY -gt $seg.MaxY) { continue }; if (Test-SegmentsIntersect $dim.LineA $dim.LineB $seg.A $seg.B) { Add-Warning 'DIM_SOURCE_CROSSING' "Dimension line intersects source segment." @($dim.Handle, $seg.Handle) } }
}
foreach ($box in $boxes) {
    foreach ($seg in $segments) { if ($box.MaxX -lt $seg.MinX -or $box.MinX -gt $seg.MaxX -or $box.MaxY -lt $seg.MinY -or $box.MinY -gt $seg.MaxY) { continue }; if (Test-SegmentRect $seg.A $seg.B $box) { Add-Warning "$($box.Kind)_SOURCE_COLLISION" "$($box.Kind) box intersects source outline." @($box.DimensionHandle, $box.Handle, $seg.Handle) } }
}
$texts = @($boxes | Where-Object Kind -eq 'TEXT'); $arrows = @($boxes | Where-Object Kind -eq 'ARROW')
for ($i = 0; $i -lt $texts.Count; $i++) { for ($j = $i + 1; $j -lt $texts.Count; $j++) { if (Test-RectOverlap $texts[$i] $texts[$j]) { Add-Warning 'TEXT_TEXT_COLLISION' 'Text boxes overlap.' @($texts[$i].DimensionHandle, $texts[$i].Handle, $texts[$j].DimensionHandle, $texts[$j].Handle) } } }
foreach ($text in $texts) { foreach ($arrow in $arrows) { # MVP: same-DIM block AABBs are intentionally ignored until glyph/arrow polygons are available.
    if ($text.DimensionHandle -ne $arrow.DimensionHandle -and (Test-HardRectOverlap $text $arrow $contactTolerance)) { Add-Warning 'TEXT_ARROW_COLLISION' 'Text and arrow boxes overlap.' @($text.DimensionHandle, $text.Handle, $arrow.DimensionHandle, $arrow.Handle) }
} }
foreach ($dim in $dimensions) { foreach ($text in $texts | Where-Object { $_.DimensionHandle -ne $dim.Handle }) {
    $allowance = [math]::Max($text.MaxX - $text.MinX, $text.MaxY - $text.MinY) + $positiveDimAsz
    $end1 = Get-ExtensionEnd $dim.Ext1 $dim.LineA $allowance; $end2 = Get-ExtensionEnd $dim.Ext2 $dim.LineB $allowance
    if (($end1 -and (Test-SegmentRect $dim.Ext1 $end1 $text)) -or ($end2 -and (Test-SegmentRect $dim.Ext2 $end2 $text))) { Add-Warning 'EXTENSION_TEXT_COLLISION' 'Extension line intersects another dimension text box.' @($dim.Handle, $text.DimensionHandle, $text.Handle) }
} }

$layerSnapshot = @()
if ($sources.Count) {
    $outline = [pscustomobject]@{ MinX = ($sources | Measure-Object MinX -Minimum).Minimum; MinY = ($sources | Measure-Object MinY -Minimum).Minimum; MaxX = ($sources | Measure-Object MaxX -Maximum).Maximum; MaxY = ($sources | Measure-Object MaxY -Maximum).Maximum }
    $leftLayoutTolerance = if ($positiveScales.Count) { ($positiveScales | Measure-Object -Maximum).Maximum } else { 0 }
    foreach ($dim in $dimensions) {
        if ($dim.Horizontal -or $dim.Definition.X -gt $outline.MinX) { continue }
        $extensionMinY = [math]::Min($dim.Ext1.Y, $dim.Ext2.Y); $extensionMaxY = [math]::Max($dim.Ext1.Y, $dim.Ext2.Y)
        foreach ($text in $texts) {
            if ($text.DimensionHandle -ne $dim.Handle) { continue }
            $centerY = ($text.MinY + $text.MaxY) / 2
            $outside = if ($centerY -lt $extensionMinY) { $extensionMinY - $centerY } elseif ($centerY -gt $extensionMaxY) { $centerY - $extensionMaxY } else { 0 }
            if ($outside -gt $leftLayoutTolerance) { Add-Warning 'LEFT_LAYOUT_TEXT_PLACEMENT' 'Left vertical dimension text is outside its extension range.' @($dim.Handle, $text.Handle) }
        }
    }
    foreach ($dim in $dimensions) {
        if ($dim.Horizontal) { $dim | Add-Member Side $(if ($dim.Definition.Y -ge $outline.MaxY) { 'Top' } elseif ($dim.Definition.Y -le $outline.MinY) { 'Bottom' } else { 'Interior' }); $dim | Add-Member Outward $(if ($dim.Definition.Y -ge $outline.MaxY) { $dim.Definition.Y - $outline.MaxY } elseif ($dim.Definition.Y -le $outline.MinY) { $outline.MinY - $dim.Definition.Y } else { 0 })
        } else { $dim | Add-Member Side $(if ($dim.Definition.X -ge $outline.MaxX) { 'Right' } elseif ($dim.Definition.X -le $outline.MinX) { 'Left' } else { 'Interior' }); $dim | Add-Member Outward $(if ($dim.Definition.X -ge $outline.MaxX) { $dim.Definition.X - $outline.MaxX } elseif ($dim.Definition.X -le $outline.MinX) { $outline.MinX - $dim.Definition.X } else { 0 })
        }
        if ($ExpectedDirection -in 'Top', 'Bottom', 'Left', 'Right' -and $dim.Side -ne 'Interior' -and $dim.Side -ne $ExpectedDirection) { Add-Warning 'EXPECTED_DIRECTION_MISMATCH' "Dimension is on $($dim.Side), not expected $ExpectedDirection." @($dim.Handle) }
    }
    foreach ($group in $dimensions | Where-Object Side -ne 'Interior' | Group-Object Side) {
        $seen = @{}; $lastLayer = $null; $lastOutward = $null; $lastSign = 0
        foreach ($dim in $group.Group | Sort-Object @{ Expression = { try { [Convert]::ToInt64($_.Handle, 16) } catch { [Int64]::MaxValue } } }, Handle) {
            $layer = ('{0:R}' -f $dim.Outward)
            if ($lastLayer -and $layer -ne $lastLayer -and $seen.ContainsKey($layer)) { Add-Warning 'OUTWARD_LAYER_REENTRY' "Outward layer re-enters on $($group.Name)." @($dim.Handle) }
            if ($null -ne $lastOutward) { $sign = [math]::Sign($dim.Outward - $lastOutward); if ($sign -and $lastSign -and $sign -ne $lastSign) { Add-Warning 'OUTWARD_LAYER_DIRECTION_REVERSED' "Outward layer direction reverses on $($group.Name)." @($dim.Handle) }; if ($sign) { $lastSign = $sign } }
            $seen[$layer] = $true; $lastLayer = $layer; $lastOutward = $dim.Outward
            $layerSnapshot += [ordered]@{ handle = $dim.Handle; side = $dim.Side; outward = $dim.Outward; layer = $layer; rotation = $dim.Rotation }
        }
    }
}

$truthStatus = $null; $imageBaselineStatus = 'NoPinnedImages'; $approvedImages = @{}
if ($CaseId -and (Test-Path -LiteralPath $TruthMatrixPath)) {
    $case = ((Get-Content -LiteralPath $TruthMatrixPath -Raw | ConvertFrom-Json).cases | Where-Object id -eq $CaseId | Select-Object -First 1)
    if ($case) {
        $truthStatus = $case.status
        foreach ($artifact in @($case.evidence | ForEach-Object artifacts)) { if ($artifact.path -match '(?i)(^|/)(full|detail)\.png$') { $approvedImages[$matches[2].ToLowerInvariant()] = $artifact.sha256.ToUpperInvariant() } }
        if ($approvedImages.Count) {
            $providedImages = @{ full = $FullImagePath; detail = $DetailImagePath }; $allMatched = $true; $supplied = $false
            foreach ($name in $approvedImages.Keys) { $path = $providedImages[$name]; if (-not $path) { $allMatched = $false; continue }; $supplied = $true; if (-not (Test-Path -LiteralPath $path) -or (Get-Sha256 $path) -ne $approvedImages[$name]) { $allMatched = $false } }
            $imageBaselineStatus = if ($allMatched) { 'Matched' } elseif ($supplied) { 'Mismatch' } else { 'NotSupplied' }
        }
    }
}
$calibration = [ordered]@{ imageBaselineStatus = $imageBaselineStatus; falsePositive = $null; falseNegative = $null; positiveDefectSensitivity = $null }
if ($imageBaselineStatus -eq 'Matched' -and $truthStatus -eq 'ConfirmedCorrect') { $calibration.falsePositive = $warnings.Count; $calibration.falseNegative = 0; $calibration.positiveDefectSensitivity = 'NotMeasured' }

$warningOutput = @($warnings | Sort-Object code, message, @{ Expression = { $_.handles -join ',' } })
$blockingWarnings = @($warningOutput | Where-Object { $visualGate.mode -eq 'Blocking' -and $_.code -in @($visualGate.blockingWarningCodes) -and $_.code -notin @($visualGate.exemptWarningCodes) })
$blocking = $blockingWarnings.Count -gt 0
$shouldFail = $blocking -and $visualGate.failurePolicy -eq 'FailOnBlockingWarnings'
$status = if ($shouldFail) { 'Failed' } else { 'Passed' }
$gate = [ordered]@{ mode = $visualGate.mode; blockingWarningCodes = @($visualGate.blockingWarningCodes); warningOnly = $visualGate.warningOnly; exemptWarningCodes = @($visualGate.exemptWarningCodes); failurePolicy = $visualGate.failurePolicy; rollback = [ordered]@{ mode = $visualGate.rollback.mode; blockingWarningCodes = @($visualGate.rollback.blockingWarningCodes) }; blockingWarnings = $blockingWarnings }
$report = [ordered]@{ schemaVersion = 1; blocking = $blocking; mode = $visualGate.mode; status = $status; caseId = $CaseId; expectedDirection = $ExpectedDirection; truthStatus = $truthStatus; environment = $environment; counts = [ordered]@{ source = $sources.Count; segment = $segments.Count; dimension = $dimensions.Count; text = $texts.Count; arrow = $arrows.Count }; layerSnapshot = $layerSnapshot; warnings = $warningOutput; warningCount = $warningOutput.Count; gate = $gate; calibration = $calibration }
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding utf8
$markdown = @(
    '# CAD Visual Inspection',
    '',
    "- Mode/status: $($visualGate.mode)/$status", "- Case: $CaseId", "- Expected direction: $ExpectedDirection", "- Truth status: $truthStatus", "- Counts: source=$($sources.Count), dimension=$($dimensions.Count), text=$($texts.Count), arrow=$($arrows.Count)", "- Warning count: $($warningOutput.Count)", "- Blocking warning count: $($blockingWarnings.Count)", "- Image baseline: $($calibration.imageBaselineStatus)", "- False positive: $($calibration.falsePositive)", "- False negative: $($calibration.falseNegative)", "- Positive-defect sensitivity: $($calibration.positiveDefectSensitivity)",
    '', '## Warnings', ''
)
if ($warningOutput.Count) { $markdown += '| Code | Message | Handles |'; $markdown += '| --- | --- | --- |'; foreach ($warning in $warningOutput) { $markdown += "| $($warning.code) | $($warning.message) | $($warning.handles -join ', ') |" } } else { $markdown += 'None.' }
$markdown -join [Environment]::NewLine | Set-Content -LiteralPath $MarkdownPath -Encoding utf8
exit 0
