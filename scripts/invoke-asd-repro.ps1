[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DiagnosticPath,

    [Parameter(Mandatory = $true)]
    [string]$ReferenceImagePath,

    [Parameter(Mandatory = $true)]
    [string]$DllDirectory,

    [switch]$PlanOnly,

    [ValidateRange(30, 1800)]
    [int]$TimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-ExistingFile {
    param([string]$Path, [string]$Label)

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    if (-not (Test-Path -LiteralPath $resolved.Path -PathType Leaf)) {
        throw "$Label 不是文件: $Path"
    }
    return $resolved.Path
}

function Resolve-ExistingDirectory {
    param([string]$Path, [string]$Label)

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    if (-not (Test-Path -LiteralPath $resolved.Path -PathType Container)) {
        throw "$Label 不是目录: $Path"
    }
    return $resolved.Path.TrimEnd("\")
}

function Get-Sha256 {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-RequiredBounds {
    param($Bounds, [string]$Label)

    if ($null -eq $Bounds) {
        throw "$Label 缺失。"
    }
    $values = @(
        [double]$Bounds.minX,
        [double]$Bounds.minY,
        [double]$Bounds.minZ,
        [double]$Bounds.maxX,
        [double]$Bounds.maxY,
        [double]$Bounds.maxZ
    )
    if ($values[3] -lt $values[0] -or $values[4] -lt $values[1] -or $values[5] -lt $values[2]) {
        throw "$Label 范围无效。"
    }
    return $values
}

function Get-RequiredPoint {
    param($Point, [string]$Label)

    if ($null -eq $Point) {
        throw "$Label 缺失。"
    }
    return @([double]$Point.x, [double]$Point.y, [double]$Point.z)
}

function ConvertTo-LispString {
    param([string]$Value)

    $escaped = $Value.Replace("\", "/").Replace('"', '\"')
    return '"' + $escaped + '"'
}

function ConvertTo-LispNumber {
    param([double]$Value)

    return $Value.ToString("0.###############", [System.Globalization.CultureInfo]::InvariantCulture)
}

function Write-JsonFile {
    param([string]$Path, $Value)

    $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Test-PropertyPath {
    param($Value, [string]$Path)

    foreach ($name in $Path.Split(".")) {
        if ($null -eq $Value) {
            return $false
        }
        $property = $Value.PSObject.Properties[$name]
        if ($null -eq $property) {
            return $false
        }
        $Value = $property.Value
    }
    return $null -ne $Value
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$diagnosticFile = Resolve-ExistingFile -Path $DiagnosticPath -Label "诊断日志"
$referenceImageFile = Resolve-ExistingFile -Path $ReferenceImagePath -Label "正确标注图片"
$dllRoot = Resolve-ExistingDirectory -Path $DllDirectory -Label "DLL 目录"

try {
    $diagnostic = Get-Content -LiteralPath $diagnosticFile -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    throw "诊断日志不是可读 JSON: $($_.Exception.Message)"
}

$requiredDiagnosticPaths = @(
    "schemaVersion", "runId", "generatedAt", "command", "commandScope", "diagnosticSide", "coordinateSystem",
    "drawing.path", "drawing.fileName", "drawing.sizeBytes", "drawing.lastWriteTimeUtc",
    "drawing.identityStatus", "drawing.sha256",
    "selection.entityCount", "selection.entityTypeCounts", "selection.entityHandles",
    "selection.selectedGeometryBoundsStatus", "selection.selectedGeometryBoundsWcs",
    "selection.recognizedOutlineBoundsWcs",
    "plugin.path", "plugin.identityStatus", "plugin.sha256",
    "environment.autoCadVersion", "environment.insUnits", "environment.dimScale", "environment.dimStyle",
    "environment.ucsOriginWcs", "environment.ucsXDirectionWcs", "environment.ucsYDirectionWcs",
    "datum.hasDatumHole", "datum.useToleranceX", "datum.useToleranceY",
    "features.pinHoleCount"
)
$missingDiagnosticPaths = @($requiredDiagnosticPaths | Where-Object {
    -not (Test-PropertyPath -Value $diagnostic -Path $_)
})
if ($missingDiagnosticPaths.Count -gt 0) {
    throw "诊断日志不是完整 schema v2，缺少字段: $($missingDiagnosticPaths -join ', ')"
}

if ([int]$diagnostic.schemaVersion -ne 2) {
    throw "只允许 schema v2 自动复现；schema v1 仅可人工分析。"
}
if ([string]::IsNullOrWhiteSpace([string]$diagnostic.runId) -or
    [string]::IsNullOrWhiteSpace([string]$diagnostic.generatedAt) -or
    [string]::IsNullOrWhiteSpace([string]$diagnostic.command) -or
    [string]::IsNullOrWhiteSpace([string]$diagnostic.commandScope) -or
    [string]::IsNullOrWhiteSpace([string]$diagnostic.diagnosticSide)) {
    throw "诊断日志缺少 runId、生成时间、命令、范围或方向。"
}
$null = [DateTime]::Parse(
    [string]$diagnostic.generatedAt,
    [System.Globalization.CultureInfo]::InvariantCulture,
    [System.Globalization.DateTimeStyles]::RoundtripKind
)
if ([string]$diagnostic.coordinateSystem -ne "WCS") {
    throw "诊断坐标系不是 WCS，禁止自动复现。"
}
if ([string]$diagnostic.drawing.identityStatus -ne "Ok" -or [string]::IsNullOrWhiteSpace([string]$diagnostic.drawing.sha256)) {
    throw "源 DWG 缺少可验证的 SHA256 身份。"
}
if ([string]$diagnostic.selection.selectedGeometryBoundsStatus -ne "Ok") {
    throw "诊断中的选择范围不是完整 Ok 状态。"
}
if ([string]$diagnostic.plugin.identityStatus -ne "Ok" -or
    [string]::IsNullOrWhiteSpace([string]$diagnostic.plugin.path) -or
    [string]::IsNullOrWhiteSpace([string]$diagnostic.plugin.sha256)) {
    throw "原始插件缺少可审计的路径和 SHA256。"
}
if ([string]::IsNullOrWhiteSpace([string]$diagnostic.environment.autoCadVersion) -or
    [string]::IsNullOrWhiteSpace([string]$diagnostic.environment.insUnits) -or
    [string]::IsNullOrWhiteSpace([string]$diagnostic.environment.dimStyle)) {
    throw "AutoCAD 环境证据不完整。"
}
$ucsOrigin = Get-RequiredPoint -Point $diagnostic.environment.ucsOriginWcs -Label "environment.ucsOriginWcs"
$ucsXDirection = Get-RequiredPoint -Point $diagnostic.environment.ucsXDirectionWcs -Label "environment.ucsXDirectionWcs"
$ucsYDirection = Get-RequiredPoint -Point $diagnostic.environment.ucsYDirectionWcs -Label "environment.ucsYDirectionWcs"
$insUnits = [int]::Parse([string]$diagnostic.environment.insUnits, [System.Globalization.CultureInfo]::InvariantCulture)
$dimScale = [double]$diagnostic.environment.dimScale

$sourceDwg = Resolve-ExistingFile -Path ([string]$diagnostic.drawing.path) -Label "源 DWG"
$sourceInfo = Get-Item -LiteralPath $sourceDwg
$sourceHash = Get-Sha256 -Path $sourceDwg
if (-not [string]::Equals($sourceHash, [string]$diagnostic.drawing.sha256, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "源 DWG SHA256 与诊断日志不一致，禁止复现。"
}
if ([long]$sourceInfo.Length -ne [long]$diagnostic.drawing.sizeBytes) {
    throw "源 DWG 大小与诊断日志不一致，禁止复现。"
}
if (-not [string]::Equals($sourceInfo.Name, [string]$diagnostic.drawing.fileName, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "源 DWG 文件名与诊断日志不一致，禁止复现。"
}
if (-not [string]::IsNullOrWhiteSpace([string]$diagnostic.drawing.lastWriteTimeUtc)) {
    $loggedWriteTime = if ($diagnostic.drawing.lastWriteTimeUtc -is [DateTime]) {
        $diagnostic.drawing.lastWriteTimeUtc.ToUniversalTime()
    } else {
        [DateTime]::Parse(
            [string]$diagnostic.drawing.lastWriteTimeUtc,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::RoundtripKind
        ).ToUniversalTime()
    }
    if ([Math]::Abs(($sourceInfo.LastWriteTimeUtc - $loggedWriteTime).TotalSeconds) -gt 1.0) {
        throw "源 DWG 修改时间与诊断日志不一致，禁止复现。"
    }
}

$handles = @($diagnostic.selection.entityHandles | ForEach-Object { [string]$_ })
$entityCount = [int]$diagnostic.selection.entityCount
if ($entityCount -le 0 -or $handles.Count -ne $entityCount -or ($handles | Select-Object -Unique).Count -ne $entityCount) {
    throw "选择实体 Handle 数量、唯一性或 entityCount 不一致。"
}
$typeProperties = @($diagnostic.selection.entityTypeCounts.PSObject.Properties)
$typeCountTotal = 0
foreach ($typeProperty in $typeProperties) {
    $typeCountTotal += [int]$typeProperty.Value
}
if ($typeCountTotal -ne $entityCount) {
    throw "选择实体类型统计与 entityCount 不一致。"
}
$selectedBounds = Get-RequiredBounds -Bounds $diagnostic.selection.selectedGeometryBoundsWcs -Label "selectedGeometryBoundsWcs"
$null = Get-RequiredBounds -Bounds $diagnostic.selection.recognizedOutlineBoundsWcs -Label "recognizedOutlineBoundsWcs"

$hasDatumHole = [bool]$diagnostic.datum.hasDatumHole
if ($hasDatumHole) {
    if ([string]::IsNullOrWhiteSpace([string]$diagnostic.datum.holeHandle) -or
        $null -eq $diagnostic.datum.holeCenterWcs -or
        $null -eq $diagnostic.datum.xBaseCoordinate -or
        $null -eq $diagnostic.datum.yBaseCoordinate) {
        throw "基准孔复现字段不完整。"
    }
    if ($handles -notcontains [string]$diagnostic.datum.holeHandle) {
        throw "基准孔 Handle 不在可重放选择集中。"
    }
}

$requiredDllNames = @("AutoFixtureDim.dll", "CadAuto.Core.dll", "CadAuto.CadAdapter.dll")
$dllFiles = [ordered]@{}
foreach ($dllName in $requiredDllNames) {
    $dllPath = Resolve-ExistingFile -Path (Join-Path $dllRoot $dllName) -Label $dllName
    $dllFiles[$dllName] = [ordered]@{
        path = $dllPath
        sha256 = Get-Sha256 -Path $dllPath
    }
}
$relativeDllRoot = [System.IO.Path]::GetFullPath($dllRoot)
$relativeProjectRoot = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd("\") + "\"
if (-not $relativeDllRoot.StartsWith($relativeProjectRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path -Leaf $dllRoot) -notmatch "^Debug-v[0-9]+$" -or
    (Split-Path -Leaf (Split-Path -Parent $dllRoot)) -ne "bin") {
    throw "DLL 目录必须是本项目 bin\Debug-vN 的全新版本目录。"
}
$selectedVersion = [int]([regex]::Match((Split-Path -Leaf $dllRoot), "[0-9]+").Value)
$existingVersions = @(
    Get-ChildItem -LiteralPath (Join-Path $projectRoot "bin") -Directory -Filter "Debug-v*" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match "^Debug-v([0-9]+)$" } |
        ForEach-Object { [int]$Matches[1] }
)
if ($existingVersions.Count -gt 0 -and $selectedVersion -ne ($existingVersions | Measure-Object -Maximum).Maximum) {
    throw "DLL 目录不是当前最高的 Debug-vN 版本，禁止使用旧构建复现。"
}

Add-Type -AssemblyName System.Drawing
$referenceImage = [System.Drawing.Image]::FromFile($referenceImageFile)
try {
    $referenceImageWidth = $referenceImage.Width
    $referenceImageHeight = $referenceImage.Height
}
finally {
    $referenceImage.Dispose()
}
$referenceImageHash = Get-Sha256 -Path $referenceImageFile
$diagnosticHash = Get-Sha256 -Path $diagnosticFile

$preflight = [ordered]@{
    status = "Ready"
    schemaVersion = 2
    inputRunId = [string]$diagnostic.runId
    originalCommand = [string]$diagnostic.command
    commandScope = [string]$diagnostic.commandScope
    diagnosticSide = [string]$diagnostic.diagnosticSide
    sourceDwg = $sourceDwg
    sourceDwgSha256 = $sourceHash
    diagnosticPath = $diagnosticFile
    diagnosticSha256 = $diagnosticHash
    referenceImagePath = $referenceImageFile
    referenceImageSha256 = $referenceImageHash
    referenceImageSize = [ordered]@{
        width = $referenceImageWidth
        height = $referenceImageHeight
    }
    dlls = $dllFiles
    selectedEntityCount = $entityCount
    timeoutSeconds = $TimeoutSeconds
}

if ($PlanOnly) {
    $preflight | ConvertTo-Json -Depth 12
    return
}

$acadExe = "D:\Program Files\Autodesk\AutoCAD 2020\acad.exe"
if (-not (Test-Path -LiteralPath $acadExe -PathType Leaf)) {
    throw "找不到 AutoCAD 2020: $acadExe"
}
$existingCadProcesses = @(Get-Process -Name "acad" -ErrorAction SilentlyContinue)
if ($existingCadProcesses.Count -gt 0) {
    $existingCadPids = ($existingCadProcesses | ForEach-Object { $_.Id }) -join ", "
    throw "确定性复现前必须退出全部 AutoCAD 实例；当前 PID: $existingCadPids"
}

$runRoot = Join-Path $projectRoot "regression\asd-repair\runs"
$runDirectory = Join-Path $runRoot (Get-Date -Format "yyyyMMdd-HHmmss-fff")
$null = New-Item -ItemType Directory -Path $runDirectory -Force
$workingDwg = Join-Path $runDirectory ("working-" + $sourceInfo.Name)
$archivedDiagnostic = Join-Path $runDirectory "original-report.json"
$referenceExtension = [System.IO.Path]::GetExtension($referenceImageFile)
$archivedReference = Join-Path $runDirectory ("reference-image" + $referenceExtension)
$tracePath = Join-Path $runDirectory "trace.log"
$reportPath = Join-Path $runDirectory "report.json"
$resultPath = Join-Path $runDirectory "result.json"
$fullImagePath = Join-Path $runDirectory "full.png"
$scopeImagePath = Join-Path $runDirectory "scope.png"
$lspPath = Join-Path $runDirectory "repro.lsp"
$driverPath = Join-Path $runDirectory "repro.scr"

Copy-Item -LiteralPath $sourceDwg -Destination $workingDwg
Copy-Item -LiteralPath $diagnosticFile -Destination $archivedDiagnostic
Copy-Item -LiteralPath $referenceImageFile -Destination $archivedReference

$result = [ordered]@{
    status = "Running"
    error = $null
    processId = $null
    startedAtUtc = $null
    finishedAtUtc = $null
    runDirectory = $runDirectory
    inputRunId = [string]$diagnostic.runId
    outputRunId = $null
    originalCommand = [string]$diagnostic.command
    replayCommand = "ASDREPRO"
    commandScope = [string]$diagnostic.commandScope
    diagnosticSide = [string]$diagnostic.diagnosticSide
    diagnosticPath = $archivedDiagnostic
    diagnosticSha256 = $diagnosticHash
    referenceImagePath = $archivedReference
    referenceImageSha256 = $referenceImageHash
    sourceDwg = $sourceDwg
    sourceDwgSha256 = $sourceHash
    workingDwg = $workingDwg
    dlls = $dllFiles
    tracePath = $tracePath
    reportPath = $reportPath
    fullImagePath = $fullImagePath
    scopeImagePath = $scopeImagePath
    screenshotBoundsWcs = $null
    timeoutSeconds = $TimeoutSeconds
}

$failure = $null
try {
    $handleLiteral = "(" + (($handles | ForEach-Object { ConvertTo-LispString $_ }) -join " ") + ")"
    $typePairLiteral = "(" + (($typeProperties | Sort-Object Name | ForEach-Object {
        "(" + (ConvertTo-LispString ([string]$_.Name)) + " . " + ([int]$_.Value).ToString([System.Globalization.CultureInfo]::InvariantCulture) + ")"
    }) -join " ") + ")"
    $boundsLiteral = "(" + (($selectedBounds | ForEach-Object { ConvertTo-LispNumber $_ }) -join " ") + ")"
    $scopeKeyword = switch ([string]$diagnostic.commandScope) {
        "All" { "A" }
        "OutlineOnly" { "O" }
        "HoleOnly" { "H" }
        "CornerOnly" { "C" }
        default { throw "不支持的 commandScope: $($diagnostic.commandScope)" }
    }
    $sideKeyword = switch ([string]$diagnostic.diagnosticSide) {
        "All" { "A" }
        "Top" { "T" }
        "Bottom" { "B" }
        "Left" { "L" }
        "Right" { "R" }
        default { throw "不支持的 diagnosticSide: $($diagnostic.diagnosticSide)" }
    }
    $datumHandleLiteral = ConvertTo-LispString ([string]$diagnostic.datum.holeHandle)
    $xBase = if ($hasDatumHole) { ConvertTo-LispNumber ([double]$diagnostic.datum.xBaseCoordinate) } else { "0.0" }
    $yBase = if ($hasDatumHole) { ConvertTo-LispNumber ([double]$diagnostic.datum.yBaseCoordinate) } else { "0.0" }
    $datumCenterX = if ($hasDatumHole) { ConvertTo-LispNumber ([double]$diagnostic.datum.holeCenterWcs.x) } else { "0.0" }
    $datumCenterY = if ($hasDatumHole) { ConvertTo-LispNumber ([double]$diagnostic.datum.holeCenterWcs.y) } else { "0.0" }
    $toleranceX = if ([bool]$diagnostic.datum.useToleranceX) { "S" } else { "A" }
    $toleranceY = if ([bool]$diagnostic.datum.useToleranceY) { "S" } else { "A" }
    $hasDatumLiteral = if ($hasDatumHole) { "T" } else { "nil" }
    $hasPinHolesLiteral = if ([int]$diagnostic.features.pinHoleCount -gt 0) { "T" } else { "nil" }
    $boundsSpan = [Math]::Max($selectedBounds[3] - $selectedBounds[0], $selectedBounds[4] - $selectedBounds[1])
    $boundsTolerance = ConvertTo-LispNumber ([Math]::Max(0.001, $boundsSpan * 0.00000001))
    $ucsOriginLiteral = "(" + (($ucsOrigin | ForEach-Object { ConvertTo-LispNumber $_ }) -join " ") + ")"
    $ucsXDirectionLiteral = "(" + (($ucsXDirection | ForEach-Object { ConvertTo-LispNumber $_ }) -join " ") + ")"
    $ucsYDirectionLiteral = "(" + (($ucsYDirection | ForEach-Object { ConvertTo-LispNumber $_ }) -join " ") + ")"

    $lsp = @"
(vl-load-com)

(setq *asd-trace-path* $(ConvertTo-LispString $tracePath))
(setq *asd-full-image-path* $(ConvertTo-LispString $fullImagePath))
(setq *asd-scope-image-path* $(ConvertTo-LispString $scopeImagePath))
(setq *asd-expected-handles* '$handleLiteral)
(setq *asd-expected-types* '$typePairLiteral)
(setq *asd-expected-bounds* '$boundsLiteral)
(setq *asd-bounds-tolerance* $boundsTolerance)
(setq *asd-command-scope* $(ConvertTo-LispString $scopeKeyword))
(setq *asd-diagnostic-side* $(ConvertTo-LispString $sideKeyword))
(setq *asd-has-datum* $hasDatumLiteral)
(setq *asd-has-pin-holes* $hasPinHolesLiteral)
(setq *asd-datum-handle* $datumHandleLiteral)
(setq *asd-x-base-point* (list $xBase $datumCenterY 0.0))
(setq *asd-y-base-point* (list $datumCenterX $yBase 0.0))
(setq *asd-tolerance-x* $(ConvertTo-LispString $toleranceX))
(setq *asd-tolerance-y* $(ConvertTo-LispString $toleranceY))
(setq *asd-expected-acadver* $(ConvertTo-LispString ([string]$diagnostic.environment.autoCadVersion)))
(setq *asd-expected-insunits* $insUnits)
(setq *asd-expected-ucsname* $(ConvertTo-LispString ([string]$diagnostic.environment.ucsName)))
(setq *asd-expected-ucsorigin* '$ucsOriginLiteral)
(setq *asd-expected-ucsxdir* '$ucsXDirectionLiteral)
(setq *asd-expected-ucsydir* '$ucsYDirectionLiteral)
(setq *asd-expected-dimscale* $(ConvertTo-LispNumber $dimScale))
(setq *asd-expected-dimstyle* $(ConvertTo-LispString ([string]$diagnostic.environment.dimStyle)))

(defun asd-trace (message / stream)
  (setq stream (open *asd-trace-path* "a"))
  (if stream
    (progn
      (write-line message stream)
      (close stream)
    )
  )
)

(defun asd-build-selection (/ selection entity ok)
  (setq selection (ssadd))
  (setq ok T)
  (foreach handle *asd-expected-handles*
    (setq entity (handent handle))
    (if entity
      (ssadd entity selection)
      (progn
        (asd-trace (strcat "ERROR missingHandle=" handle))
        (setq ok nil)
      )
    )
  )
  (if ok selection nil)
)

(defun asd-object-type (entity / name)
  (cdr (assoc 0 (entget entity)))
)

(defun asd-type-counts (selection / index entity name pair result)
  (setq index 0)
  (setq result nil)
  (while (< index (sslength selection))
    (setq entity (ssname selection index))
    (setq name (asd-object-type entity))
    (setq pair (assoc name result))
    (if pair
      (setq result (subst (cons name (1+ (cdr pair))) pair result))
      (setq result (cons (cons name 1) result))
    )
    (setq index (1+ index))
  )
  result
)

(defun asd-types-match (selection / actual ok pair)
  (setq actual (asd-type-counts selection))
  (setq ok T)
  (foreach expected *asd-expected-types*
    (setq pair (assoc (car expected) actual))
    (if (or (null pair) (/= (cdr pair) (cdr expected)))
      (progn
        (asd-trace (strcat "ERROR typeMismatch=" (car expected)))
        (setq ok nil)
      )
    )
  )
  ok
)

(defun asd-selection-bounds (selection / index entity object minPoint maxPoint low high result)
  (setq index 0)
  (setq result nil)
  (while (< index (sslength selection))
    (setq entity (ssname selection index))
    (setq object (vlax-ename->vla-object entity))
    (vla-GetBoundingBox object 'minPoint 'maxPoint)
    (setq low (vlax-safearray->list minPoint))
    (setq high (vlax-safearray->list maxPoint))
    (if result
      (setq result
        (list
          (min (nth 0 result) (nth 0 low))
          (min (nth 1 result) (nth 1 low))
          (min (nth 2 result) (nth 2 low))
          (max (nth 3 result) (nth 0 high))
          (max (nth 4 result) (nth 1 high))
          (max (nth 5 result) (nth 2 high))
        )
      )
      (setq result (append low high))
    )
    (setq index (1+ index))
  )
  result
)

(defun asd-bounds-match (actual expected tolerance)
  (and
    actual
    (<= (abs (- (nth 0 actual) (nth 0 expected))) tolerance)
    (<= (abs (- (nth 1 actual) (nth 1 expected))) tolerance)
    (<= (abs (- (nth 2 actual) (nth 2 expected))) tolerance)
    (<= (abs (- (nth 3 actual) (nth 3 expected))) tolerance)
    (<= (abs (- (nth 4 actual) (nth 4 expected))) tolerance)
    (<= (abs (- (nth 5 actual) (nth 5 expected))) tolerance)
  )
)

(defun asd-environment-match ()
  (and
    (= (getvar "ACADVER") *asd-expected-acadver*)
    (= (getvar "INSUNITS") *asd-expected-insunits*)
    (= (getvar "UCSNAME") *asd-expected-ucsname*)
    (equal (getvar "UCSORG") *asd-expected-ucsorigin* 0.00000001)
    (equal (getvar "UCSXDIR") *asd-expected-ucsxdir* 0.00000001)
    (equal (getvar "UCSYDIR") *asd-expected-ucsydir* 0.00000001)
    (equal (getvar "DIMSCALE") *asd-expected-dimscale* 0.00000001)
    (= (strcase (getvar "DIMSTYLE")) (strcase *asd-expected-dimstyle*))
  )
)

(defun asd-add-set (source target / index)
  (if source
    (progn
      (setq index 0)
      (while (< index (sslength source))
        (ssadd (ssname source index) target)
        (setq index (1+ index))
      )
    )
  )
  target
)

(defun asd-export (selection / annotations combined bounds width height padding paddedBounds)
  (setq annotations (ssget "_X" '((-3 ("AUTOFIXDIM")))))
  (setq combined (ssadd))
  (asd-add-set selection combined)
  (asd-add-set annotations combined)
  (setq bounds (asd-selection-bounds combined))
  (setq width (- (nth 3 bounds) (nth 0 bounds)))
  (setq height (- (nth 4 bounds) (nth 1 bounds)))
  (setq padding (max 1.0 (* 0.05 (max width height))))
  (setq paddedBounds
    (list
      (- (nth 0 bounds) padding)
      (- (nth 1 bounds) padding)
      (nth 2 bounds)
      (+ (nth 3 bounds) padding)
      (+ (nth 4 bounds) padding)
      (nth 5 bounds)
    )
  )
  (asd-trace
    (strcat
      "SCOPE_BOUNDS minX=" (rtos (nth 0 paddedBounds) 2 8)
      " minY=" (rtos (nth 1 paddedBounds) 2 8)
      " minZ=" (rtos (nth 2 paddedBounds) 2 8)
      " maxX=" (rtos (nth 3 paddedBounds) 2 8)
      " maxY=" (rtos (nth 4 paddedBounds) 2 8)
      " maxZ=" (rtos (nth 5 paddedBounds) 2 8)
    )
  )
  (vl-cmdf "_.REGEN")
  (vl-cmdf "_.ZOOM" "_E")
  (vl-cmdf "_.PNGOUT" *asd-full-image-path* "_ALL" "")
  (vl-cmdf
    "_.ZOOM"
    "_W"
    (list (nth 0 paddedBounds) (nth 1 paddedBounds) 0.0)
    (list (nth 3 paddedBounds) (nth 4 paddedBounds) 0.0)
  )
  (vl-cmdf "_.PNGOUT" *asd-scope-image-path* combined "")
)

(defun asd-invoke (selection / datum)
  (if *asd-has-datum*
    (progn
      (setq datum (handent *asd-datum-handle*))
      (if datum
        (vl-cmdf
          "ASDREPRO"
          *asd-command-scope*
          *asd-diagnostic-side*
          selection
          ""
          datum
          *asd-x-base-point*
          *asd-tolerance-x*
          *asd-y-base-point*
          *asd-tolerance-y*
        )
        (asd-trace "ERROR datumHandleMissing")
      )
    )
    (if *asd-has-pin-holes*
      (vl-cmdf "ASDREPRO" *asd-command-scope* *asd-diagnostic-side* selection "" "")
      (vl-cmdf "ASDREPRO" *asd-command-scope* *asd-diagnostic-side* selection "")
    )
  )
)

(defun c:ASDREPROLOOP (/ oldCmdecho oldOsmode selection actualBounds)
  (setq oldCmdecho (getvar "CMDECHO"))
  (setq oldOsmode (getvar "OSMODE"))
  (setvar "CMDECHO" 1)
  (setvar "OSMODE" 0)
  (asd-trace "START")
  (setq selection (asd-build-selection))
  (cond
    ((not (asd-environment-match))
      (asd-trace "ERROR environmentMismatch")
    )
    ((null selection)
      (asd-trace "ERROR selectionBuildFailed")
    )
    ((/= (sslength selection) (length *asd-expected-handles*))
      (asd-trace "ERROR selectionCountMismatch")
    )
    ((not (asd-types-match selection))
      (asd-trace "ERROR selectionTypeMismatch")
    )
    (T
      (setq actualBounds (asd-selection-bounds selection))
      (if (asd-bounds-match actualBounds *asd-expected-bounds* *asd-bounds-tolerance*)
        (progn
          (asd-trace (strcat "INPUT selection=" (itoa (sslength selection))))
          (asd-invoke selection)
          (asd-export selection)
          (asd-trace "COMPLETE")
        )
        (asd-trace "ERROR selectionBoundsMismatch")
      )
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
(command "_.NETLOAD" $(ConvertTo-LispString ([string]$dllFiles["AutoFixtureDim.dll"].path)))
(load $(ConvertTo-LispString $lspPath))
(c:ASDREPROLOOP)
_.QSAVE
_.QUIT
"@
    Set-Content -LiteralPath $driverPath -Value $driver -Encoding ASCII

    $globalReportPath = $reportPath
    $reportWriteTimeBefore = if (Test-Path -LiteralPath $globalReportPath) {
        (Get-Item -LiteralPath $globalReportPath).LastWriteTimeUtc
    }
    else {
        [DateTime]::MinValue
    }

    $startedAt = [DateTime]::UtcNow
    $result.startedAtUtc = $startedAt.ToString("o", [System.Globalization.CultureInfo]::InvariantCulture)
    $cadArguments = @(
        "/nologo",
        ('"{0}"' -f $workingDwg),
        "/b",
        ('"{0}"' -f $driverPath)
    )
    $previousDiagnosticReportPath = $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH
    $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH = $reportPath
    try {
        $cadProcess = Start-Process -FilePath $acadExe -ArgumentList $cadArguments -WindowStyle Hidden -PassThru
    }
    finally {
        $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH = $previousDiagnosticReportPath
    }
    $result.processId = $cadProcess.Id
    $deadline = $startedAt.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $cadProcess.Refresh()
        if ($cadProcess.HasExited) {
            break
        }
        Start-Sleep -Seconds 2
    }
    $cadProcess.Refresh()
    if (-not $cadProcess.HasExited) {
        $result.status = "TimedOut"
        throw "AutoCAD 超时；未强杀进程，PID=$($cadProcess.Id)。"
    }

    if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) {
        throw "AutoCAD 未生成 trace.log。"
    }
    $trace = Get-Content -LiteralPath $tracePath
    if ($trace -match "^ERROR ") {
        throw "CAD trace 报告错误: $(($trace | Where-Object { $_ -match '^ERROR ' } | Select-Object -First 1))"
    }
    if ($trace -notcontains "COMPLETE") {
        throw "CAD trace 缺少 COMPLETE。"
    }
    foreach ($imagePath in @($fullImagePath, $scopeImagePath)) {
        if (-not (Test-Path -LiteralPath $imagePath -PathType Leaf) -or (Get-Item -LiteralPath $imagePath).Length -le 0) {
            throw "缺少有效截图: $imagePath"
        }
    }
    if (-not (Test-Path -LiteralPath $globalReportPath -PathType Leaf)) {
        throw "缺少新的 diagnostics\last-run.json。"
    }
    $globalReportInfo = Get-Item -LiteralPath $globalReportPath
    if ($globalReportInfo.LastWriteTimeUtc -le $reportWriteTimeBefore -or
        $globalReportInfo.LastWriteTimeUtc -lt $startedAt.AddSeconds(-2)) {
        throw "诊断报告不是本轮新生成。"
    }

    $newReport = Get-Content -LiteralPath $globalReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([int]$newReport.schemaVersion -ne 2 -or
        [string]$newReport.command -ne "ASDREPRO" -or
        [string]$newReport.commandScope -ne [string]$diagnostic.commandScope -or
        [string]$newReport.diagnosticSide -ne [string]$diagnostic.diagnosticSide) {
        throw "新报告的 schema、命令范围或诊断方向不匹配。"
    }
    $reportedWorkingDwg = [System.IO.Path]::GetFullPath([string]$newReport.drawing.path)
    if (-not [string]::Equals($reportedWorkingDwg, [System.IO.Path]::GetFullPath($workingDwg), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "新报告记录的工作 DWG 不匹配。"
    }
    if ([string]::IsNullOrWhiteSpace([string]$newReport.runId) -or
        [string]::Equals([string]$newReport.runId, [string]$diagnostic.runId, [System.StringComparison]::Ordinal)) {
        throw "新报告 runId 无效或仍是旧 runId。"
    }
    $newGeneratedAt = [DateTime]::Parse(
        [string]$newReport.generatedAt,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind
    ).ToUniversalTime()
    if ($newGeneratedAt -lt $startedAt.AddSeconds(-2)) {
        throw "新报告 generatedAt 早于本轮启动时间。"
    }

    $scopeLine = $trace | Where-Object { $_ -match "^SCOPE_BOUNDS " } | Select-Object -Last 1
    if ($scopeLine -match "^SCOPE_BOUNDS minX=([^ ]+) minY=([^ ]+) minZ=([^ ]+) maxX=([^ ]+) maxY=([^ ]+) maxZ=([^ ]+)$") {
        $result.screenshotBoundsWcs = [ordered]@{
            minX = [double]::Parse($Matches[1], [System.Globalization.CultureInfo]::InvariantCulture)
            minY = [double]::Parse($Matches[2], [System.Globalization.CultureInfo]::InvariantCulture)
            minZ = [double]::Parse($Matches[3], [System.Globalization.CultureInfo]::InvariantCulture)
            maxX = [double]::Parse($Matches[4], [System.Globalization.CultureInfo]::InvariantCulture)
            maxY = [double]::Parse($Matches[5], [System.Globalization.CultureInfo]::InvariantCulture)
            maxZ = [double]::Parse($Matches[6], [System.Globalization.CultureInfo]::InvariantCulture)
        }
    }
    $result.outputRunId = [string]$newReport.runId
    $result.status = "Completed"
}
catch {
    $failure = $_
    if ($result.status -eq "Running") {
        $result.status = "Failed"
    }
    $result.error = $_.Exception.Message
}
finally {
    $result.finishedAtUtc = [DateTime]::UtcNow.ToString("o", [System.Globalization.CultureInfo]::InvariantCulture)
    Write-JsonFile -Path $resultPath -Value $result
}

if ($null -ne $failure) {
    throw $failure
}

$result | ConvertTo-Json -Depth 12
