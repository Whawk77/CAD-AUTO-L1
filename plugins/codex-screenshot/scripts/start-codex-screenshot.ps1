$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptRoot "CodexScreenshot"
$project = Join-Path $projectDir "CodexScreenshot.csproj"
$exe = Join-Path $projectDir "bin\Release\net8.0-windows\CodexScreenshot.exe"

if (-not (Test-Path -LiteralPath $exe)) {
    dotnet build $project -c Release
}

if (Get-Process -Name "CodexScreenshot" -ErrorAction SilentlyContinue) {
    Write-Host "CodexScreenshot is already running."
    exit 0
}

Start-Process -FilePath $exe -WindowStyle Hidden
