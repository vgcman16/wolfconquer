using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Tests;

public sealed class DataToolClientLaunchEvidenceTests
{
    private const string ClientLaunchEvidenceCommandText =
        "WolfConquer.DataTool client-launch-evidence";

    [Fact]
    public async Task HelpMentionsClientLaunchEvidenceUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(ClientLaunchEvidenceCommandText, result.StandardOutput);
        Assert.Contains("--max-string-offsets N", result.StandardOutput);
        Assert.Contains("--max-import-dlls N", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task EvidenceReportsMetadataPeImportsStringsAndComparison()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var oldPath = Path.Combine(tempRoot, "old-play.exe");
            var currentPath = Path.Combine(tempRoot, "current-play.exe");
            await File.WriteAllBytesAsync(
                oldPath,
                CreateMinimalPeFixture("kernel32.dll", Encoding.ASCII.GetBytes("pre-GameInfo.ini-post")));
            await File.WriteAllBytesAsync(
                currentPath,
                CreateMinimalPeFixture(
                    "user32.dll",
                    [.. Encoding.ASCII.GetBytes("pre-GameInfo.ini-post"), .. Encoding.Unicode.GetBytes("xxEnv_DX9yy")],
                    extraBytes: 16));

            var result = await RunDataToolAsync(
                "client-launch-evidence",
                "--no-default-strings",
                "--string",
                "GameInfo.ini",
                "--string",
                "Env_DX9",
                "--max-string-offsets",
                "2",
                "--max-import-dlls",
                "4",
                "old=" + oldPath,
                "current=" + currentPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client launch evidence:", result.StandardOutput);
            Assert.Contains("Inputs: 2", result.StandardOutput);
            Assert.Contains("Strings: GameInfo.ini, Env_DX9", result.StandardOutput);
            Assert.Contains("String encodings: ascii, utf-16le", result.StandardOutput);
            Assert.Contains($"Executable: label=old path={oldPath}", result.StandardOutput);
            Assert.Contains($"Executable: label=current path={currentPath}", result.StandardOutput);
            Assert.Contains($"SHA256: {ComputeSha256(oldPath)}", result.StandardOutput);
            Assert.Contains($"SHA256: {ComputeSha256(currentPath)}", result.StandardOutput);
            Assert.Contains("Attributes:", result.StandardOutput);
            Assert.Contains("Version: file=<missing> product=<missing>", result.StandardOutput);
            Assert.Contains("PE: valid=True machine=I386 subsystem=WindowsGui importDllCount=1 imports=kernel32.dll", result.StandardOutput);
            Assert.Contains("PE: valid=True machine=I386 subsystem=WindowsGui importDllCount=1 imports=user32.dll", result.StandardOutput);
            Assert.Contains("String: term=GameInfo.ini encoding=ascii present=True count=1", result.StandardOutput);
            Assert.Contains("String: term=Env_DX9 encoding=utf-16le present=True count=1", result.StandardOutput);
            Assert.Contains("Comparison:", result.StandardOutput);
            Assert.Contains("Same size: False", result.StandardOutput);
            Assert.Contains("Same SHA256: False", result.StandardOutput);
            Assert.Contains("Same PE machine: True", result.StandardOutput);
            Assert.Contains("Same PE subsystem: True", result.StandardOutput);
            Assert.Contains("Same import DLL set: False", result.StandardOutput);
            Assert.Contains("Same string presence: False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task EvidenceHandlesNonPeFilesWithoutFailingStringScan()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var path = Path.Combine(tempRoot, "plain-client.bin");
            await File.WriteAllBytesAsync(
                path,
                [
                    .. Encoding.ASCII.GetBytes("prefix play.exe suffix"),
                    .. new byte[80],
                    .. Encoding.Unicode.GetBytes("Update.exe"),
                ]);

            var result = await RunDataToolAsync(
                "client-launch-evidence",
                "--no-default-strings",
                "--string",
                "play.exe",
                "--string",
                "Update.exe",
                "plain=" + path);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Executable: label=plain", result.StandardOutput);
            Assert.Contains("PE: valid=False reason=missing-mz-signature", result.StandardOutput);
            Assert.Contains("String: term=play.exe encoding=ascii present=True count=1", result.StandardOutput);
            Assert.Contains("String: term=Update.exe encoding=utf-16le present=True count=1", result.StandardOutput);
            Assert.Contains("Same SHA256: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task MissingInputReturnsExitOne()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "wolfconquer-missing-client-" + Guid.NewGuid().ToString("N"), "play.exe");

        var result = await RunDataToolAsync("client-launch-evidence", "missing=" + missingPath);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains($"Client executable not found: label=missing path={missingPath}", result.StandardError);
    }

    private static byte[] CreateMinimalPeFixture(string importDllName, byte[] payload, int extraBytes = 0)
    {
        const int peOffset = 0x80;
        const int optionalHeaderSize = 0xE0;
        const int rawSectionOffset = 0x200;
        const int importDescriptorOffset = 0x300;
        const int importNameOffset = 0x340;
        const int payloadOffset = 0x380;

        var bytes = new byte[0x800 + extraBytes];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        WriteUInt32(bytes, 0x3C, peOffset);

        bytes[peOffset] = (byte)'P';
        bytes[peOffset + 1] = (byte)'E';
        var coffOffset = peOffset + 4;
        WriteUInt16(bytes, coffOffset, 0x014C);
        WriteUInt16(bytes, coffOffset + 2, 1);
        WriteUInt16(bytes, coffOffset + 16, optionalHeaderSize);

        var optionalOffset = coffOffset + 20;
        WriteUInt16(bytes, optionalOffset, 0x10B);
        WriteUInt32(bytes, optionalOffset + 16, 0x1000);
        WriteUInt32(bytes, optionalOffset + 20, 0x1000);
        WriteUInt32(bytes, optionalOffset + 24, 0x2000);
        WriteUInt32(bytes, optionalOffset + 28, 0x400000);
        WriteUInt32(bytes, optionalOffset + 32, 0x1000);
        WriteUInt32(bytes, optionalOffset + 36, 0x200);
        WriteUInt32(bytes, optionalOffset + 56, 0x2000);
        WriteUInt32(bytes, optionalOffset + 60, 0x200);
        WriteUInt16(bytes, optionalOffset + 68, 2);
        WriteUInt32(bytes, optionalOffset + 92, 16);
        WriteUInt32(bytes, optionalOffset + 104, 0x1100);
        WriteUInt32(bytes, optionalOffset + 108, 40);

        var sectionOffset = optionalOffset + optionalHeaderSize;
        Encoding.ASCII.GetBytes(".rdata\0\0").CopyTo(bytes, sectionOffset);
        WriteUInt32(bytes, sectionOffset + 8, 0x600);
        WriteUInt32(bytes, sectionOffset + 12, 0x1000);
        WriteUInt32(bytes, sectionOffset + 16, 0x600);
        WriteUInt32(bytes, sectionOffset + 20, rawSectionOffset);

        WriteUInt32(bytes, importDescriptorOffset + 12, 0x1140);
        Encoding.ASCII.GetBytes(importDllName + "\0").CopyTo(bytes, importNameOffset);
        payload.CopyTo(bytes, payloadOffset);
        return bytes;
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-client-launch-evidence-" + Guid.NewGuid().ToString("N"));
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
