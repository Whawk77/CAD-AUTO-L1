$ErrorActionPreference = 'Stop'

$repoDir = Split-Path -Parent $PSScriptRoot
$deployScript = Join-Path $repoDir 'deploy_next_version.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('autofixdim-deploy-check-' + [Guid]::NewGuid().ToString('N'))
$sourceDir = Join-Path $root 'source'
$deployDir = Join-Path $root 'deploy'
New-Item -ItemType Directory -Path $sourceDir, $deployDir | Out-Null

foreach ($name in @('AutoFixtureDim.dll', 'CadAuto.Core.dll', 'CadAuto.CadAdapter.dll')) {
    [IO.File]::WriteAllText((Join-Path $sourceDir $name), "fake-$name")
}
[IO.File]::WriteAllText((Join-Path $deployDir 'autofixdim-v4.dll'), 'legacy-flat')
New-Item -ItemType Directory -Path (Join-Path $deployDir 'autofixdim-v9') | Out-Null

function Invoke-Deployment {
    $output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $deployScript `
        -SourceDll (Join-Path $sourceDir 'AutoFixtureDim.dll') -DeployDir $deployDir
    if ($LASTEXITCODE -ne 0) {
        throw "Deployment script failed with exit code $LASTEXITCODE."
    }
    return ($output | Select-Object -Last 1).ToString()
}

function Assert-DeploymentSet([string]$mainPath, [string]$expectedVersion) {
    $directory = Split-Path -Parent $mainPath
    if ((Split-Path -Leaf $directory) -ne "autofixdim-v$expectedVersion") {
        throw "Expected version directory autofixdim-v$expectedVersion, got $directory."
    }
    foreach ($name in @('AutoFixtureDim.dll', 'CadAuto.Core.dll', 'CadAuto.CadAdapter.dll')) {
        $source = Join-Path $sourceDir $name
        $destination = Join-Path $directory $name
        if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
            throw "Missing deployed file: $destination"
        }
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) {
            throw "SHA256 mismatch: $destination"
        }
    }
}

$first = Invoke-Deployment
Assert-DeploymentSet $first 10
$firstDirectory = Split-Path -Parent $first
$firstHashes = @{}
foreach ($name in @('AutoFixtureDim.dll', 'CadAuto.Core.dll', 'CadAuto.CadAdapter.dll')) {
    $firstHashes[$name] = (Get-FileHash -LiteralPath (Join-Path $firstDirectory $name) -Algorithm SHA256).Hash
}
foreach ($name in @('AutoFixtureDim.dll', 'CadAuto.Core.dll', 'CadAuto.CadAdapter.dll')) {
    [IO.File]::WriteAllText((Join-Path $sourceDir $name), "changed-$name")
}
$second = Invoke-Deployment
Assert-DeploymentSet $second 11
foreach ($name in $firstHashes.Keys) {
    $currentHash = (Get-FileHash -LiteralPath (Join-Path $firstDirectory $name) -Algorithm SHA256).Hash
    if ($currentHash -ne $firstHashes[$name]) {
        throw "Second deployment overwrote the first deployment: $name"
    }
}

Write-Output "deploy_next_version self-check passed; temporary output: $root"
