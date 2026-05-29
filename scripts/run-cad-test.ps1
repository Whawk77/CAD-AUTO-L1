param(
    [switch]$Restart
)

$ErrorActionPreference = "Stop"

$ProjectDir = Join-Path $PSScriptRoot "..\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340"
$ProjectScript = Join-Path $ProjectDir "scripts\run-cad-test.ps1"

if (-not (Test-Path -LiteralPath $ProjectScript -PathType Leaf)) {
    throw "Project debug script not found: $ProjectScript"
}

$arguments = @(
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $ProjectScript
)

if ($Restart) {
    $arguments += "-Restart"
}

& powershell @arguments
exit $LASTEXITCODE
