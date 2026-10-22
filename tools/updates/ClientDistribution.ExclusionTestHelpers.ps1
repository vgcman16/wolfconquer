Set-StrictMode -Version Latest

function Add-CeJunction([string]$Path, [string]$Target) {
    New-Item -ItemType Junction -Path $Path -Value $Target -ErrorAction Stop | Out-Null
    $exclusionTestLinks.Add([pscustomobject]@{ Path = $Path; Target = $Target; Directory = $true })
    Assert-CeLinkIdentity $exclusionTestLinks[$exclusionTestLinks.Count - 1]
}

function Try-CeFileLink([string]$Path, [string]$Target) {
    try { $null = [IO.File]::CreateSymbolicLink($Path, $Target) }
    catch [UnauthorizedAccessException] { return $false }
    catch [IO.IOException] {
        if (($_.Exception.HResult -band 65535) -eq 1314) { return $false }
        throw
    }
    $exclusionTestLinks.Add([pscustomobject]@{ Path = $Path; Target = $Target; Directory = $false })
    Assert-CeLinkIdentity $exclusionTestLinks[$exclusionTestLinks.Count - 1]
    return $true
}

function Assert-CeLinkIdentity([object]$Link) {
    $attributes = [IO.File]::GetAttributes($Link.Path)
    Assert-CdTest ([bool]($attributes -band [IO.FileAttributes]::ReparsePoint)) 'created leaf is still a reparse point'
    Assert-CdTest ([bool]($attributes -band [IO.FileAttributes]::Directory) -eq $Link.Directory) 'created leaf kind unchanged'
    $entry = if ($Link.Directory) { [IO.DirectoryInfo]::new($Link.Path) } else { [IO.FileInfo]::new($Link.Path) }
    $target = $entry.LinkTarget # Test-owned link identity only; the planner must never query this.
    Assert-CdTest (-not [string]::IsNullOrEmpty($target)) 'created link still has a target'
    if ($target.StartsWith('\??\') -or $target.StartsWith('\\?\')) { $target = $target.Substring(4) }
    Assert-CdTest ([IO.Path]::GetFullPath($target).Equals([IO.Path]::GetFullPath($Link.Target), [StringComparison]::OrdinalIgnoreCase)) 'created link exact target unchanged'
}

function Get-CeOrdinarySnapshot([string]$Root) {
    $snapshot = [Collections.Generic.SortedDictionary[string, string]]::new([StringComparer]::Ordinal)
    $pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($Root)
    while ($pending.Count -gt 0) {
        foreach ($path in [IO.Directory]::GetFileSystemEntries($pending.Pop())) {
            $attributes = [IO.File]::GetAttributes($path)
            if ($attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($attributes -band [IO.FileAttributes]::Directory) { $pending.Push($path); continue }
            $snapshot.Add([IO.Path]::GetRelativePath($Root, $path),
                [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($path))))
        }
    }
    return ($snapshot | ConvertTo-Json -Compress)
}

function Assert-CeOriginalPlan([object]$Fixture, [object]$Outputs, [string[]]$Exclusions) {
    $plan = [IO.File]::ReadAllText($Outputs.Plan) | ConvertFrom-Json
    $report = [IO.File]::ReadAllText($Outputs.Report) | ConvertFrom-Json
    Assert-CdTest ($plan.sourceRoot -ceq $Fixture.Source -and $plan.files.Count -eq 2) 'ordinary source root and original two-file plan retained'
    Assert-CdTest (($plan.files.path -join ',') -ceq 'Client.exe,data/a.bin') 'excluded links do not broaden the exact original file list'
    Assert-CdTest ($null -eq $plan.files[0].baseSha256 -and $null -eq $plan.files[1].baseSha256) 'no byte authority invented'
    Assert-CdTest ($report.outcome -ceq 'planned-not-published' -and $report.includedFiles -eq 2 -and
        $report.reviewRequiredCount -eq 0 -and $report.blockedCount -eq 0) 'successful metadata-only report'
    Assert-CdTest ($report.excludedEntryCount -eq $Exclusions.Count) 'only exact excluded leaves counted'
    $expected = $Exclusions.Clone(); [Array]::Sort($expected, [StringComparer]::Ordinal)
    $actual = [string[]]@($report.excluded | ForEach-Object { $_.path }); [Array]::Sort($actual, [StringComparer]::Ordinal)
    Assert-CdTest (($actual -join '|') -ceq ($expected -join '|')) 'report lists exact excluded leaves, never target descendants'
    Assert-CdTest (-not [IO.Directory]::Exists($Fixture.Policy.publication.publishRoot)) 'no publication created'
}

function Assert-CeRefused([object]$Fixture, [string]$Suffix, [switch]$BeforeArtifacts) {
    $outputs = Get-CdTestOutputs $Fixture $Suffix
    Assert-CdTestThrows { Invoke-CdTestPlanner $Fixture $outputs }
    Assert-CdTest (-not [IO.File]::Exists($outputs.Plan)) 'refusal never emits a publisher plan'
    if ($BeforeArtifacts) { Assert-CdTest (-not [IO.File]::Exists($outputs.Report)) 'unsafe explicit path refused before artifacts' }
}
