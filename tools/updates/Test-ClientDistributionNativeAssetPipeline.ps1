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
    $root = Join-Path $owned $Name; $source = Join-Path $root 'source'; $review = Join-Path $root 'review'
    $null = [IO.Directory]::CreateDirectory($source); $null = [IO.Directory]::CreateDirectory($review)
    $paths = @('data/catalog.dat','data/main/LogS.BMP','data/map/mapobj/city/obj/face.dds')
    for ($i=0; $i -lt $paths.Count; $i++) {
        $path = Join-Path $source $paths[$i]; $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
        [IO.File]::WriteAllBytes($path,[byte[]]@(($i+11),($i+21),($i+31),($i+41),($i+51)))
    }
    $rows = foreach ($relative in $paths[1..2]) {
        $bytes = [IO.File]::ReadAllBytes((Join-Path $source $relative)); $map = $relative.EndsWith('.dds',[StringComparison]::Ordinal)
        [ordered]@{path=$relative;collisionPath=$(if($map){'data/map/mapobj/city/obj'}else{$relative})
            collisionNodeType=$(if($map){'directory'}else{'file'});category=$(if($map){'native-map-object-directory'}else{'native-logs-bitmap'})
            length=[long]$bytes.Length;expectedSourceSha256=(Get-PipelineHash $bytes);evidence='synthetic-pipeline-review'}
    }
    $key = [Security.Cryptography.ECDsa]::Create([Security.Cryptography.ECCurve+NamedCurves]::nistP256)
    $resources.Add($key); $verifier = [Security.Cryptography.ECDsa]::Create(); $resources.Add($verifier)
    $verifier.ImportParameters($key.ExportParameters($false))
    $keyPath = Join-Path $root 'ephemeral-signing.pem'
    [IO.File]::WriteAllText($keyPath,$key.ExportPkcs8PrivateKeyPem(),[Text.UTF8Encoding]::new($false))
    $publish = Join-Path $root 'published'; $now = [DateTimeOffset]::UtcNow
    $policy = [ordered]@{schemaVersion=2;assetDirectories=@('data');files=@();excludePaths=@();nativeAssetReviews=@($rows)
        publication=[ordered]@{publishRoot=$publish;productId='synthetic-native-pipeline';channel='preview';sequence=1
            contentBaseUrl='https://downloads.example.invalid/';createdUtc=$now.AddMinutes(-1).ToString('O');expiresUtc=$now.AddHours(1).ToString('O')
            keyId='synthetic-pipeline-key';includeBundle=$true;allowLoopbackHttp=$false}}
    $f = [pscustomobject]@{Source=$source;Stage=(Join-Path $root 'stage');Publish=$publish;Key=$keyPath;Verifier=$verifier;Paths=$paths
        Policy=(Join-Path $review 'policy.json');Plan=(Join-Path $review 'plan.json');Report=(Join-Path $review 'report.json');Rebased=(Join-Path $review 'rebased.json')}
    [IO.File]::WriteAllText($f.Policy,($policy | ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false))
    return $f
}
function Invoke-PipelineStage([object]$F) {
    & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $F.Source -PolicyPath $F.Policy -PlanPath $F.Plan -ReportPath $F.Report | Out-Null
    & (Join-Path $PSScriptRoot 'New-ClientDistributionStage.ps1') -PlanPath $F.Plan -StageDirectory $F.Stage -OutputPlan $F.Rebased | Out-Null
    $before = [IO.File]::ReadAllText($F.Plan) | ConvertFrom-Json -AsHashtable
    $after = [IO.File]::ReadAllText($F.Rebased) | ConvertFrom-Json -AsHashtable
    Assert-Pipeline ($before.schemaVersion -eq 2 -and $after.schemaVersion -eq 2 -and $after.sourceRoot -ceq $F.Stage) 'schema2 plan is rebased to the real stage'
    Assert-Pipeline (($before.nativeAssetReviews | ConvertTo-Json -Depth 8 -Compress) -ceq ($after.nativeAssetReviews | ConvertTo-Json -Depth 8 -Compress)) 'review fields survive staging unchanged'
    foreach ($relative in $F.Paths) {
        Assert-Pipeline ((Get-PipelineHash ([IO.File]::ReadAllBytes((Join-Path $F.Source $relative)))) -ceq
            (Get-PipelineHash ([IO.File]::ReadAllBytes((Join-Path $F.Stage $relative))))) 'stage contains exact original bytes'
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
    $feed = Join-Path $F.Publish 'feeds/synthetic-native-pipeline-preview.json'
    $index = Read-PipelineEnvelope ([IO.File]::ReadAllBytes($feed)) $F 'synthetic-native-pipeline.index'
    Assert-Pipeline (@($index.files).Count -eq 1 -and $index.files[0].path -ceq 'manifest.json') 'index points only to the full manifest'
    $pointer = $index.files[0]; $fullPath = Join-Path $F.Publish ('blobs/' + $pointer.sha256.ToLowerInvariant()); $full = [IO.File]::ReadAllBytes($fullPath)
    Assert-Pipeline ($full.LongLength -eq $pointer.length -and (Get-PipelineHash $full) -ceq $pointer.sha256) 'full envelope length/hash matches signed index and immutable blob name'
    $manifest = Read-PipelineEnvelope $full $F 'synthetic-native-pipeline'
    Assert-Pipeline (@($manifest.files).Count -eq $F.Paths.Count) 'exact three-file payload'
    $actual = @($manifest.files | ForEach-Object { $_.path } | Sort-Object); $expected = @($F.Paths | Sort-Object)
    Assert-Pipeline (($actual -join '|') -ceq ($expected -join '|')) 'only selected obj/bitmap/ordinary paths are signed'
    foreach ($entry in $manifest.files) {
        $blob = [IO.File]::ReadAllBytes((Join-Path $F.Publish ('blobs/' + $entry.sha256.ToLowerInvariant())))
        Assert-Pipeline ($blob.LongLength -eq $entry.length -and (Get-PipelineHash $blob) -ceq $entry.sha256) 'payload blob matches signed length/hash'
        Assert-Pipeline ((Get-PipelineHash $blob) -ceq (Get-PipelineHash ([IO.File]::ReadAllBytes((Join-Path $F.Stage $entry.path))))) 'published bytes equal verified stage bytes'
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
    Assert-Pipeline ([IO.Path]::GetDirectoryName($owned).Equals([IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath())),[StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($owned).StartsWith('wolf-native-pipeline-',[StringComparison]::Ordinal)) 'fresh cleanup root identity is confined to local Temp'
    foreach ($changed in @($false,$true)) {
        $f=New-PipelineFixture $(if($changed){'changed-stage'}else{'positive'}); $original=Get-PipelineSnapshot $f.Source
        Invoke-PipelineStage $f
        if ($changed) {
            $path=Join-Path $f.Stage 'data/map/mapobj/city/obj/face.dds'; $stamp=[IO.File]::GetLastWriteTimeUtc($path)
            $bytes=[IO.File]::ReadAllBytes($path); $bytes[0]=$bytes[0] -bxor 0x5A; [IO.File]::WriteAllBytes($path,$bytes); [IO.File]::SetLastWriteTimeUtc($path,$stamp)
        }
        $stage=Get-PipelineSnapshot $f.Stage; $result=Invoke-PipelinePublish $f
        if ($changed) {
            Assert-Pipeline ($result.Exit -eq 1 -and $result.Stderr.Contains('Reviewed source content differs from its review.')) 'actual CLI refuses changed reviewed stage bytes'
            Assert-Pipeline (-not $result.Stdout.Contains('Published ') -and -not [IO.Directory]::Exists($f.Publish)) 'rejection creates no feed or blobs'
        } else { Assert-Pipeline ($result.Exit -eq 0 -and -not $result.Stderr) 'actual CLI publishes the valid synthetic plan'; Assert-PipelineRelease $f }
        Assert-Pipeline ($original -ceq (Get-PipelineSnapshot $f.Source) -and $stage -ceq (Get-PipelineSnapshot $f.Stage)) 'publishing preserves original and staged bytes/names/times'
        $results.Add([pscustomobject]@{case=$(if($changed){'changed-stage-refused'}else{'signed-pipeline'});outcome='passed'})
    }
} catch { $primary=$_ }
foreach ($resource in $resources) { try { $resource.Dispose() } catch { $cleanup.Add($_.Exception) } }
try { Remove-PipelineOwned $owned } catch { $cleanup.Add($_.Exception) }
if ($null -ne $primary -and $cleanup.Count -gt 0) { throw [AggregateException]::new('Synthetic pipeline failed and cleanup also failed.',[Exception[]](@($primary.Exception)+$cleanup.ToArray())) }
if ($null -ne $primary) { $PSCmdlet.ThrowTerminatingError($primary) }
if ($cleanup.Count -gt 0) { throw [AggregateException]::new('Synthetic pipeline cleanup failed.',$cleanup.ToArray()) }
[ordered]@{outcome='passed';cases=$results.ToArray();scope='Synthetic owned planner -> stage -> compiled updater publish only. No actual client, network, deployment or parity claim.'} | ConvertTo-Json -Depth 4
