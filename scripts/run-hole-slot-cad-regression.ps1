[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DllDirectory,

    [ValidateSet('HS01-normal-hole', 'HS02-concentric-holes', 'HS03-loose-hole-chain', 'HS04-unique-pin-datum', 'HS05-multiple-pin-datum', 'HS06-functional-hole', 'HS07-vertical-waist-slot')]
    [string]$CaseId = 'HS01-normal-hole',

    [ValidateRange(1, 3)]
    [int]$RepeatCount = 2,

    [string]$CoreConsolePath,

    [string]$Profile,

    [ValidateRange(30, 1800)]
    [int]$TimeoutSeconds = 180,

    [string]$OutputRoot,

    [switch]$PlanOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$casesPath = Join-Path $projectRoot 'regression\hole-slot\cases.json'
$cases = Get-Content -Raw -Encoding UTF8 -LiteralPath $casesPath | ConvertFrom-Json
$case = @($cases.cases | Where-Object { $_.id -eq $CaseId })
if ($case.Count -ne 1) { throw "Expected exactly one $CaseId in $casesPath." }
$case = $case[0]
$fixturePath = Join-Path (Join-Path $projectRoot 'regression\hole-slot') ([string]$case.fixture)
$expectedFixtureSha256 = ([string]$case.fixtureSha256).ToUpperInvariant()
$fixtureReady = [bool]$case.fixtureReady
$requiredFinal = @($case.requiredFinal)
if ($requiredFinal.Count -eq 0) { throw "Case $CaseId has no requiredFinal contract." }
$successStatus = if ($fixtureReady) { 'Passed' } else { 'EvidenceCapturedNotReady' }

if ([string]::IsNullOrWhiteSpace($CoreConsolePath)) { $CoreConsolePath = $env:AUTOCAD_CORE_CONSOLE_PATH }
if ([string]::IsNullOrWhiteSpace($CoreConsolePath)) { $CoreConsolePath = 'D:\Program Files\Autodesk\AutoCAD 2020\accoreconsole.exe' }
if ([string]::IsNullOrWhiteSpace($Profile)) { $Profile = $env:AUTOCAD_PROFILE }
if ([string]::IsNullOrWhiteSpace($Profile)) { $Profile = '1' }

function Get-Sha256([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($Path)
        try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose() }
    }
    finally { $sha.Dispose() }
}

function ConvertTo-LispString([string]$Value) {
    return '"' + $Value.Replace('\', '/').Replace('"', '\"') + '"'
}

function Write-Json([string]$Path, $Value) {
    $Value | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-PowerShellExecutable {
    $pwsh = Get-Command pwsh -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $pwsh -and (Test-Path -LiteralPath $pwsh.Source -PathType Leaf)) { return $pwsh.Source }
    $command = Get-Command powershell.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command -and (Test-Path -LiteralPath $command.Source -PathType Leaf)) { return $command.Source }
    $systemPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (Test-Path -LiteralPath $systemPowerShell -PathType Leaf) { return $systemPowerShell }
    throw 'Could not resolve Windows PowerShell.'
}

function Get-DllEvidence([string]$Directory) {
    $resolved = (Resolve-Path -LiteralPath $Directory -ErrorAction Stop).Path
    $evidence = [ordered]@{}
    foreach ($name in 'AutoFixtureDim.dll', 'CadAuto.Core.dll', 'CadAuto.CadAdapter.dll') {
        $path = Join-Path $resolved $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required DLL missing: $path" }
        $evidence[$name] = [ordered]@{ path = (Resolve-Path -LiteralPath $path).Path; sha256 = Get-Sha256 $path }
    }
    return [pscustomobject]@{ directory = $resolved; files = $evidence }
}

function Get-NormalizedReportHash([string]$Path) {
    $report = Get-Content -Raw -Encoding UTF8 -LiteralPath $Path | ConvertFrom-Json
    foreach ($name in 'runId', 'generatedAt') { if ($report.PSObject.Properties.Name -contains $name) { $report.PSObject.Properties.Remove($name) } }
    if ($report.plugin) { foreach ($name in 'path', 'lastWriteTimeUtc') { if ($report.plugin.PSObject.Properties.Name -contains $name) { $report.plugin.PSObject.Properties.Remove($name) } } }
    if ($report.drawing) { foreach ($name in 'path', 'fullPath') { if ($report.drawing.PSObject.Properties.Name -contains $name) { $report.drawing.PSObject.Properties.Remove($name) } } }
    $json = $report | ConvertTo-Json -Depth 100 -Compress
    $runtimeIds = @{}
    $json = [regex]::Replace($json, '"\([0-9]+\)"', { param($match) if (-not $runtimeIds.ContainsKey($match.Value)) { $runtimeIds[$match.Value] = '"(__runtime-id-{0})"' -f ($runtimeIds.Count + 1) }; return $runtimeIds[$match.Value] })
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json)))).Replace('-', '') }
    finally { $sha.Dispose() }
}

function Get-NormalizedVisualHash([string]$Path) {
    $report = Get-Content -Raw -Encoding UTF8 -LiteralPath $Path | ConvertFrom-Json
    if ($report.environment) {
        if ($report.environment.PSObject.Properties.Name -contains 'DWGPREFIX') { $report.environment.PSObject.Properties.Remove('DWGPREFIX') }
        $orderedEnvironment = [ordered]@{}
        foreach ($property in $report.environment.PSObject.Properties | Sort-Object Name) { $orderedEnvironment[$property.Name] = $property.Value }
        $report.environment = [pscustomobject]$orderedEnvironment
    }
    $json = $report | ConvertTo-Json -Depth 100 -Compress
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json)))).Replace('-', '') }
    finally { $sha.Dispose() }
}

function Invoke-CoreConsole([string]$DrawingPath, [string]$ScriptPath, [string]$StdoutPath, [string]$StderrPath) {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $CoreConsolePath
    $startInfo.Arguments = @(
        '/i', ('"{0}"' -f $DrawingPath),
        '/s', ('"{0}"' -f $ScriptPath),
        '/l', 'zh-CN',
        '/p', ('"{0}"' -f $Profile)
    ) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [Text.Encoding]::Unicode
    $startInfo.StandardErrorEncoding = [Text.Encoding]::Unicode
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) { $process.Dispose(); throw "Could not start AutoCAD Core Console: $CoreConsolePath" }
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { throw "AutoCAD Core Console timed out; process was not killed. PID=$($process.Id)." }
    $stdoutText = $stdoutTask.Result
    $stderrText = $stderrTask.Result
    [IO.File]::WriteAllText($StdoutPath, $stdoutText, [Text.Encoding]::Unicode)
    [IO.File]::WriteAllText($StderrPath, $stderrText, [Text.Encoding]::Unicode)
    $exitCode = [int]$process.ExitCode
    $process.Dispose()
    return $exitCode
}

