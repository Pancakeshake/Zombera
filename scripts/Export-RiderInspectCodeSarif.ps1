<#
.SYNOPSIS
    Run JetBrains InspectCode (Rider-equivalent inspections) and write a SARIF report.

.DESCRIPTION
    InspectCode is the same analysis engine Rider uses for solution-wide inspections.
    Default output format is SARIF (see JetBrains docs: InspectCode).

    Prerequisites:
      1) A solution file (.sln). This Unity repo gitignores *.sln — generate it from Unity
         (File > Open Project triggers generation) or pass -SolutionPath explicitly.
      2) ReSharper command line tools (provides the `jb` shim):
           dotnet tool install -g JetBrains.ReSharper.GlobalTools
         Open a new terminal so `jb` is on PATH, or pass -JbPath.

    Examples:
      .\scripts\Export-RiderInspectCodeSarif.ps1
      .\scripts\Export-RiderInspectCodeSarif.ps1 -SolutionPath "C:\work\Zombera\Zombera.sln" -OutputPath ".\rider-inspections.sarif"
      .\scripts\Export-RiderInspectCodeSarif.ps1 -NoBuild
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string] $SolutionPath = "",

    [Parameter(Mandatory = $false)]
    [string] $OutputPath = "",

    [Parameter(Mandatory = $false)]
    [string] $JbPath = "",

    [Parameter(Mandatory = $false)]
    [string] $DotSettingsPath = "",

    [Parameter(Mandatory = $false)]
    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    param([string] $ScriptDir)
    return (Resolve-Path (Join-Path $ScriptDir "..")).Path
}

function Find-SolutionFile {
    param([string] $RepoRoot)

    $fromEnv = [Environment]::GetEnvironmentVariable("INSPECTCODE_SOLUTION")
    if (-not [string]::IsNullOrWhiteSpace($fromEnv)) {
        $p = $fromEnv.Trim()
        if (Test-Path -LiteralPath $p) { return (Resolve-Path -LiteralPath $p).Path }
        throw "INSPECTCODE_SOLUTION is set but path does not exist: $p"
    }

    $candidates = @(Get-ChildItem -Path $RepoRoot -Filter "*.sln" -File -ErrorAction SilentlyContinue)
    if ($candidates.Count -eq 0) {
        throw @"
No .sln found under: $RepoRoot

Unity projects often gitignore *.sln. Generate it (open the project in Unity), or set:
  `$env:INSPECTCODE_SOLUTION = 'C:\path\YourGame.sln'
or pass -SolutionPath.
"@
    }
    if ($candidates.Count -gt 1) {
        $names = ($candidates | ForEach-Object { $_.Name }) -join ", "
        throw "Multiple .sln files in repo root ($names). Pass -SolutionPath to pick one."
    }

    return $candidates[0].FullName
}

function Resolve-JbExecutable {
    param([string] $ExplicitJbPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitJbPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitJbPath)) {
            throw "-JbPath not found: $ExplicitJbPath"
        }
        return (Resolve-Path -LiteralPath $ExplicitJbPath).Path
    }

    $cmd = Get-Command "jb" -ErrorAction SilentlyContinue
    if ($null -ne $cmd -and $cmd.Source) {
        return $cmd.Source
    }

    $localToolsJb = Join-Path $env:USERPROFILE ".dotnet\tools\jb.exe"
    if (Test-Path -LiteralPath $localToolsJb) {
        return (Resolve-Path -LiteralPath $localToolsJb).Path
    }

    throw @"
Could not find `jb` (JetBrains ReSharper global tools).

Install once:
  dotnet tool install -g JetBrains.ReSharper.GlobalTools

Then reopen your terminal, or pass -JbPath to jb.exe explicitly.
"@
}

$repoRoot = Resolve-RepoRoot -ScriptDir $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    $SolutionPath = Find-SolutionFile -RepoRoot $repoRoot
}
else {
    if (-not (Test-Path -LiteralPath $SolutionPath)) {
        throw "-SolutionPath not found: $SolutionPath"
    }
    $SolutionPath = (Resolve-Path -LiteralPath $SolutionPath).Path
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "rider-inspections.sarif"
}
else {
    if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath = Join-Path $repoRoot $OutputPath
    }
}

$jb = Resolve-JbExecutable -ExplicitJbPath $JbPath

$outDir = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$arguments = New-Object System.Collections.Generic.List[string]
$arguments.Add("inspectcode")
$arguments.Add([string]$SolutionPath)
$arguments.Add("-o=$OutputPath")

if ($NoBuild) {
    $arguments.Add("--no-build")
}

if (-not [string]::IsNullOrWhiteSpace($DotSettingsPath)) {
    if (-not (Test-Path -LiteralPath $DotSettingsPath)) {
        throw "-DotSettingsPath not found: $DotSettingsPath"
    }
    $resolvedDotSettings = (Resolve-Path -LiteralPath $DotSettingsPath).Path
    $arguments.Add("--settings=$resolvedDotSettings")
}

Write-Host "Repo:       $repoRoot"
Write-Host "Solution:   $SolutionPath"
Write-Host "JB:         $jb"
Write-Host "Output:     $OutputPath"
Write-Host "Arguments:  $($arguments -join ' ')"
Write-Host ""

& $jb @arguments
if ($LASTEXITCODE -ne 0) {
    throw "InspectCode failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Done. SARIF written to: $OutputPath"
