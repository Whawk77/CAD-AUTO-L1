param(
    [string]$SourceDll = ".\bin\Debug\AutoFixtureDim.dll",
    [string]$DeployDir = "",
    [string]$BaseName = "autofixdim"
)

$ErrorActionPreference = "Stop"

$defaultToolboxDir = -join ([char[]](0x4E0D, 0x52A0, 0x73ED, 0x7684, 0x5C0F, 0x5218, 0x005F, 0x5DE5, 0x5177, 0x7BB1))
if ([string]::IsNullOrWhiteSpace($DeployDir)) {
    $DeployDir = Join-Path (Join-Path "D:\app" $defaultToolboxDir) "dll"
}

$resolvedSource = Resolve-Path -LiteralPath $SourceDll
if (-not (Test-Path -LiteralPath $DeployDir -PathType Container)) {
    throw "Deploy directory does not exist: $DeployDir"
}

$maxVersion = 0
Get-ChildItem -LiteralPath $DeployDir -Filter "$BaseName-v*.dll" -File | ForEach-Object {
    if ($_.Name -match ('^' + [regex]::Escape($BaseName) + '-v(\d+)\.dll$')) {
        $version = [int]$Matches[1]
        if ($version -gt $maxVersion) {
            $maxVersion = $version
        }
    }
}

$nextVersion = $maxVersion + 1
$destination = Join-Path $DeployDir ("{0}-v{1}.dll" -f $BaseName, $nextVersion)
Copy-Item -LiteralPath $resolvedSource.Path -Destination $destination -Force

Write-Output $destination
