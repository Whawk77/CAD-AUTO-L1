param(
    [switch]$Restart
)

$ErrorActionPreference = "Stop"

# ===== Editable test settings =====
$AcadExe = "D:\Program Files\Autodesk\AutoCAD 2020\acad.exe"
$DrawingPath = "C:\Users\Administrator\Desktop\test.dwg"
$ProjectPath = ""
$DllPath = ""
$DllCopyBaseName = "autofixdim"
$DllCopyVersionPrefix = "LB"
$DllCopyStartVersion = 61
$TestCommand = "ASD"
$Configuration = "Debug"
$BuildVerbosity = "minimal"
$DisableProjectPostBuild = $true
$BuildWithoutDebugSymbols = $true
# ==================================

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Resolve-MainProject {
    param(
        [string]$Root,
        [string]$ConfiguredProjectPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredProjectPath)) {
        $candidatePath = $ConfiguredProjectPath
        if (-not [IO.Path]::IsPathRooted($candidatePath)) {
            $candidatePath = Join-Path $Root $candidatePath
        }

        if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
            throw "Configured project file does not exist: $candidatePath"
        }

        return (Resolve-Path -LiteralPath $candidatePath).Path
    }

    $projects = @(Get-ChildItem -Path $Root -Filter "*.csproj" -File -Recurse)
    if ($projects.Count -eq 0) {
        throw "No .csproj file found under $Root"
    }

    if ($projects.Count -eq 1) {
        return $projects[0].FullName
    }

    $pluginProjects = @()
    foreach ($project in $projects) {
        $content = Get-Content -LiteralPath $project.FullName -Raw
        if ($content -match "AcMgd|AcDbMgd|AcCoreMgd|Autodesk\.AutoCAD") {
            $pluginProjects += $project
        }
    }

    if ($pluginProjects.Count -eq 1) {
        return $pluginProjects[0].FullName
    }

    $names = ($projects | ForEach-Object { " - " + $_.FullName }) -join [Environment]::NewLine
    throw "Could not uniquely identify the main AutoCAD plugin project. Set `$ProjectPath at the top of this script.`nFound projects:`n$names"
}

function Get-MsBuildProperty {
    param(
        [xml]$ProjectXml,
        [string]$PropertyName,
        [string]$Configuration
    )

    $nodes = @($ProjectXml.Project.PropertyGroup)
    $matchingGroups = @()

    foreach ($node in $nodes) {
        $condition = [string]$node.Condition
        if ([string]::IsNullOrWhiteSpace($condition) -or $condition -match [regex]::Escape($Configuration)) {
            $matchingGroups += $node
        }
    }

    for ($i = $matchingGroups.Count - 1; $i -ge 0; $i--) {
        $value = $matchingGroups[$i].$PropertyName
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return [string]$value
        }
    }

    return $null
}

function Resolve-PluginDll {
    param(
        [string]$ResolvedProjectPath,
        [string]$ConfiguredDllPath,
        [string]$Configuration
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredDllPath)) {
        if ([IO.Path]::IsPathRooted($ConfiguredDllPath)) {
            return $ConfiguredDllPath
        }

        return (Join-Path (Split-Path -Parent $ResolvedProjectPath) $ConfiguredDllPath)
    }

    [xml]$projectXml = Get-Content -LiteralPath $ResolvedProjectPath -Raw
    $projectDir = Split-Path -Parent $ResolvedProjectPath
    $assemblyName = Get-MsBuildProperty -ProjectXml $projectXml -PropertyName "AssemblyName" -Configuration $Configuration

    if ([string]::IsNullOrWhiteSpace($assemblyName)) {
        $assemblyName = [IO.Path]::GetFileNameWithoutExtension($ResolvedProjectPath)
    }

    $outputPath = Get-MsBuildProperty -ProjectXml $projectXml -PropertyName "OutputPath" -Configuration $Configuration
    if ([string]::IsNullOrWhiteSpace($outputPath)) {
        $outputPath = Join-Path "bin" $Configuration
    }

    return (Join-Path (Join-Path $projectDir $outputPath) ($assemblyName + ".dll"))
}

