#requires -Version 7.4
<#
.SYNOPSIS
Copies only the exact files of an approved, successful distribution plan offline.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlanPath,
    [Parameter(Mandatory)][string]$StageDirectory,
    [Parameter(Mandatory)][string]$OutputPlan
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ClientDistribution.Paths.ps1')
. (Join-Path $PSScriptRoot 'ClientDistribution.Policy.ps1')
. (Join-Path $PSScriptRoot 'ClientDistribution.StagePlan.ps1')
. (Join-Path $PSScriptRoot 'ClientDistribution.StageCopy.ps1')

$inputPlan = Resolve-CdAbsolute $PlanPath
$stage = Resolve-CdAbsolute $StageDirectory
$output = Resolve-CdAbsolute $OutputPlan
if (-not $output.EndsWith('.json', [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputPlan must name a fresh JSON artifact.' }
$plan = Read-CdsPlan $inputPlan
$source = Resolve-CdAbsolute $plan.sourceRoot
$publish = Resolve-CdAbsolute $plan.publishRoot
if (-not [IO.Directory]::Exists($source)) { throw 'The approved source root is unavailable.' }
if ([IO.File]::Exists($publish)) { throw 'Publication root cannot be a file.' }
if ((Test-CdWithin $source $publish) -or (Test-CdWithin $publish $source) -or
    (Test-CdWithin $source $stage) -or (Test-CdWithin $stage $source) -or
    (Test-CdWithin $publish $stage) -or (Test-CdWithin $stage $publish)) {
    throw 'Original source, new stage, and served publication roots must be pairwise disjoint.'
}
if ((Test-CdWithin $source $inputPlan) -or (Test-CdWithin $publish $inputPlan) -or (Test-CdWithin $stage $inputPlan)) {
    throw 'The reviewed input plan must be outside source, stage, and served roots.'
}
if ([IO.File]::Exists($stage) -or [IO.Directory]::Exists($stage)) { throw 'StageDirectory must be NEW; existing entries are never adopted.' }
if (-not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($stage))) { throw 'Create the explicit staging parent before running this tool.' }
Assert-CdFreshArtifact $output $source $publish
if (Test-CdWithin $stage $output) { throw 'OutputPlan must be outside the staging directory.' }
$observed = Get-CdsSourceFiles $source $plan.files
# Retain all approved publication semantics, file order, and prior-install hashes.
# Only the source root is rebased; no signature or prior review hash is invented.
$rebased = [ordered]@{}
foreach ($key in $plan.Keys) { $rebased[$key] = $plan[$key] }
$rebased.sourceRoot = $stage
if ([Text.UTF8Encoding]::new($false,$true).GetByteCount(($rebased | ConvertTo-Json -Depth 16) + "`n") -gt 64MB) {
    throw 'The rebased plan exceeds the 64 MiB publication-plan bound.'
}
$stageCreated = $false; $planWritten = $false; $copiedFiles = 0; [long]$copiedBytes = 0
try {
    New-CdsDirectory $stage
    $stageCreated = $true
    $created = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $null = $created.Add($stage)
    foreach ($file in $observed.Files) {
        $destination = Get-CdsDestination $stage $file.Relative $created
        $copiedBytes += Copy-CdsFile $file $destination
        $copiedFiles++
    }
    # All content copies must succeed before a fresh publisher plan can appear.
    Assert-CdFreshArtifact $output $source $publish
    if (Test-CdWithin $stage $output) { throw 'OutputPlan escaped the approved artifact location.' }
    Write-CdNewArtifact $output $rebased
    $planWritten = $true
    [ordered]@{ outcome='staged-not-published'; files=$copiedFiles; bytes=$copiedBytes
        sourceRoot=$source; stageRoot=$stage; planPath=$output
        boundary='Offline exact-list current-byte copy verified by SHA256 readback. Names-only input is not historical byte authority. No signing, packaging, publication, client edits, or process launch.' } | ConvertTo-Json
} catch {
    if ($stageCreated) {
        if ($planWritten) { throw [IO.IOException]::new("Verified stage and rebased plan were completed at '$stage' and '$output', but reporting the result failed.", $_.Exception) }
        throw [IO.IOException]::new("Staging failed; no completed rebased output plan was written. Partial owned stage remains at '$stage' (completed files=$copiedFiles, bytes=$copiedBytes; an incomplete file may also remain). No automatic tree deletion was attempted.", $_.Exception)
    }
    throw [IO.IOException]::new("Staging directory creation failed at '$stage'; the requested entry was not adopted. No completed output plan was written and no cleanup was attempted. Inspect any partial or contended directory before retrying.", $_.Exception)
}
