[CmdletBinding()]
param(
    [string]$MatrixPath
)

$ErrorActionPreference = "Stop"

function Get-InputSha256 {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [ValidateSet("raw", "text-lf")][string]$Mode = "raw"
    )

    $bytes = if ($Mode -eq "text-lf") {
        $text = [System.IO.File]::ReadAllText($Path).Replace("`r`n", "`n").Replace("`r", "`n")
        [System.Text.UTF8Encoding]::new($false).GetBytes($text)
    }
    else {
        [System.IO.File]::ReadAllBytes($Path)
    }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace("-", "")
    }
    finally {
        $sha.Dispose()
    }
}

try {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($MatrixPath)) {
        $MatrixPath = Join-Path $repositoryRoot "regression\remediation\case-truth-matrix.json"
    }
    elseif (-not [System.IO.Path]::IsPathRooted($MatrixPath)) {
        $MatrixPath = Join-Path $repositoryRoot $MatrixPath
    }

    if (-not (Test-Path -LiteralPath $MatrixPath -PathType Leaf)) {
        throw "Truth matrix not found: $MatrixPath"
    }

    $matrix = Get-Content -LiteralPath $MatrixPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($matrix.schemaVersion -isnot [int] -or $matrix.schemaVersion -ne 1) {
        throw "Truth matrix schemaVersion must be 1."
    }

    $cases = @($matrix.cases)
    if ($cases.Count -eq 0) {
        throw "Truth matrix must contain at least one case."
    }

    $expectedIds = @(
        "CORE-P0",
        "CORE-P1",
        "CORE-P2",
        "CORE-P3",
        "OC01-bottom-outer-step-partition",
        "DL01-bottom-block-span-order",
        "DL01-top-block-span-order",
        "DL01-left-block-span-order",
        "DL01-right-block-span-order",
        "HS01-normal-hole",
        "HS02-concentric-holes",
        "HS03-loose-hole-chain",
        "HS04-unique-pin-datum",
        "HS05-multiple-pin-datum",
        "HS06-functional-hole",
        "HS07-vertical-waist-slot",
        "HS08-transformed-waist-slot"
    )
    $allowedStatuses = @("ConfirmedCorrect", "KnownWrong", "Unresolved")

    $ids = @($cases | ForEach-Object { [string]$_.id })
    $emptyIds = @($ids | Where-Object { [string]::IsNullOrWhiteSpace($_) })
    if ($emptyIds.Count -gt 0) {
        throw "Every case must have a non-empty id."
    }

    $duplicates = @(
        $ids |
            ForEach-Object { $_.ToUpperInvariant() } |
            Group-Object |
            Where-Object { $_.Count -gt 1 } |
            ForEach-Object { $_.Name }
    )
    if ($duplicates.Count -gt 0) {
        throw "Duplicate case id(s): $($duplicates -join ', ')"
    }

    $missingIds = @($expectedIds | Where-Object { $ids -notcontains $_ })
    $unexpectedIds = @($ids | Where-Object { $expectedIds -notcontains $_ })
    if ($missingIds.Count -gt 0) {
        throw "Missing fixed case(s): $($missingIds -join ', ')"
    }
    if ($unexpectedIds.Count -gt 0) {
        throw "Unexpected case(s): $($unexpectedIds -join ', ')"
    }

    foreach ($case in $cases) {
        $status = [string]$case.status
        if ($allowedStatuses -notcontains $status) {
            throw "Case '$($case.id)' has illegal status '$status'."
        }

        if ($status -eq "ConfirmedCorrect") {
            $evidence = @($case.evidence)
            if ($evidence.Count -eq 0) {
                throw "ConfirmedCorrect case '$($case.id)' must have evidence."
            }
            foreach ($item in $evidence) {
                if ([string]::IsNullOrWhiteSpace([string]$item.source) -or
                    [string]::IsNullOrWhiteSpace([string]$item.claim)) {
                    throw "ConfirmedCorrect case '$($case.id)' has incomplete evidence."
                }
            }

            $inputSource = [string]$case.input.source
            $expectedHash = [string]$case.input.sha256
            $hashMode = if ($case.input.PSObject.Properties.Name -contains "hashMode") {
                [string]$case.input.hashMode
            }
            else {
                "raw"
            }
            if ([string]::IsNullOrWhiteSpace($inputSource)) {
                throw "ConfirmedCorrect case '$($case.id)' must have input.source."
            }
            if ($expectedHash -notmatch '^[0-9A-Fa-f]{64}$') {
                throw "ConfirmedCorrect case '$($case.id)' must have a 64-character hexadecimal input.sha256."
            }
            if (@("raw", "text-lf") -notcontains $hashMode) {
                throw "ConfirmedCorrect case '$($case.id)' has unsupported input.hashMode '$hashMode'."
            }

            $repositoryFullPath = [System.IO.Path]::GetFullPath($repositoryRoot)
            $sourceFullPath = if ([System.IO.Path]::IsPathRooted($inputSource)) {
                [System.IO.Path]::GetFullPath($inputSource)
            }
            else {
                [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $inputSource))
            }
            $repositoryPrefix = $repositoryFullPath.TrimEnd([char[]]"\/") + [System.IO.Path]::DirectorySeparatorChar
            if (-not $sourceFullPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "ConfirmedCorrect case '$($case.id)' input.source must be inside the repository."
            }
            if (-not (Test-Path -LiteralPath $sourceFullPath -PathType Leaf)) {
                throw "ConfirmedCorrect case '$($case.id)' input.source does not exist: $inputSource"
            }

            $actualHash = Get-InputSha256 -Path $sourceFullPath -Mode $hashMode
            if (-not [string]::Equals($actualHash, $expectedHash, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "ConfirmedCorrect case '$($case.id)' input.sha256 mismatch: expected=$expectedHash actual=$actualHash"
            }
        }
    }

    Write-Output "PASS remediation truth matrix: $($cases.Count) fixed cases"
}
catch {
    Write-Error $_
    exit 1
}
