Set-StrictMode -Version Latest

function ConvertFrom-CdNativeReviewJson([Text.Json.JsonElement]$Element) {
    if ($Element.ValueKind -ne [Text.Json.JsonValueKind]::Array -or $Element.GetArrayLength() -lt 1 -or $Element.GetArrayLength() -gt 4096) { throw 'Native asset reviews must contain 1 through 4096 JSON objects.' }
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($entry in $Element.EnumerateArray()) {
        if ($entry.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw 'Native asset reviews must be objects.' }
        $row = [ordered]@{}
        foreach ($field in $entry.EnumerateObject()) {
            if ($field.Name -ceq 'length') {
                [long]$length = 0
                if ($field.Value.ValueKind -ne [Text.Json.JsonValueKind]::Number -or
                    -not $field.Value.TryGetInt64([ref]$length)) { throw 'Review length must be an integer.' }
                $row[$field.Name] = $length
            } else {
                if ($field.Value.ValueKind -ne [Text.Json.JsonValueKind]::String) { throw 'Review fields must be strings.' }
                $row[$field.Name] = $field.Value.GetString()
            }
        }
        $rows.Add($row)
    }
    return ,$rows.ToArray()
}

function New-CdEmptyNativeReview {
    return [pscustomobject]@{ Rows = @()
        Files = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
        Nodes = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
        Prefixes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal) }
}

function Assert-CdNativeReviewRisk([object]$Review) {
    $prefix = ''
    foreach ($segment in $Review.path.Split('/')) {
        $prefix = if ($prefix) { $prefix + '/' + $segment } else { $segment }
        if (-not (Get-CdRisk $segment)) { continue }
        if ($prefix.Equals($Review.collisionPath, [StringComparison]::Ordinal) -and
            (($Review.category -ceq 'native-map-object-directory' -and $segment -ceq 'obj') -or
             ($Review.category -ceq 'native-logs-bitmap' -and $prefix -ceq 'data/main/LogS.BMP') -or
             ($Review.category -ceq 'native-server-type-lua' -and $prefix -ceq 'ini/userdata.lua') -or
             ($Review.category -ceq 'native-luacfg-userdata-catalog' -and $prefix -ceq 'ini/luacfg/userdata.dat'))) { continue }
        throw 'A native asset review cannot waive another quarantined segment.'
    }
}

function Read-CdNativeAssetReviews([object]$Values) {
    if ($Values -isnot [Array] -or $Values.Count -lt 1 -or $Values.Count -gt 4096) { throw 'Schema2 requires 1 through 4096 native asset reviews.' }
    $result = New-CdEmptyNativeReview
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $nodes = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($row in $Values) {
        if ($row -isnot [Collections.IDictionary]) { throw 'Native asset reviews must be objects.' }
        Assert-CdKeys $row @('path','collisionPath','collisionNodeType','category','length','expectedSourceSha256','evidence')
        foreach ($field in @('path','collisionPath','collisionNodeType','category','expectedSourceSha256','evidence')) {
            if ($row[$field] -isnot [string]) { throw 'Native asset review fields must be strings.' }
        }
        Assert-CdRelative $row.path; Assert-CdRelative $row.collisionPath
        if (-not $paths.Add($row.path)) { throw 'Native review paths contain duplicates or case aliases.' }
        if (($row.length -isnot [long] -and $row.length -isnot [int]) -or $row.length -lt 0 -or $row.length -gt 4294967296) { throw 'Native review length is out of bounds.' }
        if ($row.expectedSourceSha256 -cnotmatch '\A[A-Fa-f0-9]{64}\z' -or $row.evidence -cnotmatch '\A[A-Za-z0-9._-]{1,128}\z') { throw 'Invalid native review digest or evidence identifier.' }
        if ($row.category -ceq 'native-map-object-directory') {
            if ($row.collisionNodeType -cne 'directory' -or -not $row.collisionPath.StartsWith('data/map/mapobj/', [StringComparison]::Ordinal) -or
                $row.collisionPath.Split('/').Count -lt 5 -or -not $row.collisionPath.EndsWith('/obj', [StringComparison]::Ordinal) -or
                -not $row.path.StartsWith($row.collisionPath + '/', [StringComparison]::Ordinal) -or
                [IO.Path]::GetExtension($row.path) -notin '.dds','.bmp','.png','.tga','.msk') { throw 'Invalid exact native map-object review.' }
            $prefix = $row.path.Substring(0, $row.path.LastIndexOf('/'))
            while ($prefix.Length -ge $row.collisionPath.Length) {
                $null = $result.Prefixes.Add($prefix)
                $slash = $prefix.LastIndexOf('/'); if ($slash -lt 0) { break }
                $prefix = $prefix.Substring(0,$slash)
            }
        } elseif ($row.category -ceq 'native-logs-bitmap') {
            if ($row.collisionNodeType -cne 'file' -or -not $row.path.Equals('data/main/LogS.BMP',[StringComparison]::Ordinal) -or
                -not $row.collisionPath.Equals($row.path,[StringComparison]::Ordinal)) { throw 'Invalid exact native bitmap review.' }
        } elseif ($row.category -ceq 'native-server-type-lua' -or $row.category -ceq 'native-luacfg-userdata-catalog') {
            $expected = if ($row.category -ceq 'native-server-type-lua') { 'ini/userdata.lua' } else { 'ini/luacfg/userdata.dat' }
            $maximum = if ($row.category -ceq 'native-server-type-lua') { 65536 } else { 1048576 }
            if ($row.collisionNodeType -cne 'file' -or -not $row.path.Equals($expected,[StringComparison]::Ordinal) -or
                -not $row.collisionPath.Equals($expected,[StringComparison]::Ordinal) -or
                $row.length -lt 1 -or $row.length -gt $maximum) { throw 'Invalid exact native data review.' }
        } else { throw 'Unsupported native asset review category.' }
        Assert-CdNativeReviewRisk $row
        if ($nodes.ContainsKey($row.collisionPath)) {
            $previous = $nodes[$row.collisionPath]
            if (-not $previous.collisionPath.Equals($row.collisionPath,[StringComparison]::Ordinal) -or $previous.category -cne $row.category -or
                $previous.collisionNodeType -cne $row.collisionNodeType) { throw 'Conflicting native collision nodes.' }
        } else { $nodes.Add($row.collisionPath,$row); $result.Nodes.Add($row.collisionPath,$row) }
        $result.Files.Add($row.path,$row)
    }
    foreach ($row in $Values) {
        $parent = $row.path
        while ($parent.Contains('/')) {
            $parent = $parent.Substring(0,$parent.LastIndexOf('/'))
            if ($result.Files.ContainsKey($parent)) { throw 'A reviewed file is also a directory prefix.' }
        }
    }
    $result.Rows = $Values
    return $result
}

function Get-CdNativeObjectScope([object]$Reviews, [string]$Relative) {
    $prefix = $Relative
    while ($prefix) {
        if ($Reviews.Nodes.ContainsKey($prefix) -and $Reviews.Nodes[$prefix].collisionNodeType -ceq 'directory') { return $prefix }
        $slash = $prefix.LastIndexOf('/'); if ($slash -lt 0) { break }
        $prefix = $prefix.Substring(0,$slash)
    }
    return $null
}

function Assert-CdNativeReviewPlan([object]$Reviews, [object[]]$Files) {
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($file in $Files) { $null = $paths.Add($file.path) }
    foreach ($row in $Reviews.Rows) {
        if (-not $paths.Contains($row.path)) { throw 'A native asset review is unused or differs from its exact plan path.' }
    }
}
