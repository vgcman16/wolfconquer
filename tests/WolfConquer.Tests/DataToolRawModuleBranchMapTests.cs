using System.Runtime.CompilerServices;

namespace WolfConquer.Tests;

public sealed class DataToolRawModuleBranchMapTests
{
    [Fact]
    public async Task RawModuleBranchMapReportsDecodedInstructionsAndRelativeJumpTarget()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                [
                    0xE9, 0x05, 0x00, 0x00, 0x00,
                    0x90, 0x90, 0x90, 0x90, 0x90,
                    0xC3,
                ]);

            var result = await RunDataToolAsync(
                "raw-module-branch-map",
                rawModulePath,
                "--rva",
                "0x0",
                "--length",
                "32",
                "--module-name",
                "DataThread.dll");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Raw module branch map", result.StandardOutput);
            Assert.Contains(rawModulePath, result.StandardOutput);
            Assert.Contains("Probe: DataThread.dll+0x0", result.StandardOutput);
            Assert.Contains("Instructions decoded:", result.StandardOutput);
            Assert.Contains("DataThread.dll+0x0", result.StandardOutput);
            Assert.Contains("jmp 0xA", result.StandardOutput);
            Assert.Contains("DataThread.dll+0xA", result.StandardOutput);
            Assert.Contains(
                SplitOutputLines(result.StandardOutput),
                line =>
                    line.Contains("branch", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("DataThread.dll+0xA", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchMapFollowsBranchTargetsUpToRequestedDepth()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                [
                    0xE9, 0x05, 0x00, 0x00, 0x00,
                    0x90, 0x90, 0x90, 0x90, 0x90,
                    0xE9, 0x05, 0x00, 0x00, 0x00,
                    0x90, 0x90, 0x90, 0x90, 0x90,
                    0xC3,
                ]);

            var result = await RunDataToolAsync(
                "raw-module-branch-map",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--rva",
                "0x0",
                "--length",
                "8",
                "--follow-depth",
                "2");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Probe: DataThread.dll+0x0", result.StandardOutput);
            Assert.Contains("Probe: DataThread.dll+0xA", result.StandardOutput);
            Assert.Contains("DataThread.dll+0x14", result.StandardOutput);
            Assert.Contains(
                SplitOutputLines(result.StandardOutput),
                line =>
                    line.Contains("branch", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("DataThread.dll+0x14", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchMapSummaryOnlySuppressesDecodedInstructionListings()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                [
                    0xE9, 0x05, 0x00, 0x00, 0x00,
                    0x90, 0x90, 0x90, 0x90, 0x90,
                    0xC3,
                ]);

            var result = await RunDataToolAsync(
                "raw-module-branch-map",
                rawModulePath,
                "--rva",
                "0x0",
                "--length",
                "32",
                "--module-name",
                "DataThread.dll",
                "--summary-only");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Raw module branch map", result.StandardOutput);
            Assert.Contains("Probe: DataThread.dll+0x0", result.StandardOutput);
            Assert.DoesNotContain("Instructions decoded:", result.StandardOutput);
            Assert.DoesNotContain("jmp 0xA", result.StandardOutput);
            Assert.Contains(
                SplitOutputLines(result.StandardOutput),
                line =>
                    line.StartsWith("Branch targets:", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("DataThread.dll+0xA", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchMapSummaryOnlyIncludesFollowedBranchTargets()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                [
                    0xE9, 0x05, 0x00, 0x00, 0x00,
                    0x90, 0x90, 0x90, 0x90, 0x90,
                    0xE9, 0x05, 0x00, 0x00, 0x00,
                    0x90, 0x90, 0x90, 0x90, 0x90,
                    0xC3,
                ]);

            var result = await RunDataToolAsync(
                "raw-module-branch-map",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--rva",
                "0x0",
                "--length",
                "8",
                "--follow-depth",
                "2",
                "--summary-only");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Probe: DataThread.dll+0x0", result.StandardOutput);
            Assert.Contains("Probe: DataThread.dll+0xA", result.StandardOutput);
            Assert.Contains("Probe: DataThread.dll+0x14", result.StandardOutput);
            Assert.DoesNotContain("Instructions decoded:", result.StandardOutput);
            Assert.DoesNotContain("jmp 0xA", result.StandardOutput);
            Assert.DoesNotContain("jmp 0x14", result.StandardOutput);
            Assert.Contains(
                SplitOutputLines(result.StandardOutput),
                line =>
                    line.StartsWith("Branch targets:", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("DataThread.dll+0xA", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                SplitOutputLines(result.StandardOutput),
                line =>
                    line.StartsWith("Branch targets:", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("DataThread.dll+0x14", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Theory]
    [InlineData("short-jmp", 2, 0x10, 0x20, "7505", "EB0E")]
    [InlineData("near-jmp", 5, 0x20, 0x18, "E905000000", "E9F3FFFFFF")]
    [InlineData("near-jmp-pad", 6, 0x10, 0x30, "0F8501000000", "E91B00000090")]
    public async Task RawModuleBranchPatchSpecEmitsModeSpecificPatchSpec(
        string mode,
        int byteCount,
        int sourceRva,
        int targetRva,
        string expectedHex,
        string patchHex)
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                BuildFixtureBytes(0x80, (sourceRva, Convert.FromHexString(expectedHex))));

            var result = await RunDataToolAsync(
                "raw-module-branch-patch-spec",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--source-rva",
                $"0x{sourceRva:X}",
                "--target-rva",
                $"0x{targetRva:X}",
                "--bytes",
                byteCount.ToString(),
                "--name",
                $"datathread-{mode}",
                "--mode",
                mode);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(
                $"datathread-{mode},DataThread.dll,0x{sourceRva:X},{expectedHex},{patchHex}",
                result.StandardOutput.Trim());
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchPatchSpecEmitsNoOpPatchSpecWithoutTargetRva()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                BuildFixtureBytes(0x40, (0x12, Convert.FromHexString("488B05AABBCCDD"))));

            var result = await RunDataToolAsync(
                "raw-module-branch-patch-spec",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--source-rva",
                "0x12",
                "--bytes",
                "7",
                "--name",
                "datathread-preserve-prologue",
                "--mode",
                "no-op");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(
                "datathread-preserve-prologue,DataThread.dll,0x12,488B05AABBCCDD,488B05AABBCCDD",
                result.StandardOutput.Trim());
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchPatchSpecRejectsTargetRvaForNoOpPatchSpec()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(rawModulePath, BuildFixtureBytes(0x40));

            var result = await RunDataToolAsync(
                "raw-module-branch-patch-spec",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--source-rva",
                "0x10",
                "--target-rva",
                "0x20",
                "--bytes",
                "5",
                "--name",
                "datathread-no-op-with-target",
                "--mode",
                "no-op");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "target-rva is not allowed",
                result.StandardOutput + result.StandardError,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchPatchSpecInfersShortJumpModeFromByteCount()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                BuildFixtureBytes(0x40, (0x10, Convert.FromHexString("7505"))));

            var result = await RunDataToolAsync(
                "raw-module-branch-patch-spec",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--source-rva",
                "0x10",
                "--target-rva",
                "0x20",
                "--bytes",
                "2",
                "--name",
                "datathread-35D897-force-35D8F2");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(
                "datathread-35D897-force-35D8F2,DataThread.dll,0x10,7505,EB0E",
                result.StandardOutput.Trim());
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Theory]
    [InlineData("short-jmp", "5", "requires --bytes 2")]
    [InlineData("near-jmp", "6", "requires --bytes 5")]
    [InlineData("near-jmp-pad", "5", "requires --bytes 6")]
    [InlineData("short-jmp", "0", "bytes")]
    public async Task RawModuleBranchPatchSpecRejectsInvalidByteCounts(
        string mode,
        string byteCount,
        string expectedMessage)
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(rawModulePath, BuildFixtureBytes(0x20));

            var result = await RunDataToolAsync(
                "raw-module-branch-patch-spec",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--source-rva",
                "0x10",
                "--target-rva",
                "0x20",
                "--bytes",
                byteCount,
                "--name",
                "datathread-invalid-bytes",
                "--mode",
                mode);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                expectedMessage,
                result.StandardOutput + result.StandardError,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchPatchSpecRejectsShortJumpTargetOutsideSignedByteRange()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                BuildFixtureBytes(0x200, (0x10, Convert.FromHexString("7505"))));

            var result = await RunDataToolAsync(
                "raw-module-branch-patch-spec",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--source-rva",
                "0x10",
                "--target-rva",
                "0x120",
                "--bytes",
                "2",
                "--name",
                "datathread-short-out-of-range",
                "--mode",
                "short-jmp");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "signed byte range",
                result.StandardOutput + result.StandardError,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchPatchSpecRejectsSourceBytesOutsideRawModuleBounds()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(rawModulePath, BuildFixtureBytes(0x10));

            var result = await RunDataToolAsync(
                "raw-module-branch-patch-spec",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--source-rva",
                "0xF",
                "--target-rva",
                "0x0",
                "--bytes",
                "2",
                "--name",
                "datathread-source-out-of-bounds",
                "--mode",
                "short-jmp");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "outside raw module bounds",
                result.StandardOutput + result.StandardError,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchSpineFollowsUnconditionalJumpAfterShortPrelude()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                [
                    0x90, 0x90,
                    0xE9, 0x03, 0x00, 0x00, 0x00,
                    0x90, 0x90, 0x90,
                    0xC3,
                ]);

            var result = await RunDataToolAsync(
                "raw-module-branch-spine",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--rva",
                "0x0",
                "--length",
                "16",
                "--prelude-bytes",
                "8");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Raw module branch spine", result.StandardOutput);
            Assert.Contains("Step 0: DataThread.dll+0x0 -> DataThread.dll+0xA", result.StandardOutput);
            Assert.Contains("jmp at DataThread.dll+0x2", result.StandardOutput);
            Assert.Contains("Stop: no leading unconditional jump", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchSpineStopsOnRepeat()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                [
                    0xE9, 0x05, 0x00, 0x00, 0x00,
                    0x90, 0x90, 0x90, 0x90, 0x90,
                    0xE9, 0xF1, 0xFF, 0xFF, 0xFF,
                    0xC3,
                ]);

            var result = await RunDataToolAsync(
                "raw-module-branch-spine",
                rawModulePath,
                "--module-name",
                "DataThread.dll",
                "--rva",
                "0x0",
                "--steps",
                "4",
                "--prelude-bytes",
                "5");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Step 0: DataThread.dll+0x0 -> DataThread.dll+0xA", result.StandardOutput);
            Assert.Contains("Step 1: DataThread.dll+0xA -> DataThread.dll+0x0", result.StandardOutput);
            Assert.Contains("Stop: repeat detected at DataThread.dll+0x0", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Theory]
    [InlineData("--steps", "0", "steps")]
    [InlineData("--steps", "not-a-number", "steps")]
    [InlineData("--prelude-bytes", "0", "prelude-bytes")]
    [InlineData("--prelude-bytes", "not-a-number", "prelude-bytes")]
    public async Task RawModuleBranchSpineRejectsInvalidStepsAndPreludeArguments(
        string option,
        string value,
        string expectedMessage)
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(rawModulePath, [0xC3]);

            var result = await RunDataToolAsync(
                "raw-module-branch-spine",
                rawModulePath,
                "--rva",
                "0x0",
                option,
                value);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                expectedMessage,
                result.StandardOutput + result.StandardError,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public async Task RawModuleBranchMapRejectsInvalidFollowDepth(string followDepth)
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(rawModulePath, [0xC3]);

            var result = await RunDataToolAsync(
                "raw-module-branch-map",
                rawModulePath,
                "--rva",
                "0x0",
                "--follow-depth",
                followDepth);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "follow-depth",
                result.StandardOutput + result.StandardError,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawModuleBranchMapRejectsRvaOutsideRawModuleBounds()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawModulePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            await WriteFixtureBytesAsync(
                rawModulePath,
                [
                    0xE9, 0x05, 0x00, 0x00, 0x00,
                    0x90, 0x90, 0x90, 0x90, 0x90,
                    0xC3,
                ]);

            var result = await RunDataToolAsync(
                "raw-module-branch-map",
                rawModulePath,
                "--rva",
                "0x20",
                "--length",
                "32",
                "--module-name",
                "DataThread.dll");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "outside raw module bounds",
                result.StandardOutput + result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-raw-module-branch-map-" + Guid.NewGuid().ToString("N"));
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

    private static async Task WriteFixtureBytesAsync(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
    }

    private static byte[] BuildFixtureBytes(int length, params (int Offset, byte[] Bytes)[] patches)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, (byte)0x90);
        foreach (var patch in patches)
        {
            Array.Copy(patch.Bytes, 0, bytes, patch.Offset, patch.Bytes.Length);
        }

        return bytes;
    }

    private static string[] SplitOutputLines(string output)
    {
        return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
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

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var fromOutputDirectory = TryFindRepositoryRoot(AppContext.BaseDirectory);
        if (fromOutputDirectory is not null)
        {
            return fromOutputDirectory;
        }

        var fromWorkingDirectory = TryFindRepositoryRoot(Environment.CurrentDirectory);
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (sourceDirectory is not null)
        {
            var fromSourceDirectory = TryFindRepositoryRoot(sourceDirectory);
            if (fromSourceDirectory is not null)
            {
                return fromSourceDirectory;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output or working directory.");
    }

    private static string? TryFindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
