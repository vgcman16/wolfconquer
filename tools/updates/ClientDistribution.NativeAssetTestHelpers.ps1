Set-StrictMode -Version Latest

function Assert-Nr([bool]$Condition, [string]$Message) { if (-not $Condition) { throw "Native asset self-test: $Message" } }
function Invoke-NrCase([string]$Name, [scriptblock]$Body) { & $Body; $script:nrCases++; Write-Host "PASS $Name" }
function Assert-NrThrows([scriptblock]$Body) {
    $failed = $false; try { & $Body | Out-Null } catch { $failed = $true }
    Assert-Nr $failed 'expected the actual entrypoint to refuse'
}
function Put-NrFile([object]$Fixture, [string]$Relative, [byte[]]$Bytes) {
    $path = [IO.Path]::Combine($Fixture.Source,$Relative)
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllBytes($path,$Bytes)
}
function New-NrFixture {
    $script:nrFixtures++
    $root = [IO.Path]::Combine($nrRoot,'case-' + $script:nrFixtures)
    $source = [IO.Path]::Combine($root,'source'); $review = [IO.Path]::Combine($root,'review')
    $null = [IO.Directory]::CreateDirectory($source); $null = [IO.Directory]::CreateDirectory($review)
    $f = [pscustomobject]@{ Root=$root; Source=$source; Review=$review; PolicyPath=[IO.Path]::Combine($review,'policy.json')
        Plan=[IO.Path]::Combine($review,'plan.json'); Report=[IO.Path]::Combine($review,'report.json')
        Stage=[IO.Path]::Combine($root,'stage'); Output=[IO.Path]::Combine($review,'rebased.json'); Policy=$null }
    Put-NrFile $f 'Client.exe' ([byte[]](1,2,3)); Put-NrFile $f 'data/base.bin' ([byte[]](8,9))
    $rows = @()
    foreach ($path in @('data/map/mapobj/city/obj/face.dds','data/main/LogS.BMP')) {
        $bytes = [byte[]](11,12,13,14,15); Put-NrFile $f $path $bytes
        $map = $path.EndsWith('.dds',[StringComparison]::Ordinal)
        $rows += [ordered]@{ path=$path; collisionPath=$(if ($map) { 'data/map/mapobj/city/obj' } else { $path })
            collisionNodeType=$(if ($map) { 'directory' } else { 'file' })
            category=$(if ($map) { 'native-map-object-directory' } else { 'native-logs-bitmap' })
            length=[long]$bytes.Length; expectedSourceSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
            evidence='2026-10-26' }
    }
    $f.Policy = [ordered]@{ schemaVersion=2; assetDirectories=@('data'); files=@('Client.exe'); excludePaths=@(); nativeAssetReviews=$rows
        publication=[ordered]@{ publishRoot=[IO.Path]::Combine($root,'published'); productId='synthetic-client'; channel='preview'; sequence=1
            contentBaseUrl='https://downloads.example.invalid/'; createdUtc='2026-01-01T00:00:00.1234000Z'; expiresUtc='2030-01-01T00:00:00+00:00'
            keyId='synthetic-key'; includeBundle=$true; allowLoopbackHttp=$false } }
    return $f
}
function Save-NrPolicy([object]$Fixture) { [IO.File]::WriteAllText($Fixture.PolicyPath,($Fixture.Policy | ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false)) }
function Invoke-NrPlan([object]$Fixture) {
    Save-NrPolicy $Fixture
    & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $Fixture.Source -PolicyPath $Fixture.PolicyPath -PlanPath $Fixture.Plan -ReportPath $Fixture.Report | Out-Null
}
function Invoke-NrStage([object]$Fixture) {
    & (Join-Path $PSScriptRoot 'New-ClientDistributionStage.ps1') -PlanPath $Fixture.Plan -StageDirectory $Fixture.Stage -OutputPlan $Fixture.Output | Out-Null
}
function Assert-NrPlanRefused([object]$Fixture) {
    Assert-NrThrows { Invoke-NrPlan $Fixture }
    Assert-Nr (-not [IO.File]::Exists($Fixture.Plan)) 'refusal must not emit a plan'
    Assert-Nr (-not [IO.Directory]::Exists($Fixture.Policy.publication.publishRoot)) 'no publication created'
}
function Assert-NrStageRefused([object]$Fixture, [switch]$Partial) {
    Assert-NrThrows { Invoke-NrStage $Fixture }
    Assert-Nr (-not [IO.File]::Exists($Fixture.Output)) 'refusal must not emit a rebased plan'
    if (-not $Partial) { Assert-Nr (-not [IO.Directory]::Exists($Fixture.Stage)) 'preflight refusal must not create stage' }
}
function Change-NrPlan([object]$Fixture, [scriptblock]$Change) {
    $raw = [IO.File]::ReadAllText($Fixture.Plan)
    $plan = $raw | ConvertFrom-Json -AsHashtable -Depth 16
    $document = [Text.Json.JsonDocument]::Parse($raw)
    try {
        foreach ($field in $document.RootElement.EnumerateObject()) {
            if ($field.Value.ValueKind -eq [Text.Json.JsonValueKind]::String) { $plan[$field.Name]=$field.Value.GetString() }
        }
        $rows=$document.RootElement.GetProperty('nativeAssetReviews')
        for($i=0;$i -lt $rows.GetArrayLength();$i++) {
            foreach($field in $rows[$i].EnumerateObject()) {
                if($field.Value.ValueKind -eq [Text.Json.JsonValueKind]::String){$plan.nativeAssetReviews[$i][$field.Name]=$field.Value.GetString()}
            }
        }
    } finally { $document.Dispose() }
    & $Change $plan
    [IO.File]::WriteAllText($Fixture.Plan,($plan | ConvertTo-Json -Depth 16),[Text.UTF8Encoding]::new($false))
}
function Remove-NrOwnedTree([string]$Path) {
    if (-not [IO.Path]::GetFullPath($Path).StartsWith($nrRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -and
        -not [IO.Path]::GetFullPath($Path).Equals($nrRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup target escaped its owned root.' }
    if ([IO.File]::GetAttributes($Path) -band [IO.FileAttributes]::ReparsePoint) { throw 'Cleanup refuses a reparse entry.' }
    if ([IO.Directory]::Exists($Path)) {
        foreach ($child in [IO.Directory]::GetFileSystemEntries($Path)) { Remove-NrOwnedTree $child }
        [IO.Directory]::Delete($Path,$false)
    } else { [IO.File]::Delete($Path) }
}
