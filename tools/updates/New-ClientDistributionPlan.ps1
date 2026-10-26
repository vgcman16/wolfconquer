#requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string]$PolicyPath,
    [Parameter(Mandatory)][string]$PlanPath,
    [Parameter(Mandatory)][string]$ReportPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ClientDistribution.Paths.ps1')
. (Join-Path $PSScriptRoot 'ClientDistribution.Policy.ps1')
. (Join-Path $PSScriptRoot 'ClientDistribution.Scan.ps1')

$source = Resolve-CdAbsolute $SourceRoot
if (-not [IO.Directory]::Exists($source)) { throw 'SourceRoot must name an existing ordinary directory.' }
$policyFile = Resolve-CdAbsolute $PolicyPath
$planFile = Resolve-CdAbsolute $PlanPath
$reportFile = Resolve-CdAbsolute $ReportPath
if ($planFile.Equals($reportFile, [StringComparison]::OrdinalIgnoreCase) -or
    $planFile.Equals($policyFile, [StringComparison]::OrdinalIgnoreCase) -or $reportFile.Equals($policyFile, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Policy, plan and report must be distinct files.'
}
$policy = Read-CdPolicy $policyFile $source
Assert-CdFreshArtifact $planFile $source $policy.PublishRoot
Assert-CdFreshArtifact $reportFile $source $policy.PublishRoot
$scan = Get-CdScan $source $policy
$ready = $scan.Review.Count -eq 0 -and $scan.Blocked.Count -eq 0 -and $scan.Files.Count -gt 0
$report = [ordered]@{
    schemaVersion = 1; outcome = $(if ($ready) { 'planned-not-published' } else { 'review-required-no-plan' })
    sourceRoot = $source; policySha256 = $policy.Sha256; includedFiles = $scan.Files.Count; includedBytes = $scan.Bytes
    reviewRequiredCount = $scan.Review.Count; blockedCount = $scan.Blocked.Count; excludedEntryCount = $scan.Excluded.Count
    topLevelEntries = $scan.Top.ToArray(); reviewRequired = @($scan.Review | Select-Object -First 200)
    blocked = @($scan.Blocked | Select-Object -First 200); quarantined = @($scan.Quarantined | Select-Object -First 200)
    excluded = @($scan.Excluded | Select-Object -First 200)
    detailListsCappedAt = 200
    boundary = 'Metadata-only explicit policy plan. No source contents read, copied, hashed, packaged, signed or published. Name filtering cannot prove absence of secrets hidden inside approved files.'
}
$p = $policy.Publication
$files = @($scan.Files.GetEnumerator() | ForEach-Object { [ordered]@{ path = $_.Key; baseSha256 = $null } })
$plan = [ordered]@{
    schemaVersion = $policy.SchemaVersion; sourceRoot = $source; publishRoot = $policy.PublishRoot; productId = $p.productId; channel = $p.channel
    sequence = $p.sequence; contentBaseUrl = $p.contentBaseUrl; createdUtc = $p.createdUtc; expiresUtc = $p.expiresUtc; keyId = $p.keyId
    files = $files; includeBundle = $(if ($p.Contains('includeBundle')) { $p.includeBundle } else { $true })
    allowLoopbackHttp = $(if ($p.Contains('allowLoopbackHttp')) { $p.allowLoopbackHttp } else { $false })
}
if ($policy.SchemaVersion -eq 2) { $plan.nativeAssetReviews = $policy.NativeReviews.Rows }
Write-CdNewArtifact $reportFile $report
if (-not $ready) { throw 'Explicit policy review is incomplete or selects quarantined paths; report created, publisher plan not created.' }
Write-CdNewArtifact $planFile $plan
[ordered]@{ outcome = 'planned-not-published'; files = $scan.Files.Count; bytes = $scan.Bytes
    planPath = $planFile; reportPath = $reportFile } | ConvertTo-Json
