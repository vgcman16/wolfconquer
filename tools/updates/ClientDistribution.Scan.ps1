Set-StrictMode -Version Latest

function Test-CdPolicyBelow([object]$Policy, [string]$Relative) {
    foreach ($path in ([string[]]$Policy.Files + [string[]]$Policy.Excludes)) {
        if ($path.StartsWith($Relative + '/', [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Visit-CdEntry([string]$Path, [string]$Relative, [bool]$ApprovedDirectory, [object]$Policy, [object]$State) {
    Assert-CdRelative $Relative
    if (-not $State.Seen.Add($Relative)) { throw 'Source paths contain a Windows case alias.' }
    if ($Policy.Excludes.Contains($Relative)) {
        # Exact exclusions stop at this entry, including a reparse leaf. Recheck
        # its ordinary parent chain and literal name/attributes without a target query.
        $actual = Assert-CdExactExcludedLeaf $State.Source $Relative
        if (-not $actual.Equals($Path, [StringComparison]::Ordinal)) { throw 'Excluded scan entry differs from its exact source path.' }
        $risk = Get-CdRisk $Relative
        $State.Excluded.Add([ordered]@{ path = $Relative; reason = $(if ($risk) { 'explicit-exclusion-quarantined-name' } else { 'explicit-policy-exclusion' }) })
        return
    }
    Assert-CdNoReparse $Path
    $attributes = [IO.File]::GetAttributes($Path)
    if ($attributes -band ([IO.FileAttributes]::Device -bor [IO.FileAttributes]::ReparsePoint)) { throw 'Device and reparse paths cannot be distributed.' }
    $directory = [bool]($attributes -band [IO.FileAttributes]::Directory)
    $risk = Get-CdRisk $Relative
    $approved = $ApprovedDirectory -or $Policy.Files.Contains($Relative) -or $Policy.Directories.Contains($Relative)
    $partial = -not $approved -and $directory -and (Test-CdPolicyBelow $Policy $Relative)
    $native = $Policy.NativeReviews
    $scope = Get-CdNativeObjectScope $native $Relative
    if ($scope -or $native.Files.ContainsKey($Relative)) {
        if ($directory) {
            if (-not $native.Prefixes.Contains($Relative)) { throw 'Native asset directory membership changed after review.' }
        } elseif (-not $native.Files.ContainsKey($Relative)) { throw 'Native asset file membership changed after review.' }
        $risk = $null; $approved = -not $directory; $partial = $directory
    }
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
    if ($native.Files.ContainsKey($Relative) -and $length -ne $native.Files[$Relative].length) { throw 'Native asset length changed after review.' }
    if ($length -gt [long]::MaxValue - $State.Bytes) { throw 'Source byte count overflow.' }
    $State.Files.Add($Relative, $length); $State.Bytes += $length
    if ($State.Files.Count -gt 200000) { throw 'Plan exceeds the publisher maximum file count.' }
}

function Get-CdScan([string]$Source, [object]$Policy) {
    $state = [pscustomobject]@{
        Source = $Source
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
    foreach ($review in $Policy.NativeReviews.Rows) {
        if (-not $state.Files.ContainsKey($review.path)) { throw 'A native asset review was not used by the completed scan.' }
    }
    Assert-CdNativeReviewSources $Source $Policy.NativeReviews
    return $state
}
