#requires -Version 7.4
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Excluded-reparse regression tests require real Windows junctions.' }
. (Join-Path $PSScriptRoot 'ClientDistribution.TestHelpers.ps1')
. (Join-Path $PSScriptRoot 'ClientDistribution.ExclusionTestHelpers.ps1')
$taskTestRoot = [IO.Directory]::CreateTempSubdirectory('wolf-client-exclusion-test-').FullName
$exclusionTestLinks = [Collections.Generic.List[object]]::new()
$skippedGroups = [Collections.Generic.List[string]]::new()
$script:caseCount = 0
try {
    Invoke-CdTestCase 'exact excluded junction leaves omit external nonempty targets without opening locked content' {
        $f = New-CdTestFixture 'excluded-junctions'
        $external = [IO.Path]::Combine($f.Root, 'external'); $null = [IO.Directory]::CreateDirectory($external)
        $preserved = [IO.Path]::Combine($external, 'target-only.bin'); [IO.File]::WriteAllBytes($preserved, [byte[]](8, 7, 6, 5))
        Add-CeJunction ([IO.Path]::Combine($f.Source, 'outside-link')) $external
        Add-CeJunction ([IO.Path]::Combine($f.Source, 'data', 'excluded-link')) $external
        $f.Policy.excludePaths = @('outside-link', 'data/excluded-link'); Save-CdTestPolicy $f
        $sourceBefore = Get-CeOrdinarySnapshot $f.Source; $outsideBefore = Get-CdSyntheticSnapshot $external
        $heldTarget = $null; $heldApproved = $null
        try {
            $heldTarget = [IO.File]::Open($preserved, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $heldApproved = [IO.File]::Open([IO.Path]::Combine($f.Source, 'Client.exe'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
            $outputs = Get-CdTestOutputs $f; Invoke-CdTestPlanner $f $outputs
            Assert-CeOriginalPlan $f $outputs $f.Policy.excludePaths
            Assert-CdTest (-not [IO.File]::ReadAllText($outputs.Report).Contains('target-only.bin')) 'report did not enumerate target contents'
        } finally {
            if ($null -ne $heldApproved) { $heldApproved.Dispose() }
            if ($null -ne $heldTarget) { $heldTarget.Dispose() }
        }
        $stage = [IO.Path]::Combine($f.Root, 'exact-stage')
        $rebasedPath = [IO.Path]::Combine($f.Review, 'exact-stage-plan.json')
        $originalPlan = [IO.File]::ReadAllBytes($outputs.Plan)
        $heldTarget = [IO.File]::Open($preserved, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try {
            $result = (& (Join-Path $PSScriptRoot 'New-ClientDistributionStage.ps1') -PlanPath $outputs.Plan `
                -StageDirectory $stage -OutputPlan $rebasedPath | Out-String) | ConvertFrom-Json
            Assert-CdTest ($result.outcome -ceq 'staged-not-published' -and $result.files -eq 2) 'actual planner-generated plan stages successfully while external bytes remain locked'
            $stagedPaths = [string[]]@([IO.Directory]::GetFiles($stage, '*', [IO.SearchOption]::AllDirectories) |
                ForEach-Object { [IO.Path]::GetRelativePath($stage, $_).Replace('\', '/') })
            [Array]::Sort($stagedPaths, [StringComparer]::Ordinal)
            Assert-CdTest (($stagedPaths -join ',') -ceq 'Client.exe,data/a.bin') 'actual stage contains only the original two approved files'
            foreach ($relative in $stagedPaths) {
                $sourceBytes = [IO.File]::ReadAllBytes([IO.Path]::Combine($f.Source, $relative))
                $stagedBytes = [IO.File]::ReadAllBytes([IO.Path]::Combine($stage, $relative))
                Assert-CdTest ([Convert]::ToBase64String($sourceBytes) -ceq [Convert]::ToBase64String($stagedBytes)) 'staged bytes match approved source, not external target bytes'
            }
            $stagedDirectories = [IO.Directory]::GetDirectories($stage, '*', [IO.SearchOption]::AllDirectories)
            Assert-CdTest ($stagedDirectories.Count -eq 1 -and [IO.Path]::GetRelativePath($stage, $stagedDirectories[0]) -ceq 'data') 'no excluded leaf or extra empty directory recreated in stage'
            $rebased = [IO.File]::ReadAllText($rebasedPath) | ConvertFrom-Json
            Assert-CdTest ($rebased.sourceRoot -ceq $stage -and $rebased.files.Count -eq 2 -and
                ($rebased.files.path -join ',') -ceq 'Client.exe,data/a.bin') 'actual rebased publisher plan names exact staged root and two approved paths'
            Assert-CdTest ([Convert]::ToBase64String($originalPlan) -ceq [Convert]::ToBase64String([IO.File]::ReadAllBytes($outputs.Plan))) 'stager leaves original approved plan unchanged'
        } finally { $heldTarget.Dispose() }
        Assert-CdTest ($sourceBefore -ceq (Get-CeOrdinarySnapshot $f.Source)) 'ordinary source bytes unchanged without following links'
        Assert-CdTest ($outsideBefore -ceq (Get-CdSyntheticSnapshot $external)) 'external target bytes and write times unchanged'
    }
    Invoke-CdTestCase 'unexcluded selected and unlisted junction leaves still refuse' {
        foreach ($selected in @($true, $false)) {
            $name = if ($selected) { 'selected-link' } else { 'unlisted-link' }
            $f = New-CdTestFixture $name
            $external = [IO.Path]::Combine($f.Root, 'external'); $null = [IO.Directory]::CreateDirectory($external)
            [IO.File]::WriteAllText([IO.Path]::Combine($external, 'keep'), 'owned target remains')
            $link = if ($selected) { [IO.Path]::Combine($f.Source, 'data', 'linked') } else { [IO.Path]::Combine($f.Source, 'linked') }
            Add-CeJunction $link $external; Save-CdTestPolicy $f
            Assert-CeRefused $f 'unexcluded'
            Assert-CdTest ([IO.File]::ReadAllText([IO.Path]::Combine($external, 'keep')) -ceq 'owned target remains') 'unexcluded target unchanged'
        }
    }
    Invoke-CdTestCase 'explicit exclusion or file approval beneath linked ancestor is never licensed' {
        $f = New-CdTestFixture 'linked-ancestor'
        $external = [IO.Path]::Combine($f.Root, 'external'); $null = [IO.Directory]::CreateDirectory($external)
        [IO.File]::WriteAllText([IO.Path]::Combine($external, 'inside.bin'), 'owned external bytes')
        Add-CeJunction ([IO.Path]::Combine($f.Source, 'data', 'linked')) $external
        $f.Policy.excludePaths = @('data/linked/inside.bin'); Save-CdTestPolicy $f
        Assert-CeRefused $f 'excluded-below' -BeforeArtifacts
        $f.Policy.excludePaths = @('data/linked'); $f.Policy.files = @('Client.exe', 'data/linked/inside.bin'); Save-CdTestPolicy $f
        Assert-CeRefused $f 'selected-below' -BeforeArtifacts
        Assert-CdTest ([IO.File]::ReadAllText([IO.Path]::Combine($external, 'inside.bin')) -ceq 'owned external bytes') 'ancestor target unchanged'
    }
    Invoke-CdTestCase 'missing and incorrectly cased excluded leaves remain invalid explicit paths' {
        $f = New-CdTestFixture 'exact-exclusions'
        $external = [IO.Path]::Combine($f.Root, 'external'); $null = [IO.Directory]::CreateDirectory($external)
        Add-CeJunction ([IO.Path]::Combine($f.Source, 'data', 'linked')) $external
        $index = 0
        foreach ($path in @('data/Linked', 'missing-link', 'data/missing.bin')) {
            $index++; $f.Policy.excludePaths = @($path); Save-CdTestPolicy $f
            Assert-CeRefused $f "bad-$index" -BeforeArtifacts
        }
    }
    Invoke-CdTestCase 'explicit file or asset-directory approval cannot also be excluded' {
        $f = New-CdTestFixture 'conflicting-approval'
        foreach ($path in @('Client.exe', 'data')) {
            $f.Policy.excludePaths = @($path); Save-CdTestPolicy $f
            Assert-CeRefused $f $path -BeforeArtifacts
        }
    }
    Invoke-CdTestCase 'a linked source root is still refused before policy or artifact processing' {
        $f = New-CdTestFixture 'root-link'; Save-CdTestPolicy $f
        $rootLink = [IO.Path]::Combine($f.Root, 'linked-source'); Add-CeJunction $rootLink $f.Source
        $before = Get-CeOrdinarySnapshot $f.Source; $outputs = Get-CdTestOutputs $f
        Assert-CdTestThrows { & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $rootLink -PolicyPath $f.PolicyPath `
            -PlanPath $outputs.Plan -ReportPath $outputs.Report }
        Assert-CdTest (-not [IO.File]::Exists($outputs.Plan) -and -not [IO.File]::Exists($outputs.Report)) 'root link produces no artifacts'
        Assert-CdTest ($before -ceq (Get-CeOrdinarySnapshot $f.Source)) 'ordinary root target unchanged'
    }
    $f = New-CdTestFixture 'file-links'
    $external = [IO.Path]::Combine($f.Root, 'external'); $null = [IO.Directory]::CreateDirectory($external)
    $target = [IO.Path]::Combine($external, 'kept.bin'); [IO.File]::WriteAllBytes($target, [byte[]](2, 4, 6, 8))
    if (Try-CeFileLink ([IO.Path]::Combine($f.Source, 'data', 'file-link.bin')) $target) {
        Invoke-CdTestCase 'excluded real and dangling file symbolic-link leaves need no target access' {
            Assert-CdTest (Try-CeFileLink ([IO.Path]::Combine($f.Source, 'data', 'dangling-link.bin')) ([IO.Path]::Combine($external, 'absent.bin'))) 'same existing privilege creates dangling file link'
            $f.Policy.excludePaths = @('data/file-link.bin', 'data/dangling-link.bin'); Save-CdTestPolicy $f
            $before = Get-CdSyntheticSnapshot $external
            $held = [IO.File]::Open($target, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            try {
                $outputs = Get-CdTestOutputs $f; Invoke-CdTestPlanner $f $outputs
                Assert-CeOriginalPlan $f $outputs $f.Policy.excludePaths
            } finally { $held.Dispose() }
            Assert-CdTest ($before -ceq (Get-CdSyntheticSnapshot $external)) 'symbolic-link target untouched'
            Assert-CdTest (-not [IO.File]::Exists([IO.Path]::Combine($external, 'absent.bin'))) 'dangling target not created'
            $f.Policy.excludePaths = @(); Save-CdTestPolicy $f; Assert-CeRefused $f 'without-exclusions'
        }
    } else {
        $skippedGroups.Add('file-symbolic-links: existing Windows privilege unavailable')
        Write-Host 'SKIP file-symbolic-link group: existing Windows privilege unavailable; no settings changed.'
    }
    [ordered]@{ outcome = $(if ($skippedGroups.Count) { 'passed-with-explicit-skips' } else { 'passed' })
        executedGroups = $script:caseCount; skippedGroups = $skippedGroups.ToArray()
        originalClientTouched = $false; published = $false } | ConvertTo-Json
} finally {
    foreach ($link in $exclusionTestLinks) {
        Assert-CeLinkIdentity $link
        if ($link.Directory) { [IO.Directory]::Delete($link.Path) } else { [IO.File]::Delete($link.Path) }
    }
    $resolved = [IO.Path]::GetFullPath($taskTestRoot)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.Path]::GetFileName($resolved).StartsWith('wolf-client-exclusion-test-', [StringComparison]::Ordinal) -or
        ([IO.File]::GetAttributes($resolved) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe excluded-link self-test cleanup target.' }
    [IO.Directory]::Delete($resolved, $true)
}