function Invoke-HoleSlotRun([int]$Repeat, [string]$BatchRoot, $DllEvidence, [string]$PowerShellExecutable) {
    $runRoot = Join-Path (Join-Path $BatchRoot ("repeat-{0}" -f $Repeat)) $CaseId
    New-Item -ItemType Directory -Path $runRoot | Out-Null
    $drawingPath = Join-Path $runRoot ([IO.Path]::GetFileName($fixturePath))
    Copy-Item -LiteralPath $fixturePath -Destination $drawingPath
    $tracePath = Join-Path $runRoot 'trace.log'
    $reportPath = Join-Path $runRoot 'report.json'
    $fullImagePath = Join-Path $runRoot 'full.png'
    $snapshotPath = Join-Path $runRoot 'visual-snapshot.tsv'
    $visualReportPath = Join-Path $runRoot 'visual-report.json'
    $visualMarkdownPath = Join-Path $runRoot 'visual-report.md'
    $runLspPath = Join-Path $runRoot 'run.lsp'
    $runScrPath = Join-Path $runRoot 'run.scr'
    $stdoutPath = Join-Path $runRoot 'acad.stdout.log'
    $stderrPath = Join-Path $runRoot 'acad.stderr.log'
    $validatorStdoutPath = Join-Path $runRoot 'validator.stdout.log'
    $validatorStderrPath = Join-Path $runRoot 'validator.stderr.log'
    $resultPath = Join-Path $runRoot 'result.json'
    $runStartedAt = [DateTime]::UtcNow
    $result = [ordered]@{
        schemaVersion = 1; status = 'Running'; error = $null; caseId = $CaseId; repeat = $Repeat; requiredFinal = $requiredFinal
        fixture = [ordered]@{ path = $fixturePath; sha256 = $expectedFixtureSha256; fixtureReady = $fixtureReady }
        workingDrawing = $drawingPath; cadExitCode = $null; validatorExitCode = $null; traceComplete = $false
        tracePath = $tracePath; reportPath = $reportPath; fullImagePath = $fullImagePath; visualSnapshotPath = $snapshotPath
        visualReportPath = $visualReportPath; visualMarkdownPath = $visualMarkdownPath; normalizedReportSha256 = $null
        normalizedVisualReportSha256 = $null; reportSha256 = $null; traceSha256 = $null; fullImageSha256 = $null
        visualBlocking = $false; visualBlockingCodes = @(); visualWarningCount = $null; dlls = $DllEvidence.files; pinDatum = $null; functionalHole = $null
    }
    try {
        $pluginPath = [string]$DllEvidence.files['AutoFixtureDim.dll'].path
        $visualLspPath = Join-Path $projectRoot 'scripts\cad-visual-inspection.lsp'
        $selectionExpression = '(ssget "_X" ''((8 . "DRAWING") (-4 . "<OR") (0 . "LINE") (0 . "LWPOLYLINE") (0 . "POLYLINE") (0 . "ARC") (0 . "CIRCLE") (-4 . "OR>") (410 . "Model")))'
        $reproInvocation = '(progn (vl-cmdf "_.ASDREPRO" "H" "A" selection "") T)'
        $caseLisp = ''
        if ($CaseId -in @('HS03-loose-hole-chain', 'HS04-unique-pin-datum', 'HS05-multiple-pin-datum', 'HS06-functional-hole')) {
            $selectionExpression = '(ssget "_X" ''((-4 . "<OR") (-4 . "<AND") (8 . "DRAWING") (-4 . "<OR") (0 . "LINE") (0 . "LWPOLYLINE") (0 . "POLYLINE") (0 . "ARC") (0 . "CIRCLE") (-4 . "OR>") (-4 . "AND>") (0 . "INSERT") (-4 . "OR>") (410 . "Model")))'
        }
        if ($CaseId -eq 'HS04-unique-pin-datum') {
            $reproInvocation = '(hs-run-unique-pin-datum selection)'
            $caseLisp = @'
(defun hs-run-unique-pin-datum (selection / extmin point)
  (setq extmin (getvar "EXTMIN"))
  (setq point (list (car extmin) (cadr extmin) 0.0))
  (hs-write (strcat "HS04_DATUM_POINT source=EXTMIN minX=" (rtos (car point) 2 8) " minY=" (rtos (cadr point) 2 8) " toleranceX=A toleranceY=A"))
  (vl-cmdf "_.ASDREPRO" "H" "A" selection "" point "A" point "A")
  T)
'@
        }
        elseif ($CaseId -in @('HS03-loose-hole-chain', 'HS05-multiple-pin-datum', 'HS06-functional-hole')) {
            $reproInvocation = if ($CaseId -eq 'HS03-loose-hole-chain') { '(hs-run-pin-group-datum selection)' } elseif ($CaseId -eq 'HS05-multiple-pin-datum') { '(hs-run-multiple-pin-datum selection)' } else { '(hs-run-functional-hole selection)' }
            $caseLisp = @'
(setq *hs-pin-marker-name* (vl-list->string '(67 97 100 65 105 100 101 114 95 207 250 191 215 177 234 188 199)))
(setq *hs-pin-marker-back-name* (vl-list->string '(67 97 100 65 105 100 101 114 95 207 250 191 215 177 234 188 199 177 179 195 230)))
(defun hs-pin-marker-p (data / name)
  (setq name (cdr (assoc 2 data)))
  (and name (or (= name *hs-pin-marker-name*) (= name *hs-pin-marker-back-name*))))
(defun hs-pin-circle-has-marker-p (center radius markerCenters / markerCenter matched limit limitSquared dx dy)
  (setq matched nil
        limit (max (+ radius 0.01) 1.0)
        limitSquared (* limit limit))
  (foreach markerCenter markerCenters
    (setq dx (- (car center) (car markerCenter))
          dy (- (cadr center) (cadr markerCenter)))
    (if (<= (+ (* dx dx) (* dy dy)) limitSquared)
      (setq matched T)))
  matched)
(defun hs-pin-handle-less-p (left right / index leftLength rightLength)
  (setq left (strcase left)
        right (strcase right)
        index 1
        leftLength (strlen left)
        rightLength (strlen right))
  (while (and (<= index leftLength)
              (<= index rightLength)
              (= (ascii (substr left index 1)) (ascii (substr right index 1))))
    (setq index (1+ index)))
  (cond
    ((> index leftLength) (< leftLength rightLength))
    ((> index rightLength) nil)
    (T (< (ascii (substr left index 1)) (ascii (substr right index 1))))))
(defun hs-pin-candidate-less-p (left right / leftCenter rightCenter leftX rightX leftY rightY)
  (setq leftCenter (nth 1 left)
        rightCenter (nth 1 right)
        leftX (car leftCenter)
        rightX (car rightCenter)
        leftY (cadr leftCenter)
        rightY (cadr rightCenter))
  (cond
    ((< leftX rightX) T)
    ((> leftX rightX) nil)
    ((< leftY rightY) T)
    ((> leftY rightY) nil)
    (T (hs-pin-handle-less-p (nth 3 left) (nth 3 right)))))
(defun hs-pin-collect-candidates (selection tracePrefix / scanIndex entity data entityType markerCenters rawCircles candidates center radius handle insertCount circleCount rawCircle)
  (setq scanIndex 0 markerCenters '() rawCircles '() candidates '() insertCount 0 circleCount 0)
  (while (< scanIndex (sslength selection))
    (setq entity (ssname selection scanIndex)
          data (entget entity)
          entityType (cdr (assoc 0 data)))
    (cond
      ((= entityType "INSERT")
       (setq insertCount (1+ insertCount))
       (if (hs-pin-marker-p data)
         (setq markerCenters (cons (cdr (assoc 10 data)) markerCenters))))
      ((= entityType "CIRCLE")
       (setq circleCount (1+ circleCount)
             center (cdr (assoc 10 data))
             radius (cdr (assoc 40 data))
             handle (cdr (assoc 5 data)))
       (setq rawCircles (cons (list entity center radius handle) rawCircles))))
    (setq scanIndex (1+ scanIndex)))
  (hs-write (strcat tracePrefix "_CANDIDATE_SCAN stage=raw inserts=" (itoa insertCount) " markers=" (itoa (length markerCenters)) " circles=" (itoa circleCount) " rawCircles=" (itoa (length rawCircles)) " candidates=0"))
  (foreach rawCircle rawCircles
    (if (hs-pin-circle-has-marker-p (nth 1 rawCircle) (nth 2 rawCircle) markerCenters)
      (setq candidates (cons rawCircle candidates))))
  (hs-write (strcat tracePrefix "_CANDIDATE_SCAN stage=candidates inserts=" (itoa insertCount) " markers=" (itoa (length markerCenters)) " circles=" (itoa circleCount) " rawCircles=" (itoa (length rawCircles)) " candidates=" (itoa (length candidates))))
  (setq candidates (vl-sort candidates 'hs-pin-candidate-less-p))
  (hs-write (strcat tracePrefix "_CANDIDATE_SCAN stage=sorted inserts=" (itoa insertCount) " markers=" (itoa (length markerCenters)) " circles=" (itoa circleCount) " rawCircles=" (itoa (length rawCircles)) " candidates=" (itoa (length candidates))))
  (list (length markerCenters) candidates))
(defun hs-pin-write-candidates (tracePrefix candidates / candidate index center)
  (setq index 1)
  (foreach candidate candidates
    (setq center (nth 1 candidate))
    (hs-write (strcat tracePrefix "_PIN_CANDIDATE index=" (itoa index) " handle=" (nth 3 candidate) " centerX=" (rtos (car center) 2 8) " centerY=" (rtos (cadr center) 2 8) " radius=" (rtos (nth 2 candidate) 2 8)))
    (setq index (1+ index))))
(defun hs-pin-write-selected (tracePrefix selected selectionInput / center radius pickPoint)
  (setq center (nth 1 selected)
        radius (nth 2 selected)
        pickPoint (list (+ (car center) radius) (cadr center) (caddr center)))
  (hs-write (strcat tracePrefix "_PIN_SELECTED handle=" (nth 3 selected) " centerX=" (rtos (car center) 2 8) " centerY=" (rtos (cadr center) 2 8) " radius=" (rtos radius 2 8) " pickX=" (rtos (car pickPoint) 2 8) " pickY=" (rtos (cadr pickPoint) 2 8) " selectionInput=" selectionInput)))
(defun hs-run-datum-aware (selection tracePrefix requireMultiple / match markerCount candidates candidateCount selected extmin datumPoint)
  (setq match (hs-pin-collect-candidates selection tracePrefix)
        markerCount (car match)
        candidates (cadr match)
        candidateCount (length candidates))
  (hs-write (strcat tracePrefix "_PIN_CANDIDATES count=" (itoa candidateCount) " markers=" (itoa markerCount)))
  (cond
    ((and requireMultiple (< candidateCount 2))
      (hs-stop (strcat tracePrefix "_FIXTURE_MULTIPLE_PIN_SEMANTIC_CONFLICT candidates=" (itoa candidateCount) " required=2 ASDREPRO=not-invoked"))
      nil)
    ((= candidateCount 0)
      (hs-write (strcat tracePrefix "_PIN_INTERACTION selectedHandle=none inputMode=SelectionOnly"))
      (vl-cmdf "_.ASDREPRO" "H" "A" selection "")
      T)
    (T
      (hs-pin-write-candidates tracePrefix candidates)
      (setq selected (car candidates)
            extmin (getvar "EXTMIN")
            datumPoint (list (car extmin) (cadr extmin) 0.0))
      (if (and (not requireMultiple) (= candidateCount 1))
        (hs-write (strcat tracePrefix "_PIN_INTERACTION selectedHandle=" (nth 3 selected) " inputMode=AutoDatum"))
        (progn
          (hs-pin-write-selected tracePrefix selected "ENAME")
          (if (not requireMultiple) (hs-write (strcat tracePrefix "_PIN_INTERACTION selectedHandle=" (nth 3 selected) " inputMode=ENAME")))))
      (hs-write (strcat tracePrefix "_DATUM_POINT source=EXTMIN minX=" (rtos (car datumPoint) 2 8) " minY=" (rtos (cadr datumPoint) 2 8) " toleranceX=A toleranceY=A"))
      (if (and (not requireMultiple) (= candidateCount 1))
        (vl-cmdf "_.ASDREPRO" "H" "A" selection "" datumPoint "A" datumPoint "A")
        (vl-cmdf "_.ASDREPRO" "H" "A" selection "" (nth 0 selected) datumPoint "A" datumPoint "A"))
      T)))
(defun hs-run-multiple-pin-datum (selection)
  (hs-run-datum-aware selection "HS05" T))
(defun hs-run-pin-group-datum (selection)
  (hs-run-datum-aware selection "HS03" T))
(defun hs-run-functional-hole (selection)
  (hs-run-datum-aware selection "HS06" nil))
'@
        }
        @"
(vl-load-com)
(setq *hs-trace* $(ConvertTo-LispString $tracePath))
(setq *hs-report* $(ConvertTo-LispString $reportPath))
(setq *hs-snapshot* $(ConvertTo-LispString $snapshotPath))
(setq *hs-requires-circle* $(if ($CaseId -eq 'HS07-vertical-waist-slot') { 'nil' } else { 'T' }))
(defun hs-write (text / stream) (setq stream (open *hs-trace* "a")) (if stream (progn (write-line text stream) (close stream))))
(defun hs-type-count (selection dxf / index count entity data result)
  (setq index 0 count 0)
  (while (< index (sslength selection))
    (setq entity (ssname selection index) data (entget entity))
    (if (= (cdr (assoc 0 data)) dxf) (setq count (1+ count)))
    (setq index (1+ index)))
  count)
$caseLisp
(defun hs-stop (reason) (hs-write (strcat "ERROR " reason)) (princ))
(defun c:RUNHOLESLOT (/ selection capture repro)
  (setq selection $selectionExpression)
  (cond
    ((null selection) (hs-stop "selectionEmpty"))
    ((and *hs-requires-circle* (= 0 (hs-type-count selection "CIRCLE"))) (hs-stop "circleMissing"))
    ((= 0 (+ (hs-type-count selection "LINE") (hs-type-count selection "LWPOLYLINE") (hs-type-count selection "POLYLINE") (hs-type-count selection "ARC"))) (hs-stop "outlineMissing"))
    (T
      (hs-write (strcat "INPUT layer=DRAWING space=Model selection=" (itoa (sslength selection)) " circles=" (itoa (hs-type-count selection "CIRCLE"))))
      (if (= $(ConvertTo-LispString $CaseId) "HS07-vertical-waist-slot")
        (hs-write (strcat "HS07_SLOT_GEOMETRY lines=" (itoa (hs-type-count selection "LINE")) " lwpolylines=" (itoa (hs-type-count selection "LWPOLYLINE")) " polylines=" (itoa (hs-type-count selection "POLYLINE")) " arcs=" (itoa (hs-type-count selection "ARC")) " circles=" (itoa (hs-type-count selection "CIRCLE")))))
      (hs-write "ASDREPRO scope=Hole selection=All")
      (setq repro $reproInvocation)
      (if repro
        (cond
          ((not (findfile *hs-report*)) (hs-stop "reportMissing"))
          (T
            (load $(ConvertTo-LispString $visualLspPath))
            (setq capture (m4-capture selection *hs-snapshot* $(ConvertTo-LispString $CaseId) "All"))
            (if capture (hs-write "COMPLETE scope=Hole") (hs-stop "visualCaptureFailed"))))))))
(princ)
"@ | Set-Content -LiteralPath $runLspPath -Encoding ASCII
        $lispCode = [regex]::Replace((Get-Content -Raw -LiteralPath $runLspPath), '"(?:\\.|[^"])*"', '')
        $lispParenthesisDelta = [regex]::Matches($lispCode, '\(').Count - [regex]::Matches($lispCode, '\)').Count
        if ($lispParenthesisDelta -ne 0) { throw "Generated run.lsp has unbalanced parentheses: delta=$lispParenthesisDelta" }
        @"
(setvar "FILEDIA" 0)
(setvar "CMDDIA" 0)
(command "_.NETLOAD" $(ConvertTo-LispString $pluginPath))
(load $(ConvertTo-LispString $runLspPath))
(c:RUNHOLESLOT)
(vl-cmdf "_.REGEN")
(vl-cmdf "_.ZOOM" "_E")
(vl-cmdf "_.PNGOUT" $(ConvertTo-LispString $fullImagePath) "_ALL" "")
_.QUIT
_N
"@ | Set-Content -LiteralPath $runScrPath -Encoding ASCII

        $previousReportPath = $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH
        $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH = $reportPath
        try { $result.cadExitCode = Invoke-CoreConsole $drawingPath $runScrPath $stdoutPath $stderrPath }
        finally { $env:AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH = $previousReportPath }
        if ($result.cadExitCode -ne 0) { throw "AutoCAD Core Console exited $($result.cadExitCode)." }
        if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) { throw "Required artifact was not generated: $tracePath" }
        $trace = @(Get-Content -LiteralPath $tracePath)
        $traceError = @($trace | Where-Object { $_ -like 'ERROR *' } | Select-Object -First 1)
        if ($traceError.Count) { throw "CAD trace failure: $traceError" }
        if ($CaseId -eq 'HS04-unique-pin-datum') {
            $datumPointLines = @($trace | Where-Object { $_ -like 'HS04_DATUM_POINT *' })
            if ($datumPointLines.Count -ne 1) { throw 'HS04 trace lacks exactly one datum-point record.' }
            $datumPointMatch = [regex]::Match($datumPointLines[0], '^HS04_DATUM_POINT source=EXTMIN minX=(?<minX>\S+) minY=(?<minY>\S+) toleranceX=A toleranceY=A$')
            if (-not $datumPointMatch.Success) { throw "HS04 datum-point trace is malformed: $($datumPointLines[0])" }
        }
        if ($CaseId -eq 'HS07-vertical-waist-slot') {
            $slotGeometryLines = @($trace | Where-Object { $_ -like 'HS07_SLOT_GEOMETRY *' })
            if ($slotGeometryLines.Count -ne 1) { throw 'HS07 trace lacks exactly one slot geometry record.' }
            $slotGeometryMatch = [regex]::Match($slotGeometryLines[0], '^HS07_SLOT_GEOMETRY lines=(?<lines>\d+) lwpolylines=(?<lwpolylines>\d+) polylines=(?<polylines>\d+) arcs=(?<arcs>\d+) circles=(?<circles>\d+)$')
            if (-not $slotGeometryMatch.Success) { throw "HS07 slot geometry trace is malformed: $($slotGeometryLines[0])" }
            $outlineEntityCount = [int]$slotGeometryMatch.Groups['lines'].Value + [int]$slotGeometryMatch.Groups['lwpolylines'].Value + [int]$slotGeometryMatch.Groups['polylines'].Value + [int]$slotGeometryMatch.Groups['arcs'].Value
            if ($outlineEntityCount -eq 0) { throw 'HS07 slot geometry trace contains no outline entities.' }
        }
        if ($CaseId -eq 'HS03-loose-hole-chain') {
            $candidateSummary = @($trace | Where-Object { $_ -like 'HS03_PIN_CANDIDATES *' })
            if ($candidateSummary.Count -ne 1) { throw 'HS03 trace lacks exactly one candidate datum record.' }
        }
        elseif ($CaseId -eq 'HS05-multiple-pin-datum') {
            $candidateSummary = @($trace | Where-Object { $_ -like 'HS05_PIN_CANDIDATES *' })
            if ($candidateSummary.Count -ne 1) { throw 'HS05 trace lacks exactly one candidate datum record.' }
        }
        elseif ($CaseId -eq 'HS06-functional-hole') {
            $candidateSummary = @($trace | Where-Object { $_ -like 'HS06_PIN_CANDIDATES *' })
            if ($candidateSummary.Count -ne 1) { throw 'HS06 trace lacks exactly one candidate datum record.' }
        }
        if ($CaseId -eq 'HS05-multiple-pin-datum') {
            $manualDatumMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('6K+36YCJ5oup5LiA5Liq6ZSA5a2U5L2c5Li65Z+65YeG5a2U'))
            $matchedPinMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5bey5Yy56YWN5Yiw5bey6K+G5Yir6ZSA5a2U'))
            $datumSetMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5bey6K6+572u5Z+65YeG5a2U'))
            $autoDatumMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5LuF5pyJ5LiA5Liq5ZCI5rOV6ZSA5a2U5YCJ77yM5bey6Ieq5Yqo6K6+5Li65Z+65YeG5a2U'))
            $acadStdout = Get-Content -Raw -Encoding Unicode -LiteralPath $stdoutPath
            if (-not $acadStdout.Contains($manualDatumMessage)) { throw 'HS05 CAD stdout lacks manual pin datum selection prompt.' }
            if (-not $acadStdout.Contains($matchedPinMessage)) { throw 'HS05 CAD stdout lacks recognized pin match message.' }
            if (-not $acadStdout.Contains($datumSetMessage)) { throw 'HS05 CAD stdout lacks datum-set success message.' }
            if ($acadStdout.Contains($autoDatumMessage)) { throw 'HS05 CAD stdout unexpectedly auto-selected a unique pin datum.' }
        }
        if ($CaseId -eq 'HS04-unique-pin-datum') {
            $report = Get-Content -Raw -Encoding UTF8 -LiteralPath $reportPath | ConvertFrom-Json
            if ($null -eq $report -or -not ($report.PSObject.Properties.Name -contains 'finalDimensions')) { throw 'HS04 report lacks finalDimensions.' }
            $selectedDatumX = @($report.finalDimensions | Where-Object { $_.debugRole -eq 'DatumX' -and $_.decision -eq 'Selected' -and $_.decisionStatus -eq 'Selected' -and $_.isSelected -eq $true -and $_.isAttachmentValid -eq $true })
            $selectedDatumY = @($report.finalDimensions | Where-Object { $_.debugRole -eq 'DatumY' -and $_.decision -eq 'Selected' -and $_.decisionStatus -eq 'Selected' -and $_.isSelected -eq $true -and $_.isAttachmentValid -eq $true })
            if ($selectedDatumX.Count -ne 1 -or $selectedDatumY.Count -ne 1) { throw "HS04 report must contain exactly one selected, valid DatumX and DatumY; DatumX=$($selectedDatumX.Count), DatumY=$($selectedDatumY.Count)." }
            $result.pinDatum = [ordered]@{ trace = $datumPointLines[0]; selectedFinalRoles = @('DatumX', 'DatumY') }
        }
        elseif ($CaseId -eq 'HS03-loose-hole-chain') {
            $candidateLines = @($trace | Where-Object { $_ -like 'HS03_PIN_CANDIDATE *' })
            $selectedLines = @($trace | Where-Object { $_ -like 'HS03_PIN_SELECTED *' })
            if ($selectedLines.Count -ne 1) { throw 'HS03 trace lacks exactly one selected datum record.' }
            $summaryMatch = [regex]::Match($candidateSummary[0], '^HS03_PIN_CANDIDATES count=(?<count>\d+) markers=(?<markers>\d+)$')
            if (-not $summaryMatch.Success) { throw "HS03 candidate trace is malformed: $($candidateSummary[0])" }
            $candidateCount = [int]$summaryMatch.Groups['count'].Value
            if ($candidateCount -lt 2 -or $candidateLines.Count -ne $candidateCount) { throw "HS03 multiple-pin trace contradicts candidate count: declared=$candidateCount, recorded=$($candidateLines.Count)." }
            $candidateRecords = @(
                foreach ($candidateLine in $candidateLines) {
                    $candidateMatch = [regex]::Match($candidateLine, '^HS03_PIN_CANDIDATE index=(?<index>\d+) handle=(?<handle>\S+) centerX=(?<centerX>\S+) centerY=(?<centerY>\S+) radius=(?<radius>\S+)$')
                    if (-not $candidateMatch.Success) { throw "HS03 candidate trace is malformed: $candidateLine" }
                    [ordered]@{ index = [int]$candidateMatch.Groups['index'].Value; handle = $candidateMatch.Groups['handle'].Value }
                }
            )
            $selectedMatch = [regex]::Match($selectedLines[0], '^HS03_PIN_SELECTED handle=(?<handle>\S+) centerX=(?<centerX>\S+) centerY=(?<centerY>\S+) radius=(?<radius>\S+) pickX=(?<pickX>\S+) pickY=(?<pickY>\S+) selectionInput=(?<selectionInput>ENAME)$')
            if (-not $selectedMatch.Success) { throw "HS03 selected pin trace is malformed: $($selectedLines[0])" }
            if (@($candidateRecords | Where-Object { $_.handle -eq $selectedMatch.Groups['handle'].Value }).Count -ne 1) { throw 'HS03 selected pin handle is not an unambiguous matched candidate.' }
        }
        elseif ($CaseId -eq 'HS05-multiple-pin-datum') {
            $candidateLines = @($trace | Where-Object { $_ -like 'HS05_PIN_CANDIDATE *' })
            $selectedLines = @($trace | Where-Object { $_ -like 'HS05_PIN_SELECTED *' })
            if ($selectedLines.Count -ne 1) { throw 'HS05 trace lacks exactly one selected datum record.' }
            $summaryMatch = [regex]::Match($candidateSummary[0], '^HS05_PIN_CANDIDATES count=(?<count>\d+) markers=(?<markers>\d+)$')
            if (-not $summaryMatch.Success) { throw "HS05 candidate trace is malformed: $($candidateSummary[0])" }
            $candidateCount = [int]$summaryMatch.Groups['count'].Value
            if ($candidateCount -lt 2 -or $candidateLines.Count -ne $candidateCount) { throw "HS05 multiple-pin trace contradicts candidate count: declared=$candidateCount, recorded=$($candidateLines.Count)." }
            $candidateRecords = @(
                foreach ($candidateLine in $candidateLines) {
                    $candidateMatch = [regex]::Match($candidateLine, '^HS05_PIN_CANDIDATE index=(?<index>\d+) handle=(?<handle>\S+) centerX=(?<centerX>\S+) centerY=(?<centerY>\S+) radius=(?<radius>\S+)$')
                    if (-not $candidateMatch.Success) { throw "HS05 candidate trace is malformed: $candidateLine" }
                    [ordered]@{
                        index = [int]$candidateMatch.Groups['index'].Value; handle = $candidateMatch.Groups['handle'].Value
                        centerX = $candidateMatch.Groups['centerX'].Value; centerY = $candidateMatch.Groups['centerY'].Value; radius = $candidateMatch.Groups['radius'].Value
                    }
                }
            )
            $selectedMatch = [regex]::Match($selectedLines[0], '^HS05_PIN_SELECTED handle=(?<handle>\S+) centerX=(?<centerX>\S+) centerY=(?<centerY>\S+) radius=(?<radius>\S+) pickX=(?<pickX>\S+) pickY=(?<pickY>\S+) selectionInput=(?<selectionInput>ENAME)$')
            if (-not $selectedMatch.Success) { throw "HS05 selected pin trace is malformed: $($selectedLines[0])" }
            $selectedRecord = [ordered]@{
                handle = $selectedMatch.Groups['handle'].Value; centerX = $selectedMatch.Groups['centerX'].Value; centerY = $selectedMatch.Groups['centerY'].Value
                radius = $selectedMatch.Groups['radius'].Value; pickX = $selectedMatch.Groups['pickX'].Value; pickY = $selectedMatch.Groups['pickY'].Value; selectionInput = $selectedMatch.Groups['selectionInput'].Value
            }
            if (@($candidateRecords | Where-Object { $_.handle -eq $selectedRecord.handle }).Count -ne 1) { throw 'HS05 selected pin handle is not an unambiguous matched candidate.' }
            $result.pinDatum = [ordered]@{ candidateCount = $candidateCount; markerCount = [int]$summaryMatch.Groups['markers'].Value; candidates = @($candidateRecords); selected = $selectedRecord }
        }
        elseif ($CaseId -eq 'HS06-functional-hole') {
            $candidateLines = @($trace | Where-Object { $_ -like 'HS06_PIN_CANDIDATE *' })
            $selectedLines = @($trace | Where-Object { $_ -like 'HS06_PIN_SELECTED *' })
            $interactionLines = @($trace | Where-Object { $_ -like 'HS06_PIN_INTERACTION *' })
            if ($interactionLines.Count -ne 1) { throw 'HS06 trace lacks exactly one datum interaction record.' }
            $summaryMatch = [regex]::Match($candidateSummary[0], '^HS06_PIN_CANDIDATES count=(?<count>\d+) markers=(?<markers>\d+)$')
            if (-not $summaryMatch.Success) { throw "HS06 candidate trace is malformed: $($candidateSummary[0])" }
            $candidateCount = [int]$summaryMatch.Groups['count'].Value
            if ($candidateLines.Count -ne $candidateCount) { throw "HS06 candidate trace contradicts candidate count: declared=$candidateCount, recorded=$($candidateLines.Count)." }
            $candidateRecords = @(
                foreach ($candidateLine in $candidateLines) {
                    $candidateMatch = [regex]::Match($candidateLine, '^HS06_PIN_CANDIDATE index=(?<index>\d+) handle=(?<handle>\S+) centerX=(?<centerX>\S+) centerY=(?<centerY>\S+) radius=(?<radius>\S+)$')
                    if (-not $candidateMatch.Success) { throw "HS06 candidate trace is malformed: $candidateLine" }
                    [ordered]@{ index = [int]$candidateMatch.Groups['index'].Value; handle = $candidateMatch.Groups['handle'].Value }
                }
            )
            $interactionMatch = [regex]::Match($interactionLines[0], '^HS06_PIN_INTERACTION selectedHandle=(?<handle>\S+) inputMode=(?<inputMode>\S+)$')
            if (-not $interactionMatch.Success) { throw "HS06 datum interaction trace is malformed: $($interactionLines[0])" }
            $interactionRecord = [ordered]@{ handle = $interactionMatch.Groups['handle'].Value; inputMode = $interactionMatch.Groups['inputMode'].Value }
            if ($candidateCount -eq 0) {
                if ($selectedLines.Count -ne 0 -or $interactionRecord.handle -ne 'none' -or $interactionRecord.inputMode -ne 'SelectionOnly') { throw 'HS06 zero-candidate interaction contradicts the candidate trace.' }
            }
            elseif ($candidateCount -eq 1) {
                if ($selectedLines.Count -ne 0 -or $interactionRecord.inputMode -ne 'AutoDatum' -or $interactionRecord.handle -ne $candidateRecords[0].handle) { throw 'HS06 one-candidate interaction must auto-select the real candidate.' }
            }
            else {
                if ($selectedLines.Count -ne 1) { throw 'HS06 multiple-candidate trace lacks exactly one selected datum record.' }
                $selectedMatch = [regex]::Match($selectedLines[0], '^HS06_PIN_SELECTED handle=(?<handle>\S+) centerX=(?<centerX>\S+) centerY=(?<centerY>\S+) radius=(?<radius>\S+) pickX=(?<pickX>\S+) pickY=(?<pickY>\S+) selectionInput=(?<selectionInput>\S+)$')
                if (-not $selectedMatch.Success) { throw "HS06 selected pin trace is malformed: $($selectedLines[0])" }
                $selectedRecord = [ordered]@{ handle = $selectedMatch.Groups['handle'].Value; selectionInput = $selectedMatch.Groups['selectionInput'].Value }
                if (@($candidateRecords | Where-Object { $_.handle -eq $selectedRecord.handle }).Count -ne 1) { throw 'HS06 selected pin handle is not an unambiguous matched candidate.' }
                if ($selectedRecord.selectionInput -ne 'ENAME' -or $selectedRecord.handle -ne $candidateRecords[0].handle -or $interactionRecord.inputMode -ne 'ENAME' -or $interactionRecord.handle -ne $selectedRecord.handle) { throw 'HS06 multiple-candidate interaction must select the stable first candidate by ENAME.' }
            }
            $manualDatumMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('6K+36YCJ5oup5LiA5Liq6ZSA5a2U5L2c5Li65Z+65YeG5a2U'))
            $matchedPinMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5bey5Yy56YWN5Yiw5bey6K+G5Yir6ZSA5a2U'))
            $datumSetMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5bey6K6+572u5Z+65YeG5a2U'))
            $autoDatumMessage = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('5LuF5pyJ5LiA5Liq5ZCI5rOV6ZSA5a2U5YCJ77yM5bey6Ieq5Yqo6K6+5Li65YeG5a2U'))
            $acadStdout = Get-Content -Raw -Encoding Unicode -LiteralPath $stdoutPath
            if ($candidateCount -eq 0) {
                if ($acadStdout.Contains($autoDatumMessage) -or $acadStdout.Contains($manualDatumMessage) -or $acadStdout.Contains($matchedPinMessage) -or $acadStdout.Contains($datumSetMessage)) { throw 'HS06 zero-candidate datum interaction contradicts the candidate trace.' }
            }
            elseif ($candidateCount -eq 1) {
                if (-not $acadStdout.Contains($autoDatumMessage) -or $acadStdout.Contains($manualDatumMessage)) { throw 'HS06 one-candidate datum interaction contradicts automatic candidate selection.' }
            }
            elseif (-not $acadStdout.Contains($manualDatumMessage) -or -not $acadStdout.Contains($matchedPinMessage) -or -not $acadStdout.Contains($datumSetMessage) -or $acadStdout.Contains($autoDatumMessage)) {
                throw 'HS06 multiple-candidate datum interaction contradicts ENAME candidate selection.'
            }
        }
        foreach ($path in $reportPath, $snapshotPath, $fullImagePath) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required artifact was not generated: $path" } }
        $result.traceSha256 = Get-Sha256 $tracePath
        $result.reportSha256 = Get-Sha256 $reportPath
        $result.fullImageSha256 = Get-Sha256 $fullImagePath
        if ($CaseId -eq 'HS03-loose-hole-chain') {
            $report = Get-Content -Raw -Encoding UTF8 -LiteralPath $reportPath | ConvertFrom-Json
            if (-not ($report.PSObject.Properties.Name -contains 'features') -or $null -eq $report.features -or -not ($report.features.PSObject.Properties.Name -contains 'pinHoleCount')) { throw 'HS03 report lacks features.pinHoleCount.' }
            if (-not ($report.PSObject.Properties.Name -contains 'finalDimensions')) { throw 'HS03 report lacks finalDimensions.' }
            $recognizedPinHoleCount = [int]$report.features.pinHoleCount
            $selectedFinals = @($report.finalDimensions | Where-Object { $_.decision -eq 'Selected' })
            $pinGroupOwnedFinals = @($selectedFinals | Where-Object { $_.ownerKind -eq 'PinGroup' })
            $result.pinDatum = [ordered]@{
                recognizedPinHoleCount = $recognizedPinHoleCount
                pinGroupOwnedFinalCount = $pinGroupOwnedFinals.Count
                debugRoles = @($pinGroupOwnedFinals | ForEach-Object { [string]$_.debugRole } | Select-Object -Unique)
            }
            if ($recognizedPinHoleCount -lt 2) { throw "HS03 report recognized only $recognizedPinHoleCount pin holes; expected at least 2." }
            if ($pinGroupOwnedFinals.Count -lt 1) { throw 'HS03 report has no selected PinGroup-owned final dimension.' }
        }
        elseif ($CaseId -eq 'HS05-multiple-pin-datum') {
            $report = Get-Content -Raw -Encoding UTF8 -LiteralPath $reportPath | ConvertFrom-Json
            if ($null -eq $report.features -or -not ($report.features.PSObject.Properties.Name -contains 'pinHoleCount')) { throw 'HS05 report lacks features.pinHoleCount.' }
            $recognizedPinHoleCount = [int]$report.features.pinHoleCount
            if ($recognizedPinHoleCount -lt 2) { throw "HS05 report recognized only $recognizedPinHoleCount pin holes; expected at least 2." }
            $result.pinDatum['recognizedPinHoleCount'] = $recognizedPinHoleCount
        }
        elseif ($CaseId -eq 'HS06-functional-hole') {
            $report = Get-Content -Raw -Encoding UTF8 -LiteralPath $reportPath | ConvertFrom-Json
            if ($null -eq $report -or -not ($report.PSObject.Properties.Name -contains 'finalDimensions')) { throw 'HS06 report lacks finalDimensions.' }
            $functionalHoles = @($report.finalDimensions | Where-Object { $_.debugRole -eq 'FunctionalHole' })
            $functionalHoleSummary = @(
                foreach ($functionalHole in $functionalHoles) {
                    $summary = [ordered]@{
                        debugOwner = if ($functionalHole.PSObject.Properties.Name -contains 'debugOwner') { $functionalHole.debugOwner } else { $null }
                        ownerKind = if ($functionalHole.PSObject.Properties.Name -contains 'ownerKind') { $functionalHole.ownerKind } else { $null }
                        usesLocalBoundary = if ($functionalHole.PSObject.Properties.Name -contains 'usesLocalBoundary') { $functionalHole.usesLocalBoundary } else { $null }
                        placementSide = if ($functionalHole.PSObject.Properties.Name -contains 'placementSide') { $functionalHole.placementSide } else { $null }
                    }
                    if ($functionalHole.PSObject.Properties.Name -contains 'sourceGeometryIds') { $summary['sourceGeometryIds'] = @($functionalHole.sourceGeometryIds) }
                    [pscustomobject]$summary
                }
            )
            $result.functionalHole = [ordered]@{ count = $functionalHoles.Count; dimensions = @($functionalHoleSummary) }
            if ($functionalHoles.Count -lt 1) { throw 'HS06 report has no FunctionalHole final dimension.' }
            if (@($functionalHoles | Where-Object { -not ($_.PSObject.Properties.Name -contains 'ownerKind') -or $_.ownerKind -ne 'PinGroup' }).Count) { throw 'HS06 FunctionalHole ownerKind must be PinGroup.' }
            if (@($functionalHoles | Where-Object { -not ($_.PSObject.Properties.Name -contains 'usesLocalBoundary') -or $_.usesLocalBoundary -ne $true }).Count) { throw 'HS06 FunctionalHole must use a local boundary.' }
        }
        if ($trace -notcontains 'COMPLETE scope=Hole') { throw "CAD trace has no COMPLETE scope=Hole marker for $CaseId." }
        $result.traceComplete = $true

        $validatorArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $projectRoot 'scripts\validate-hole-slot-report.ps1'), '-CaseId', $CaseId, '-ReportPath', $reportPath, '-RunStartedAt', $runStartedAt)
        if (-not $fixtureReady) { $validatorArguments += '-AllowNotReady' }
        & $PowerShellExecutable @validatorArguments 1> $validatorStdoutPath 2> $validatorStderrPath
        $result.validatorExitCode = $LASTEXITCODE
        if ($result.validatorExitCode -ne 0) { throw "Hole/slot validator exited $($result.validatorExitCode)." }

        & $PowerShellExecutable -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot 'scripts\write-cad-visual-report.ps1') -SnapshotPath $snapshotPath -ReportPath $visualReportPath -MarkdownPath $visualMarkdownPath -CaseId $CaseId -ExpectedDirection All -FullImagePath $fullImagePath
        if ($LASTEXITCODE -ne 0) { throw "Visual report exited $LASTEXITCODE." }
        if (-not (Test-Path -LiteralPath $visualReportPath -PathType Leaf) -or -not (Test-Path -LiteralPath $visualMarkdownPath -PathType Leaf)) { throw 'Visual report artifacts were not generated.' }
        $visual = Get-Content -Raw -Encoding UTF8 -LiteralPath $visualReportPath | ConvertFrom-Json
        $result.visualWarningCount = [int]$visual.warningCount
        $result.visualBlocking = [bool]$visual.blocking
        $result.visualBlockingCodes = @($visual.gate.blockingWarnings | ForEach-Object { [string]$_.code } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
        $result.normalizedReportSha256 = Get-NormalizedReportHash $reportPath
        $result.normalizedVisualReportSha256 = Get-NormalizedVisualHash $visualReportPath
        if ($result.visualBlocking) { throw "Visual gate blocked ${CaseId}: $($result.visualBlockingCodes -join ', ')." }
        $result.status = $successStatus
    }
    catch { $result.status = 'Failed'; $result.error = $_.Exception.Message }
    Write-Json $resultPath $result
    return [pscustomobject]$result
}

$plan = [ordered]@{
    schemaVersion = 1; caseId = $CaseId; fixture = $fixturePath; fixtureSha256 = $expectedFixtureSha256; fixtureReady = $fixtureReady; requiredFinal = $requiredFinal; successStatus = $successStatus
    dllDirectory = $DllDirectory; outputRoot = $OutputRoot; repeatCount = $RepeatCount; coreConsolePath = $CoreConsolePath; profile = $Profile; timeoutSeconds = $TimeoutSeconds
    execution = "No build; serial ASDREPRO Hole run over DRAWING/Model outline plus Circle sources; validator=$($(if ($fixtureReady) { 'Ready' } else { 'AllowNotReady' })); capture visual evidence."
}
if ($PlanOnly) { $plan | ConvertTo-Json -Depth 8; return }

if (-not (Test-Path -LiteralPath $CoreConsolePath -PathType Leaf)) { throw "AutoCAD Core Console not found: $CoreConsolePath" }
if (@(Get-Process -Name accoreconsole -ErrorAction SilentlyContinue).Count) { throw 'Another AutoCAD Core Console process is active; stopping without force-kill.' }
if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) { throw "Fixture not found: $fixturePath" }
if ((Get-Sha256 $fixturePath) -ne $expectedFixtureSha256) { throw "Fixture hash mismatch for $CaseId." }

