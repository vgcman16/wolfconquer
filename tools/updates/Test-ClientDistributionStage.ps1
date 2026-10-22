#requires -Version 7.4
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Synthetic staging tests require Windows sharing rules and reparse checks.' }
. (Join-Path $PSScriptRoot 'ClientDistribution.StageTestHelpers.ps1')
$stageTestRoot = [IO.Directory]::CreateTempSubdirectory('wolf-client-stage-test-').FullName
$stageTestLinks = [Collections.Generic.List[object]]::new()
$script:stageTestCount = 0
try {
    Invoke-DsCase 'exact selected bytes only, rebased publisher plan and untouched original' {
        $f = New-DsFixture 'exact'; Save-DsPlan $f
        $original = Get-DsSnapshot $f.Source; $inputBytes = [IO.File]::ReadAllBytes($f.Input)
        $result = (Invoke-DsStage $f | Out-String) | ConvertFrom-Json
        Assert-DsTest ($result.outcome -ceq 'staged-not-published' -and $result.files -eq 2) 'actual successful entrypoint outcome'
        Assert-DsTest ($result.bytes -eq $f.Executable.Length + 256) 'actual copied byte count'
        Assert-DsRebasedPlan $f
        Assert-DsTest ($original -ceq (Get-DsSnapshot $f.Source)) 'all original file bytes/names/write times unchanged'
        Assert-DsTest ([Convert]::ToBase64String($inputBytes) -ceq [Convert]::ToBase64String([IO.File]::ReadAllBytes($f.Input))) 'input plan unchanged'
        $paths = [IO.Directory]::GetFiles($f.Stage, '*', [IO.SearchOption]::AllDirectories)
        Assert-DsTest ($paths.Count -eq 2 -and -not [IO.File]::Exists([IO.Path]::Combine($f.Stage, 'unlisted-private.pem'))) 'no unlisted files copied'
        foreach ($relative in @('Client.exe', 'data/asset.bin')) {
            $old = [IO.File]::ReadAllBytes([IO.Path]::Combine($f.Source, $relative))
            $new = [IO.File]::ReadAllBytes([IO.Path]::Combine($f.Stage, $relative))
            Assert-DsTest ([Convert]::ToBase64String($old) -ceq [Convert]::ToBase64String($new)) 'staged bytes match actual selected source'
        }
        Assert-DsTest (-not [IO.Directory]::Exists($f.Plan.publishRoot)) 'no served blobs, ZIP, feed or publication created'
    }
    Invoke-DsCase 'literal spaces brackets semicolon and dollar paths are not expressions or patterns' {
        $f = New-DsFixture 'literal ;$ [copy]'
        $relative = 'data/asset [one] ;$.bin'
        [IO.File]::Move([IO.Path]::Combine($f.Source, 'data', 'asset.bin'), [IO.Path]::Combine($f.Source, $relative))
        $f.Plan.files[1].path = $relative
        $f.Stage = [IO.Path]::Combine($f.Root, 'staged [copy] ;$')
        $f.Output = [IO.Path]::Combine($f.Root, 'review', 'staged [plan] ;$.json')
        Save-DsPlan $f; $before = Get-DsSnapshot $f.Source
        Invoke-DsStage $f | Out-Null; Assert-DsRebasedPlan $f
        $actual = [IO.File]::ReadAllBytes([IO.Path]::Combine($f.Stage, $relative))
        Assert-DsTest ([Convert]::ToBase64String($actual) -ceq [Convert]::ToBase64String([byte[]](0..255))) 'literal selected path copied exactly'
        Assert-DsTest ($before -ceq (Get-DsSnapshot $f.Source)) 'special-character original names and bytes unchanged'
    }
    Invoke-DsCase 'names-only approval stages current pre-invocation bytes, not a historical hash' {
        $f = New-DsFixture 'current-bytes'; Save-DsPlan $f
        [IO.File]::WriteAllBytes([IO.Path]::Combine($f.Source, 'data', 'asset.bin'), [byte[]](8, 6, 4, 2))
        Invoke-DsStage $f | Out-Null; Assert-DsRebasedPlan $f
        Assert-DsTest ([Convert]::ToBase64String([IO.File]::ReadAllBytes([IO.Path]::Combine($f.Stage, 'data', 'asset.bin'))) -ceq 'CAYEAg==') 'current exact bytes accepted before source acquisition'
    }
    Invoke-DsCase 'existing staging directory or output plan is never overwritten' {
        $f = New-DsFixture 'existing-stage'; Save-DsPlan $f
        $null = [IO.Directory]::CreateDirectory($f.Stage); [IO.File]::WriteAllText([IO.Path]::Combine($f.Stage, 'keep.txt'), 'keep staged work')
        $before = Get-DsSnapshot $f.Stage
        Assert-DsRefused $f -AllowPartial
        Assert-DsTest ($before -ceq (Get-DsSnapshot $f.Stage)) 'preexisting destination untouched'
        $f = New-DsFixture 'existing-output'; Save-DsPlan $f
        [IO.File]::WriteAllText($f.Output, 'keep output plan')
        $refused = $false; try { Invoke-DsStage $f | Out-Null } catch { $refused = $true }
        Assert-DsTest ($refused -and [IO.File]::ReadAllText($f.Output) -ceq 'keep output plan') 'existing output preserved'
        Assert-DsTest (-not [IO.Directory]::Exists($f.Stage)) 'known output collision refuses before staging'
    }
    Invoke-DsCase 'unknown duplicate and unsupported publication schema is refused before staging' {
        $f = New-DsFixture 'bad-schema'
        $f.Plan.schemaVersion = 2; Save-DsPlan $f; Assert-DsRefused $f
        $f.Plan.schemaVersion = 1; $f.Plan.Add('unknownProperty', $true); Save-DsPlan $f; Assert-DsRefused $f
        $f.Plan.Remove('unknownProperty'); Save-DsPlan $f
        $raw = [IO.File]::ReadAllText($f.Input)
        $duplicate = $raw -replace '("schemaVersion"\s*:\s*1)', '$1,"SchemaVersion":1'
        Assert-DsTest ($duplicate -cne $raw) 'duplicate property inserted'
        [IO.File]::WriteAllText($f.Input, $duplicate); Assert-DsRefused $f
    }
    Invoke-DsCase 'duplicate aliases traversal and quarantined file rows are refused' {
        $f = New-DsFixture 'bad-paths'
        foreach ($paths in @(@('Client.exe', 'Client.exe'), @('Client.exe', 'client.exe'), @('../outside'), @('/absolute'),
            @('data\asset.bin'), @('data', 'data/asset.bin'), @('unlisted-private.pem'))) {
            $f.Plan.files = @($paths | ForEach-Object { [ordered]@{ path = $_; baseSha256 = $null } })
            Save-DsPlan $f; Assert-DsRefused $f
        }
        $f.Plan.files = @([ordered]@{ path = 'Client.exe'; baseSha256 = $null; extraField = $true })
        Save-DsPlan $f; Assert-DsRefused $f
    }
    Invoke-DsCase 'original staged and served roots plus output containment cannot overlap' {
        $f = New-DsFixture 'overlap'; Save-DsPlan $f
        $original = Get-DsSnapshot $f.Source
        foreach ($target in @($f.Source, [IO.Path]::Combine($f.Source, 'new-stage'), $f.Root)) {
            $copy = $f.PSObject.Copy(); $copy.Stage = $target
            Assert-DsRefused $copy -AllowPartial
        }
        $null = [IO.Directory]::CreateDirectory($f.Plan.publishRoot)
        $copy = $f.PSObject.Copy(); $copy.Stage = [IO.Path]::Combine($f.Plan.publishRoot, 'new-stage')
        Assert-DsRefused $copy
        foreach ($target in @([IO.Path]::Combine($f.Source, 'output.json'), [IO.Path]::Combine($f.Stage, 'output.json'),
            [IO.Path]::Combine($f.Plan.publishRoot, 'output.json'))) {
            $copy = $f.PSObject.Copy(); $copy.Output = $target
            Assert-DsRefused $copy
        }
        Assert-DsTest ($original -ceq (Get-DsSnapshot $f.Source)) 'overlap refusals leave original unchanged'
    }
    Invoke-DsCase 'missing selected source fails preflight without creating staging tree' {
        $f = New-DsFixture 'missing'; Save-DsPlan $f
        [IO.File]::Delete([IO.Path]::Combine($f.Source, 'data', 'asset.bin')) # Only this fixture-owned synthetic file.
        $before = Get-DsSnapshot $f.Source; Assert-DsRefused $f
        Assert-DsTest ($before -ceq (Get-DsSnapshot $f.Source)) 'failed operation does not delete or restore original files'
    }
    Invoke-DsCase 'active writer on second source leaves explicit first-file partial tree and no plan' {
        $f = New-DsFixture 'partial'; Save-DsPlan $f
        $before = Get-DsSnapshot $f.Source
        $held = [IO.File]::Open([IO.Path]::Combine($f.Source, 'data', 'asset.bin'), [IO.FileMode]::Open,
            [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
        $failureMessage = ''
        try { Assert-DsRefused $f -AllowPartial -Failure ([ref]$failureMessage) } finally { $held.Dispose() }
        Assert-DsTest ($failureMessage.Contains('Partial owned stage remains') -and
            $failureMessage.Contains("completed files=1, bytes=$($f.Executable.Length)")) 'failure explicitly identifies retained partial work and actual completed bytes'
        Assert-DsTest ([IO.Directory]::Exists($f.Stage)) 'explicit partial tree retained rather than deleted'
        Assert-DsTest ([IO.File]::Exists([IO.Path]::Combine($f.Stage, 'Client.exe'))) 'actual earlier file remains copied'
        Assert-DsTest ([IO.Directory]::GetFiles($f.Stage, '*', [IO.SearchOption]::AllDirectories).Count -eq 1) 'failure did not copy blocked later source'
        Assert-DsTest ($before -ceq (Get-DsSnapshot $f.Source)) 'original unchanged after real sharing refusal'
        Assert-DsRefused $f -AllowPartial # A retry cannot reuse or delete a partial stage.
    }
    Invoke-DsCase 'selected junction ancestor and file symbolic link are refused without following targets' {
        $f = New-DsFixture 'junction'; Save-DsPlan $f
        $moved = [IO.Path]::Combine($f.Root, 'ordinary-data'); [IO.Directory]::Move([IO.Path]::Combine($f.Source, 'data'), $moved)
        Add-DsJunction ([IO.Path]::Combine($f.Source, 'data')) $moved
        Assert-DsRefused $f
        Assert-DsTest ([IO.File]::Exists([IO.Path]::Combine($moved, 'asset.bin'))) 'junction target preserved'
        $f = New-DsFixture 'ancestor'; $link = [IO.Path]::Combine($f.Root, 'root-link')
        Add-DsJunction $link $f.Root
        $f.Plan.sourceRoot = [IO.Path]::Combine($link, 'source'); Save-DsPlan $f; Assert-DsRefused $f
        $f = New-DsFixture 'file-link'; Save-DsPlan $f
        $path = [IO.Path]::Combine($f.Source, 'data', 'asset.bin'); $outside = [IO.Path]::Combine($f.Root, 'outside-asset.bin')
        [IO.File]::Move($path, $outside)
        $null = [IO.File]::CreateSymbolicLink($path, $outside) # Requires existing Windows symlink privilege; never silently skipped.
        $stageTestLinks.Add([pscustomobject]@{ Path = $path; Directory = $false })
        Assert-DsTest ([bool]([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint)) 'actual file symbolic link exists'
        Assert-DsRefused $f
        Assert-DsTest ([IO.File]::ReadAllBytes($outside).Length -eq 256) 'file-link target preserved'
    }
    [ordered]@{ outcome = 'passed'; syntheticCases = $script:stageTestCount; originalClientTouched = $false; published = $false } | ConvertTo-Json
} finally {
    foreach ($link in $stageTestLinks) {
        if (-not ([IO.File]::GetAttributes($link.Path) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Owned link identity changed; cleanup refused.' }
        if ($link.Directory) { [IO.Directory]::Delete($link.Path) } else { [IO.File]::Delete($link.Path) }
    }
    $resolved = [IO.Path]::GetFullPath($stageTestRoot)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.Path]::GetFileName($resolved).StartsWith('wolf-client-stage-test-', [StringComparison]::Ordinal) -or
        ([IO.File]::GetAttributes($resolved) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe staging self-test cleanup target.' }
    [IO.Directory]::Delete($resolved, $true)
}
