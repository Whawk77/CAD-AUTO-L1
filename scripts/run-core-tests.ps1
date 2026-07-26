param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $projectRoot "CadAuto.Core.Tests\CadAuto.Core.Tests.csproj"
$testExecutable = Join-Path $projectRoot ("CadAuto.Core.Tests\bin\{0}\CadAuto.Core.Tests.exe" -f $Configuration)

Push-Location $projectRoot
try {
    & dotnet msbuild $testProject /t:Build "/p:Configuration=$Configuration" /v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "CadAuto.Core.Tests build failed with exit code $LASTEXITCODE."
    }

    if ([string]::IsNullOrWhiteSpace($Filter)) {
        & $testExecutable
    }
    else {
        & $testExecutable $Filter
    }
    if ($LASTEXITCODE -ne 0) {
        throw "CadAuto.Core.Tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