$dllEvidence = Get-DllEvidence $DllDirectory
$powerShellExecutable = Get-PowerShellExecutable
$batchStartedAt = [DateTime]::UtcNow.ToString('o')
$batchId = Get-Date -Format 'yyyyMMdd-HHmmssfff'
$batchRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) { Join-Path (Join-Path $projectRoot 'regression\hole-slot\runs') $batchId } else { [IO.Path]::GetFullPath($OutputRoot) }
if (Test-Path -LiteralPath $batchRoot) { throw "Refusing to overwrite existing batch root: $batchRoot" }
New-Item -ItemType Directory -Path $batchRoot | Out-Null
$dllEvidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $batchRoot 'dll-hashes.json') -Encoding UTF8
$records = @()
$errors = @()
for ($repeat = 1; $repeat -le $RepeatCount; $repeat++) {
    $record = Invoke-HoleSlotRun $repeat $batchRoot $dllEvidence $powerShellExecutable
    $records += $record
    if ($record.status -ne $successStatus) { $errors += "${CaseId}/repeat-$repeat failed: $($record.error)" }
    if (@(Get-Process -Name accoreconsole -ErrorAction SilentlyContinue).Count) { $errors += 'An AutoCAD Core Console process remains active; stopping without force-kill.'; break }
}
$reportHashes = @($records.normalizedReportSha256 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
$visualHashes = @($records.normalizedVisualReportSha256 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
$fullImageHashes = @($records.fullImageSha256 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
if ($records.Count -ne $RepeatCount -or $reportHashes.Count -ne 1 -or $visualHashes.Count -ne 1 -or $fullImageHashes.Count -ne 1) { $errors += "Determinism failed: runs=$($records.Count), reportHashes=$($reportHashes.Count), visualHashes=$($visualHashes.Count), fullImageHashes=$($fullImageHashes.Count)." }
$summary = [ordered]@{
    schemaVersion = 1; batchId = $batchId; caseId = $CaseId; startedAtUtc = $batchStartedAt; finishedAtUtc = [DateTime]::UtcNow.ToString('o')
    status = if ($errors.Count) { 'Failed' } else { $successStatus }; fixtureReady = $fixtureReady; repeatCount = $RepeatCount; dllDirectory = $dllEvidence.directory
    records = @($records); normalizedReportSha256 = if ($reportHashes.Count -eq 1) { $reportHashes[0] } else { $null }
    normalizedVisualReportSha256 = if ($visualHashes.Count -eq 1) { $visualHashes[0] } else { $null }
    fullImageSha256 = if ($fullImageHashes.Count -eq 1) { $fullImageHashes[0] } else { $null }; errors = @($errors)
}
Write-Json (Join-Path $batchRoot 'summary.json') $summary
if ($summary.status -eq 'Failed') { throw "$CaseId CAD bring-up failed. Evidence: $batchRoot" }
$summary | ConvertTo-Json -Depth 32
