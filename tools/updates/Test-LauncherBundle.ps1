#requires -Version 7.2
<# Synthetic files only. Never launches a packaged program or reads a real publish directory. #>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'These Windows path and junction controls require Windows.' }
. (Join-Path $PSScriptRoot 'LauncherBundle.Paths.ps1')
$packager = Join-Path $PSScriptRoot 'New-LauncherBundle.ps1'
$script:assertions = 0
function Assert-LbTest([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Launcher bundle assertion failed: $Message" }
    $script:assertions++
}
function Assert-LbRefused([scriptblock]$Action, [string]$Message) {
    $refused = $false
    try { $null = & $Action } catch { $refused = $true }
    Assert-LbTest $refused $Message
}
function Get-LbTestHash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }

$tempParent = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))
$rootName = 'wolfconquer-launcher-bundle-test-' + [guid]::NewGuid().ToString('N')
$root = [IO.Path]::Combine($tempParent,$rootName)
$null = Resolve-LbPath $root -NewFile
$null = [IO.Directory]::CreateDirectory($root)
$junctions = [Collections.Generic.List[string]]::new()
try {
    $source = [IO.Path]::Combine($root,'source')
    $output = [IO.Path]::Combine($root,'output')
    $null = [IO.Directory]::CreateDirectory($source)
    $null = [IO.Directory]::CreateDirectory($output)
    # Independent literal expected file set, not the packager's allowlist function.
    $required = @('WolfConquer.Launcher.deps.json','WolfConquer.Launcher.dll','WolfConquer.Launcher.exe',
        'WolfConquer.Launcher.runtimeconfig.json','WolfConquer.Updater.deps.json','WolfConquer.Updater.dll',
        'WolfConquer.Updater.exe','WolfConquer.Updater.runtimeconfig.json','WolfConquer.Updates.dll')
    foreach ($name in $required) {
        $bytes = [Text.Encoding]::UTF8.GetBytes('synthetic-only:' + $name)
        if ($name.EndsWith('.exe',[StringComparison]::Ordinal)) {
            # Metadata stub only, not a runnable program. MZ + bounded PE machine metadata.
            $bytes = [byte[]]::new(128)
            $bytes[0]=0x4d; $bytes[1]=0x5a; $bytes[0x3c]=0x40
            $bytes[0x40]=0x50; $bytes[0x41]=0x45; $bytes[0x44]=0x64; $bytes[0x45]=0x86
        } elseif ($name -ceq 'WolfConquer.Launcher.runtimeconfig.json') {
            $bytes = [Text.Encoding]::UTF8.GetBytes('{"runtimeOptions":{"tfm":"net9.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"9.0.0"},{"name":"Microsoft.WindowsDesktop.App","version":"9.0.0"}]}}')
        } elseif ($name -ceq 'WolfConquer.Updater.runtimeconfig.json') {
            $bytes = [Text.Encoding]::UTF8.GetBytes('{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}')
        }
        [IO.File]::WriteAllBytes([IO.Path]::Combine($source,$name),$bytes)
    }
    foreach ($name in @('WolfConquer.Launcher.pdb','WolfConquer.Updater.pdb','WolfConquer.Updates.pdb')) {
        [IO.File]::WriteAllText([IO.Path]::Combine($source,$name),'excluded synthetic PDB')
    }
    $before = @{}
    foreach ($path in [IO.Directory]::EnumerateFiles($source)) {
        $before[[IO.Path]::GetFileName($path)] = @{ Hash=(Get-LbTestHash $path); Time=[IO.File]::GetLastWriteTimeUtc($path) }
    }
    $zip1 = [IO.Path]::Combine($output,'first.zip')
    $zip2 = [IO.Path]::Combine($output,'second.zip')
    $first = & $packager -PublishDirectory $source -OutputZip $zip1
    $second = & $packager -PublishDirectory $source -OutputZip $zip2
    Assert-LbTest ($first.FileCount -eq 9 -and $first.Files.Count -eq 9) 'exact nine runtime files'
    Assert-LbTest ($first.FrameworkDependent -and $first.Prerequisite.Contains('Desktop Runtime 9.0.0')) 'truthful framework prerequisite'
    Assert-LbTest ($first.Sha256 -ceq $second.Sha256 -and $first.ArchiveBytes -eq $second.ArchiveBytes) 'deterministic archive hashes and lengths'
    $bytes1 = [IO.File]::ReadAllBytes($zip1); $bytes2 = [IO.File]::ReadAllBytes($zip2)
    Assert-LbTest ([Convert]::ToHexString($bytes1) -ceq [Convert]::ToHexString($bytes2)) 'byte-for-byte deterministic ZIP'
    Assert-LbTest ($first.Sha256 -ceq (Get-LbTestHash $zip1)) 'archive report matches actual SHA256'
    $archive = [IO.Compression.ZipFile]::OpenRead($zip1)
    try {
        Assert-LbTest (($archive.Entries.FullName -join '|') -ceq ($required -join '|')) 'fixed exact entry order, no configs or PDBs'
        foreach ($entry in $archive.Entries) {
            Assert-LbTest ($entry.LastWriteTime.DateTime -eq [datetime]::new(1980,1,1)) 'fixed ZIP timestamp'
            Assert-LbTest ($entry.ExternalAttributes -eq 0) 'fixed ZIP attributes'
            $stream = $entry.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
            Assert-LbTest ($hash -ceq $before[$entry.FullName].Hash) 'entry bytes equal owned source'
        }
    } finally { $archive.Dispose() }

    Assert-LbRefused { & $packager -PublishDirectory $source -OutputZip $zip1 } 'no overwrite'
    Assert-LbTest ((Get-LbTestHash $zip1) -ceq $first.Sha256) 'existing output preserved'
    Assert-LbRefused { & $packager -PublishDirectory $source -OutputZip ([IO.Path]::Combine($source,'inside.zip')) } 'output inside input'
    Assert-LbRefused { & $packager -PublishDirectory ($root + '\source\..\source') -OutputZip ([IO.Path]::Combine($output,'traversal.zip')) } 'input traversal'
    Assert-LbRefused { & $packager -PublishDirectory $source -OutputZip ($output + '\..\traversal.zip') } 'output traversal'
    Assert-LbRefused { & $packager -PublishDirectory ($root + '\SOURCE') -OutputZip ([IO.Path]::Combine($output,'case.zip')) } 'case alias'
    Assert-LbRefused { & $packager -PublishDirectory ($source + '.') -OutputZip ([IO.Path]::Combine($output,'dot.zip')) } 'trailing dot alias'
    Assert-LbRefused { & $packager -PublishDirectory '\\localhost\C$\unread' -OutputZip ([IO.Path]::Combine($output,'unc.zip')) } 'UNC rejected before access'
    Assert-LbRefused { & $packager -PublishDirectory ([IO.Path]::GetPathRoot($source)) -OutputZip ([IO.Path]::Combine($output,'volume.zip')) } 'volume root'
    Assert-LbRefused { & $packager -PublishDirectory $source -OutputZip ($output + '\alias.zip:stream') } 'alternate data stream alias'
    $unknown = [IO.Path]::Combine($source,'wolfconquer-updates.json')
    [IO.File]::WriteAllText($unknown,'{"synthetic":true}')
    try { Assert-LbRefused { & $packager -PublishDirectory $source -OutputZip ([IO.Path]::Combine($output,'unknown.zip')) } 'unknown config excluded, not silently packaged' }
    finally { [IO.File]::Delete($unknown) }
    $extraDirectory = [IO.Path]::Combine($source,'extra')
    $null = [IO.Directory]::CreateDirectory($extraDirectory)
    try { Assert-LbRefused { & $packager -PublishDirectory $source -OutputZip ([IO.Path]::Combine($output,'directory.zip')) } 'unknown directory refused, no recursive packaging' }
    finally { [IO.Directory]::Delete($extraDirectory) }
    $missing = [IO.Path]::Combine($source,'WolfConquer.Updates.dll')
    $saved = [IO.File]::ReadAllBytes($missing)
    [IO.File]::Delete($missing)
    try { Assert-LbRefused { & $packager -PublishDirectory $source -OutputZip ([IO.Path]::Combine($output,'missing.zip')) } 'missing required runtime refused' }
    finally { [IO.File]::WriteAllBytes($missing,$saved); [IO.File]::SetLastWriteTimeUtc($missing,$before['WolfConquer.Updates.dll'].Time) }

    foreach ($target in @($source,$output)) {
        $junction = [IO.Path]::Combine($root,([IO.Path]::GetFileName($target) + '-link'))
        $null = New-Item -ItemType Junction -Path $junction -Target $target
        $junctions.Add($junction)
        if ($target -ceq $source) {
            Assert-LbRefused { & $packager -PublishDirectory $junction -OutputZip ([IO.Path]::Combine($output,'linked-source.zip')) } 'reparse input root'
        } else {
            Assert-LbRefused { & $packager -PublishDirectory $source -OutputZip ([IO.Path]::Combine($junction,'linked-output.zip')) } 'reparse output ancestor'
        }
    }
    foreach ($name in $before.Keys) {
        $path = [IO.Path]::Combine($source,$name)
        Assert-LbTest ((Get-LbTestHash $path) -ceq $before[$name].Hash -and [IO.File]::GetLastWriteTimeUtc($path) -eq $before[$name].Time) 'source bytes and write times unchanged'
    }
    Assert-LbTest ([IO.Directory]::GetFiles($source).Length -eq 12) 'source file set unchanged'
    Assert-LbTest ([IO.Directory]::GetFiles($output).Length -eq 2) 'rejections leave no archives or temporary outputs'
    [pscustomobject]@{ Passed=$true; Assertions=$script:assertions; Scope='Synthetic, non-runnable fixtures only; no build, signing, or real-client execution.' }
} finally {
    foreach ($junction in $junctions) {
        if ([IO.Directory]::Exists($junction)) {
            if (-not (Test-LbWithin $root $junction) -or
                -not ([IO.File]::GetAttributes($junction) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Refusing unexpected junction cleanup target.' }
            [IO.Directory]::Delete($junction)
        }
    }
    $resolved = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($resolved) -cne $tempParent -or [IO.Path]::GetFileName($resolved) -cne $rootName -or
        $rootName -cnotmatch '^wolfconquer-launcher-bundle-test-[0-9a-f]{32}$') { throw 'Refusing unexpected test cleanup root.' }
    Assert-LbNoReparse $resolved
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
