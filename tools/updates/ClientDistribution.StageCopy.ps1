Set-StrictMode -Version Latest

function New-CdsDirectory([string]$Path) {
    Assert-CdNoReparse $Path
    if ([IO.File]::Exists($Path) -or [IO.Directory]::Exists($Path)) { throw 'Staging only creates new directories; existing entries are refused.' }
    $parent = [IO.Path]::GetDirectoryName($Path)
    if (-not [IO.Directory]::Exists($parent)) { throw 'The explicit staging parent must already exist.' }
    # New-Item without Force rejects an existing directory instead of adopting it.
    # Escape the provider Path; Name is the literal single new child segment.
    $null = New-Item -ItemType Directory -Path ([WildcardPattern]::Escape($parent)) -Name ([IO.Path]::GetFileName($Path)) -ErrorAction Stop
    Assert-CdNoReparse $Path
    if (-not [IO.Directory]::Exists($Path)) { throw 'The exact ordinary staging directory was not created.' }
}

function Copy-CdsFile([object]$Observed, [string]$Destination) {
    Assert-CdNoReparse $Observed.Source
    Assert-CdNoReparse $Destination
    $source = $null; $target = $null; $digest = $null
    try {
        $source = [IO.File]::Open($Observed.Source, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        if ($source.Length -ne $Observed.Length -or [IO.File]::GetLastWriteTimeUtc($Observed.Source) -ne $Observed.WriteUtc) {
            throw 'An approved source changed after staging metadata preflight.'
        }
        $target = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $digest = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
        $buffer = [byte[]]::new(65536)
        [long]$copied = 0
        while (($count = $source.Read($buffer,0,$buffer.Length)) -gt 0) {
            $copied += $count
            if ($copied -gt $Observed.Length) { throw 'An approved source grew during copying.' }
            $target.Write($buffer,0,$count)
            $digest.AppendData($buffer,0,$count)
        }
        if ($copied -ne $Observed.Length -or $source.Length -ne $Observed.Length -or
            [IO.File]::GetLastWriteTimeUtc($Observed.Source) -ne $Observed.WriteUtc) { throw 'An approved source changed during copying.' }
        $target.Flush($true); $target.Position = 0
        $sourceHash = [Convert]::ToHexString($digest.GetHashAndReset())
        $targetHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($target))
        if ($target.Length -ne $copied -or $sourceHash -cne $targetHash) { throw 'Staging source/destination byte verification failed.' }
        Assert-CdNoReparse $Observed.Source
        Assert-CdNoReparse $Destination
        return $copied
    } finally {
        try { if ($null -ne $target) { $target.Dispose() } }
        finally {
            try { if ($null -ne $source) { $source.Dispose() } }
            finally { if ($null -ne $digest) { $digest.Dispose() } }
        }
    }
}

function Get-CdsDestination([string]$Stage, [string]$Relative, [Collections.Generic.HashSet[string]]$Created) {
    $segments = $Relative.Split('/')
    $current = $Stage
    for ($i = 0; $i -lt $segments.Length - 1; $i++) {
        $current = [IO.Path]::Combine($current,$segments[$i])
        if (-not $Created.Contains($current)) {
            New-CdsDirectory $current
            $null = $Created.Add($current)
        }
        Assert-CdNoReparse $current
    }
    return [IO.Path]::Combine($current,$segments[-1])
}
