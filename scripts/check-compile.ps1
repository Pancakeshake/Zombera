# Compile-check harness for the Unity project.
#
# Unity's generated .csproj files are legacy (ToolsVersion 4.0, NoStdLib) and cannot be built by
# `dotnet build`. This script extracts the source list and references from a generated csproj and
# runs the Roslyn compiler that ships with the Unity editor, giving real compiler diagnostics
# without opening the editor.
#
# Usage:  pwsh -File scripts/check-compile.ps1 -Project Zombera.World.City
param(
    [Parameter(Mandatory = $true)][string]$Project,
    [string[]]$Exclude = @(),
    # The human edits this repo concurrently. A stray trailing brace (CS1022) or a missing class
    # brace (CS1514) in a file this harness does not own would otherwise block every check.
    # AutoPatchEof compiles a patched COPY from tmp\patched and never writes to the original.
    [switch]$AutoPatchEof,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $root 'Assets'))) { $root = 'A:\Zombera' }

$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1-x86_64\Editor\Data'
$csc = Join-Path $unity 'DotNetSdkRoslyn\csc.dll'
$framework = Join-Path $unity 'MonoBleedingEdge\lib\mono\4.7.1-api'
$csproj = Join-Path $root "$Project.csproj"

foreach ($required in @($csc, $framework, $csproj)) {
    if (-not (Test-Path $required)) { throw "Missing required path: $required" }
}

[xml]$xml = Get-Content -LiteralPath $csproj
$sources = New-Object System.Collections.Generic.List[string]
$defines = ''

foreach ($property in $xml.Project.PropertyGroup) {
    if ($null -eq $property) { continue }
    foreach ($node in $property.ChildNodes) {
        if ($node.Name -eq 'DefineConstants' -and $node.InnerText -and -not $defines) {
            $defines = $node.InnerText
        }
    }
}

foreach ($group in $xml.Project.ItemGroup) {
    if ($null -eq $group) { continue }
    foreach ($node in $group.ChildNodes) {
        if ($node.Name -eq 'Compile' -and $node.Include) {
            $path = $node.Include
            if (-not [System.IO.Path]::IsPathRooted($path)) { $path = Join-Path $root $path }
            $skip = $false
            foreach ($pattern in $Exclude) {
                if ([System.IO.Path]::GetFileName($path) -like $pattern) { $skip = $true; break }
            }
            if (-not $skip -and (Test-Path -LiteralPath $path)) { $sources.Add($path) }
        }
    }
}

if ($sources.Count -eq 0) { throw "No compilable sources found in $csproj" }

# Unity's generated csproj can lag behind newly added files, which silently drops them from the
# compilation. Recover the missing sources so a newly created file can never produce a false green.
$asmdefDirs = @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets') -Filter *.asmdef -Recurse |
    ForEach-Object { $_.Directory.FullName })
$asmdef = Get-ChildItem -LiteralPath (Join-Path $root 'Assets') -Filter *.asmdef -Recurse |
    Where-Object { (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).name -eq $Project } |
    Select-Object -First 1
$fromAsmdef = 0
$known = @{}
foreach ($s in $sources) { $known[[System.IO.Path]::GetFullPath($s)] = $true }

