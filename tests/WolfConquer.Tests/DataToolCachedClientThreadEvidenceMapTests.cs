using System.Text.Json;

namespace WolfConquer.Tests;

public sealed class DataToolCachedClientThreadEvidenceMapTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    [Fact]
    public async Task HelpMentionsCachedClientThreadEvidenceMapCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool cached-client-thread-evidence-map <patchResultJsonPath> [--probe-json patchResultJsonPath] [--module DataThread.dll] [--license-module license.dll]",
            result.StandardOutput);
        Assert.Contains(
            "WolfConquer.DataTool cached-client-module-probe-compare <leftPatchResultJsonPath> <rightPatchResultJsonPath> " +
            "[--left-field ModuleMemoryProbesBeforeRelayResponse|ModuleMemoryProbesAfterObserve] " +
            "[--right-field ModuleMemoryProbesBeforeRelayResponse|ModuleMemoryProbesAfterObserve]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapReportsDataThreadJumpAndLicenseImmediate()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "ret-only-thread-evidence.json");
            await WriteThreadEvidenceAsync(evidencePath);

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Cached client thread evidence map:", result.StandardOutput);
            Assert.Contains($"Path: {evidencePath}", result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
            Assert.Contains("Thread: id=17556 state=Running cpuMs=17484", result.StandardOutput);
            Assert.Contains("Instruction: DataThread.dll+0x342B8D ip=0x2852B8D base=0x2510000", result.StandardOutput);
            Assert.Contains("0x2852B8D (DataThread.dll+0x342B8D): pop esp", result.StandardOutput);
            Assert.Contains("0x2852B8E (DataThread.dll+0x342B8E): jmp 0x2852CA2 -> 0x2852CA2 (DataThread.dll+0x342CA2)", result.StandardOutput);
            Assert.Contains("0x2852B95 (DataThread.dll+0x342B95): mov ecx, 0x6F99F794 -> 0x6F99F794 (license.dll+0x27F794)", result.StandardOutput);
            Assert.Contains("Jump targets: 0x2852CA2 (DataThread.dll+0x342CA2)", result.StandardOutput);
            Assert.Contains("License immediates: 0x6F99F794 (license.dll+0x27F794)", result.StandardOutput);
            Assert.Contains("Module probes decoded: 1", result.StandardOutput);
            Assert.Contains("Probe: datathread-33A461 DataThread.dll+0x33A461 address=0x284A461 bytes=11", result.StandardOutput);
            Assert.Contains("0x284A461 (DataThread.dll+0x33A461): jne 0x26F151B -> 0x26F151B (DataThread.dll+0x1E151B)", result.StandardOutput);
            Assert.Contains("0x284A467 (DataThread.dll+0x33A467): jmp 0x284943E -> 0x284943E (DataThread.dll+0x33943E)", result.StandardOutput);
            Assert.Contains(
                "Module probe branch targets: 0x26F151B (DataThread.dll+0x1E151B), 0x284943E (DataThread.dll+0x33943E)",
                result.StandardOutput);
            Assert.Contains(
                "Next probe: Map 0x2852CA2 (DataThread.dll+0x342CA2) and inspect 0x6F99F794 (license.dll+0x27F794)",
                result.StandardOutput);
            Assert.Contains(
                "Continue read-only module probes at 0x26F151B (DataThread.dll+0x1E151B), 0x284943E (DataThread.dll+0x33943E)",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapReportsRelocatedLicenseImmediateWhenAbsolutePointerIsStale()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "stale-license-immediate-thread-evidence.json");
            await WriteThreadEvidenceAsync(
                evidencePath,
                [
                    new
                    {
                        Name = "license-state-27F794",
                        ModuleName = "license.dll",
                        ModuleFileName = @"client\Env_DX9\license.dll",
                        Rva = "0x27F794",
                        Length = 4,
                        Address = "0x6E9CF794",
                        Read = true,
                        Hex = "90 90 90 90",
                        FailureReason = string.Empty,
                    },
                ],
                licenseBaseAddress: 0x6E750000);

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Ready: True", result.StandardOutput);
            Assert.Contains("0x2852B95 (DataThread.dll+0x342B95): mov ecx, 0x6F99F794", result.StandardOutput);
            Assert.Contains("Jump targets: 0x2852CA2 (DataThread.dll+0x342CA2)", result.StandardOutput);
            Assert.Contains("License immediates: <none>", result.StandardOutput);
            Assert.Contains(
                "Stale/relocated license immediates: immediateAddress=0x6F99F794 currentModuleCandidate=license.dll candidateRva=0x27F794 currentAddress=license.dll+0x27F794 currentAbsoluteAddress=0x6E9CF794 staleModuleBase=0x6F720000 matchingModuleProbeBytes=True",
                result.StandardOutput);
            Assert.Contains(
                "Next probe: Map 0x2852CA2 (DataThread.dll+0x342CA2) and inspect 0x6F99F794 relocated to 0x6E9CF794 (license.dll+0x27F794)",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapCanUseSeparateProbeJsonForRelocatedLicenseImmediate()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "hot-thread-evidence.json");
            var probePath = Path.Combine(tempRoot, "relocated-license-probe.json");
            await WriteThreadEvidenceAsync(
                evidencePath,
                [],
                licenseBaseAddress: 0x6F720000);
            await WriteThreadEvidenceAsync(
                probePath,
                [
                    new
                    {
                        Name = "license-state-27F794",
                        ModuleName = "license.dll",
                        ModuleFileName = @"client\Env_DX9\license.dll",
                        Rva = "0x27F794",
                        Length = 4,
                        Address = "0x6E9CF794",
                        Read = true,
                        Hex = "90 90 90 90",
                        FailureReason = string.Empty,
                    },
                ],
                licenseBaseAddress: 0x6E750000);

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath, "--probe-json", probePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Path: {evidencePath}", result.StandardOutput);
            Assert.Contains($"Probe path: {probePath}", result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
            Assert.Contains("0x2852B95 (DataThread.dll+0x342B95): mov ecx, 0x6F99F794", result.StandardOutput);
            Assert.Contains("License immediates: 0x6F99F794 (license.dll+0x27F794)", result.StandardOutput);
            Assert.Contains(
                "Stale/relocated license immediates: immediateAddress=0x6F99F794 currentModuleCandidate=license.dll candidateRva=0x27F794 currentAddress=license.dll+0x27F794 currentAbsoluteAddress=0x6E9CF794 staleModuleBase=0x6F720000 matchingModuleProbeBytes=True",
                result.StandardOutput);
            Assert.Contains(
                "Next probe: Map 0x2852CA2 (DataThread.dll+0x342CA2) and inspect 0x6F99F794 relocated to 0x6E9CF794 (license.dll+0x27F794)",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapKeepsPrimaryThreadModuleBaseWhenProbeJsonUsesDifferentBase()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "hot-thread-evidence.json");
            var probePath = Path.Combine(tempRoot, "datathread-probe-different-base.json");
            await WriteThreadEvidenceAsync(
                evidencePath,
                []);
            await WriteThreadEvidenceAsync(
                probePath,
                [
                    DataThreadProbe("datathread-339824", "0x339824", "0x2439824", 5, "E9 D0 00 00 00"),
                ],
                dataThreadBaseAddress: 0x2100000);

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath, "--probe-json", probePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("0x2852B95 (DataThread.dll+0x342B95): mov ecx, 0x6F99F794", result.StandardOutput);
            Assert.Contains(
                "0x2439824 (DataThread.dll+0x339824): jmp 0x24398F9 -> 0x24398F9 (DataThread.dll+0x3398F9)",
                result.StandardOutput);
            Assert.Contains(
                "Next probe: Map 0x2852CA2 (DataThread.dll+0x342CA2) and inspect 0x6F99F794 (license.dll+0x27F794)",
                result.StandardOutput);
            Assert.DoesNotContain("DataThread.dll+0x752CA2", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapFormatsProbeBranchesWithProbeModuleBaseWhenRangesOverlap()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "hot-thread-lower-base.json");
            var probePath = Path.Combine(tempRoot, "datathread-probe-overlapping-base.json");
            await WriteThreadEvidenceAsync(
                evidencePath,
                [],
                dataThreadBaseAddress: 0x20E0000);
            await WriteThreadEvidenceAsync(
                probePath,
                [
                    DataThreadProbe("datathread-339824", "0x339824", "0x2519824", 5, "E9 D0 00 00 00"),
                ],
                dataThreadBaseAddress: 0x21E0000);

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath, "--probe-json", probePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "0x2422B95 (DataThread.dll+0x342B95): mov ecx, 0x6F99F794",
                result.StandardOutput);
            Assert.Contains(
                "0x2519824 (DataThread.dll+0x339824): jmp 0x25198F9 -> 0x25198F9 (DataThread.dll+0x3398F9)",
                result.StandardOutput);
            Assert.DoesNotContain("DataThread.dll+0x4398F9", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapReportsActiveDataThreadProbeBranchTargets()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "active-datathread-342b7f-probe.json");
            await WriteThreadEvidenceAsync(
                evidencePath,
                [
                    new
                    {
                        Name = "datathread-342B7F",
                        ModuleName = "DataThread.dll",
                        ModuleFileName = @"client\Env_DX9\DataThread.dll",
                        Rva = "0x342B7F",
                        Length = 20,
                        Address = "0x2852B7F",
                        Read = true,
                        Hex = "E9 5F 00 00 00 90 90 90 90 90 90 90 90 90 5C E9 0F 01 00 00",
                        FailureReason = string.Empty,
                    },
                ]);

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Module probes decoded: 1", result.StandardOutput);
            Assert.Contains("Probe: datathread-342B7F DataThread.dll+0x342B7F address=0x2852B7F bytes=20", result.StandardOutput);
            Assert.Contains(
                "0x2852B7F (DataThread.dll+0x342B7F): jmp 0x2852BE3 -> 0x2852BE3 (DataThread.dll+0x342BE3)",
                result.StandardOutput);
            Assert.Contains(
                "0x2852B8E (DataThread.dll+0x342B8E): jmp 0x2852CA2 -> 0x2852CA2 (DataThread.dll+0x342CA2)",
                result.StandardOutput);
            Assert.Contains(
                "Module probe branch targets: 0x2852BE3 (DataThread.dll+0x342BE3), 0x2852CA2 (DataThread.dll+0x342CA2)",
                result.StandardOutput);
            Assert.Contains(
                "Continue read-only module probes at 0x2852BE3 (DataThread.dll+0x342BE3), 0x2852CA2 (DataThread.dll+0x342CA2) before patching the protected block.",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapReportsRootReductionDataThreadProbeBranchTargets()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "root-reduction-datathread-probes.json");
            await WriteThreadEvidenceAsync(
                evidencePath,
                [
                    DataThreadProbe("datathread-342CA2", "0x342CA2", "0x2852CA2", 7, "E9 18 6B FF FF EB 0C"),
                    DataThreadRootCandidateProbe(),
                ]);

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Module probes decoded: 2", result.StandardOutput);
            Assert.Contains("Probe: datathread-342CA2 DataThread.dll+0x342CA2 address=0x2852CA2 bytes=7", result.StandardOutput);
            Assert.Contains(
                "0x2852CA2 (DataThread.dll+0x342CA2): jmp 0x28497BF -> 0x28497BF (DataThread.dll+0x3397BF)",
                result.StandardOutput);
            Assert.Contains(
                "0x2852CA7 (DataThread.dll+0x342CA7): jmp 0x2852CB5 -> 0x2852CB5 (DataThread.dll+0x342CB5)",
                result.StandardOutput);
            Assert.Contains("Probe: datathread-3397BF DataThread.dll+0x3397BF address=0x28497BF bytes=24", result.StandardOutput);
            Assert.Contains(
                "0x28497D2 (DataThread.dll+0x3397D2): jmp 0x2849824 -> 0x2849824 (DataThread.dll+0x339824)",
                result.StandardOutput);
            Assert.Contains(
                "Module probe branch targets: 0x28497BF (DataThread.dll+0x3397BF), 0x2852CB5 (DataThread.dll+0x342CB5), 0x2849824 (DataThread.dll+0x339824)",
                result.StandardOutput);
            Assert.Contains(
                "Continue read-only module probes at 0x28497BF (DataThread.dll+0x3397BF), 0x2852CB5 (DataThread.dll+0x342CB5), 0x2849824 (DataThread.dll+0x339824) before patching the protected block.",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapReportsMultiProbeBranchFrontierGuidance()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "datathread-339824-frontier-probes.json");
            await WriteThreadEvidenceAsync(
                evidencePath,
                [
                    DataThreadProbe("datathread-339824", "0x339824", "0x2849824", 5, "E9 D0 00 00 00"),
                    DataThreadProbe("datathread-3398F9", "0x3398F9", "0x28498F9", 5, "E9 BA FA FE FF"),
                ]);

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Module probes decoded: 2", result.StandardOutput);
            Assert.Contains("Probe: datathread-339824 DataThread.dll+0x339824 address=0x2849824 bytes=5", result.StandardOutput);
            Assert.Contains(
                "0x2849824 (DataThread.dll+0x339824): jmp 0x28498F9 -> 0x28498F9 (DataThread.dll+0x3398F9)",
                result.StandardOutput);
            Assert.Contains("Probe: datathread-3398F9 DataThread.dll+0x3398F9 address=0x28498F9 bytes=5", result.StandardOutput);
            Assert.Contains(
                "0x28498F9 (DataThread.dll+0x3398F9): jmp 0x28393B8 -> 0x28393B8 (DataThread.dll+0x3293B8)",
                result.StandardOutput);
            Assert.Contains(
                "Module probe branch targets: 0x28498F9 (DataThread.dll+0x3398F9), 0x28393B8 (DataThread.dll+0x3293B8)",
                result.StandardOutput);
            Assert.Contains(
                "Next probe: Map 0x2852CA2 (DataThread.dll+0x342CA2) and inspect 0x6F99F794 (license.dll+0x27F794) before patching the ret-only shortcut. Continue read-only module probes at 0x28498F9 (DataThread.dll+0x3398F9), 0x28393B8 (DataThread.dll+0x3293B8) before patching the protected block.",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapReportsStableDataThreadTargetSet()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "active-datathread-stable-target-probes.json");
            await WriteThreadEvidenceAsync(
                evidencePath,
                [
                    DataThreadRootCandidateProbe(),
                    DataThreadProbe("datathread-33A461", "0x33A461", "0x284A461", 11, "0F 85 B4 70 EA FF E9 D2 EF FF FF"),
                    DataThreadProbe("datathread-31ED44", "0x31ED44", "0x282ED44", 5, "E9 9E 00 00 00"),
                    DataThreadProbe("datathread-33C900", "0x33C900", "0x284C900", 5, "E9 78 00 00 00"),
                    DataThreadProbe("datathread-341300", "0x341300", "0x2851300", 5, "E9 54 00 00 00"),
                ]);

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Module probes decoded: 5", result.StandardOutput);
            Assert.Contains(
                "0x28497D2 (DataThread.dll+0x3397D2): jmp 0x2849824 -> 0x2849824 (DataThread.dll+0x339824)",
                result.StandardOutput);
            Assert.Contains(
                "0x284A461 (DataThread.dll+0x33A461): jne 0x26F151B -> 0x26F151B (DataThread.dll+0x1E151B)",
                result.StandardOutput);
            Assert.Contains(
                "0x284A467 (DataThread.dll+0x33A467): jmp 0x284943E -> 0x284943E (DataThread.dll+0x33943E)",
                result.StandardOutput);
            Assert.Contains(
                "0x282ED44 (DataThread.dll+0x31ED44): jmp 0x282EDE7 -> 0x282EDE7 (DataThread.dll+0x31EDE7)",
                result.StandardOutput);
            Assert.Contains(
                "0x284C900 (DataThread.dll+0x33C900): jmp 0x284C97D -> 0x284C97D (DataThread.dll+0x33C97D)",
                result.StandardOutput);
            Assert.Contains(
                "0x2851300 (DataThread.dll+0x341300): jmp 0x2851359 -> 0x2851359 (DataThread.dll+0x341359)",
                result.StandardOutput);
            Assert.Contains(
                "Module probe branch targets: 0x2849824 (DataThread.dll+0x339824), 0x26F151B (DataThread.dll+0x1E151B), 0x284943E (DataThread.dll+0x33943E), 0x282EDE7 (DataThread.dll+0x31EDE7), 0x284C97D (DataThread.dll+0x33C97D), 0x2851359 (DataThread.dll+0x341359)",
                result.StandardOutput);
            Assert.Contains(
                "Continue read-only module probes at 0x2849824 (DataThread.dll+0x339824), 0x26F151B (DataThread.dll+0x1E151B), 0x284943E (DataThread.dll+0x33943E), 0x282EDE7 (DataThread.dll+0x31EDE7), 0x284C97D (DataThread.dll+0x33C97D), 0x2851359 (DataThread.dll+0x341359) before patching the protected block.",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientThreadEvidenceMapRejectsMissingThreadEvidence()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "empty.json");
            await File.WriteAllTextAsync(
                evidencePath,
                JsonSerializer.Serialize(new { Result = new { ThreadEvidenceAfterObserve = Array.Empty<object>() } }, JsonOptions));

            var result = await RunDataToolAsync("cached-client-thread-evidence-map", evidencePath);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains("Cached client thread evidence map failed:", result.StandardError);
            Assert.Contains("No thread evidence row found for module DataThread.dll", result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientModuleProbeCompareNormalizesTargetsAcrossModuleBaseDrift()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var leftPath = Path.Combine(tempRoot, "left-probes.json");
            var rightPath = Path.Combine(tempRoot, "right-probes.json");
            await WriteModuleProbeCompareEvidenceAsync(leftPath, "left-33A461", 0x2510000);
            await WriteModuleProbeCompareEvidenceAsync(rightPath, "right-33A461", 0x3100000);

            var result = await RunDataToolAsync("cached-client-module-probe-compare", leftPath, rightPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Cached client module probe compare:", result.StandardOutput);
            Assert.Contains($"Left: {leftPath}", result.StandardOutput);
            Assert.Contains("Left field: ModuleMemoryProbesAfterObserve", result.StandardOutput);
            Assert.Contains($"Right: {rightPath}", result.StandardOutput);
            Assert.Contains("Right field: ModuleMemoryProbesAfterObserve", result.StandardOutput);
            Assert.Contains("Common probes: 1", result.StandardOutput);
            Assert.Contains("Only left: 0", result.StandardOutput);
            Assert.Contains("Only right: 0", result.StandardOutput);
            Assert.Contains(
                "Probe: DataThread.dll+0x33A461 leftName=left-33A461 rightName=right-33A461 " +
                "SameHex=True FirstDifferingByteOffset=<none> SameLeadingBranchTargets=True",
                result.StandardOutput);
            Assert.Contains("LeftTargets=DataThread.dll+0x1E151B, DataThread.dll+0x33943E", result.StandardOutput);
            Assert.Contains("RightTargets=DataThread.dll+0x1E151B, DataThread.dll+0x33943E", result.StandardOutput);
            Assert.DoesNotContain("0x284943E", result.StandardOutput);
            Assert.DoesNotContain("0x343943E", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientModuleProbeCompareSelectsBeforeAndAfterFieldsFromSameArtifact()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "before-after-probes.json");
            await WriteModuleProbeCompareEvidenceAsync(
                evidencePath,
                "datathread-33A461",
                0x2510000,
                beforeHex: "90 90 CC 90",
                afterHex: "90 90 90 90");

            var result = await RunDataToolAsync(
                "cached-client-module-probe-compare",
                evidencePath,
                evidencePath,
                "--left-field",
                "ModuleMemoryProbesBeforeRelayResponse",
                "--right-field",
                "ModuleMemoryProbesAfterObserve");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Left field: ModuleMemoryProbesBeforeRelayResponse", result.StandardOutput);
            Assert.Contains("Right field: ModuleMemoryProbesAfterObserve", result.StandardOutput);
            Assert.Contains("Common probes: 1", result.StandardOutput);
            Assert.Contains(
                "SameHex=False FirstDifferingByteOffset=2 SameLeadingBranchTargets=True",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientModuleProbeCompareReportsLengthDifferenceAtFirstMissingByteOffset()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var leftPath = Path.Combine(tempRoot, "short-probes.json");
            var rightPath = Path.Combine(tempRoot, "long-probes.json");
            await WriteModuleProbeCompareEvidenceAsync(
                leftPath,
                "short-33A461",
                0x2510000,
                afterHex: "90 90 90 90");
            await WriteModuleProbeCompareEvidenceAsync(
                rightPath,
                "long-33A461",
                0x2510000,
                afterHex: "90 90 90 90 CC");

            var result = await RunDataToolAsync("cached-client-module-probe-compare", leftPath, rightPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "SameHex=False FirstDifferingByteOffset=4 SameLeadingBranchTargets=True",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task CachedClientModuleProbeCompareDefaultsBothSidesToAfterObserve()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var leftPath = Path.Combine(tempRoot, "left-default-probes.json");
            var rightPath = Path.Combine(tempRoot, "right-default-probes.json");
            await WriteModuleProbeCompareEvidenceAsync(
                leftPath,
                "left-default-33A461",
                0x2510000,
                beforeHex: "90 CC 90",
                afterHex: "90 90 90");
            await WriteModuleProbeCompareEvidenceAsync(
                rightPath,
                "right-default-33A461",
                0x2510000,
                beforeHex: "90 C3 90",
                afterHex: "90 90 90");

            var result = await RunDataToolAsync("cached-client-module-probe-compare", leftPath, rightPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Left field: ModuleMemoryProbesAfterObserve", result.StandardOutput);
            Assert.Contains("Right field: ModuleMemoryProbesAfterObserve", result.StandardOutput);
            Assert.Contains(
                "SameHex=True FirstDifferingByteOffset=<none> SameLeadingBranchTargets=True",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Theory]
    [InlineData("--left-field", "ModuleMemoryProbesUnknown", "Unknown cached-client-module-probe-compare probe field")]
    [InlineData("--unknown-option", "ModuleMemoryProbesAfterObserve", "Unknown cached-client-module-probe-compare option")]
    public async Task CachedClientModuleProbeCompareRejectsUnknownFieldAndOption(
        string option,
        string value,
        string expectedError)
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var evidencePath = Path.Combine(tempRoot, "valid-probes.json");
            await WriteModuleProbeCompareEvidenceAsync(evidencePath, "datathread-33A461", 0x2510000);

            var result = await RunDataToolAsync(
                "cached-client-module-probe-compare",
                evidencePath,
                evidencePath,
                option,
                value);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains(expectedError, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static async Task WriteThreadEvidenceAsync(
        string path,
        object[]? moduleMemoryProbes = null,
        long licenseBaseAddress = 0x6F720000,
        long dataThreadBaseAddress = 0x2510000)
    {
        moduleMemoryProbes ??=
        [
            new
            {
                Name = "datathread-33A461",
                ModuleName = "DataThread.dll",
                ModuleFileName = @"client\Env_DX9\DataThread.dll",
                Rva = "0x33A461",
                Length = 11,
                Address = "0x284A461",
                Read = true,
                Hex = "0F 85 B4 70 EA FF E9 D2 EF FF FF",
                FailureReason = string.Empty,
            },
        ];

        var evidence = new
        {
            Result = new
            {
                ProcessId = 16428,
                ModuleEvidenceAfterObserve = new object[]
                {
                    new
                    {
                        ModuleName = "DataThread.dll",
                        FileName = @"client\Env_DX9\DataThread.dll",
                        BaseAddress = $"0x{dataThreadBaseAddress:X}",
                        ModuleMemorySize = 12414976,
                    },
                    new
                    {
                        ModuleName = "license.dll",
                        FileName = @"client\Env_DX9\license.dll",
                        BaseAddress = $"0x{licenseBaseAddress:X}",
                        ModuleMemorySize = 16863232,
                    },
                },
                ThreadEvidenceAfterObserve = new object[]
                {
                    new
                    {
                        ProcessId = 16428,
                        ThreadId = 30940,
                        ThreadState = "Wait",
                        InstructionPointer = "0x7765B92C",
                        InstructionModuleName = "ntdll.dll",
                        InstructionBaseAddress = "0x775E0000",
                        InstructionRva = "0x7B92C",
                        InstructionBytes = "C2 14 00",
                        TotalProcessorTimeMs = 16,
                    },
                    new
                    {
                        ProcessId = 16428,
                        ThreadId = 17556,
                        ThreadState = "Running",
                        InstructionPointer = $"0x{dataThreadBaseAddress + 0x342B8D:X}",
                        InstructionModuleName = "DataThread.dll",
                        InstructionBaseAddress = $"0x{dataThreadBaseAddress:X}",
                        InstructionRva = "0x342B8D",
                        InstructionBytes = "5C E9 0F 01 00 00 50 51 B9 94 F7 99 6F 89 4C 24 04 59 89 0C 24 8B 14 24 81 C4 04 00 00 00 50 C7",
                        StackPointer = "0x1AF87E",
                        Eax = "0x3977",
                        Ebx = "0x3A72",
                        Ecx = "0xFFFF96C7",
                        Edx = "0xFFFF043C",
                        Ebp = "0x1AF8BC",
                        TotalProcessorTimeMs = 17484,
                    },
                },
                ModuleMemoryProbesAfterObserve = moduleMemoryProbes,
            },
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence, JsonOptions));
    }

    private static object DataThreadProbe(string name, string rva, string address, int length, string hex)
    {
        return new
        {
            Name = name,
            ModuleName = "DataThread.dll",
            ModuleFileName = @"client\Env_DX9\DataThread.dll",
            Rva = rva,
            Length = length,
            Address = address,
            Read = true,
            Hex = hex,
            FailureReason = string.Empty,
        };
    }

    private static object DataThreadRootCandidateProbe()
    {
        return DataThreadProbe(
            "datathread-3397BF",
            "0x3397BF",
            "0x28497BF",
            24,
            "66 56 66 68 49 54 66 8B 34 24 51 89 E1 81 C1 04 00 00 00 E9 4D 00 00 00");
    }

    private static async Task WriteModuleProbeCompareEvidenceAsync(
        string path,
        string probeName,
        long dataThreadBaseAddress,
        string? beforeHex = null,
        string? afterHex = null)
    {
        beforeHex ??= "0F 85 B4 70 EA FF E9 D2 EF FF FF";
        afterHex ??= "0F 85 B4 70 EA FF E9 D2 EF FF FF";
        var evidence = new
        {
            Result = new
            {
                ModuleEvidenceAfterObserve = new object[]
                {
                    new
                    {
                        ModuleName = "DataThread.dll",
                        FileName = @"C:\fixtures\DataThread.dll",
                        BaseAddress = $"0x{dataThreadBaseAddress:X}",
                        ModuleMemorySize = 0x500000,
                    },
                },
                ModuleMemoryProbesBeforeRelayResponse = new object[]
                {
                    ModuleProbeCompareRow(probeName, dataThreadBaseAddress, beforeHex),
                },
                ModuleMemoryProbesAfterObserve = new object[]
                {
                    ModuleProbeCompareRow(probeName, dataThreadBaseAddress, afterHex),
                },
            },
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence, JsonOptions));
    }

    private static object ModuleProbeCompareRow(string probeName, long dataThreadBaseAddress, string hex)
    {
        return new
        {
            Name = probeName,
            ModuleName = "DataThread.dll",
            ModuleFileName = @"C:\fixtures\DataThread.dll",
            Rva = "0x33A461",
            Length = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            Address = $"0x{dataThreadBaseAddress + 0x33A461:X}",
            Read = true,
            Hex = hex,
            FailureReason = string.Empty,
        };
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-cached-client-thread-evidence-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
