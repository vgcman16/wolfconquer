#requires -Version 7.4
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Native review tests require Windows file sharing.' }
. (Join-Path $PSScriptRoot 'ClientDistribution.NativeAssetTestHelpers.ps1')
$nrPrefix = 'wolf-native-asset-review-'
$nrRoot = [IO.Directory]::CreateTempSubdirectory($nrPrefix).FullName
$script:nrCases = 0; $script:nrFixtures = 0
$failures = [Collections.Generic.List[Exception]]::new()
try {
    Invoke-NrCase 'metadata-only planning and exact review rebase preserve originals' {
        $f = New-NrFixture; Save-NrPolicy $f
        $held = [Collections.Generic.List[IDisposable]]::new()
        try {
            foreach ($row in $f.Policy.nativeAssetReviews) { $held.Add([IO.File]::Open([IO.Path]::Combine($f.Source,$row.path),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)) }
            Invoke-NrPlan $f
        } finally { foreach ($handle in $held) { $handle.Dispose() } }
        Change-NrPlan $f { param($p) $p.files[0].baseSha256='B'*64 }
        $before = [IO.File]::ReadAllText($f.Plan) | ConvertFrom-Json -AsHashtable -Depth 16
        Assert-Nr ($before.schemaVersion -eq 2 -and $before.files.Count -eq 4) 'actual schema2 exact selection'
        Invoke-NrStage $f
        $a = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($f.Plan))
        $b = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($f.Output))
        try {
            Assert-Nr ($b.RootElement.GetProperty('sourceRoot').GetString() -ceq $f.Stage) 'source root rebased'
            Assert-Nr ($b.RootElement.GetProperty('files')[0].GetProperty('baseSha256').GetString() -ceq ('B'*64)) 'prior-install base digest preserved independently'
            $old = $a.RootElement.GetProperty('nativeAssetReviews'); $new = $b.RootElement.GetProperty('nativeAssetReviews')
            Assert-Nr ($new.GetArrayLength() -eq 2) 'reviews retained'
            for ($i=0; $i -lt 2; $i++) {
                foreach ($field in $old[$i].EnumerateObject()) { Assert-Nr ($field.Value.GetRawText() -ceq $new[$i].GetProperty($field.Name).GetRawText()) 'exact review scalar retained' }
                Assert-Nr ($new[$i].GetProperty('evidence').GetString() -ceq '2026-10-26') 'date-like evidence stays string'
            }
        } finally { $a.Dispose(); $b.Dispose() }
        foreach ($row in $f.Policy.nativeAssetReviews) {
            $source = [IO.File]::ReadAllBytes([IO.Path]::Combine($f.Source,$row.path)); $staged = [IO.File]::ReadAllBytes([IO.Path]::Combine($f.Stage,$row.path))
            Assert-Nr ([Convert]::ToBase64String($source) -ceq [Convert]::ToBase64String($staged)) 'exact staged and original bytes'
        }
        Assert-Nr (-not [IO.Directory]::Exists($f.Policy.publication.publishRoot)) 'no signing or publication'
    }
    Invoke-NrCase 'supported native extensions retain exact case while matching suffix case-insensitively' {
        foreach ($extension in @('.dds','.BMP','.png','.tga','.MSK')) {
            $f = New-NrFixture; $row=$f.Policy.nativeAssetReviews[0]
            $path=$row.path.Substring(0,$row.path.LastIndexOf('.')) + $extension
            if(-not $row.path.Equals($path,[StringComparison]::Ordinal)){[IO.File]::Move([IO.Path]::Combine($f.Source,$row.path),[IO.Path]::Combine($f.Source,$path))}; $row.path=$path
            Invoke-NrPlan $f
            Assert-Nr ([IO.File]::Exists($f.Plan)) 'actual planner accepts only listed suffixes'
        }
    }
    Invoke-NrCase 'strict seven-field shape bounds categories paths and review counts' {
        foreach ($fault in @('extra','missing','null','empty','object','duplicate','case-alias','unknown-category','wrong-node','wrong-collision','wrong-case','bad-evidence','bad-hash','negative','oversized','fraction','boolean','schema1','many')) {
            $f=New-NrFixture; $row=$f.Policy.nativeAssetReviews[0]
            switch ($fault) {
                'extra' { $row.extra=$true }; 'missing' { $row.Remove('evidence') }; 'null' { $f.Policy.nativeAssetReviews=$null }
                'empty' { $f.Policy.nativeAssetReviews=@() }; 'object' { $f.Policy.nativeAssetReviews=[ordered]@{} }
                'duplicate' { $f.Policy.nativeAssetReviews=@($row,$row) }
                'case-alias' { $copy=[ordered]@{}; foreach($k in $row.Keys){$copy[$k]=$row[$k]}; $copy.path=$row.path.ToUpperInvariant(); $f.Policy.nativeAssetReviews=@($row,$copy) }
                'unknown-category' { $row.category='arbitrary-asset' }; 'wrong-node' { $row.collisionNodeType='file' }
                'wrong-collision' { $row.collisionPath='data/map/mapobj/city' }; 'wrong-case' { $row.path=$row.path.Replace('city','City') }
                'bad-evidence' { $row.evidence="proof`n" }; 'bad-hash' { $row.expectedSourceSha256='A'*63 }
                'negative' { $row.length=-1 }; 'oversized' { $row.length=[long]4294967297 }; 'fraction' { $row.length=5.5 }; 'boolean' { $row.length=$true }
                'schema1' { $f.Policy.schemaVersion=1 }; 'many' { $f.Policy.nativeAssetReviews=@(1..4097 | ForEach-Object { $row }) }
            }
            Assert-NrPlanRefused $f
        }
    }
    Invoke-NrCase 'no private or AutoP segment can be hidden by a reviewed obj ancestor' {
        foreach ($suffix in @('secret.dds','accounts/face.dds','AutoP.exe/face.dds','nested/obj/face.dds','face.exe')) {
            $f=New-NrFixture; $row=$f.Policy.nativeAssetReviews[0]
            [IO.File]::Delete([IO.Path]::Combine($f.Source,$row.path)); $row.path=$row.collisionPath+'/'+$suffix
            Put-NrFile $f $row.path ([byte[]](11,12,13,14,15))
            Assert-NrPlanRefused $f
        }
        foreach ($path in @('ini/token_type.dat','ini/userdata.lua','ini/luacfg/userdata.dat')) {
            $f=New-NrFixture; $row=$f.Policy.nativeAssetReviews[1]; $row.path=$path; $row.collisionPath=$path
            Put-NrFile $f $path ([byte[]](11,12,13,14,15)); $f.Policy.assetDirectories=@('data','ini')
            Assert-NrPlanRefused $f
        }
    }
    Invoke-NrCase 'new files empty directories unused exclusions missing leaves and length changes refuse metadata planning' {
        foreach ($fault in @('file','empty-dir','unused','excluded','missing','length','node-type')) {
            $f=New-NrFixture; $row=$f.Policy.nativeAssetReviews[0]
            switch($fault) {
                'file' { Put-NrFile $f ($row.collisionPath+'/new.dds') ([byte[]](1)) }
                'empty-dir' { $null=[IO.Directory]::CreateDirectory([IO.Path]::Combine($f.Source,$row.collisionPath,'empty')) }
                'unused' { $f.Policy.assetDirectories=@() }
                'excluded' { $f.Policy.excludePaths=@($row.path) }
                'missing' { [IO.File]::Delete([IO.Path]::Combine($f.Source,$row.path)) }
                'length' { $row.length=4 }
                'node-type' { $p=[IO.Path]::Combine($f.Source,$row.collisionPath); [IO.Directory]::Move($p,$p+'-moved'); [IO.File]::WriteAllBytes($p,[byte[]](1)) }
            }
            Assert-NrPlanRefused $f
        }
    }
    Invoke-NrCase 'direct staged plan cannot forge drop mismatch or leave unused native review metadata' {
        foreach ($fault in @('drop','schema1','unused','case','extra-field','digest-shape','private')) {
            $f=New-NrFixture; Invoke-NrPlan $f
            Change-NrPlan $f { param($p)
                switch($fault) {
                    'drop' { $p.Remove('nativeAssetReviews') }; 'schema1' { $p.schemaVersion=1 }
                    'unused' { $p.files=@($p.files | Where-Object path -cne $p.nativeAssetReviews[0].path) }
                    'case' { $p.nativeAssetReviews[0].path=$p.nativeAssetReviews[0].path.ToUpperInvariant() }
                    'extra-field' { $p.nativeAssetReviews[0].extra=$true }; 'digest-shape' { $p.nativeAssetReviews[0].expectedSourceSha256='bad' }
                    'private' { $p.nativeAssetReviews[0].path=$p.nativeAssetReviews[0].collisionPath+'/secret.dds' }
                }
            }
            Assert-NrStageRefused $f
        }
    }
    Invoke-NrCase 'staging rechecks actual complete obj membership before creating a destination' {
        foreach ($kind in @('file','empty-dir')) {
            $f=New-NrFixture; Invoke-NrPlan $f; $root=$f.Policy.nativeAssetReviews[0].collisionPath
            if($kind -eq 'file'){Put-NrFile $f ($root+'/new.dds') ([byte[]](1))}else{$null=[IO.Directory]::CreateDirectory([IO.Path]::Combine($f.Source,$root,'empty'))}
            Assert-NrStageRefused $f
        }
    }
    Invoke-NrCase 'same-length changed bytes with restored time fail the held-source digest check' {
        $f=New-NrFixture; Invoke-NrPlan $f; $path=[IO.Path]::Combine($f.Source,$f.Policy.nativeAssetReviews[0].path)
        $time=[IO.File]::GetLastWriteTimeUtc($path); $bytes=[byte[]](21,22,23,24,25)
        [IO.File]::WriteAllBytes($path,$bytes); [IO.File]::SetLastWriteTimeUtc($path,$time)
        Assert-NrStageRefused $f -Partial
        Assert-Nr ([Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -ceq [Convert]::ToBase64String($bytes)) 'rejected copy preserves changed source'
        Assert-Nr ([IO.File]::GetLastWriteTimeUtc($path) -eq $time) 'metadata timestamp cannot substitute for digest'
    }
    Invoke-NrCase 'real source sharing failure preserves original and supported fresh retry succeeds' {
        $f=New-NrFixture; Invoke-NrPlan $f; $path=[IO.Path]::Combine($f.Source,$f.Policy.nativeAssetReviews[0].path)
        $held=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
        try { Assert-NrStageRefused $f -Partial } finally { $held.Dispose() }
        $f.Stage=[IO.Path]::Combine($f.Root,'retry-stage'); $f.Output=[IO.Path]::Combine($f.Review,'retry-rebased.json')
        Invoke-NrStage $f
        Assert-Nr ([IO.File]::Exists($f.Output)) 'fresh actual stage succeeds after real sharing release'
    }
} catch { $failures.Add($_.Exception) }
try {
    $temp=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))
    if(-not [IO.Path]::GetDirectoryName($nrRoot).Equals($temp,[StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.Path]::GetFileName($nrRoot).StartsWith($nrPrefix,[StringComparison]::Ordinal)) { throw 'Unsafe native review cleanup root.' }
    Remove-NrOwnedTree $nrRoot
} catch { $failures.Add($_.Exception) }
if($failures.Count -eq 1){[Runtime.ExceptionServices.ExceptionDispatchInfo]::Capture($failures[0]).Throw()}
if($failures.Count -gt 1){throw [AggregateException]::new($failures)}
[ordered]@{ outcome='passed'; syntheticCases=$script:nrCases; fixtures=$script:nrFixtures; realClientTouched=$false; published=$false } | ConvertTo-Json