function Test-UnderAsmdef([string]$full) {
    foreach ($d in $asmdefDirs) {
        if ($full.StartsWith($d + '\', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Test-Excluded([string]$name) {
    foreach ($pattern in $Exclude) {
        if ($name -like $pattern) { return $true }
    }
    return $false
}

if ($asmdef) {
    $asmRoot = $asmdef.Directory.FullName
    Get-ChildItem -LiteralPath $asmRoot -Filter *.cs -Recurse | ForEach-Object {
        $full = $_.FullName
        if ($known.ContainsKey($full) -or (Test-Excluded $_.Name)) { return }
        $sources.Add($full); $fromAsmdef++
    }
}
elseif ($Project -eq 'Assembly-CSharp-Editor' -or $Project -eq 'Assembly-CSharp') {
    # No asmdef: Unity splits these by whether the script sits in a folder named "Editor". Only the
    # project's own Assets/Tests tree is recovered here — sweeping all of Assets pulls in third-party
    # editor scripts that Unity assigns elsewhere and that cannot compile standalone.
    $wantEditor = $Project -eq 'Assembly-CSharp-Editor'
    if ($wantEditor) {
        Get-ChildItem -LiteralPath (Join-Path $root 'Assets\Tests') -Filter *.cs -Recurse |
            ForEach-Object {
                $full = $_.FullName
                if ($known.ContainsKey($full) -or (Test-Excluded $_.Name)) { return }
                $sources.Add($full); $fromAsmdef++
            }
    }
}

# Compile-only brace repair for files the harness does not own. A file whose closing braces
# outnumber its opening braces is a mid-edit artefact; compile a trimmed copy so one stray brace
# cannot block verification of everything else. The original is never written to.
$patchRoot = Join-Path $root 'tmp\patched'
$patchedFiles = New-Object System.Collections.Generic.List[string]
if ($AutoPatchEof) {
    New-Item -ItemType Directory -Force -Path $patchRoot | Out-Null
    for ($i = 0; $i -lt $sources.Count; $i++) {
        $original = $sources[$i]
        $text = [System.IO.File]::ReadAllText($original)
        $opens = ([regex]::Matches($text, '\{')).Count
        $closes = ([regex]::Matches($text, '\}')).Count
        if ($closes -le $opens) { continue }
        $trimmed = $text
        $drop = $closes - $opens
        for ($d = 0; $d -lt $drop; $d++) {
            $idx = $trimmed.LastIndexOf('}')
            if ($idx -lt 0) { break }
            $trimmed = $trimmed.Remove($idx, 1)
        }
        $hash = [System.BitConverter]::ToString(
            [System.Security.Cryptography.SHA1]::Create().ComputeHash(
                [System.Text.Encoding]::UTF8.GetBytes($original))).Replace('-', '').Substring(0, 8)
        $target = Join-Path $patchRoot "$hash-$([System.IO.Path]::GetFileName($original))"
        [System.IO.File]::WriteAllText($target, $trimmed)
        $patchedFiles.Add("$([System.IO.Path]::GetFileName($original)) (trimmed $drop trailing brace(s))")
        $sources[$i] = $target
    }
}

$seen = @{}
$refs = New-Object System.Collections.Generic.List[string]
function Add-Ref([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    $name = [System.IO.Path]::GetFileName($path)
    if ($seen.ContainsKey($name)) { return }
    $seen[$name] = $true
    $refs.Add($path)
}

foreach ($group in $xml.Project.ItemGroup) {
    if ($null -eq $group) { continue }
    foreach ($node in $group.ChildNodes) {
        if ($node.Name -ne 'Reference') { continue }
        foreach ($child in $node.ChildNodes) {
            if ($child.Name -ne 'HintPath') { continue }
            $path = $child.InnerText
            if (-not [System.IO.Path]::IsPathRooted($path)) { $path = Join-Path $root $path }
            Add-Ref $path
        }
    }
}

Get-ChildItem -LiteralPath $framework -Filter *.dll | ForEach-Object { Add-Ref $_.FullName }
Get-ChildItem -LiteralPath (Join-Path $unity 'Managed\UnityEngine') -Filter *.dll |
    ForEach-Object { Add-Ref $_.FullName }
Get-ChildItem -LiteralPath (Join-Path $root 'Library\ScriptAssemblies') -Filter *.dll |
    Where-Object { $_.BaseName -ne $Project -and -not $_.BaseName.StartsWith("$Project.") } |
    ForEach-Object {
        # Prefer a freshly compiled dependency from this harness over a stale Unity build,
        # otherwise dependent assemblies are checked against out-of-date type definitions.
        $fresh = Join-Path $root "tmp\compile-$($_.BaseName).dll"
        if (Test-Path -LiteralPath $fresh) { Add-Ref $fresh } else { Add-Ref $_.FullName }
    }

$rsp = Join-Path $root "tmp\compile-$Project.rsp"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $rsp) | Out-Null
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('-target:library')
$lines.Add('-nologo')
$lines.Add('-nostdlib+')
$lines.Add('-noconfig')
$lines.Add('-langversion:9.0')
$lines.Add('-nowarn:0169,0649,0618,1591')
$lines.Add('-out:' + (Join-Path $root "tmp\compile-$Project.dll"))
$lines.Add('-define:' + $defines)
foreach ($r in $refs) { $lines.Add('-reference:"' + $r + '"') }
foreach ($s in $sources) { $lines.Add('"' + $s + '"') }
Set-Content -LiteralPath $rsp -Value $lines -Encoding UTF8

if (-not $Quiet) {
    Write-Host "Compiling $Project ($($sources.Count) files, $($refs.Count) references)"
    if ($fromAsmdef -gt 0) { Write-Host "  +$fromAsmdef source(s) recovered from the asmdef folder (stale csproj)" }
    foreach ($p in $patchedFiles) { Write-Host "  PATCHED COPY (original untouched): $p" }
}

$output = & dotnet $csc "@$rsp" 2>&1
$codes = $output | Select-String -Pattern 'error [A-Z]{2}\d+' -AllMatches
if ($codes) {
    $output | Where-Object { $_ -match 'error ' } | ForEach-Object { Write-Host $_ }
    Write-Host "FAILED: $($codes.Count) error line(s)"
    exit 1
}

Write-Host "OK: $Project compiled with no errors"
exit 0