function Convert-ToAutoCadScriptString {
    param([string]$Value)

    return $Value.Replace("\", "/").Replace('"', '\"')
}

function Get-VersionedDllPath {
    param(
        [string]$SourceDllPath,
        [string]$BaseName,
        [string]$VersionPrefix,
        [int]$StartVersion
    )

    $directory = Split-Path -Parent $SourceDllPath
    $extension = [IO.Path]::GetExtension($SourceDllPath)
    $escapedBaseName = [regex]::Escape($BaseName)
    $escapedPrefix = [regex]::Escape($VersionPrefix)
    $pattern = "^$escapedBaseName-$escapedPrefix(\d+)$([regex]::Escape($extension))$"
    $nextVersion = $StartVersion

    $existing = @(Get-ChildItem -Path $directory -Filter ($BaseName + "-" + $VersionPrefix + "*" + $extension) -File -ErrorAction SilentlyContinue)
    foreach ($file in $existing) {
        $match = [regex]::Match($file.Name, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if (-not $match.Success) {
            continue
        }

        $version = [int]$match.Groups[1].Value
        if ($version -ge $nextVersion) {
            $nextVersion = $version + 1
        }
    }

    return (Join-Path $directory ($BaseName + "-" + $VersionPrefix + $nextVersion.ToString() + $extension))
}

$repoRoot = Get-RepoRoot
$resolvedProjectPath = Resolve-MainProject -Root $repoRoot -ConfiguredProjectPath $ProjectPath
$scriptPath = Join-Path $PSScriptRoot "cad-test.scr"
$resolvedDllPath = Resolve-PluginDll -ResolvedProjectPath $resolvedProjectPath -ConfiguredDllPath $DllPath -Configuration $Configuration

if (-not (Test-Path -LiteralPath $AcadExe -PathType Leaf)) {
    throw "acad.exe not found: $AcadExe"
}

if (-not [string]::IsNullOrWhiteSpace($DrawingPath) -and -not (Test-Path -LiteralPath $DrawingPath -PathType Leaf)) {
    throw "Drawing file not found: $DrawingPath"
}

Write-Host "Project: $resolvedProjectPath"
Write-Host "DLL: $resolvedDllPath"
Write-Host "Drawing: $DrawingPath"
Write-Host "Command: $TestCommand"

$buildArgs = @(
    "msbuild",
    $resolvedProjectPath,
    "/p:Configuration=$Configuration",
    "/v:$BuildVerbosity"
)

if ($DisableProjectPostBuild) {
    $buildArgs += "/p:PostBuildEvent="
}

if ($BuildWithoutDebugSymbols) {
    $buildArgs += "/p:DebugType=None"
    $buildArgs += "/p:DebugSymbols=false"
}

Write-Host "Building..."
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $resolvedDllPath -PathType Leaf)) {
    throw "Build completed, but DLL was not found: $resolvedDllPath"
}

$loadDllPath = $resolvedDllPath
if ([string]::IsNullOrWhiteSpace($DllPath)) {
    $loadDllPath = Get-VersionedDllPath -SourceDllPath $resolvedDllPath -BaseName $DllCopyBaseName -VersionPrefix $DllCopyVersionPrefix -StartVersion $DllCopyStartVersion
    Copy-Item -LiteralPath $resolvedDllPath -Destination $loadDllPath -Force
    Write-Host "Copied DLL for NETLOAD: $loadDllPath"
}

$scrDllPath = Convert-ToAutoCadScriptString -Value (Resolve-Path -LiteralPath $loadDllPath).Path
$scriptLines = @(
    "FILEDIA",
    "0",
    "CMDDIA",
    "0",
    "NETLOAD",
    "`"$scrDllPath`"",
    $TestCommand
)

Set-Content -LiteralPath $scriptPath -Value $scriptLines -Encoding ASCII
Write-Host "Generated AutoCAD script: $scriptPath"

if ($Restart) {
    Write-Host "Restart requested. Closing existing AutoCAD processes..."
    $acadProcesses = @(Get-Process -Name "acad" -ErrorAction SilentlyContinue)
    foreach ($process in $acadProcesses) {
        if ($process.MainWindowHandle -ne 0) {
            [void]$process.CloseMainWindow()
        }
    }

    if ($acadProcesses.Count -gt 0) {
        Start-Sleep -Seconds 5
        $stillRunning = @(Get-Process -Name "acad" -ErrorAction SilentlyContinue)
        if ($stillRunning.Count -gt 0) {
            Write-Warning "Some AutoCAD processes are still running, possibly because a save prompt is open. This script will not force-kill them."
        }
    }
}

Write-Host "Starting AutoCAD..."
$acadArgs = @()
if (-not [string]::IsNullOrWhiteSpace($DrawingPath)) {
    $acadArgs += "`"$DrawingPath`""
}

$acadArgs += "/b"
$acadArgs += "`"$scriptPath`""

Start-Process -FilePath $AcadExe -ArgumentList $acadArgs
