Set-StrictMode -Version Latest

function Assert-CdTest([bool]$Condition, [string]$Message) { if (-not $Condition) { throw "Self-test failed: $Message" } }
function Assert-CdTestThrows([scriptblock]$Action) {
    $refused = $false
    try { & $Action | Out-Null } catch { $refused = $true }
    Assert-CdTest $refused 'expected refusal'
}
function Invoke-CdTestCase([string]$Name, [scriptblock]$Action) {
    & $Action
    $script:caseCount++
    Write-Host "PASS $Name"
}
function New-CdTestFixture([string]$Name) {
    $root = [IO.Path]::Combine($taskTestRoot, $Name)
    $source = [IO.Path]::Combine($root, 'source')
    $review = [IO.Path]::Combine($root, 'review')
    $null = [IO.Directory]::CreateDirectory([IO.Path]::Combine($source, 'data'))
    $null = [IO.Directory]::CreateDirectory($review)
    [IO.File]::WriteAllBytes([IO.Path]::Combine($source, 'data', 'a.bin'), [byte[]](1, 2, 3))
    [IO.File]::WriteAllText([IO.Path]::Combine($source, 'Client.exe'), 'Synthetic bytes. Never execute.')
    return [pscustomobject]@{ Root = $root; Source = $source; Review = $review
        PolicyPath = [IO.Path]::Combine($review, 'policy.json')
        Policy = [ordered]@{ schemaVersion = 1; assetDirectories = @('data'); files = @('Client.exe'); excludePaths = @()
            publication = [ordered]@{ publishRoot = [IO.Path]::Combine($root, 'published'); productId = 'synthetic-client'
                channel = 'preview'; sequence = 1; contentBaseUrl = 'https://downloads.example.invalid/'
                createdUtc = '2026-01-01T00:00:00Z'; expiresUtc = '2030-01-01T00:00:00Z'; keyId = 'synthetic-key'
                includeBundle = $true; allowLoopbackHttp = $false } } }
}
function Save-CdTestPolicy([object]$Fixture) {
    [IO.File]::WriteAllText($Fixture.PolicyPath, ($Fixture.Policy | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
}
function Get-CdTestOutputs([object]$Fixture, [string]$Suffix = 'first') {
    return [pscustomobject]@{ Plan = [IO.Path]::Combine($Fixture.Review, "$Suffix-plan.json")
        Report = [IO.Path]::Combine($Fixture.Review, "$Suffix-report.json") }
}
function Invoke-CdTestPlanner([object]$Fixture, [object]$Outputs) {
    & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $Fixture.Source -PolicyPath $Fixture.PolicyPath `
        -PlanPath $Outputs.Plan -ReportPath $Outputs.Report | Out-Null
}
function Get-CdSyntheticSnapshot([string]$Source) {
    $paths = [IO.Directory]::GetFiles($Source, '*', [IO.SearchOption]::AllDirectories)
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $snapshot = [ordered]@{}
    foreach ($path in $paths) {
        $file = [IO.FileInfo]::new($path)
        $snapshot[[IO.Path]::GetRelativePath($Source, $path)] = [ordered]@{ length = $file.Length; writeTicks = $file.LastWriteTimeUtc.Ticks
            sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($path))) }
    }
    return ($snapshot | ConvertTo-Json -Depth 5 -Compress)
}
