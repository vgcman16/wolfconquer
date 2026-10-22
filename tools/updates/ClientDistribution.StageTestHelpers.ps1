Set-StrictMode -Version Latest

function Assert-DsTest([bool]$Condition, [string]$Message) { if (-not $Condition) { throw "Staging self-test failed: $Message" } }
function Invoke-DsCase([string]$Name, [scriptblock]$Action) {
    & $Action
    $script:stageTestCount++
    Write-Host "PASS $Name"
}
function New-DsFixture([string]$Name) {
    $root = [IO.Path]::Combine($stageTestRoot, $Name)
    $source = [IO.Path]::Combine($root, 'source')
    $review = [IO.Path]::Combine($root, 'review')
    $null = [IO.Directory]::CreateDirectory([IO.Path]::Combine($source, 'data'))
    $null = [IO.Directory]::CreateDirectory($review)
    $exe = [Text.Encoding]::UTF8.GetBytes('Synthetic bytes, not an executable. NEVER LAUNCH.')
    [IO.File]::WriteAllBytes([IO.Path]::Combine($source, 'Client.exe'), $exe)
    [IO.File]::WriteAllBytes([IO.Path]::Combine($source, 'data', 'asset.bin'), [byte[]](0..255))
    [IO.File]::WriteAllText([IO.Path]::Combine($source, 'unlisted-private.pem'), 'Synthetic unlisted text; not a real key.')
    return [pscustomobject]@{ Root = $root; Source = $source; Executable = $exe
        Stage = [IO.Path]::Combine($root, 'staged-client'); Output = [IO.Path]::Combine($review, 'staged-plan.json')
        Input = [IO.Path]::Combine($review, 'reviewed-plan.json')
        Plan = [ordered]@{ schemaVersion = 1; sourceRoot = $source; publishRoot = [IO.Path]::Combine($root, 'published')
            productId = 'synthetic-client'; channel = 'preview'; sequence = 2; contentBaseUrl = 'https://downloads.example.invalid/'
            createdUtc = '2026-01-01T00:00:00.1234000Z'; expiresUtc = '2030-01-01T00:00:00+00:00'; keyId = 'synthetic-key'
            files = @([ordered]@{ path = 'Client.exe'; baseSha256 = $null }, [ordered]@{ path = 'data/asset.bin'; baseSha256 = ('A' * 64) })
            includeBundle = $true; allowLoopbackHttp = $false } }
}
function Save-DsPlan([object]$Fixture) {
    [IO.File]::WriteAllText($Fixture.Input, ($Fixture.Plan | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
}
function Invoke-DsStage([object]$Fixture) {
    return & (Join-Path $PSScriptRoot 'New-ClientDistributionStage.ps1') -PlanPath $Fixture.Input `
        -StageDirectory $Fixture.Stage -OutputPlan $Fixture.Output
}
function Assert-DsRefused([object]$Fixture, [switch]$AllowPartial, [ref]$Failure) {
    $refused = $false
    try { Invoke-DsStage $Fixture | Out-Null } catch {
        $refused = $true
        if ($null -ne $Failure) { $Failure.Value = $_.Exception.Message }
    }
    Assert-DsTest $refused 'expected the real staging entrypoint to refuse'
    Assert-DsTest (-not [IO.File]::Exists($Fixture.Output)) 'failed operation must not emit a publisher plan'
    if (-not $AllowPartial) { Assert-DsTest (-not [IO.Directory]::Exists($Fixture.Stage)) 'preflight failure must not create staging tree' }
}
function Get-DsSnapshot([string]$Root) {
    $paths = [IO.Directory]::GetFiles($Root, '*', [IO.SearchOption]::AllDirectories)
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $snapshot = [ordered]@{}
    foreach ($path in $paths) {
        $file = [IO.FileInfo]::new($path)
        $snapshot[[IO.Path]::GetRelativePath($Root, $path)] = [ordered]@{ bytes = $file.Length; writeTicks = $file.LastWriteTimeUtc.Ticks
            sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($path))) }
    }
    return ($snapshot | ConvertTo-Json -Depth 6 -Compress)
}
function Add-DsJunction([string]$Link, [string]$Target) {
    New-Item -ItemType Junction -Path $Link -Value $Target | Out-Null
    $stageTestLinks.Add([pscustomobject]@{ Path = $Link; Directory = $true })
    Assert-DsTest ([bool]([IO.File]::GetAttributes($Link) -band [IO.FileAttributes]::ReparsePoint)) 'junction actually created'
}
function Assert-DsRebasedPlan([object]$Fixture) {
    $before = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($Fixture.Input))
    $after = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($Fixture.Output))
    try {
        Assert-DsTest ($after.RootElement.GetProperty('sourceRoot').GetString() -ceq $Fixture.Stage) 'output sourceRoot rebased to staged tree'
        $names = @($after.RootElement.EnumerateObject() | ForEach-Object { $_.Name })
        Assert-DsTest ($names.Count -eq 13) 'exact publisher root shape'
        foreach ($field in $before.RootElement.EnumerateObject()) {
            if ($field.Name -in 'sourceRoot', 'files') { continue }
            $actual = $after.RootElement.GetProperty($field.Name)
            if ($field.Value.ValueKind -eq [Text.Json.JsonValueKind]::String) {
                Assert-DsTest ($actual.ValueKind -eq [Text.Json.JsonValueKind]::String -and $actual.GetString() -ceq $field.Value.GetString()) 'publication strings preserved exactly'
            } else { Assert-DsTest ($actual.GetRawText() -ceq $field.Value.GetRawText()) 'publication scalar metadata preserved exactly' }
        }
        $oldFiles = $before.RootElement.GetProperty('files'); $newFiles = $after.RootElement.GetProperty('files')
        Assert-DsTest ($oldFiles.GetArrayLength() -eq $newFiles.GetArrayLength()) 'file count preserved'
        for ($index = 0; $index -lt $oldFiles.GetArrayLength(); $index++) {
            Assert-DsTest (@($newFiles[$index].EnumerateObject()).Count -eq 2) 'exact publisher file-entry shape'
            foreach ($field in @('path', 'baseSha256')) {
                Assert-DsTest ($oldFiles[$index].GetProperty($field).GetRawText() -ceq $newFiles[$index].GetProperty($field).GetRawText()) 'row order/path/base hash preserved, not reinterpreted'
            }
        }
    } finally { $before.Dispose(); $after.Dispose() }
}
