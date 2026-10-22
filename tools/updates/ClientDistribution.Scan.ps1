Set-StrictMode -Version Latest

function Test-CdExcluded([object]$Policy, [string]$Relative) {
    for ($candidate = $Relative; $candidate; ) {
        if ($Policy.Excludes.Contains($candidate)) { return $true }
        $slash = $candidate.LastIndexOf('/')
        $candidate = if ($slash -lt 0) { '' } else { $candidate.Substring(0, $slash) }
    }
    return $false
}

function Test-CdPolicyBelow([object]$Policy, [string]$Relative) {
    foreach ($path in ([string[]]$Policy.Files + [string[]]$Policy.Excludes)) {
        if ($path.StartsWith($Relative + '/', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Visit-CdEntry([string]$Path, [string]$Relative, [bool]$ApprovedDirectory, [object]$Policy, [object]$State) {
    Assert-CdRelative $Relative
    if (-not $State.Seen.Add($Relative)) { throw 'Source paths contain a Windows case alias.' }
    Assert-CdNoReparse $Path
    $attributes = [IO.File]::GetAttributes($Path)
    if ($attributes -band ([IO.FileAttributes]::Device -bor [IO.FileAttributes]::ReparsePoint)) { throw 'Device and reparse paths cannot be distributed.' }
    $directory = [bool]($attributes -band [IO.FileAttributes]::Directory)
    $risk = Get-CdRisk $Relative
    if (Test-CdExcluded $Policy $Relative) {
        $State.Excluded.Add([ordered]@{ path = $Relative; reason = $(if ($risk) { 'explicit-exclusion-quarantined-name' } else { 'explicit-policy-exclusion' }) })
        return # Never inspect a private/excluded directory's descendants or file contents.
    }
    $approved = $ApprovedDirectory -or $Policy.Files.Contains($Relative) -or $Policy.Directories.Contains($Relative)
    $partial = -not $approved -and $directory -and (Test-CdPolicyBelow $Policy $Relative)
    if ($risk) {
        $State.Quarantined.Add([ordered]@{ path = $Relative; reason = $risk })
        if ($approved -or $partial) { $State.Blocked.Add($Relative) }
        else { $State.Review.Add($Relative) }
        return
    }
    if (-not $approved -and -not $partial) { $State.Review.Add($Relative); return }
    if ($directory) {
        foreach ($child in @(Get-CdChildren $Path)) {
            Visit-CdEntry $child ($Relative + '/' + [IO.Path]::GetFileName($child)) $approved $Policy $State
        }
        return
    }
    if (-not $approved) { $State.Review.Add($Relative); return }
    $length = ([IO.FileInfo]::new($Path)).Length
    if ($length -gt [long]::MaxValue - $State.Bytes) { throw 'Source byte count overflow.' }
    $State.Files.Add($Relative, $length); $State.Bytes += $length
    if ($State.Files.Count -gt 200000) { throw 'Plan exceeds the publisher maximum file count.' }
}

function Get-CdScan([string]$Source, [object]$Policy) {
    $state = [pscustomobject]@{
        Files = [Collections.Generic.SortedDictionary[string, long]]::new([StringComparer]::Ordinal)
        Seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        Excluded = [Collections.Generic.List[object]]::new(); Quarantined = [Collections.Generic.List[object]]::new()
        Review = [Collections.Generic.List[string]]::new(); Blocked = [Collections.Generic.List[string]]::new()
        Bytes = [long]0; Top = [Collections.Generic.List[object]]::new()
    }
    foreach ($path in @(Get-CdChildren $Source)) {
        $fileCount = $state.Files.Count; $bytes = $state.Bytes; $excluded = $state.Excluded.Count
        $review = $state.Review.Count; $blocked = $state.Blocked.Count
        $relative = [IO.Path]::GetFileName($path)
        Visit-CdEntry $path $relative $false $Policy $state
        $status = if ($state.Blocked.Count -gt $blocked) { 'blocked' } elseif ($state.Review.Count -gt $review) { 'review-required' } elseif ($state.Files.Count -gt $fileCount) { 'included' } else { 'excluded-or-empty' }
        $state.Top.Add([ordered]@{ path = $relative; status = $status; includedFiles = $state.Files.Count - $fileCount
            includedBytes = $state.Bytes - $bytes; excludedEntries = $state.Excluded.Count - $excluded
            reviewRequiredEntries = $state.Review.Count - $review; blockedEntries = $state.Blocked.Count - $blocked })
    }
    return $state
}
