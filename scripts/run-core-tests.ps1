param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$Filter = "",
    [ValidateRange(1, 180)]
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Invoke-CoreTestStage {
    param(
        [string]$Stage,
        [string]$FilePath,
        [string[]]$Arguments,
        [int]$TimeoutSeconds = 180
    )

    # Start-Process joins ArgumentList; quote each Windows argument before joining.
    $quotedArguments = @($Arguments | ForEach-Object {
        '"' + [regex]::Replace([regex]::Replace($_, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
    })
    $logBase = Join-Path ([IO.Path]::GetTempPath()) ("cad-core-{0}-{1}" -f $Stage, [guid]::NewGuid().ToString('N'))
    $stdout = "$logBase.stdout.log"
    $stderr = "$logBase.stderr.log"
    Write-Host "$Stage logs: $stdout ; $stderr"
    $process = $null
    try {
        $process = Start-Process -FilePath $FilePath -ArgumentList $quotedArguments -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            # Target only this owned PID and its descendants, never a process name.
            & "$env:SystemRoot\System32\taskkill.exe" /PID $process.Id /T /F | Out-Host
            $killExitCode = $LASTEXITCODE
            if (-not $process.WaitForExit(5000)) {
                throw "$Stage TIMEOUT after ${TimeoutSeconds}s; process cleanup failed (taskkill exit=$killExitCode). PID=$($process.Id)"
            }
            throw "$Stage TIMEOUT after ${TimeoutSeconds}s (exit=$($process.ExitCode), taskkill exit=$killExitCode)."
        }
        if ($process.ExitCode -ne 0) {
            throw "$Stage FAILED with exit code $($process.ExitCode)."
        }
        Write-Host "$Stage PASSED (exit=0)."
    }
    finally {
        if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout | Out-Host }
        if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr | Out-Host }
        if ($null -ne $process) { $process.Dispose() }
    }
}

# Dot-sourcing exposes only the stage helper for its isolated process self-check.
if ($MyInvocation.InvocationName -eq '.') { return }

$projectRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $projectRoot "CadAuto.Core.Tests\CadAuto.Core.Tests.csproj"
$testExecutable = Join-Path $projectRoot ("CadAuto.Core.Tests\bin\{0}\CadAuto.Core.Tests.exe" -f $Configuration)

Push-Location $projectRoot
try {
    Invoke-CoreTestStage -Stage Build -FilePath (Get-Command dotnet -CommandType Application).Source -Arguments @('msbuild', $testProject, '/t:Build', "/p:Configuration=$Configuration", '/v:minimal') -TimeoutSeconds $TimeoutSeconds
    # An empty quoted argument is harmless and keeps Start-Process ArgumentList nonempty.
    Invoke-CoreTestStage -Stage Tests -FilePath $testExecutable -Arguments @($Filter) -TimeoutSeconds $TimeoutSeconds
}
finally {
    Pop-Location
}
