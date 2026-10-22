using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientItemCatalogCodecCommandTests : IDisposable
{
    private const string Command = "client-item-catalog-codec";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolfconquer-catalog-codec-" + Guid.NewGuid().ToString("N"));

    public ClientItemCatalogCodecCommandTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("encode")]
    [InlineData("decode")]
    [InlineData("EnCoDe")]
    [InlineData("DeCoDe")]
    public async Task DefaultBothModesReportExactRoundtripAndLeaveTheSourceAndDirectoryUnchanged(string requestedMode)
    {
        var mode = requestedMode.ToLowerInvariant();
        var plain = PlainFixture();
        var sourceBytes = InputForMode(mode, plain);
        var source = Path.Combine(_root, "input.dat");
        await File.WriteAllBytesAsync(source, sourceBytes);
        var writeTime = File.GetLastWriteTimeUtc(source);

        var result = await RunAsync(requestedMode, source);

        AssertSuccess(result, mode, sourceBytes.Length, rows: 2);
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(source));
        Assert.Equal(writeTime, File.GetLastWriteTimeUtc(source));
        Assert.Equal(new[] { source }, Directory.GetFiles(_root));
        Assert.Empty(Directory.GetDirectories(_root));
    }

    [Theory]
    [InlineData("encode")]
    [InlineData("decode")]
    public async Task CreateNewOutputIntegratesTheCodecUsingOnlySyntheticRows(string mode)
    {
        var plain = PlainFixture();
        var input = InputForMode(mode, plain);
        var source = Path.Combine(_root, "input.dat");
        var output = Path.Combine(_root, "fresh-output.dat");
        await File.WriteAllBytesAsync(source, input);

        var result = await RunAsync(mode, source, "--output-new", output);

        AssertSuccess(result, mode, input.Length, rows: 2);
        var actual = await File.ReadAllBytesAsync(output);
        Assert.Equal(input.Length, actual.Length);
        Assert.Equal(input, await File.ReadAllBytesAsync(source));
        if (mode == "encode")
        {
            // This is CLI/Core integration, not captured-client oracle evidence.
            Assert.Equal(plain, ClientItemCatalogCodec.Decode(actual));
        }
        else
        {
            Assert.Equal(plain, actual);
        }
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Theory]
    [InlineData("encode")]
    [InlineData("decode")]
    public async Task ExistingOutputIsNeverOverwrittenInEitherMode(string mode)
    {
        var input = InputForMode(mode, PlainFixture());
        var source = Path.Combine(_root, "input.dat");
        var output = Path.Combine(_root, "preserved.dat");
        byte[] preserved = [0xFE, 0xFF, 0, 1, 2];
        await File.WriteAllBytesAsync(source, input);
        await File.WriteAllBytesAsync(output, preserved);
        var writeTime = File.GetLastWriteTimeUtc(output);

        var result = await RunAsync(mode, source, "--output-new", output);

        AssertFailure(result);
        Assert.Equal(input, await File.ReadAllBytesAsync(source));
        Assert.Equal(preserved, await File.ReadAllBytesAsync(output));
        Assert.Equal(writeTime, File.GetLastWriteTimeUtc(output));
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Theory]
    [InlineData("unknown-mode")]
    [InlineData("missing-mode")]
    [InlineData("missing-input")]
    [InlineData("missing-output-value")]
    [InlineData("unknown-option")]
    [InlineData("duplicate-output-option")]
    [InlineData("extra-positional")]
    [InlineData("option-before-source")]
    public async Task BadArgumentsDoNotCreateAnyOutputsOrChangeTheInput(string scenario)
    {
        var source = Path.Combine(_root, "input.dat");
        var output = Path.Combine(_root, "must-not-exist.dat");
        var bytes = PlainFixture();
        await File.WriteAllBytesAsync(source, bytes);
        string[] arguments = scenario switch
        {
            "unknown-mode" => ["encrypt", source],
            "missing-mode" => [source],
            "missing-input" => ["decode"],
            "missing-output-value" => ["encode", source, "--output-new"],
            "unknown-option" => ["encode", source, "--unknown", output],
            "duplicate-output-option" => ["encode", source, "--output-new", output, "--output-new", output + ".other"],
            "extra-positional" => ["encode", source, output],
            "option-before-source" => ["encode", "--output-new", output, source],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        var result = await RunAsync(arguments);

        AssertFailure(result);
        Assert.Contains(Command, result.Error);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.Equal(new[] { source }, Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData("encode", 64)]
    [InlineData("encode", 67)]
    [InlineData("decode", 64)]
    [InlineData("decode", 67)]
    public async Task BothModesRejectNonNativeSchemasBeforePublishingOutput(string mode, int fields)
    {
        var plain = Row("101", Encoding.ASCII.GetBytes("Synthetic One"), "\r\n", fields);
        var bytes = InputForMode(mode, plain);
        var source = Path.Combine(_root, "invalid-schema.dat");
        var output = Path.Combine(_root, "must-not-exist.dat");
        await File.WriteAllBytesAsync(source, bytes);

        var result = await RunAsync(mode, source, "--output-new", output);

        AssertFailure(result);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.False(File.Exists(output));
        Assert.Equal(new[] { source }, Directory.GetFiles(_root));
    }

    [Fact]
    public async Task InvalidPackedBytesAreNotTreatedAsASuccessfulEmptyDocument()
    {
        var source = Path.Combine(_root, "invalid-packed.dat");
        var output = Path.Combine(_root, "must-not-exist.dat");
        byte[] bytes = [1, 2, 3, 4, 5, 6, 7];
        await File.WriteAllBytesAsync(source, bytes);

        var result = await RunAsync("decode", source, "--output-new", output);

        AssertFailure(result);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task MissingInputDoesNotCreateOutput()
    {
        var result = await RunAsync("decode", Path.Combine(_root, "missing.dat"), "--output-new", Path.Combine(_root, "new.dat"));

        AssertFailure(result);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public async Task InputCannotAlsoBeTheOutput()
    {
        var source = Path.Combine(_root, "input.dat");
        var bytes = PlainFixture();
        await File.WriteAllBytesAsync(source, bytes);

        var result = await RunAsync("encode", source, "--output-new", source);

        AssertFailure(result);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.Equal(new[] { source }, Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData("encode")]
    [InlineData("decode")]
    public async Task OversizedInputsAreRefusedBeforeAnyOutputInBothModes(string mode)
    {
        var source = Path.Combine(_root, "oversized.dat");
        var output = Path.Combine(_root, "must-not-exist.dat");
        const long length = 64L * 1024 * 1024 + 1;
        using (var file = new FileStream(source, FileMode.CreateNew, FileAccess.Write)) file.SetLength(length);
        var writeTime = File.GetLastWriteTimeUtc(source);

        var result = await RunAsync(mode, source, "--output-new", output);

        AssertFailure(result);
        Assert.Contains("64", result.Error);
        Assert.Equal(length, new FileInfo(source).Length);
        Assert.Equal(writeTime, File.GetLastWriteTimeUtc(source));
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedReparseGuardRejectsLinkedInputOrOutputAncestors(bool inputThroughLink)
    {
        var target = Path.Combine(_root, "physical-target");
        var link = Path.Combine(_root, "junction");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "preserve.marker");
        await File.WriteAllBytesAsync(marker, [0x12, 0x34]);
        var physicalSource = Path.Combine(inputThroughLink ? target : _root, "input.dat");
        var bytes = PlainFixture();
        await File.WriteAllBytesAsync(physicalSource, bytes);
        await CreateDirectoryLinkAsync(link, target);
        try
        {
            var source = inputThroughLink ? Path.Combine(link, "input.dat") : physicalSource;
            var output = Path.Combine(inputThroughLink ? _root : link, "must-not-exist.dat");

            var result = await RunAsync("encode", source, "--output-new", output);

            AssertFailure(result);
            Assert.Contains("reparse", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(physicalSource));
            Assert.Equal(new byte[] { 0x12, 0x34 }, await File.ReadAllBytesAsync(marker));
            Assert.False(File.Exists(Path.Combine(_root, "must-not-exist.dat")));
            Assert.False(File.Exists(Path.Combine(target, "must-not-exist.dat")));
        }
        finally
        {
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            Directory.Delete(link);
        }
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_root);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.StartsWith(temporaryRoot, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("wolfconquer-catalog-codec-", Path.GetFileName(resolved));
        if (!Directory.Exists(resolved)) return;
        foreach (var directory in Directory.GetDirectories(resolved))
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) Directory.Delete(directory);
        Directory.Delete(resolved, recursive: true);
    }

    private static byte[] PlainFixture()
    {
        var first = Row("101", Encoding.ASCII.GetBytes("Synthetic One"), "\r\n");
        var second = Row("202", [0x81, 0x80, 0xFE, 0xFF], "");
        return first.Concat(second).ToArray();
    }

    private static byte[] InputForMode(string mode, byte[] plain) =>
        mode == "decode" ? ClientItemCatalogCodec.Encode(plain) : plain.ToArray();

    private static byte[] Row(string id, byte[] name, string ending, int fields = 65)
    {
        using var output = new MemoryStream();
        for (var index = 0; index < fields; ++index)
        {
            output.Write(index == 0 ? Encoding.ASCII.GetBytes(id) : index == 1 ? name : new byte[] { (byte)'0' });
            output.Write("@@"u8);
        }
        output.Write(Encoding.ASCII.GetBytes(ending));
        return output.ToArray();
    }

    private async Task<RunResult> RunAsync(params string[] arguments)
    {
        using var process = TestProjectProcess.Create(FindRepositoryRoot(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = _root;
        process.StartInfo.ArgumentList.Add(Command);
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "Could not start DataTool.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await output} stderr: {await error}");
        }
        return new RunResult(process.ExitCode, await output, await error);
    }

    private static async Task CreateDirectoryLinkAsync(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            },
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "Could not start junction creation.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"Junction creation timed out. stdout: {await output} stderr: {await error}");
        }
        Assert.True(process.ExitCode == 0, $"Junction creation failed. stdout: {await output} stderr: {await error}");
    }

    private static void AssertSuccess(RunResult result, string mode, int bytes, int rows)
    {
        Assert.True(result.ExitCode == 0, $"Exit {result.ExitCode}. stdout: {result.Output} stderr: {result.Error}");
        Assert.Equal(string.Empty, result.Error);
        Assert.Contains($"Mode={mode} Rows={rows} FieldsPerRow=65 InputBytes={bytes} OutputBytes={bytes} ExactRoundtrip=True", result.Output);
        if (mode == "decode")
        {
            Assert.Contains("Decoded", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not a packed", result.Output, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains("candidate", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("client", result.Output, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertFailure(RunResult result)
    {
        Assert.Equal(1, result.ExitCode);
        Assert.NotEqual(string.Empty, result.Error);
        Assert.DoesNotContain("Unhandled exception", result.Error);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(sourcePath)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Could not locate WolfConquer.sln.");
    }

    private sealed record RunResult(int ExitCode, string Output, string Error);
}
