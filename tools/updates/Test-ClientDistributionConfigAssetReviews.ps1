#requires -Version 7.4
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if (-not $IsWindows) { throw 'Config asset review tests require Windows file sharing.' }
. (Join-Path $PSScriptRoot 'ClientDistribution.NativeAssetTestHelpers.ps1')
$nrPrefix='wolf-config-asset-review-'
$nrRoot=[IO.Directory]::CreateTempSubdirectory($nrPrefix).FullName
$script:nrCases=0; $script:nrFixtures=0
$failures=[Collections.Generic.List[Exception]]::new()
function New-ConfigFixture([string]$Path,[string]$Category,[byte[]]$Bytes) {
    $f=New-NrFixture
    Remove-NrOwnedTree ([IO.Path]::Combine($f.Source,'data/map'))
    Remove-NrOwnedTree ([IO.Path]::Combine($f.Source,'data/main'))
    Put-NrFile $f $Path $Bytes
    $f.Policy.assetDirectories=@('data','ini')
    $f.Policy.nativeAssetReviews=@([ordered]@{path=$Path;collisionPath=$Path;collisionNodeType='file';category=$Category
        length=[long]$Bytes.Length;expectedSourceSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes));evidence='fixture-review-01'})
    return $f
}
try {
    foreach ($spec in @(@('ini/userdata.lua','native-server-type-lua',65536),@('ini/luacfg/userdata.dat','native-luacfg-userdata-catalog',1048576))) {
        $path=$spec[0]; $category=$spec[1]; $maximum=[int]$spec[2]
        Invoke-NrCase "exact config plan and stage $path" {
            $bytes=[byte[]](11,22,33,44,55); $f=New-ConfigFixture $path $category $bytes
            Invoke-NrPlan $f
            Change-NrPlan $f { param($p) ($p.files | Where-Object path -CEQ $path).baseSha256='B'*64 }
            $before=[Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($f.Plan))
            Invoke-NrStage $f
            $after=[Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($f.Output))
            try {
                $old=$before.RootElement.GetProperty('nativeAssetReviews')[0]; $new=$after.RootElement.GetProperty('nativeAssetReviews')[0]
                foreach($field in $old.EnumerateObject()){Assert-Nr ($field.Value.GetRawText() -ceq $new.GetProperty($field.Name).GetRawText()) 'review scalar retained'}
                $file=@($after.RootElement.GetProperty('files').EnumerateArray() | Where-Object { $_.GetProperty('path').GetString() -ceq $path })
                Assert-Nr ($file.Count -eq 1 -and $file[0].GetProperty('baseSha256').GetString() -ceq ('B'*64)) 'independent prior install hash preserved'
                Assert-Nr ($new.GetProperty('expectedSourceSha256').GetString() -cne ('B'*64)) 'source review digest differs from prior hash'
            } finally { $before.Dispose(); $after.Dispose() }
            Assert-Nr ([Convert]::ToBase64String([IO.File]::ReadAllBytes([IO.Path]::Combine($f.Stage,$path))) -ceq [Convert]::ToBase64String($bytes)) 'stage exact synthetic bytes'
        }
        Invoke-NrCase "config length limits $path" {
            foreach($length in @(0,1,$maximum,($maximum+1))) {
                $f=New-ConfigFixture $path $category ([byte[]]::new($length))
                if($length -eq 0 -or $length -gt $maximum){Assert-NrPlanRefused $f} else {Invoke-NrPlan $f; Invoke-NrStage $f}
            }
        }
        Invoke-NrCase "config identity and shape $path" {
            foreach($fault in @('missing','case-path','case-collision','collision','node','category','schema1','extra','hash','evidence','duplicate','unused','excluded')) {
                $f=New-ConfigFixture $path $category ([byte[]](1,2,3)); $row=$f.Policy.nativeAssetReviews[0]
                switch($fault) {
                    'missing' {$f.Policy.Remove('nativeAssetReviews')}; 'case-path' {$row.path=$path.ToUpperInvariant()}
                    'case-collision' {$row.collisionPath=$path.ToUpperInvariant()}; 'collision' {$row.collisionPath='ini/other.dat'}
                    'node' {$row.collisionNodeType='directory'}; 'category' {$row.category=if($category -ceq 'native-server-type-lua'){'native-luacfg-userdata-catalog'}else{'native-server-type-lua'}}
                    'schema1' {$f.Policy.schemaVersion=1}; 'extra' {$row.extra=$true}; 'hash' {$row.expectedSourceSha256='A'*63}
                    'evidence' {$row.evidence=''}; 'duplicate' {$f.Policy.nativeAssetReviews=@($row,$row)}
                    'unused' {$f.Policy.assetDirectories=@('data')}; 'excluded' {$f.Policy.excludePaths=@($path)}
                }
                Assert-NrPlanRefused $f
            }
        }
        Invoke-NrCase "held bytes and source sharing $path" {
            foreach($fault in @('mutated','digest','sharing')) {
                $f=New-ConfigFixture $path $category ([byte[]](1,2,3,4,5)); Invoke-NrPlan $f
                $source=[IO.Path]::Combine($f.Source,$path)
                if($fault -eq 'mutated') {$stamp=[IO.File]::GetLastWriteTimeUtc($source);[IO.File]::WriteAllBytes($source,[byte[]](6,7,8,9,10));[IO.File]::SetLastWriteTimeUtc($source,$stamp)}
                if($fault -eq 'digest') {Change-NrPlan $f {param($p) $p.nativeAssetReviews[0].expectedSourceSha256='A'*64}}
                $handle=$null
                try {
                    if($fault -eq 'sharing') {$handle=[IO.File]::Open($source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)}
                    Assert-NrStageRefused $f -Partial
                } finally {if($null -ne $handle){$handle.Dispose()}}
                if($fault -eq 'mutated'){Assert-Nr ([IO.File]::GetLastWriteTimeUtc($source) -eq $stamp) 'timestamp restored'}
            }
        }
    }
    Invoke-NrCase 'other userdata and private names remain quarantined' {
        foreach($path in @('ini/token_type.dat','ini/userdata/other.dat','ini/userdata2.lua','ini/account/userdata.lua','ini/private/userdata.lua','ini/userdata.lua.bak')) {
            $f=New-ConfigFixture $path 'native-server-type-lua' ([byte[]](1,2,3)); Assert-NrPlanRefused $f
        }
    }
} catch {$failures.Add($_.Exception)}
try {
    $temp=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))
    if(-not [IO.Path]::GetDirectoryName($nrRoot).Equals($temp,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($nrRoot).StartsWith($nrPrefix,[StringComparison]::Ordinal)){throw 'Unsafe config review cleanup root.'}
    Remove-NrOwnedTree $nrRoot
} catch {$failures.Add($_.Exception)}
if($failures.Count -eq 1){[Runtime.ExceptionServices.ExceptionDispatchInfo]::Capture($failures[0]).Throw()}
if($failures.Count -gt 1){throw [AggregateException]::new($failures)}
[ordered]@{outcome='passed';syntheticCases=$script:nrCases;fixtures=$script:nrFixtures;realClientTouched=$false;published=$false}|ConvertTo-Json
