using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolMinidumpModuleCarveTests
{
    private const string CommandName = "minidump-module-carve";
    private const string ExpectedUsage =
        "WolfConquer.DataTool minidump-module-carve <dumpPath> <moduleName> <outputPath> [--fill hexByte]";

    [Fact]
    public async Task HelpMentionsMinidumpModuleCarveCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(ExpectedUsage, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task MinidumpModuleCarveWritesFlatModuleImageWithFillForGaps()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var dumpPath = Path.Combine(tempRoot, "client.dmp");
            var outputPath = Path.Combine(tempRoot, "DataThread.runtime.bin");
            await File.WriteAllBytesAsync(
                dumpPath,
                BuildSyntheticMinidump(
                    moduleBase: 0x10000000,
                    moduleSize: 0x40,
                    rangeBase: 0x10000010,
                    rangeBytes: [0xDE, 0xAD, 0xBE, 0xEF]));

            var result = await RunDataToolAsync(
                CommandName,
                dumpPath,
                "DataThread.dll",
                outputPath,
                "--fill",
                "CC");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Minidump module carve:", result.StandardOutput);
            Assert.Contains($"Dump: {dumpPath}", result.StandardOutput);
            Assert.Contains("Module: DataThread.dll", result.StandardOutput);
            Assert.Contains("Base: 0x10000000", result.StandardOutput);
            Assert.Contains("Size: 0x40 (64 bytes)", result.StandardOutput);
            Assert.Contains("Memory ranges copied: 1", result.StandardOutput);
            Assert.Contains("Bytes copied: 4", result.StandardOutput);
            Assert.Contains("Fill byte: 0xCC", result.StandardOutput);
            Assert.Contains($"Output: {outputPath}", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);

            var carved = await File.ReadAllBytesAsync(outputPath);
            Assert.Equal(0x40, carved.Length);
            Assert.All(carved.Take(0x10), value => Assert.Equal(0xCC, value));
            Assert.Equal([0xDE, 0xAD, 0xBE, 0xEF], carved.Skip(0x10).Take(4).ToArray());
            Assert.All(carved.Skip(0x14), value => Assert.Equal(0xCC, value));
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task MinidumpModuleCarveRejectsNonMinidumpInput()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var dumpPath = Path.Combine(tempRoot, "not-a-dump.bin");
            var outputPath = Path.Combine(tempRoot, "out.bin");
            await File.WriteAllBytesAsync(dumpPath, [0x4E, 0x4F, 0x50, 0x45]);

            var result = await RunDataToolAsync(CommandName, dumpPath, "DataThread.dll", outputPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains("Input file is not a minidump", result.StandardError);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static byte[] BuildSyntheticMinidump(
        ulong moduleBase,
        uint moduleSize,
        ulong rangeBase,
        byte[] rangeBytes)
    {
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
        writer.Write(moduleBase);
        writer.Write(moduleSize);
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
        writer.Write(rangeBase);
        writer.Write((uint)rangeBytes.Length);
        var memoryBytesRvaPosition = stream.Position;
        writer.Write(0U);
        var memoryListSize = checked((uint)(stream.Position - memoryListRva));

        Align(writer, 4);
        var memoryBytesRva = (uint)stream.Position;
        writer.Write(rangeBytes);
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
            "wolfconquer-minidump-module-carve-" + Guid.NewGuid().ToString("N"));
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
