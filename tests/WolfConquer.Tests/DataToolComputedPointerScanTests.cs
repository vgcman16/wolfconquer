using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolComputedPointerScanTests
{
    private const uint LicenseRoutePointerValue = 0x6F99F794;
    private const string ComputedPointerScanCommandText =
        "WolfConquer.DataTool computed-pointer-scan";

    [Fact]
    public async Task HelpMentionsComputedPointerScanUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(ComputedPointerScanCommandText, result.StandardOutput);
        Assert.Contains("--context-bytes N", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task RawImageScanReportsPerFileTargetsCountsAndHexOffsets()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawImagePath = Path.Combine(tempRoot, "DataThread.raw.dll");
            var noHitPath = Path.Combine(tempRoot, "license.raw.dll");
            await WriteFixtureBytesAsync(
                rawImagePath,
                [
                    0x00, 0x01, 0x02, 0x03,
                    0x94, 0xF7, 0x99, 0x6F,
                    0x10, 0x11, 0x12,
                    0x94, 0xF7, 0x99, 0x6F,
                    0x20,
                ]);
            await WriteFixtureBytesAsync(noHitPath, [0xDE, 0xAD, 0xBE, 0xEF, 0x00]);

            var result = await RunDataToolAsync(
                "computed-pointer-scan",
                "--target",
                "license-route-pointer=94F7996F",
                "--target",
                "deadbeef=EFBEADDE",
                rawImagePath,
                noHitPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Computed pointer scan:", result.StandardOutput);
            Assert.Contains("Targets: 2", result.StandardOutput);
            Assert.Contains("Target: name=license-route-pointer bytes=94f7996f", result.StandardOutput);
            Assert.Contains("Target: name=deadbeef bytes=efbeadde", result.StandardOutput);
            Assert.Contains($"File: path={rawImagePath}", result.StandardOutput);
            Assert.Contains("minidump=False modules=0 memoryRanges=0", result.StandardOutput);
            Assert.Contains("Hit: target=license-route-pointer count=2 offsets=0x4,0xB", result.StandardOutput);
            Assert.Contains("Hit: target=deadbeef count=0 offsets=<none>", result.StandardOutput);
            Assert.Contains($"File: path={noHitPath}", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RawImageScanWithContextBytesReportsContextLineForEachHit()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawImagePath = Path.Combine(tempRoot, "context.raw.dll");
            await WriteFixtureBytesAsync(
                rawImagePath,
                [
                    0xAA, 0xBB, 0xCC, 0xDD,
                    0x94, 0xF7, 0x99, 0x6F,
                    0x11, 0x22, 0x33, 0x44,
                ]);

            var result = await RunDataToolAsync(
                "computed-pointer-scan",
                "--context-bytes",
                "2",
                "--target",
                "ptr=94F7996F",
                rawImagePath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Hit: target=ptr count=1 offsets=0x4", result.StandardOutput);
            var contextLine = Assert.Single(
                result.StandardOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
                line => line.StartsWith("Context:", StringComparison.Ordinal));
            Assert.Matches(
                "^Context: target=ptr offset=0x4 startOffset=0x2 bytes=ccdd94f7996f1122$",
                contextLine);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task MinidumpScanMapsLittleEndianPointerHitsToVirtualAddressModuleAndRva()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var firstDumpPath = Path.Combine(tempRoot, "license-route-first.dmp");
            var secondDumpPath = Path.Combine(tempRoot, "license-route-second.dmp");
            var firstDump = BuildSyntheticMinidump(
                dataThreadBase: 0x02510000,
                targetRva: 0x342B96,
                out var firstHitFileOffset,
                out var firstHitVirtualAddress);
            var secondDump = BuildSyntheticMinidump(
                dataThreadBase: 0x02510000,
                targetRva: 0x8D76C3,
                out var secondHitFileOffset,
                out var secondHitVirtualAddress);
            await WriteFixtureBytesAsync(firstDumpPath, firstDump);
            await WriteFixtureBytesAsync(secondDumpPath, secondDump);

            var result = await RunDataToolAsync(
                "computed-pointer-scan",
                "--target",
                "license-route-pointer=94F7996F",
                firstDumpPath,
                secondDumpPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Computed pointer scan:", result.StandardOutput);
            Assert.Contains($"File: path={firstDumpPath}", result.StandardOutput);
            Assert.Contains("minidump=True modules=1 memoryRanges=1", result.StandardOutput);
            Assert.Contains("Hit: target=license-route-pointer count=1", result.StandardOutput);
            Assert.Contains(
                $"Map: target=license-route-pointer offset=0x{firstHitFileOffset:X} va=0x{firstHitVirtualAddress:X} module=DataThread.dll rva=0x342B96",
                result.StandardOutput);
            Assert.Contains($"File: path={secondDumpPath}", result.StandardOutput);
            Assert.Contains(
                $"Map: target=license-route-pointer offset=0x{secondHitFileOffset:X} va=0x{secondHitVirtualAddress:X} module=DataThread.dll rva=0x8D76C3",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task MissingTargetsOrInputsReturnUsageAndExitOne()
    {
        var result = await RunDataToolAsync("computed-pointer-scan");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains($"Usage: {ComputedPointerScanCommandText}", result.StandardError);
        Assert.Contains("--context-bytes", result.StandardError);
    }

    [Fact]
    public async Task InvalidContextBytesReturnUsageAndExitOne()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var rawImagePath = Path.Combine(tempRoot, "context.raw.dll");
            await WriteFixtureBytesAsync(rawImagePath, [0x94, 0xF7, 0x99, 0x6F]);

            var result = await RunDataToolAsync(
                "computed-pointer-scan",
                "--context-bytes",
                "nope",
                "--target",
                "ptr=94F7996F",
                rawImagePath);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Matches("(?i)context[- ]bytes", result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static byte[] BuildSyntheticMinidump(
        ulong dataThreadBase,
        uint targetRva,
        out long hitFileOffset,
        out ulong hitVirtualAddress)
    {
        var memoryBytes = Enumerable.Repeat((byte)0xCC, 0x40).ToArray();
        const int hitOffsetInMemoryRange = 0x10;
        BitConverter.GetBytes(LicenseRoutePointerValue).CopyTo(memoryBytes, hitOffsetInMemoryRange);
        var memoryRangeBase = dataThreadBase + targetRva - hitOffsetInMemoryRange;
        hitVirtualAddress = memoryRangeBase + hitOffsetInMemoryRange;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);

        writer.Write(0x504D444DU);
        writer.Write(0x0000A793U);
        writer.Write(2U);
        writer.Write(32U);
        writer.Write(0U);
        writer.Write(0U);
        writer.Write(0UL);

        var moduleListDirectoryPosition = stream.Position;
        writer.Write(4U);
        writer.Write(0U);
        writer.Write(0U);

        var memoryListDirectoryPosition = stream.Position;
        writer.Write(5U);
        writer.Write(0U);
        writer.Write(0U);

        Align(writer, 4);
        var moduleListRva = (uint)stream.Position;
        writer.Write(1U);
        writer.Write(dataThreadBase);
        writer.Write(0x01000000U);
        writer.Write(0U);
        writer.Write(0U);
        var moduleNameRvaPosition = stream.Position;
        writer.Write(0U);
        WriteZeros(writer, 52);
        WriteZeros(writer, 8);
        WriteZeros(writer, 8);
        writer.Write(0UL);
        writer.Write(0UL);
        var moduleListSize = checked((uint)(stream.Position - moduleListRva));

        Align(writer, 4);
        var moduleNameRva = (uint)stream.Position;
        var moduleNameBytes = Encoding.Unicode.GetBytes("DataThread.dll");
        writer.Write((uint)moduleNameBytes.Length);
        writer.Write(moduleNameBytes);
        WriteUInt32At(writer, moduleNameRvaPosition, moduleNameRva);

        Align(writer, 4);
        var memoryListRva = (uint)stream.Position;
        writer.Write(1U);
        writer.Write(memoryRangeBase);
        writer.Write((uint)memoryBytes.Length);
        var memoryBytesRvaPosition = stream.Position;
        writer.Write(0U);
        var memoryListSize = checked((uint)(stream.Position - memoryListRva));

        Align(writer, 4);
        var memoryBytesRva = (uint)stream.Position;
        hitFileOffset = memoryBytesRva + hitOffsetInMemoryRange;
        writer.Write(memoryBytes);
        WriteUInt32At(writer, memoryBytesRvaPosition, memoryBytesRva);

        PatchDirectoryEntry(writer, moduleListDirectoryPosition, moduleListSize, moduleListRva);
        PatchDirectoryEntry(writer, memoryListDirectoryPosition, memoryListSize, memoryListRva);

        return stream.ToArray();
    }

    private static void PatchDirectoryEntry(BinaryWriter writer, long entryPosition, uint dataSize, uint rva)
    {
        WriteUInt32At(writer, entryPosition + 4, dataSize);
        WriteUInt32At(writer, entryPosition + 8, rva);
    }

    private static void WriteUInt32At(BinaryWriter writer, long position, uint value)
    {
        var stream = writer.BaseStream;
        var savedPosition = stream.Position;
        stream.Position = position;
        writer.Write(value);
        stream.Position = savedPosition;
    }

    private static void Align(BinaryWriter writer, int alignment)
    {
        while (writer.BaseStream.Position % alignment != 0)
        {
            writer.Write((byte)0);
        }
    }

    private static void WriteZeros(BinaryWriter writer, int count)
    {
        for (var index = 0; index < count; index++)
        {
            writer.Write((byte)0);
        }
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-computed-pointer-scan-" + Guid.NewGuid().ToString("N"));
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
