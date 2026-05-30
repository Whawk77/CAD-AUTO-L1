$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $scriptRoot "CodexScreenshot\CodexScreenshot.csproj"

dotnet build $project -c Release

