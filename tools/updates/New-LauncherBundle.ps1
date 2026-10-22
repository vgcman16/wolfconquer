#requires -Version 7.2
<#
.SYNOPSIS
Packages only the reviewed, framework-dependent launcher runtime allowlist.
.DESCRIPTION
No build, executable invocation, bootstrap config, keys, game files, or signing.
Requires a pre-existing publish directory and output parent. Run in PowerShell 7
on Windows. All source handles remain read-only until ZIP publication completes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$OutputZip
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This launcher bundle profile is Windows-only.' }
. (Join-Path $PSScriptRoot 'LauncherBundle.Paths.ps1')
$source = Resolve-LbPath $PublishDirectory
if (-not [IO.Directory]::Exists($source)) { throw 'PublishDirectory must be an existing directory.' }
$output = Resolve-LbPath $OutputZip -NewFile
if (-not [IO.Path]::GetExtension($output).Equals('.zip', [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputZip must end in .zip.' }
if (Test-LbWithin $source $output) { throw 'The output ZIP must be outside the input directory.' }
$required = @(Get-LbRuntimeFiles)
$ignored = @('WolfConquer.Launcher.pdb','WolfConquer.Updater.pdb','WolfConquer.Updates.pdb')
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($path in [IO.Directory]::EnumerateFileSystemEntries($source)) {
    Assert-LbNoReparse $path
    $name = [IO.Path]::GetFileName($path)
    if (-not $seen.Add($name) -or -not [IO.File]::Exists($path) -or
        ($required -cnotcontains $name -and $ignored -cnotcontains $name)) {
        throw 'Publish output contains an unknown file, directory, or name alias. Only nine runtime files and three excluded PDB names are permitted.'
    }
}
foreach ($name in $required) { if (-not $seen.Contains($name)) { throw "Required launcher runtime file is missing: $name" } }

function Read-LbFrameworks([IO.FileStream]$Stream, [string[]]$Expected) {
    if ($Stream.Length -gt 65536) { throw 'Runtime metadata is oversized.' }
    $reader = [IO.StreamReader]::new($Stream, [Text.UTF8Encoding]::new($false,$true), $true, 1024, $true)
    try { $config = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable -Depth 16 }
    finally { $reader.Dispose(); $Stream.Position = 0 }
    $options = $config['runtimeOptions']
    if ($null -eq $options -or $options['tfm'] -cne 'net9.0' -or $options.ContainsKey('includedFrameworks')) {
        throw 'Only framework-dependent net9.0 runtime metadata is accepted.'
    }
    $frameworks = @(if ($options.ContainsKey('frameworks')) { $options['frameworks'] } else { $options['framework'] })
    if ($frameworks.Count -ne $Expected.Count) { throw 'Unexpected framework-dependent runtime requirements.' }
    $version = $null
    for ($i = 0; $i -lt $Expected.Count; $i++) {
        if ($frameworks[$i]['name'] -cne $Expected[$i] -or $frameworks[$i]['version'] -notmatch '^9\.[0-9]+\.[0-9]+$') {
            throw 'Unexpected .NET framework name, order, or version.'
        }
        if ($null -ne $version -and $version -cne $frameworks[$i]['version']) { throw 'Runtime framework versions must agree.' }
        $version = $frameworks[$i]['version']
    }
    return $version
}

function Read-LbMachine([IO.FileStream]$Stream) {
    $reader = [IO.BinaryReader]::new($Stream, [Text.Encoding]::UTF8, $true)
    try {
        if ($Stream.Length -lt 70 -or $reader.ReadUInt16() -ne 0x5a4d) { throw 'Apphost metadata is not a PE image.' }
        $Stream.Position = 0x3c; $pe = $reader.ReadUInt32()
        if ($pe -gt $Stream.Length - 6) { throw 'Apphost PE metadata is out of bounds.' }
        $Stream.Position = $pe
        if ($reader.ReadUInt32() -ne 0x4550) { throw 'Apphost PE signature is invalid.' }
        $machine = $reader.ReadUInt16()
        if ($machine -eq 0x8664) { return 'x64' }
        if ($machine -eq 0x14c) { return 'x86' }
        if ($machine -eq 0xaa64) { return 'arm64' }
        throw 'Unrecognized Windows apphost architecture.'
    } finally { $reader.Dispose(); $Stream.Position = 0 }
}

$handles = [Collections.Generic.Dictionary[string,IO.FileStream]]::new([StringComparer]::Ordinal)
$temporary = $null; $destination = $null; $archive = $null
try {
    [long]$totalBytes = 0
    foreach ($name in $required) {
        $path = Resolve-LbPath ([IO.Path]::Combine($source,$name))
        $handle = [IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        $handles.Add($name,$handle)
        if ($handle.Length -gt 64MB) { throw 'A runtime file exceeds the bounded 64 MiB bundle profile.' }
        $totalBytes += $handle.Length
    }
    if ($totalBytes -gt 256MB) { throw 'The launcher runtime exceeds the bounded 256 MiB bundle profile.' }
    $version = Read-LbFrameworks $handles['WolfConquer.Launcher.runtimeconfig.json'] @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')
    if ((Read-LbFrameworks $handles['WolfConquer.Updater.runtimeconfig.json'] @('Microsoft.NETCore.App')) -cne $version) { throw 'Launcher and updater runtime versions differ.' }
    $architecture = Read-LbMachine $handles['WolfConquer.Launcher.exe']
    if ((Read-LbMachine $handles['WolfConquer.Updater.exe']) -cne $architecture) { throw 'Launcher and updater apphost architectures differ.' }
    $temporary = [IO.Path]::Combine([IO.Path]::GetDirectoryName($output), '.launcher-bundle-' + [guid]::NewGuid().ToString('N') + '.tmp')
    $null = Resolve-LbPath $temporary -NewFile
    $destination = [IO.File]::Open($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $archive = [IO.Compression.ZipArchive]::new($destination,[IO.Compression.ZipArchiveMode]::Create,$true,[Text.Encoding]::UTF8)
    $reports = [Collections.Generic.List[object]]::new()
    foreach ($name in $required) {
        $entry = $archive.CreateEntry($name,[IO.Compression.CompressionLevel]::NoCompression)
        $entry.LastWriteTime = [DateTimeOffset]::new(1980,1,1,0,0,0,[TimeSpan]::Zero)
        $entry.ExternalAttributes = 0
        $entryStream = $entry.Open()
        try { $handles[$name].CopyTo($entryStream) } finally { $entryStream.Dispose() }
        if ($handles[$name].Position -ne $handles[$name].Length) { throw 'A source file did not copy completely.' }
        $handles[$name].Position = 0
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($handles[$name]))
        $reports.Add([pscustomobject]@{ Path=$name; Bytes=$handles[$name].Length; Sha256=$hash })
    }
    $archive.Dispose(); $archive = $null
    $destination.Flush($true); $destination.Position = 0
    $zipHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($destination))
    $zipBytes = $destination.Length
    $destination.Dispose(); $destination = $null
    $null = Resolve-LbPath $output -NewFile
    Assert-LbNoReparse $temporary
    [IO.File]::Move($temporary,$output,$false)
    $temporary = $null
    [pscustomobject]@{ OutputZip=$output; FileCount=$reports.Count; InputBytes=$totalBytes; ArchiveBytes=$zipBytes
        Sha256=$zipHash; Files=$reports.ToArray(); FrameworkDependent=$true
        Prerequisite="Windows $architecture with Microsoft .NET Desktop Runtime $version (matching architecture; runtime not included)."
        Notice='Tool bundle only; no bootstrap configuration, keys, game client, manifest signing, or archive signature is included.' }
} finally {
    try { if ($null -ne $archive) { $archive.Dispose() } }
    finally {
        try { if ($null -ne $destination) { $destination.Dispose() } }
        finally {
            foreach ($handle in $handles.Values) { $handle.Dispose() }
            if ($null -ne $temporary -and [IO.File]::Exists($temporary)) {
                Assert-LbNoReparse $temporary; [IO.File]::Delete($temporary)
            }
        }
    }
}
