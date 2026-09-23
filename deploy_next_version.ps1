param(
    [string]$SourceDll = ".\bin\Debug\AutoFixtureDim.dll",
    [string]$DeployDir = "",
    [string]$BaseName = "autofixdim"
)

$ErrorActionPreference = "Stop"

$defaultToolboxDir = -join ([char[]](0x4E0D, 0x52A0, 0x73ED, 0x7684, 0x5C0F, 0x5218, 0x005F, 0x5DE5, 0x5177, 0x7BB1))
if ([string]::IsNullOrWhiteSpace($DeployDir)) {
    if (-not [string]::IsNullOrWhiteSpace($env:AUTOFIXDIM_DEPLOY_DIR)) {
        $DeployDir = $env:AUTOFIXDIM_DEPLOY_DIR
    } else {
        $DeployDir = Join-Path (Join-Path "D:\app" $defaultToolboxDir) "dll"
    }
}

$resolvedSource = Resolve-Path -LiteralPath $SourceDll
if (-not (Test-Path -LiteralPath $resolvedSource.Path -PathType Leaf)) {
    throw "SourceDll is not a file: $SourceDll"
}
if (-not (Test-Path -LiteralPath $DeployDir -PathType Container)) {
    throw "Deploy directory does not exist: $DeployDir"
}

# NETLOAD probes dependencies next to the loaded DLL; a build folder missing
# CadAuto.Core.dll / CadAuto.CadAdapter.dll deploys a shell that silently
# binds to whatever stale core DLLs already sit in the deploy directory.
$requiredDlls = @('AutoFixtureDim.dll', 'CadAuto.Core.dll', 'CadAuto.CadAdapter.dll')
$sourceDir = Split-Path -Parent $resolvedSource.Path
foreach ($requiredDll in $requiredDlls) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDir $requiredDll) -PathType Leaf)) {
        throw "Missing $requiredDll in $sourceDir - refusing to deploy an incomplete build."
    }
}

$maxVersion = 0
foreach ($entry in Get-ChildItem -LiteralPath $DeployDir -Force) {
    if (($entry.PSIsContainer -or $entry.Extension -eq '.dll') -and
        $entry.Name -match ('^' + [regex]::Escape($BaseName) + '-v(\d+)(?:\.dll)?$')) {
        $maxVersion = [Math]::Max($maxVersion, [int]$Matches[1])
    }
}
$nextVersion = $maxVersion + 1
$versionDirectory = Join-Path $DeployDir ("{0}-v{1}" -f $BaseName, $nextVersion)
if (Test-Path -LiteralPath $versionDirectory) {
    throw "Refusing to overwrite existing deployment directory: $versionDirectory"
}
New-Item -ItemType Directory -Path $versionDirectory | Out-Null

$sourceFiles = @{
    'AutoFixtureDim.dll' = $resolvedSource.Path
    'CadAuto.Core.dll' = Join-Path $sourceDir 'CadAuto.Core.dll'
    'CadAuto.CadAdapter.dll' = Join-Path $sourceDir 'CadAuto.CadAdapter.dll'
}
foreach ($fileName in $sourceFiles.Keys) {
    $sourcePath = $sourceFiles[$fileName]
    $destinationPath = Join-Path $versionDirectory $fileName
    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
    $sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    $destinationHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
    if ($sourceHash -ne $destinationHash) {
        throw "SHA256 mismatch after copying $fileName."
    }
}

Write-Output (Join-Path $versionDirectory 'AutoFixtureDim.dll')
