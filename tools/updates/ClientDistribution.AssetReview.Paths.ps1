Set-StrictMode -Version Latest

function Assert-CdNativeReviewTree([string]$Path, [string]$Relative, [string]$Collision, [object]$Reviews) {
    foreach ($child in @(Get-CdChildren $Path)) {
        $relativeChild = $Relative + '/' + [IO.Path]::GetFileName($child)
        Assert-CdRelative $relativeChild; Assert-CdNoReparse $child
        $attributes = [IO.File]::GetAttributes($child)
        if ($attributes -band ([IO.FileAttributes]::Device -bor [IO.FileAttributes]::ReparsePoint)) { throw 'Native review membership contains a nonordinary entry.' }
        if ($attributes -band [IO.FileAttributes]::Directory) {
            if (-not $Reviews.Prefixes.Contains($relativeChild)) { throw 'Native review membership contains an unreviewed directory.' }
            Assert-CdNativeReviewTree $child $relativeChild $Collision $Reviews
        } elseif (-not $Reviews.Files.ContainsKey($relativeChild) -or -not $Reviews.Files[$relativeChild].collisionPath.Equals($Collision,[StringComparison]::Ordinal)) {
            throw 'Native review membership contains an unreviewed file.'
        }
    }
}

function Assert-CdNativeReviewSources([string]$Source, [object]$Reviews) {
    foreach ($row in $Reviews.Rows) {
        $collision = Assert-CdExactExistingPath $Source $row.collisionPath
        $nodeAttributes = [IO.File]::GetAttributes($collision)
        if ($nodeAttributes -band ([IO.FileAttributes]::Device -bor [IO.FileAttributes]::ReparsePoint)) { throw 'Native collision nodes must be ordinary.' }
        $directory = [bool]($nodeAttributes -band [IO.FileAttributes]::Directory)
        if ($directory -ne ($row.collisionNodeType -ceq 'directory')) { throw 'Native collision node type differs from review.' }
        $file = Assert-CdExactExistingPath $Source $row.path
        $attributes = [IO.File]::GetAttributes($file)
        if ($attributes -band ([IO.FileAttributes]::Directory -bor [IO.FileAttributes]::Device -bor [IO.FileAttributes]::ReparsePoint) -or
            ([IO.FileInfo]::new($file)).Length -ne $row.length) { throw 'Native reviewed file type or length differs from review.' }
    }
    foreach ($node in $Reviews.Nodes.Values) {
        if ($node.collisionNodeType -ceq 'directory') {
            $path = Assert-CdExactExistingPath $Source $node.collisionPath
            Assert-CdNativeReviewTree $path $node.collisionPath $node.collisionPath $Reviews
        }
    }
}

function Assert-CdNativeReviewPolicy([string]$Source, [object]$Reviews, [object]$Directories, [object]$Files, [object]$Excludes) {
    foreach ($row in $Reviews.Rows) {
        $selected = $Files.Contains($row.path)
        foreach ($directory in $Directories) {
            if ($row.path.StartsWith($directory + '/', [StringComparison]::Ordinal)) { $selected = $true }
        }
        if (-not $selected) { throw 'A native asset review is not selected by policy.' }
        foreach ($exclude in $Excludes) {
            $node = $row.collisionPath
            if ($row.path.Equals($exclude, [StringComparison]::OrdinalIgnoreCase) -or
                $row.path.StartsWith($exclude + '/', [StringComparison]::OrdinalIgnoreCase) -or
                $node.Equals($exclude, [StringComparison]::OrdinalIgnoreCase) -or
                $node.StartsWith($exclude + '/', [StringComparison]::OrdinalIgnoreCase) -or
                ($row.collisionNodeType -ceq 'directory' -and $exclude.StartsWith($node + '/', [StringComparison]::OrdinalIgnoreCase))) {
                throw 'Native asset review conflicts with an exclusion.'
            }
        }
    }
    Assert-CdNativeReviewSources $Source $Reviews
}
