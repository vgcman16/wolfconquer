#requires -Version 7.4
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'These synthetic self-tests require Windows, including actual junction refusal checks.' }
. (Join-Path $PSScriptRoot 'ClientDistribution.TestHelpers.ps1')
$taskTestRoot = [IO.Directory]::CreateTempSubdirectory('wolf-client-plan-test-').FullName
$ownedLinks = [Collections.Generic.List[string]]::new()
$script:caseCount = 0
try {
    Invoke-CdTestCase 'deterministic publisher shape and unchanged source' {
        $f = New-CdTestFixture 'determinism'; Save-CdTestPolicy $f
        $before = Get-CdSyntheticSnapshot $f.Source
        $a = Get-CdTestOutputs $f 'a'; $b = Get-CdTestOutputs $f 'b'
        Invoke-CdTestPlanner $f $a; Invoke-CdTestPlanner $f $b
        Assert-CdTest ([Convert]::ToBase64String([IO.File]::ReadAllBytes($a.Plan)) -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes($b.Plan))) 'deterministic plan bytes'
        Assert-CdTest ([Convert]::ToBase64String([IO.File]::ReadAllBytes($a.Report)) -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes($b.Report))) 'deterministic report bytes'
        $plan = Get-Content -LiteralPath $a.Plan -Raw | ConvertFrom-Json
        Assert-CdTest (($plan.files.path -join ',') -ceq 'Client.exe,data/a.bin') 'ordinal publisher file order'
        Assert-CdTest ($plan.schemaVersion -eq 1 -and $plan.sourceRoot -ceq $f.Source -and $plan.files.Count -eq 2) 'publisher plan shape'
        Assert-CdTest ($null -eq $plan.files[0].baseSha256 -and $plan.includeBundle) 'fresh plan explicit null bases'
        Assert-CdTest ($before -ceq (Get-CdSyntheticSnapshot $f.Source)) 'source files, contents and write times unchanged'
        Assert-CdTest (-not [IO.Directory]::Exists($f.Policy.publication.publishRoot)) 'no publication tree or ZIP created'
    }
    Invoke-CdTestCase 'unknown top-level requires explicit exclusion' {
        $f = New-CdTestFixture 'unknown'; [IO.File]::WriteAllText([IO.Path]::Combine($f.Source, 'unknown.txt'), 'synthetic unknown')
        Save-CdTestPolicy $f; $a = Get-CdTestOutputs $f
        Assert-CdTestThrows { Invoke-CdTestPlanner $f $a }
        $report = Get-Content -LiteralPath $a.Report -Raw | ConvertFrom-Json
        Assert-CdTest ($report.reviewRequiredCount -eq 1 -and -not [IO.File]::Exists($a.Plan)) 'review report without publisher plan'
        $f.Policy.excludePaths = @('unknown.txt'); Save-CdTestPolicy $f; $b = Get-CdTestOutputs $f 'approved'
        Invoke-CdTestPlanner $f $b
        $plan = Get-Content -LiteralPath $b.Plan -Raw | ConvertFrom-Json
        Assert-CdTest ($plan.files.Count -eq 2 -and 'unknown.txt' -cnotin $plan.files.path) 'exclusion does not broaden inclusion'
    }
    Invoke-CdTestCase 'UTC timestamp JSON strings retain exact spelling and reject numeric or non-UTC input' {
        $f = New-CdTestFixture 'utc-strings'
        $createdText = '2026-01-01T00:00:00.1234000Z'; $expiresText = '2030-01-01T00:00:00+00:00'
        $f.Policy.publication.createdUtc = $createdText; $f.Policy.publication.expiresUtc = $expiresText
        Save-CdTestPolicy $f; $a = Get-CdTestOutputs $f 'valid'; Invoke-CdTestPlanner $f $a
        $document = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($a.Plan))
        try {
            foreach ($name in @('createdUtc', 'expiresUtc')) {
                $element = $document.RootElement.GetProperty($name)
                Assert-CdTest ($element.ValueKind -eq [Text.Json.JsonValueKind]::String) 'publisher timestamp remains a JSON string'
                $expected = if ($name -eq 'createdUtc') { $createdText } else { $expiresText }
                Assert-CdTest ($element.GetString() -ceq $expected) 'original UTC suffix and fractional precision preserved'
            }
        } finally { $document.Dispose() }
        $invalidIndex = 0
        foreach ($name in @('createdUtc', 'expiresUtc')) {
            foreach ($bad in @([long]1735689600, '2027-01-01T02:00:00+02:00', '2027-01-01T00:00:00')) {
                $f.Policy.publication.createdUtc = $createdText; $f.Policy.publication.expiresUtc = $expiresText
                $f.Policy.publication[$name] = $bad; Save-CdTestPolicy $f
                $invalidIndex++; $output = Get-CdTestOutputs $f "invalid-$invalidIndex"
                Assert-CdTestThrows { Invoke-CdTestPlanner $f $output }
                Assert-CdTest (-not [IO.File]::Exists($output.Plan) -and -not [IO.File]::Exists($output.Report)) 'invalid timestamp refused before artifacts'
            }
        }
    }
    Invoke-CdTestCase 'sensitive descendants cannot be included and are never content-read' {
        $f = New-CdTestFixture 'sensitive'
        $secret = [IO.Path]::Combine($f.Source, 'data', 'private-key.pem')
        [IO.File]::WriteAllText($secret, 'Synthetic text, not a real key.')
        $null = [IO.Directory]::CreateDirectory([IO.Path]::Combine($f.Source, 'data', 'profiles'))
        [IO.File]::WriteAllText([IO.Path]::Combine($f.Source, 'data', 'profiles', 'person.ini'), 'synthetic private fixture')
        Save-CdTestPolicy $f; $a = Get-CdTestOutputs $f
        $held = [IO.File]::Open($secret, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        try {
            Assert-CdTestThrows { Invoke-CdTestPlanner $f $a }
            $report = Get-Content -LiteralPath $a.Report -Raw | ConvertFrom-Json
            Assert-CdTest ($report.blockedCount -eq 2 -and $report.quarantined.Count -eq 2) 'selected private paths quarantine'
            Assert-CdTest (-not [IO.File]::Exists($a.Plan)) 'no dangerous plan'
            $f.Policy.excludePaths = @('data/private-key.pem', 'data/profiles'); Save-CdTestPolicy $f
            $b = Get-CdTestOutputs $f 'excluded'; Invoke-CdTestPlanner $f $b
            Assert-CdTest ([IO.File]::Exists($b.Plan)) 'explicitly excluded locked key is not opened'
        } finally { $held.Dispose() }
    }
    Invoke-CdTestCase 'traversal patterns missing paths and case aliases are rejected' {
        $f = New-CdTestFixture 'bad-policy'
        foreach ($bad in @('../escape', '/absolute', 'data\a.bin', 'data/*.bin', 'client.exe', 'missing.bin')) {
            $f.Policy.files = @($bad); Save-CdTestPolicy $f
            Assert-CdTestThrows { Invoke-CdTestPlanner $f (Get-CdTestOutputs $f) }
        }
        $f.Policy.files = @('Client.exe', 'client.exe'); Save-CdTestPolicy $f
        Assert-CdTestThrows { Invoke-CdTestPlanner $f (Get-CdTestOutputs $f) }
        Assert-CdTest (-not [IO.File]::Exists((Get-CdTestOutputs $f).Plan)) 'no malformed-policy artifact'
    }
    Invoke-CdTestCase 'duplicate and unknown JSON policy properties are refused' {
        $f = New-CdTestFixture 'json-shape'; Save-CdTestPolicy $f
        $json = [IO.File]::ReadAllText($f.PolicyPath)
        $duplicate = $json -replace '("schemaVersion"\s*:\s*1)', '$1,"SchemaVersion":1'
        Assert-CdTest ($duplicate -cne $json) 'synthetic duplicate was actually inserted'
        [IO.File]::WriteAllText($f.PolicyPath, $duplicate)
        Assert-CdTestThrows { Invoke-CdTestPlanner $f (Get-CdTestOutputs $f) }
        $f.Policy.Add('unreviewedProperty', $true); Save-CdTestPolicy $f
        Assert-CdTestThrows { Invoke-CdTestPlanner $f (Get-CdTestOutputs $f) }
        Assert-CdTest (-not [IO.File]::Exists((Get-CdTestOutputs $f).Report)) 'malformed policy creates no report'
    }
    Invoke-CdTestCase 'source-contained and preexisting artifact outputs are refused' {
        $f = New-CdTestFixture 'outputs'; Save-CdTestPolicy $f; $a = Get-CdTestOutputs $f
        $before = Get-CdSyntheticSnapshot $f.Source
        Assert-CdTestThrows { & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $f.Source -PolicyPath $f.PolicyPath `
            -PlanPath ([IO.Path]::Combine($f.Source, 'plan.json')) -ReportPath $a.Report }
        Assert-CdTestThrows { & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $f.Source -PolicyPath $f.PolicyPath `
            -PlanPath ([IO.Path]::Combine($f.Source + '.', 'alias-plan.json')) -ReportPath $a.Report }
        Assert-CdTest ($before -ceq (Get-CdSyntheticSnapshot $f.Source)) 'no source output writes'
        [IO.File]::WriteAllText($a.Plan, 'keep existing plan')
        Assert-CdTestThrows { Invoke-CdTestPlanner $f $a }
        Assert-CdTest ([IO.File]::ReadAllText($a.Plan) -ceq 'keep existing plan') 'existing plan preserved'
        Assert-CdTest (-not [IO.File]::Exists($a.Report)) 'no partial report on known output collision'
        $b = Get-CdTestOutputs $f 'report-exists'; [IO.File]::WriteAllText($b.Report, 'keep existing report')
        Assert-CdTestThrows { Invoke-CdTestPlanner $f $b }
        Assert-CdTest (-not [IO.File]::Exists($b.Plan) -and [IO.File]::ReadAllText($b.Report) -ceq 'keep existing report') 'existing report preserved'
    }
    Invoke-CdTestCase 'root share relative and policy-in-source are refused before outputs' {
        $f = New-CdTestFixture 'roots'; Save-CdTestPolicy $f; $a = Get-CdTestOutputs $f
        foreach ($bad in @([IO.Path]::GetPathRoot($f.Source), '\\invalid.example.test\share\client', '..\client')) {
            Assert-CdTestThrows { & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $bad -PolicyPath $f.PolicyPath -PlanPath $a.Plan -ReportPath $a.Report }
        }
        $inside = [IO.Path]::Combine($f.Source, 'policy.json'); [IO.File]::Copy($f.PolicyPath, $inside)
        Assert-CdTestThrows { & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $f.Source -PolicyPath $inside -PlanPath $a.Plan -ReportPath $a.Report }
        Assert-CdTest (-not [IO.File]::Exists($a.Report)) 'no artifacts from unsafe roots or input policy'
    }
    Invoke-CdTestCase 'Windows junction root and selected descendant are refused' {
        $f = New-CdTestFixture 'reparse'; Save-CdTestPolicy $f; $a = Get-CdTestOutputs $f
        $rootLink = [IO.Path]::Combine($f.Root, 'linked-source')
        New-Item -ItemType Junction -Path $rootLink -Value $f.Source | Out-Null; $ownedLinks.Add($rootLink)
        Assert-CdTestThrows { & (Join-Path $PSScriptRoot 'New-ClientDistributionPlan.ps1') -SourceRoot $rootLink -PolicyPath $f.PolicyPath -PlanPath $a.Plan -ReportPath $a.Report }
        $outside = [IO.Path]::Combine($f.Root, 'outside'); $null = [IO.Directory]::CreateDirectory($outside)
        [IO.File]::WriteAllText([IO.Path]::Combine($outside, 'preserved.txt'), 'outside fixture preserved')
        $childLink = [IO.Path]::Combine($f.Source, 'data', 'linked')
        New-Item -ItemType Junction -Path $childLink -Value $outside | Out-Null; $ownedLinks.Add($childLink)
        Assert-CdTestThrows { Invoke-CdTestPlanner $f $a }
        Assert-CdTest ([IO.File]::ReadAllText([IO.Path]::Combine($outside, 'preserved.txt')) -ceq 'outside fixture preserved') 'junction destination untouched'
        Assert-CdTest (-not [IO.File]::Exists($a.Plan)) 'no reparse plan'
    }
    [ordered]@{ outcome = 'passed'; syntheticCases = $script:caseCount; originalClientTouched = $false; published = $false } | ConvertTo-Json
} finally {
    foreach ($link in $ownedLinks) {
        if (-not ([IO.File]::GetAttributes($link) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Owned junction identity changed; cleanup refused.' }
        [IO.Directory]::Delete($link) # Never recurse through a junction.
    }
    $resolved = [IO.Path]::GetFullPath($taskTestRoot)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.Path]::GetFileName($resolved).StartsWith('wolf-client-plan-test-', [StringComparison]::Ordinal) -or
        ([IO.File]::GetAttributes($resolved) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe self-test cleanup target.' }
    [IO.Directory]::Delete($resolved, $true)
}
