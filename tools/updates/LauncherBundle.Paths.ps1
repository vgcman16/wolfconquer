Set-StrictMode -Version Latest

function Assert-LbNoReparse([string]$Path) {
    for ($current = $Path; $current; $current = [IO.Path]::GetDirectoryName($current)) {
        try {
            if ([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Launcher bundles refuse reparse files, directories, and ancestors.'
            }
        } catch [IO.FileNotFoundException] { } catch [IO.DirectoryNotFoundException] { }
    }
}

function Resolve-LbPath([string]$Path, [switch]$NewFile) {
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.Length -gt 1024 -or
        $Path -cnotmatch '^[A-Z]:\\[^\\]' -or $Path -match '[\x00-\x1f\x7f/<>"|?*]' -or
        $Path.Substring(2).Contains(':')) {
        throw 'Use an explicit local absolute Windows path with an uppercase drive and backslash separators.'
    }
    $segments = $Path.Substring(3).Split('\')
    foreach ($segment in $segments) {
        if ($segment.Length -eq 0 -or $segment.Length -gt 255 -or $segment -in '.', '..' -or
            $segment.EndsWith('.') -or $segment.EndsWith(' ')) { throw 'Path aliases and traversal are refused.' }
        $stem = $segment.Split('.')[0].ToUpperInvariant()
        if ($stem -in 'CON','PRN','AUX','NUL','CLOCK$','CONIN$','CONOUT$' -or
            $stem -match '^(COM|LPT)[1-9¹²³]$') { throw 'Windows device path aliases are refused.' }
    }
    if (-not $Path.Equals([IO.Path]::GetFullPath($Path), [StringComparison]::Ordinal)) {
        throw 'Use the canonical path spelling without aliases.'
    }
    Assert-LbNoReparse $Path
    $current = [IO.Path]::GetPathRoot($Path)
    for ($i = 0; $i -lt $segments.Length; $i++) {
        if (-not [IO.Directory]::Exists($current)) { throw 'The explicit path parent must already exist.' }
        $exact = $false; $alias = $false
        foreach ($child in [IO.Directory]::EnumerateFileSystemEntries($current)) {
            $name = [IO.Path]::GetFileName($child)
            if ($name.Equals($segments[$i], [StringComparison]::Ordinal)) { $exact = $true }
            elseif ($name.Equals($segments[$i], [StringComparison]::OrdinalIgnoreCase)) { $alias = $true }
        }
        $last = $i -eq $segments.Length - 1
        if ($NewFile -and $last) {
            if ($exact -or $alias -or [IO.File]::Exists($Path) -or [IO.Directory]::Exists($Path)) {
                throw 'Output already exists or aliases an existing entry; nothing is overwritten.'
            }
        } elseif (-not $exact -or $alias) { throw 'Existing path spelling must match exactly; short-name/case aliases are refused.' }
        $current = [IO.Path]::Combine($current, $segments[$i])
        Assert-LbNoReparse $current
    }
    return $Path
}

function Test-LbWithin([string]$Parent, [string]$Child) {
    return $Child.Equals($Parent, [StringComparison]::OrdinalIgnoreCase) -or
        $Child.StartsWith($Parent + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Get-LbRuntimeFiles {
    return @('WolfConquer.Launcher.deps.json', 'WolfConquer.Launcher.dll', 'WolfConquer.Launcher.exe',
        'WolfConquer.Launcher.runtimeconfig.json', 'WolfConquer.Updater.deps.json',
        'WolfConquer.Updater.dll', 'WolfConquer.Updater.exe', 'WolfConquer.Updater.runtimeconfig.json',
        'WolfConquer.Updates.dll')
}
