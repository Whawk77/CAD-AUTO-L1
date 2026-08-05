[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$writer = Join-Path $PSScriptRoot 'write-cad-visual-report.ps1'
$legacyPowershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$temp = Join-Path $env:TEMP ('cad-visual-report-' + [guid]::NewGuid().ToString('N'))
$paths = @{}
foreach ($name in 'zero', 'bad', 'deep', 'contact', 'left', 'collision', 'modelText') {
    $paths[$name] = [pscustomobject]@{ Snapshot = "$temp-$name.tsv"; Report = "$temp-$name.json"; Markdown = "$temp-$name.md" }
}

function Invoke-VisualReport([string]$Name) {
    $path = $paths[$Name]
    & $legacyPowershell -NoProfile -ExecutionPolicy Bypass -File $writer `
        -SnapshotPath $path.Snapshot -ReportPath $path.Report -MarkdownPath $path.Markdown -CaseId "TEST-$($Name.ToUpperInvariant())" -ExpectedDirection All
    if ($LASTEXITCODE -ne 0) { throw "Windows PowerShell 5.1 report invocation failed: $Name" }
    return Get-Content -LiteralPath $path.Report -Raw | ConvertFrom-Json
}

function Get-Codes($Report) { return @($Report.warnings | ForEach-Object code) }

try {
    @(
        "META`tcaseId`tTEST-ZERO", "META`texpectedDirection`tAll", "ENV`tACADVER`t24", "ENV`tDIMTXT`t1", "ENV`tDIMASZ`t1", "SOURCE`tS1`tLINE`t0`t0`t100`t100",
        "SEG`tS1`t1`t0`t0`t100`t0", "SEG`tS1`t2`t100`t0`t100`t100", "SEG`tS1`t3`t100`t100`t0`t100", "SEG`tS1`t4`t0`t100`t0`t0",
        "DIM`tD1`t100`t0`t100`t100`t100`t0`t120`t50`t126`tB1", "TEXT`tD1`tT1`t45`t124`t55`t130", "ARROW`tD1`tA1`tSOLID`t0`t118`t2`t122"
    ) | Set-Content -LiteralPath $paths.zero.Snapshot -Encoding utf8
    $zero = Invoke-VisualReport zero
    if ($zero.warningCount -ne 0 -or $zero.blocking) { throw 'A clean snapshot did not remain zero-warning and non-blocking.' }

    @(
        "META`tcaseId`tTEST-BAD", "META`texpectedDirection`tAll", "ENV`tACADVER`t24", "ENV`tDIMTXT`t1", "ENV`tDIMASZ`t1", "SOURCE`tS1`tLINE`t0`t0`t100`t100",
        "SEG`tS1`t1`t50`t0`t50`t100", "DIM`tD1`t100`t0`t100`t50`t100`t0`t50`t50`t52`tB1", "TEXT`tD1`tT1`t48`t45`t55`t55", "ARROW`tD1`tA1`tSOLID`t0`t48`t2`t52"
    ) | Set-Content -LiteralPath $paths.bad.Snapshot -Encoding utf8
    $bad = Invoke-VisualReport bad; $badCodes = Get-Codes $bad
    if ('DIM_SOURCE_CROSSING' -notin $badCodes -or 'TEXT_SOURCE_COLLISION' -notin $badCodes -or $bad.blocking) { throw 'B did not produce required non-blocking source-collision warnings.' }

    @(
        "META`tcaseId`tTEST-DEEP", "META`texpectedDirection`tAll", "ENV`tACADVER`t24", "ENV`tDIMTXT`t1", "ENV`tDIMASZ`t1", "SOURCE`tS1`tLINE`t0`t0`t100`t100",
        "SEG`tS1`t1`t0`t0`t100`t0", "SEG`tS1`t2`t100`t0`t100`t100", "SEG`tS1`t3`t100`t100`t0`t100", "SEG`tS1`t4`t0`t100`t0`t0",
        "DIM`tD1`t100`t0`t100`t100`t100`t0`t120`t50`t126`tB1", "DIM`tD2`t60`t20`t100`t80`t100`t20`t140`t50`t146`tB2",
        "TEXT`tD2`tT2`t-1`t108`t2`t112", "ARROW`tD1`tA1`tSOLID`t0`t108`t5`t112"
    ) | Set-Content -LiteralPath $paths.deep.Snapshot -Encoding utf8
    $deep = Invoke-VisualReport deep; $deepCodes = Get-Codes $deep
    if ('TEXT_ARROW_COLLISION' -notin $deepCodes -or 'EXTENSION_TEXT_COLLISION' -notin $deepCodes -or $deep.blocking) { throw 'C did not produce required non-blocking deep-collision warnings.' }

    @(
        "META`tcaseId`tTEST-CONTACT", "META`texpectedDirection`tAll", "ENV`tACADVER`t24", "ENV`tDIMTXT`t2", "ENV`tDIMASZ`t2", "SOURCE`tS1`tLINE`t0`t0`t100`t100",
        "SEG`tS1`t1`t0`t0`t100`t0", "SEG`tS1`t2`t100`t0`t100`t100", "SEG`tS1`t3`t100`t100`t0`t100", "SEG`tS1`t4`t0`t100`t0`t0",
        "DIM`tD1`t100`t0`t100`t100`t100`t0`t120`t50`t126`tB1", "DIM`tD2`t60`t20`t100`t80`t100`t20`t140`t50`t146`tB2", "DIM`tD3`t10`t80`t100`t90`t100`t80`t140`t85`t146`tB3", "DIM`tD4`t10`t60`t100`t70`t100`t60`t140`t65`t146`tB4",
        "TEXT`tD1`tT1`t10`t120`t15`t125", "ARROW`tD1`tA1`tSOLID`t10`t120`t15`t125", "TEXT`tD2`tT2`t20`t120`t25`t125", "ARROW`tD4`tA4`tSOLID`t24.875`t120`t30`t125", "TEXT`tD3`tT3`t-1`t120`t1`t125"
    ) | Set-Content -LiteralPath $paths.contact.Snapshot -Encoding utf8
    $contact = Invoke-VisualReport contact; $contactCodes = Get-Codes $contact
    if ($contact.blocking -or $contact.warningCount -ne 0 -or 'TEXT_ARROW_COLLISION' -in $contactCodes -or 'EXTENSION_TEXT_COLLISION' -in $contactCodes) { throw 'D legal contacts produced a visual warning.' }

    @(
        "META`tcaseId`tTEST-LEFT", "META`texpectedDirection`tAll", "ENV`tACADVER`t24", "ENV`tDIMTXT`t3.5", "ENV`tDIMASZ`t2.5", "SOURCE`tS1`tLINE`t10`t10`t100`t100", "SOURCE`tS2`tLINE`t10`t10`t100`t100",
        "SEG`tS1`t1`t10`t10`t100`t10", "SEG`tS1`t2`t100`t10`t100`t100", "SEG`tS1`t3`t100`t100`t10`t100", "SEG`tS1`t4`t10`t100`t10`t10",
        "DIM`tD1`t100`t10`t30`t10`t90`t5`t30`t5`t120`tB1", "TEXT`tD1`tT1`t0`t120`t10`t122", "ARROW`tD1`tA1`tSOLID`t0`t45`t5`t50"
    ) | Set-Content -LiteralPath $paths.left.Snapshot -Encoding utf8
    $left = Invoke-VisualReport left; $leftCodes = Get-Codes $left
    if ('LEFT_LAYOUT_TEXT_PLACEMENT' -notin $leftCodes -or 'TEXT_TEXT_COLLISION' -in $leftCodes -or -not $left.blocking -or $left.status -ne 'Failed' -or $left.gate.mode -ne 'Blocking' -or $left.gate.failurePolicy -ne 'FailOnBlockingWarnings' -or $left.gate.exemptWarningCodes.Count -ne 0 -or $left.gate.rollback.mode -ne 'WarningOnly' -or $left.gate.rollback.blockingWarningCodes.Count -ne 0 -or @($left.gate.blockingWarnings | ForEach-Object code) -notcontains 'LEFT_LAYOUT_TEXT_PLACEMENT') { throw 'E did not fail the configured left-layout blocking warning.' }

    @(
        "META`tcaseId`tTEST-COLLISION", "META`texpectedDirection`tAll", "ENV`tACADVER`t24", "ENV`tDIMTXT`t1", "ENV`tDIMASZ`t1", "SOURCE`tS1`tLINE`t0`t0`t100`t100",
        "SEG`tS1`t1`t0`t0`t100`t0", "SEG`tS1`t2`t100`t0`t100`t100", "SEG`tS1`t3`t100`t100`t0`t100", "SEG`tS1`t4`t0`t100`t0`t0",
        "DIM`tD1`t0`t100`t100`t100`t0`t120`t50`t120`t56`tB1", "DIM`tD2`t0`t90`t100`t90`t0`t140`t50`t140`t56`tB2", "TEXT`tD1`tT1`t45`t124`t55`t130", "TEXT`tD2`tT2`t50`t124`t60`t130", "ARROW`tD1`tA1`tSOLID`t0`t118`t2`t122", "ARROW`tD2`tA2`tSOLID`t0`t138`t2`t142"
    ) | Set-Content -LiteralPath $paths.collision.Snapshot -Encoding utf8
    $collision = Invoke-VisualReport collision; $collisionCodes = Get-Codes $collision
    if ('TEXT_TEXT_COLLISION' -notin $collisionCodes -or -not $collision.blocking -or @($collision.gate.blockingWarnings | ForEach-Object code) -notcontains 'TEXT_TEXT_COLLISION') { throw 'F did not fail the configured text-collision blocking warning.' }

    @(
        "META`tcaseId`tTEST-MODEL-TEXT", "META`texpectedDirection`tAll", "ENV`tACADVER`t24", "ENV`tDIMTXT`t1", "ENV`tDIMASZ`t1", "SOURCE`tS1`tLINE`t0`t0`t100`t100",
        "SEG`tS1`t1`t0`t0`t100`t0", "SEG`tS1`t2`t100`t0`t100`t100", "SEG`tS1`t3`t100`t100`t0`t100", "SEG`tS1`t4`t0`t100`t0`t0",
        "DIM`tD1`t0`t100`t100`t100`t100`t0`t120`t20`t120`t26`tB1", "TEXT`tD1`tT1`t20`t124`t25`t130", "ARROW`tD1`tA1`tSOLID`t0`t118`t2`t122",
        "TEXT`tMODELSPACE`tM1`t45`t124`t55`t130", "TEXT`tMODELSPACE`tM2`t50`t124`t60`t130"
    ) | Set-Content -LiteralPath $paths.modelText.Snapshot -Encoding utf8
    $modelText = Invoke-VisualReport modelText; $modelTextCodes = Get-Codes $modelText
    $modelTextWarnings = @($modelText.warnings | Where-Object code -eq 'TEXT_TEXT_COLLISION')
    $modelTextHandles = @($modelTextWarnings | ForEach-Object { @($_.handles) })
    if ('TEXT_TEXT_COLLISION' -notin $modelTextCodes -or -not $modelText.blocking -or 'MODELSPACE' -notin $modelTextHandles -or @($modelText.gate.blockingWarnings | ForEach-Object code) -notcontains 'TEXT_TEXT_COLLISION') { throw 'G did not fail the configured collision warning for standalone model-space text.' }

    Write-Output 'cad visual report self-check passed'
}
finally {
    foreach ($path in @($paths.Values | ForEach-Object { @($_.Snapshot, $_.Report, $_.Markdown) })) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
}
