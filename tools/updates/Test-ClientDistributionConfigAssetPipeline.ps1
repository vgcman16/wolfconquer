#requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$UpdaterDllPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This synthetic pipeline check requires Windows.' }
. (Join-Path $PSScriptRoot 'ClientDistribution.Paths.ps1')
$updater = Resolve-CdAbsolute $UpdaterDllPath
if (-not [IO.File]::Exists($updater) -or [IO.Path]::GetFileName($updater) -cne 'WolfConquer.Updater.dll') { throw 'Pass the explicit existing compiled updater DLL.' }
$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
$owned = [IO.Path]::TrimEndingDirectorySeparator([IO.Directory]::CreateTempSubdirectory('wolf-native-pipeline-').FullName)
$resources = [Collections.Generic.List[IDisposable]]::new()
$results = [Collections.Generic.List[object]]::new()

function Assert-Pipeline([bool]$Condition, [string]$Message) { if (-not $Condition) { throw "Synthetic pipeline: $Message" } }
function Get-PipelineHash([byte[]]$Bytes) { return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)) }
function Get-PipelineSnapshot([string]$Root) {
    $rows = foreach ($file in @(Get-ChildItem -LiteralPath $Root -File -Recurse | Sort-Object FullName)) {
        Assert-CdNoReparse $file.FullName
        [IO.Path]::GetRelativePath($Root,$file.FullName) + '|' + $file.Length + '|' +
            (Get-PipelineHash ([IO.File]::ReadAllBytes($file.FullName))) + '|' + $file.LastWriteTimeUtc.Ticks
    }
    return $rows -join "`n"
}
function New-PipelineFixture([string]$Name) {
    $root=Join-Path $owned $Name; $source=Join-Path $root 'source'; $review=Join-Path $root 'review'
    $null=[IO.Directory]::CreateDirectory($source); $null=[IO.Directory]::CreateDirectory($review)
    $expected=[ordered]@{'ini/userdata.lua'=[byte[]](11,21,31,41,51); 'ini/luacfg/userdata.dat'=[byte[]](12,22,32,42,52,62)}
    $categories=[ordered]@{'ini/userdata.lua'='native-server-type-lua'; 'ini/luacfg/userdata.dat'='native-luacfg-userdata-catalog'}
    $rows=foreach($relative in $expected.Keys) {
        $target=Join-Path $source $relative; $null=[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)); [IO.File]::WriteAllBytes($target,$expected[$relative])
        [ordered]@{path=$relative;collisionPath=$relative;collisionNodeType='file';category=$categories[$relative];length=[long]$expected[$relative].Length;expectedSourceSha256=(Get-PipelineHash $expected[$relative]);evidence='synthetic-config-review'}
    }
    $key=[Security.Cryptography.ECDsa]::Create([Security.Cryptography.ECCurve+NamedCurves]::nistP256); $resources.Add($key)
    $verifier=[Security.Cryptography.ECDsa]::Create(); $resources.Add($verifier); $verifier.ImportParameters($key.ExportParameters($false))
    $keyPath=Join-Path $root 'ephemeral-signing.pem'; [IO.File]::WriteAllText($keyPath,$key.ExportPkcs8PrivateKeyPem(),[Text.UTF8Encoding]::new($false))
    $publish=Join-Path $root 'published'; $now=[DateTimeOffset]::UtcNow
    $policy=[ordered]@{schemaVersion=2;assetDirectories=@('ini');files=@();excludePaths=@();nativeAssetReviews=@($rows)
        publication=[ordered]@{publishRoot=$publish;productId='synthetic-config-pipeline';channel='preview';sequence=1;contentBaseUrl='https://downloads.example.invalid/';createdUtc=$now.AddMinutes(-1).ToString('O');expiresUtc=$now.AddHours(1).ToString('O');keyId='synthetic-pipeline-key';includeBundle=$true;allowLoopbackHttp=$false}}
    $f=[pscustomobject]@{Source=$source;Stage=(Join-Path $root 'stage');Publish=$publish;Key=$keyPath;Verifier=$verifier;Expected=$expected;Paths=@($expected.Keys)
        Policy=(Join-Path $review 'policy.json');Plan=(Join-Path $review 'plan.json');Report=(Join-Path $review 'report.json');Rebased=(Join-Path $review 'rebased.json')}
    [IO.File]::WriteAllText($f.Policy,($policy|ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false)); return $f
}
function Read-PipelinePlan([string]$Path) {
    $raw=[IO.File]::ReadAllText($Path)
    $plan=$raw | ConvertFrom-Json -AsHashtable -Depth 16
    $document=[Text.Json.JsonDocument]::Parse($raw)
    try {
        foreach($field in $document.RootElement.EnumerateObject()) {
            if($field.Value.ValueKind -eq [Text.Json.JsonValueKind]::String){$plan[$field.Name]=$field.Value.GetString()}
        }
        foreach($collection in @('files','nativeAssetReviews')) {
            $rows=$document.RootElement.GetProperty($collection)
            for($i=0;$i -lt $rows.GetArrayLength();$i++) {
                foreach($field in $rows[$i].EnumerateObject()) {
                    if($field.Value.ValueKind -eq [Text.Json.JsonValueKind]::String){$plan[$collection][$i][$field.Name]=$field.Value.GetString()}
                }
            }
        }
    } finally {$document.Dispose()}
    return $plan
}
function Invoke-PipelineStage([object]$F) {
    & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $F.Source -PolicyPath $F.Policy -PlanPath $F.Plan -ReportPath $F.Report | Out-Null
    $plan=Read-PipelinePlan $F.Plan
    foreach($entry in $plan.files){$entry.baseSha256='B'*64}
    [IO.File]::WriteAllText($F.Plan,($plan|ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false))
    & (Join-Path $PSScriptRoot 'New-ClientDistributionStage.ps1') -PlanPath $F.Plan -StageDirectory $F.Stage -OutputPlan $F.Rebased | Out-Null
    $after=Read-PipelinePlan $F.Rebased
    Assert-Pipeline ($plan.schemaVersion -eq 2 -and $after.schemaVersion -eq 2 -and $after.sourceRoot -ceq $F.Stage) 'schema2 stage rebase'
    Assert-Pipeline (($plan.nativeAssetReviews|ConvertTo-Json -Depth 8 -Compress) -ceq ($after.nativeAssetReviews|ConvertTo-Json -Depth 8 -Compress)) 'all review scalars retained'
    foreach($relative in $F.Paths) {
        $row=@($after.nativeAssetReviews|Where-Object path -CEQ $relative); $entry=@($after.files|Where-Object path -CEQ $relative)
        Assert-Pipeline ($row.Count -eq 1 -and $entry.Count -eq 1 -and $row[0].evidence -ceq 'synthetic-config-review' -and $row[0].expectedSourceSha256 -ceq (Get-PipelineHash $F.Expected[$relative]) -and $entry[0].baseSha256 -ceq ('B'*64) -and $entry[0].baseSha256 -cne $row[0].expectedSourceSha256) 'source digest and independently different base hash retained'
        Assert-Pipeline ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $F.Stage $relative))) -ceq [Convert]::ToBase64String($F.Expected[$relative])) 'stage matches independent fixture bytes'
    }
}
function Invoke-PipelinePublish([object]$F) {
    $start = [Diagnostics.ProcessStartInfo]::new($dotnet)
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    foreach ($argument in @($updater,'publish','--plan',$F.Rebased,'--key',$F.Key)) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $resources.Add($process); $process.StartInfo=$start
    Assert-Pipeline ($process.Start()) 'owned updater CLI started'
    $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(30000)) { $process.Kill($true); $process.WaitForExit(); throw 'Owned synthetic publish CLI exceeded30seconds.' }
    return [pscustomobject]@{Exit=$process.ExitCode;Stdout=$stdout.GetAwaiter().GetResult();Stderr=$stderr.GetAwaiter().GetResult()}
}
function Read-PipelineEnvelope([byte[]]$Bytes, [object]$F, [string]$Product) {
    $envelope = [Text.UTF8Encoding]::new($false,$true).GetString($Bytes) | ConvertFrom-Json -AsHashtable
    Assert-Pipeline ($envelope.Count -eq 3 -and $envelope.keyId -ceq 'synthetic-pipeline-key') 'signed envelope has the pinned synthetic key ID'
    $payload = [Convert]::FromBase64String($envelope.payload); $signature = [Convert]::FromBase64String($envelope.signature)
    # Documented byte[] overload, explicit P1363 format; no span/reflection bridge or compilation.
    $valid = $F.Verifier.VerifyData([byte[]]$payload,[byte[]]$signature,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
    Assert-Pipeline ($signature.Length -eq 64 -and $valid) 'exact payload signature verifies using the generated public parameters'
    $manifest = [Text.UTF8Encoding]::new($false,$true).GetString($payload) | ConvertFrom-Json -AsHashtable
    Assert-Pipeline ($manifest.schemaVersion -eq 1 -and $manifest.productId -ceq $Product -and $manifest.channel -ceq 'preview' -and $manifest.sequence -eq 1) 'signed release identity/schema is exact'
    return $manifest
}
function Assert-PipelineRelease([object]$F) {
    $feed = Join-Path $F.Publish 'feeds/synthetic-config-pipeline-preview.json'
    $index = Read-PipelineEnvelope ([IO.File]::ReadAllBytes($feed)) $F 'synthetic-config-pipeline.index'
    Assert-Pipeline (@($index.files).Count -eq 1 -and $index.files[0].path -ceq 'manifest.json') 'index points only to the full manifest'
    $pointer = $index.files[0]; $fullPath = Join-Path $F.Publish ('blobs/' + $pointer.sha256.ToLowerInvariant()); $full = [IO.File]::ReadAllBytes($fullPath)
    Assert-Pipeline ($full.LongLength -eq $pointer.length -and (Get-PipelineHash $full) -ceq $pointer.sha256) 'full envelope length/hash matches signed index and immutable blob name'
    $manifest = Read-PipelineEnvelope $full $F 'synthetic-config-pipeline'
    Assert-Pipeline (@($manifest.files).Count -eq $F.Paths.Count) 'exact two-file payload'
    $actual = @($manifest.files | ForEach-Object { $_.path } | Sort-Object); $expected = @($F.Paths | Sort-Object)
    Assert-Pipeline (($actual -join '|') -ceq ($expected -join '|')) 'only exact config paths are signed'
    foreach ($entry in $manifest.files) {
        $blob = [IO.File]::ReadAllBytes((Join-Path $F.Publish ('blobs/' + $entry.sha256.ToLowerInvariant())))
        Assert-Pipeline ($blob.LongLength -eq $entry.length -and (Get-PipelineHash $blob) -ceq $entry.sha256) 'payload blob matches signed length/hash'
        Assert-Pipeline ([Convert]::ToBase64String($blob) -ceq [Convert]::ToBase64String($F.Expected[$entry.path]) -and $entry.baseSha256 -ceq ('B'*64) -and $entry.sha256 -cne $entry.baseSha256) 'published bytes match independent fixture and base hash'
    }
    $bundle = [IO.File]::ReadAllBytes((Join-Path $F.Publish ('blobs/' + $manifest.bundle.sha256.ToLowerInvariant())))
    Assert-Pipeline ($bundle.LongLength -eq $manifest.bundle.length -and (Get-PipelineHash $bundle) -ceq $manifest.bundle.sha256) 'bundle transport matches its signed length/hash'
}
function Remove-PipelineOwned([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not ($full.Equals($owned,[StringComparison]::OrdinalIgnoreCase) -or $full.StartsWith($owned+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))) { throw 'Cleanup escaped owned synthetic fixture root.' }
    Assert-CdNoReparse $full
    if ([IO.Directory]::Exists($full)) {
        foreach ($child in [IO.Directory]::GetFileSystemEntries($full)) { Remove-PipelineOwned $child }
        [IO.Directory]::Delete($full,$false)
    } elseif ([IO.File]::Exists($full)) { [IO.File]::Delete($full) }
}

$primary=$null; $cleanup=[Collections.Generic.List[Exception]]::new()
try {
    Assert-CdNoReparse $owned
    Assert-Pipeline ([IO.Path]::GetDirectoryName($owned).Equals([IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath())),[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($owned).StartsWith('wolf-native-pipeline-',[StringComparison]::Ordinal)) 'fresh cleanup root confined to local Temp'
    foreach($case in @('positive','changed-stage','token','wrong-category','wrong-path')) {
        $f=New-PipelineFixture $case; $original=Get-PipelineSnapshot $f.Source; Invoke-PipelineStage $f
        if($case -ne 'positive') {
            $feed=Join-Path $f.Publish 'feeds/synthetic-config-pipeline-preview.json'; $null=[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($feed))
            [IO.File]::WriteAllBytes($feed,[byte[]](91,92,93,94)); $before=Get-PipelineSnapshot $f.Publish
            if($case -eq 'changed-stage') {
                $path=Join-Path $f.Stage 'ini/userdata.lua'; $stamp=[IO.File]::GetLastWriteTimeUtc($path)
                [IO.File]::WriteAllBytes($path,[byte[]](51,41,31,21,11)); [IO.File]::SetLastWriteTimeUtc($path,$stamp)
            } else {
                $plan=Read-PipelinePlan $f.Rebased
                $row=@($plan.nativeAssetReviews|Where-Object path -CEQ 'ini/userdata.lua')[0]
                $file=@($plan.files|Where-Object path -CEQ $row.path)[0]
                if($case -eq 'token') {$row.path='ini/token_type.dat';$row.collisionPath='ini/token_type.dat';$file.path='ini/token_type.dat'}
                if($case -eq 'wrong-category') {$row.category='native-luacfg-userdata-catalog'}
                if($case -eq 'wrong-path') {$row.collisionPath='ini/other.dat'}
                [IO.File]::WriteAllText($f.Rebased,($plan|ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false))
            }
        }
        $staged=Get-PipelineSnapshot $f.Stage; $result=Invoke-PipelinePublish $f
        if($case -eq 'positive') {Assert-Pipeline ($result.Exit -eq 0 -and -not $result.Stderr) 'CLI signs valid staged plan';Assert-PipelineRelease $f}
        else {
            Assert-Pipeline ($result.Exit -ne 0 -and -not $result.Stdout.Contains('Published ')) 'CLI refuses invalid reviewed input'
            Assert-Pipeline ($before -ceq (Get-PipelineSnapshot $f.Publish)) 'preexisting feed and publication tree unchanged'
        }
        Assert-Pipeline ($original -ceq (Get-PipelineSnapshot $f.Source) -and $staged -ceq (Get-PipelineSnapshot $f.Stage)) 'publisher leaves source and staged files unchanged'
        $results.Add([pscustomobject]@{case=$case;outcome='passed'})
    }
} catch {$primary=$_}
foreach($resource in $resources){try{$resource.Dispose()}catch{$cleanup.Add($_.Exception)}}
try{Remove-PipelineOwned $owned}catch{$cleanup.Add($_.Exception)}
if($null -ne $primary -and $cleanup.Count -gt 0){throw [AggregateException]::new('Synthetic pipeline failed and cleanup also failed.',[Exception[]](@($primary.Exception)+$cleanup.ToArray()))}
if($null -ne $primary){$PSCmdlet.ThrowTerminatingError($primary)}
if($cleanup.Count -gt 0){throw [AggregateException]::new('Synthetic pipeline cleanup failed.',$cleanup.ToArray())}
[ordered]@{outcome='passed';cases=$results.ToArray();scope='Synthetic owned planner -> stage -> signed updater only; no live client or deployment claim.'}|ConvertTo-Json -Depth 4