[CmdletBinding()]
param(
    [string]$DllDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\Debug-v3'),
    [ValidateRange(30, 180)]
    [int]$TimeoutSeconds = 180,
    [string]$GatePath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$authoritativeDwg = Join-Path $projectRoot 'regression\visual-inspection\M4-positive-collisions.dwg'
$fixtureDwg = Join-Path $projectRoot 'regression\visual-inspection\positive\M4-positive-collisions.dwg'
$runsRoot = Join-Path $projectRoot 'regression\visual-inspection\runs\M4-positive-collisions-real'
$console = 'D:\Program Files\Autodesk\AutoCAD 2020\accoreconsole.exe'

function Get-Sha256([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant() }
function Lisp([string]$Value) { return '"' + $Value.Replace('\', '/').Replace('"', '\"') + '"' }
function Save-Json($Value, [string]$Path) { $Value | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $Path -Encoding utf8 }
function Get-NormalizedReportHash([string]$Path) {
    $report = Get-Content -LiteralPath $Path -Raw -Encoding utf8 | ConvertFrom-Json
    if ($report.environment -and $report.environment.PSObject.Properties.Name -contains 'DWGPREFIX') { $report.environment.PSObject.Properties.Remove('DWGPREFIX') }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($report | ConvertTo-Json -Depth 100 -Compress))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '') } finally { $sha.Dispose() }
}
function Invoke-CoreConsole([string]$Drawing, [string]$Script, [string]$Stdout, [string]$Stderr) {
    $process = Start-Process -FilePath $console -ArgumentList @('/i', ('"{0}"' -f $Drawing), '/s', ('"{0}"' -f $Script), '/l', 'en-US') -WindowStyle Hidden -RedirectStandardOutput $Stdout -RedirectStandardError $Stderr -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) { $process.Refresh(); if ($process.HasExited) { break }; Start-Sleep -Milliseconds 500 }
    $process.Refresh()
    if (-not $process.HasExited) { throw "AutoCAD Core Console timed out; process was not killed. PID=$($process.Id)." }
    $process.WaitForExit(); $process.Refresh()
    if ($null -ne $process.ExitCode -and $process.ExitCode -ne 0) { throw "AutoCAD Core Console exited $($process.ExitCode)." }
}
function Get-Window([object[]]$Rows, [string[]]$Handles) {
    $points = @()
    foreach ($row in $Rows) {
        $fields = @($row.Split([char]9)); if ($fields.Count -lt 3 -or $Handles -notcontains $fields[1]) { continue }
        switch ($fields[0]) {
            'DIM' { if ($fields.Count -ge 11) { foreach ($offset in 3,5,7,9) { $points += [pscustomobject]@{ X=[double]$fields[$offset]; Y=[double]$fields[$offset + 1] } } } }
            'TEXT' { if ($fields.Count -ge 7) { $points += [pscustomobject]@{ X=[double]$fields[3]; Y=[double]$fields[4] }; $points += [pscustomobject]@{ X=[double]$fields[5]; Y=[double]$fields[6] } } }
            'ARROW' { if ($fields.Count -ge 8) { $points += [pscustomobject]@{ X=[double]$fields[4]; Y=[double]$fields[5] }; $points += [pscustomobject]@{ X=[double]$fields[6]; Y=[double]$fields[7] } } }
            'SEG' { if ($fields.Count -ge 7) { $points += [pscustomobject]@{ X=[double]$fields[3]; Y=[double]$fields[4] }; $points += [pscustomobject]@{ X=[double]$fields[5]; Y=[double]$fields[6] } } }
        }
    }
    if ($points.Count -eq 0) { throw "No snapshot bounds found for handles: $($Handles -join ',')" }
    $minX = [double](($points | Measure-Object X -Minimum).Minimum); $maxX = [double](($points | Measure-Object X -Maximum).Maximum)
    $minY = [double](($points | Measure-Object Y -Minimum).Minimum); $maxY = [double](($points | Measure-Object Y -Maximum).Maximum)
    $pad = [math]::Max(5.0, [math]::Max($maxX - $minX, $maxY - $minY) * 0.25)
    return @(($minX - $pad), ($minY - $pad), ($maxX + $pad), ($maxY + $pad))
}
function Save-FixedCrop([string]$Source, [string]$Destination, [double]$Left, [double]$Top, [double]$Width, [double]$Height) {
    Add-Type -AssemblyName System.Drawing
    $image = [Drawing.Image]::FromFile($Source)
    try {
        $rect = [Drawing.Rectangle]::FromLTRB([int]($image.Width * $Left), [int]($image.Height * $Top), [int]($image.Width * ($Left + $Width)), [int]($image.Height * ($Top + $Height)))
        $bitmap = New-Object Drawing.Bitmap $rect.Width, $rect.Height
        try { $graphics = [Drawing.Graphics]::FromImage($bitmap); try { $graphics.DrawImage($image, (New-Object Drawing.Rectangle 0, 0, $rect.Width, $rect.Height), $rect, [Drawing.GraphicsUnit]::Pixel); $bitmap.Save($Destination, [Drawing.Imaging.ImageFormat]::Png) } finally { $graphics.Dispose() } } finally { $bitmap.Dispose() }
    }
    finally { $image.Dispose() }
}

