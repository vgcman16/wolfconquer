Set-StrictMode -Version Latest

function Test-CdWithin([string]$Parent, [string]$Child) {
    return $Child.Equals($Parent, [StringComparison]::OrdinalIgnoreCase) -or
        $Child.StartsWith($Parent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-CdNoReparse([string]$Path) {
    for ($current = $Path; $current; $current = [IO.Path]::GetDirectoryName($current)) {
        try {
            if ([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse paths are refused.' }
        } catch [IO.FileNotFoundException] { } catch [IO.DirectoryNotFoundException] { }
    }
}

function Resolve-CdAbsolute([string]$Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path) -or $Path.StartsWith('\\') -or $Path.StartsWith('//') -or
        ($Path -split '[/\\]' | Where-Object { $_ -in '.', '..' })) { throw 'Use an explicit local absolute path without traversal.' }
    $full = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
    if ($full -eq [IO.Path]::GetPathRoot($full)) { throw 'A volume root is not a curated directory or artifact.' }
    $tail = $Path.Substring([IO.Path]::GetPathRoot($Path).Length).Replace('\', '/').TrimEnd('/')
    Assert-CdRelative $tail
    $full = Get-CdCanonicalSpelling $full
    Assert-CdNoReparse $full
    return $full
}

function Get-CdCanonicalSpelling([string]$Full) {
    # Resolve ordinary existing names through their actual parent entries. Reject
    # DOS short-name aliases rather than letting an output bypass source containment.
    $volume = [IO.Path]::GetPathRoot($Full)
    $current = $volume
    foreach ($segment in [IO.Path]::GetRelativePath($volume, $Full).Split([IO.Path]::DirectorySeparatorChar)) {
        $candidate = [IO.Path]::Combine($current, $segment)
        $actual = $null
        if ([IO.Directory]::Exists($current)) {
            $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            foreach ($child in @(Get-CdChildren $current)) {
                $name = [IO.Path]::GetFileName($child)
                if (-not $names.Add($name)) { throw 'Existing absolute path names contain a Windows case alias.' }
                if ($name.Equals($segment, [StringComparison]::OrdinalIgnoreCase)) { $actual = $child }
            }
        }
        if ($null -eq $actual -and ([IO.File]::Exists($candidate) -or [IO.Directory]::Exists($candidate))) {
            throw 'Short-name or noncanonical filesystem aliases are refused.'
        }
        $current = if ($null -ne $actual) { $actual } else { $candidate }
        Assert-CdNoReparse $current
    }
    return $current
}

function Assert-CdRelative([string]$Path) {
    if ([string]::IsNullOrEmpty($Path) -or $Path.Length -gt 1024 -or $Path.StartsWith('/') -or $Path -match '[\p{Cc}\\:<>"|?*]') {
        throw 'Policy paths must be safe relative Windows paths using forward slashes, never patterns.'
    }
    foreach ($segment in $Path.Split('/')) {
        if ($segment.Length -eq 0 -or $segment.Length -gt 255 -or $segment -in '.', '..' -or $segment.EndsWith('.') -or $segment.EndsWith(' ')) {
            throw 'A policy or source path has an unsafe segment.'
        }
        $stem = $segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant()
        if ($stem -in 'CON', 'PRN', 'AUX', 'NUL', 'CLOCK$', 'CONIN$', 'CONOUT$' -or $stem -match '^(COM|LPT)[1-9¹²³]$') {
            throw 'A path contains a reserved Windows device name.'
        }
    }
}

function Get-CdChildren([string]$Directory) {
    Assert-CdNoReparse $Directory
    $paths = [IO.Directory]::GetFileSystemEntries($Directory)
    Assert-CdNoReparse $Directory
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    return $paths
}

function Assert-CdExactExistingPath([string]$Source, [string]$Relative) {
    $current = $Source
    foreach ($segment in $Relative.Split('/')) {
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $matched = $false
        foreach ($child in @(Get-CdChildren $current)) {
            $name = [IO.Path]::GetFileName($child)
            if (-not $names.Add($name)) { throw 'Source names contain a Windows case alias.' }
            if ($name.Equals($segment, [StringComparison]::Ordinal)) { $matched = $true }
        }
        if (-not $matched) { throw 'A policy path is missing or differs from the actual source spelling.' }
        $current = [IO.Path]::Combine($current, $segment)
        Assert-CdNoReparse $current
    }
    return $current
}

function Assert-CdExactExcludedLeaf([string]$Source, [string]$Relative) {
    # This exception is only for an explicitly excluded FINAL entry. Never use
    # the leaf as a directory, follow its target, or read target metadata/data.
    Assert-CdRelative $Relative
    Assert-CdNoReparse $Source
    $slash = $Relative.LastIndexOf('/')
    $parent = if ($slash -lt 0) { $Source } else {
        Assert-CdExactExistingPath $Source ($Relative.Substring(0,$slash))
    }
    $leaf = $Relative.Substring($slash + 1)
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $actual = $null
    foreach ($child in @(Get-CdChildren $parent)) {
        $name = [IO.Path]::GetFileName($child)
        if (-not $names.Add($name)) { throw 'Excluded-leaf parent names contain a Windows case alias.' }
        if ($name.Equals($leaf, [StringComparison]::Ordinal)) { $actual = $child }
    }
    if ($null -eq $actual) { throw 'An excluded leaf is missing or differs from its exact source spelling.' }
    Assert-CdNoReparse $parent
    $attributes = [IO.File]::GetAttributes($actual)
    if ($attributes -band [IO.FileAttributes]::Device) { throw 'Device entries cannot be excluded as ordinary source leaves.' }
    Assert-CdNoReparse $parent
    return $actual
}

function Get-CdRisk([string]$Relative) {
    foreach ($segment in $Relative.Split('/')) {
        # This project replaces the legacy AutoP chain with its own updater.
        # The preserved AutoP sample fails the existing launcher-risk scan.
        # A filename quarantine is not a malware verdict or a clean-file scan.
        if ($segment -in 'AutoP.exe', '._cache_AutoP.exe') {
            return 'legacy-autop-bootstrap-requires-separate-curation'
        }
        if ($segment -match '(?i)^(accounts?|profiles?|users?|userdata|user[-_]?data|logs?|cache|caches|screenshots?|screen[-_]?shots|temp|tmp|debug|backups?|crashdumps?|\.git|\.svn|\.hg|\.vs|obj|bin|\.wolfconquer-updates|\.wolfconquer-cache)($|[._-])') {
            return 'private-local-or-control-path'
        }
        if ($segment -match '(?i)(\.(pem|key|pfx|p12|ppk|jks|snk|kdbx|wallet|pdb|dmp|dump|ilk|idb|iobj|ipdb|obj|log|bak|old|tmp|temp|orig|sql|db|db3|mdb|sqlite|sqlite3|sav|suo|user)$)|^(\.env($|\.)|id_(rsa|dsa|ecdsa|ed25519)($|\.)|\.git(ignore|attributes|modules)?$)' -or
            $segment -match '(?i)\.(bak|old|orig|tmp|temp)[._-]' -or
            $segment -match '(?i)^(accounts?|profiles?|credentials?|passwords?|secrets?|private[-_]?keys?|tokens?|cookies|sessions?)([._-].*)?$' -or
            $segment -match '(?i)(^|[._-])(secret|credential|password|private[-_]?key|release[-_]?private)([._-]|$)|^(last[-_]?login|login[-_]?(history|records?)|recent[-_]?players?)([._-]|$)' -or
            $segment -match '(?i)^(wolfconquer-updates\.json|trusted-release-keys\.json|WolfConquer\.(Updater|Launcher)(\..*)?)$') {
            return 'secret-private-or-generated-file-name'
        }
    }
    return $null
}

function Assert-CdFreshArtifact([string]$Path, [string]$Source, [string]$Publish) {
    if ((Test-CdWithin $Source $Path) -or (Test-CdWithin $Publish $Path)) { throw 'Review artifacts must be outside source and served publication roots.' }
    if ([IO.File]::Exists($Path) -or [IO.Directory]::Exists($Path)) { throw 'Artifact output already exists; nothing will be overwritten.' }
    $parent = [IO.Path]::GetDirectoryName($Path)
    if (-not [IO.Directory]::Exists($parent)) { throw 'Create the explicit artifact parent directory before planning.' }
    $name = [IO.Path]::GetFileName($Path)
    if ($name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'Unsafe artifact filename.' }
    Assert-CdNoReparse $Path
}

function Write-CdNewArtifact([string]$Path, [object]$Value) {
    Assert-CdNoReparse $Path
    $temporary = $Path + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    $temporaryOwned = $false
    try {
        $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes(($Value | ConvertTo-Json -Depth 16) + "`n")
        if ($bytes.LongLength -gt 64MB) { throw 'Artifact exceeds the publisher plan read bound (64 MiB).' }
        $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $temporaryOwned = $true
        try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
        Assert-CdNoReparse $temporary; Assert-CdNoReparse $Path
        [IO.File]::Move($temporary, $Path, $false)
    } finally {
        if ($temporaryOwned -and [IO.File]::Exists($temporary)) { Assert-CdNoReparse $temporary; [IO.File]::Delete($temporary) }
    }
}