if (-not (Test-Path -LiteralPath $console -PathType Leaf)) { throw "AutoCAD Core Console not found: $console" }
if (@(Get-Process -Name accoreconsole -ErrorAction SilentlyContinue).Count) { throw 'Another AutoCAD Core Console process is active; stopping without force-kill.' }
if (-not (Test-Path -LiteralPath $authoritativeDwg -PathType Leaf)) { throw "Authoritative final drawing missing: $authoritativeDwg" }
if (-not (Test-Path -LiteralPath $fixtureDwg -PathType Leaf)) { throw "Frozen positive fixture missing: $fixtureDwg" }
if ((Get-Sha256 $authoritativeDwg) -ne (Get-Sha256 $fixtureDwg)) { throw 'Positive fixture does not byte-match the authoritative final drawing.' }
foreach ($name in 'AutoFixtureDim.dll', 'CadAuto.Core.dll', 'CadAuto.CadAdapter.dll') { if (-not (Test-Path -LiteralPath (Join-Path $DllDirectory $name) -PathType Leaf)) { throw "Required DLL missing: $name" } }

$run = Join-Path $runsRoot (Get-Date -Format 'yyyyMMdd-HHmmssfff')
New-Item -ItemType Directory -Path $run -Force | Out-Null
$working = Join-Path $run 'M4-positive-collisions.dwg'; Copy-Item -LiteralPath $fixtureDwg -Destination $working
$inventory = Join-Path $run 'source-entities.tsv'; $trace = Join-Path $run 'trace.log'; $snapshot = Join-Path $run 'visual-snapshot.tsv'; $report = Join-Path $run 'visual-report.json'; $markdown = Join-Path $run 'visual-report.md'; $full = Join-Path $run 'full.png'; $resultPath = Join-Path $run 'result.json'; $hashesPath = Join-Path $run 'hashes.json'
$result = [ordered]@{ status='Running'; mode=$null; blocking=$false; blockingCodes=@(); gate=$null; caseId='M4-positive-collisions'; runDirectory=$run; authoritativeFixture=$authoritativeDwg; authoritativeFixtureSha256=(Get-Sha256 $authoritativeDwg); fixture=$fixtureDwg; fixtureSha256=(Get-Sha256 $fixtureDwg); workingDrawing=$working; dlls=[ordered]@{}; sourceInventoryPath=$inventory; tracePath=$trace; fullImagePath=$full; detail1ImagePath=(Join-Path $run 'detail-1.png'); detail2ImagePath=(Join-Path $run 'detail-2.png'); visualSnapshotPath=$snapshot; visualReportPath=$report; visualMarkdownPath=$markdown; warnings=@(); normalizedVisualReportSha256=$null; evidenceAssessment=$null; detailEvidence=@(); error=$null }
foreach ($name in 'AutoFixtureDim.dll', 'CadAuto.Core.dll', 'CadAuto.CadAdapter.dll') { $path=Join-Path $DllDirectory $name; $result.dlls[$name]=[ordered]@{path=(Resolve-Path -LiteralPath $path).Path;sha256=(Get-Sha256 $path)} }
try {
    $runLsp = Join-Path $run 'run.lsp'; $runScr = Join-Path $run 'run.scr'
    @"
(vl-load-com)
(setq *m4-positive-trace* $(Lisp $trace))
(setq *m4-positive-inventory* $(Lisp $inventory))
(setq *m4-positive-snapshot* $(Lisp $snapshot))
(defun m4-positive-write (path text / stream) (setq stream (open path "a")) (if stream (progn (write-line text stream) (close stream))))
(defun m4-positive-handle (entity / data) (setq data (entget entity)) (if data (cdr (assoc 5 data)) ""))
(defun m4-positive-inventory-selection (selection / stream index entity data)
  (setq stream (open *m4-positive-inventory* "w"))
  (if stream (progn
    (write-line "TYPE`tHANDLE`tLAYER" stream)
    (setq index 0)
    (while (< index (sslength selection))
      (setq entity (ssname selection index) data (entget entity))
      (write-line (strcat (cdr (assoc 0 data)) (chr 9) (m4-positive-handle entity) (chr 9) (cdr (assoc 8 data))) stream)
      (setq index (1+ index)))
    (close stream))))
(defun m4-positive-stop (reason) (m4-positive-write *m4-positive-trace* (strcat "ERROR " reason)) (princ))
(defun c:RUNM4POSITIVE (/ oldCmdecho oldOsmode selection capture-result)
  (setq oldCmdecho (getvar "CMDECHO") oldOsmode (getvar "OSMODE"))
  (setvar "CMDECHO" 1) (setvar "OSMODE" 0)
  (setq selection (ssget "_X" '((8 . "DRAWING") (-4 . "<OR") (0 . "LINE") (0 . "LWPOLYLINE") (0 . "POLYLINE") (0 . "ARC") (0 . "CIRCLE") (-4 . "OR>") (410 . "Model"))))
  (cond
    ((null selection) (m4-positive-stop "selectionEmpty"))
    (T
      (m4-positive-inventory-selection selection)
      (m4-positive-write *m4-positive-trace* (strcat "INPUT selection=" (itoa (sslength selection))))
      ;; M4 inspects the saved final CAD entities only: it never invokes ASDREPRO or changes annotations.
      (load $(Lisp (Join-Path $PSScriptRoot 'cad-visual-inspection.lsp')))
      (setq capture-result (m4-capture-modelspace selection *m4-positive-snapshot* "M4-positive-collisions" "All"))
      (if capture-result (m4-positive-write *m4-positive-trace* "COMPLETE") (m4-positive-stop "visualCaptureFailed"))
    )
  )
  (setvar "OSMODE" oldOsmode) (setvar "CMDECHO" oldCmdecho) (princ)
)
(princ)
"@ | Set-Content -LiteralPath $runLsp -Encoding ascii
    @"
(setvar "FILEDIA" 0)
(setvar "CMDDIA" 0)
(command "_.NETLOAD" $(Lisp $result.dlls['AutoFixtureDim.dll'].path))
(load $(Lisp $runLsp))
(c:RUNM4POSITIVE)
(vl-cmdf "_.REGEN")
(vl-cmdf "_.ZOOM" "_E")
(vl-cmdf "_.ZOOM" "0.8X")
(vl-cmdf "_.PNGOUT" $(Lisp $full) "_ALL" "")
_.QUIT
_N
"@ | Set-Content -LiteralPath $runScr -Encoding ascii
    Invoke-CoreConsole $working $runScr (Join-Path $run 'core-console.stdout.log') (Join-Path $run 'core-console.stderr.log')
    $lines = @(Get-Content -LiteralPath $trace -ErrorAction Stop)
    $traceError = $lines | Where-Object { $_ -like 'ERROR *' } | Select-Object -First 1
    if ($traceError) { throw "CAD trace failure: $traceError" }
    if ($lines -notcontains 'COMPLETE') { throw 'CAD trace has no COMPLETE marker.' }
    if (-not (Test-Path -LiteralPath $snapshot -PathType Leaf)) { throw 'Visual snapshot was not generated.' }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw 'Full image was not generated.' }
    $visualReportArgs = @('-SnapshotPath', $snapshot, '-ReportPath', $report, '-MarkdownPath', $markdown, '-CaseId', 'M4-positive-collisions', '-ExpectedDirection', 'All', '-FullImagePath', $full)
    if ($GatePath) { $visualReportArgs += @('-GatePath', $GatePath) }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'write-cad-visual-report.ps1') @visualReportArgs
    if ($LASTEXITCODE -ne 0) { throw "Visual report exited $LASTEXITCODE." }
    $visual = Get-Content -LiteralPath $report -Raw -Encoding utf8 | ConvertFrom-Json
    $result.normalizedVisualReportSha256 = Get-NormalizedReportHash $report
    $result.mode = [string]$visual.mode
    $result.blocking = [bool]$visual.blocking
    $result.blockingCodes = @($visual.gate.blockingWarnings | ForEach-Object { [string]$_.code } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $result.gate = $visual.gate
    $rows = @(Get-Content -LiteralPath $snapshot)
    $spatialWarnings = @($visual.warnings | Where-Object { @($_.handles).Count -gt 0 })
    if (-not $spatialWarnings.Count) {
        Save-FixedCrop $full $result.detail1ImagePath 0.22 0.05 0.37 0.34
        Save-FixedCrop $full $result.detail2ImagePath 0.00 0.18 0.38 0.63
        $result.evidenceAssessment = if (@($visual.warnings).Count) { 'CaptureIncomplete: no final CAD dimensions were captured.' } else { 'FalseNegativeCandidate: final CAD has no visual warning.' }
        $result.detailEvidence = @([ordered]@{ path=$result.detail1ImagePath; detected=$false; manualReviewRequired=$true; scope='topDimensionRegion' }, [ordered]@{ path=$result.detail2ImagePath; detected=$false; manualReviewRequired=$true; scope='unverifiedSecondaryRegion' })
    }
    else {
        $warningGroups = @($spatialWarnings | Group-Object { $_.handles -join '|' } | Select-Object -First 2)
        if ($warningGroups.Count -eq 1) {
            Save-FixedCrop $full $result.detail1ImagePath 0.22 0.05 0.37 0.34
            Save-FixedCrop $full $result.detail2ImagePath 0.00 0.18 0.38 0.63
            $result.evidenceAssessment = 'PartialDetection: only one spatial warning group was detected.'
            $result.detailEvidence = @([ordered]@{ path=$result.detail1ImagePath; detected=$true; manualReviewRequired=$false; code=$warningGroups[0].Group[0].code; handles=@($warningGroups[0].Group[0].handles); scope='topDetectedWarningRegion' }, [ordered]@{ path=$result.detail2ImagePath; detected=$false; manualReviewRequired=$true; scope='unverifiedSecondaryRegion' })
        }
        else {
          $index = 0
          foreach ($group in $warningGroups) {
            $window = Get-Window $rows @($group.Group[0].handles)
            $detail = if ($index -eq 0) { $result.detail1ImagePath } else { $result.detail2ImagePath }
            $detailScr = Join-Path $run ("detail-{0}.scr" -f ($index + 1))
            @"
(vl-cmdf "_.ZOOM" "_W" (list $($window[0]) $($window[1])) (list $($window[2]) $($window[3])))
(vl-cmdf "_.PNGOUT" $(Lisp $detail) "_ALL" "")
_.QUIT
_N
"@ | Set-Content -LiteralPath $detailScr -Encoding ascii
            Invoke-CoreConsole $working $detailScr (Join-Path $run ("detail-{0}.stdout.log" -f ($index + 1))) (Join-Path $run ("detail-{0}.stderr.log" -f ($index + 1)))
            $index++
          }
        }
    }
    foreach ($path in $full,$result.detail1ImagePath,$result.detail2ImagePath) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Image missing: $path" } }
    $result.warnings = @($visual.warnings); if (-not $result.evidenceAssessment) { $result.evidenceAssessment='WarningCandidate' }
    if ($result.blocking) { throw "Visual gate blocked M4-positive-collisions: $($result.blockingCodes -join ', ')." }
    $result.status='Completed'
}
catch { $result.status='Failed'; $result.error=$_.Exception.Message }
Save-Json $result $resultPath
$hashes = [ordered]@{ authoritativeFixture=(Get-Sha256 $authoritativeDwg); fixture=(Get-Sha256 $fixtureDwg); workingDrawing=(Get-Sha256 $working); dlls=$result.dlls; artifacts=[ordered]@{} }
foreach ($path in $inventory,$trace,$snapshot,$report,$markdown,$full,$result.detail1ImagePath,$result.detail2ImagePath,$resultPath) { if (Test-Path -LiteralPath $path -PathType Leaf) { $hashes.artifacts[(Split-Path -Leaf $path)] = Get-Sha256 $path } }
Save-Json $hashes $hashesPath
if ($result.status -ne 'Completed') { throw $result.error }
$result | ConvertTo-Json -Depth 16
