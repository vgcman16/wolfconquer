using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed class ClientItemCatalogRoundtripCommandTests : IDisposable
{
    private const string Command = "client-item-catalog-roundtrip";
    private const string Warning = "Decoded catalog only; this is not a packed client file and must not be installed as itemtype.dat.";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolfconquer-catalog-roundtrip-" + Guid.NewGuid().ToString("N"));

    public ClientItemCatalogRoundtripCommandTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task DefaultCommandReportsExactDecodedRoundtripWithoutCreatingOrChangingFiles()
    {
        var source = Path.Combine(_root, "decoded.bin");
        var bytes = CatalogFixture();
        await File.WriteAllBytesAsync(source, bytes);
        var originalWriteTime = File.GetLastWriteTimeUtc(source);

        var result = await RunAsync(source);

        AssertSuccess(result);
        Assert.Contains($"Rows=4 FieldsPerRow=65 Bytes={bytes.Length} ExactRoundtrip=True", result.Output);
        Assert.Contains(Warning, result.Output);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(source));
        Assert.Equal(new[] { source }, Directory.GetFiles(_root));
        Assert.Empty(Directory.GetDirectories(_root));
    }

    [Fact]
    public async Task ExplicitFreshOutputContainsTheExactDecodedSourceBytes()
    {
        var source = Path.Combine(_root, "source.decoded");
        var output = Path.Combine(_root, "new-output.decoded");
        var bytes = CatalogFixture();
        await File.WriteAllBytesAsync(source, bytes);

        var result = await RunAsync(source, "--output-new", output);

        AssertSuccess(result);
        Assert.Contains($"Rows=4 FieldsPerRow=65 Bytes={bytes.Length} ExactRoundtrip=True", result.Output);
        Assert.Contains(Warning, result.Output);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(output));
        Assert.Equal(2, Directory.GetFiles(_root).Length);
        Assert.Empty(Directory.GetDirectories(_root));
    }

    [Fact]
    public async Task ExistingOutputIsRefusedWithoutChangingEitherFile()
    {
        var source = Path.Combine(_root, "source.decoded");
        var output = Path.Combine(_root, "preserved.output");
        var bytes = CatalogFixture();
        byte[] preserved = [0xFE, 0xFF, 0, 1, 2, 3];
        await File.WriteAllBytesAsync(source, bytes);
        await File.WriteAllBytesAsync(output, preserved);
        var originalWriteTime = File.GetLastWriteTimeUtc(output);

        var result = await RunAsync(source, "--output-new", output);

        AssertFailure(result);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.Equal(preserved, await File.ReadAllBytesAsync(output));
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(output));
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Fact]
    public async Task SourceCannotAlsoBeTheCreateNewOutput()
    {
        var source = Path.Combine(_root, "source.decoded");
        var bytes = CatalogFixture();
        await File.WriteAllBytesAsync(source, bytes);

        var result = await RunAsync(source, "--output-new", source);

        AssertFailure(result);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.Equal(new[] { source }, Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData("no-arguments")]
    [InlineData("unknown-option")]
    [InlineData("missing-output-value")]
    [InlineData("duplicate-output-option")]
    [InlineData("option-before-source")]
    [InlineData("extra-positional")]
    [InlineData("extra-after-output")]
    public async Task InvalidArgumentShapesFailWithoutWrites(string scenario)
    {
        var source = Path.Combine(_root, "source.decoded");
        var output = Path.Combine(_root, "must-not-exist.decoded");
        var bytes = CatalogFixture();
        await File.WriteAllBytesAsync(source, bytes);
        string[] arguments = scenario switch
        {
            "no-arguments" => [],
            "unknown-option" => [source, "--unknown"],
            "missing-output-value" => [source, "--output-new"],
            "duplicate-output-option" => [source, "--output-new", output, "--output-new", Path.Combine(_root, "other.decoded")],
            "option-before-source" => ["--output-new", output, source],
            "extra-positional" => [source, output],
            "extra-after-output" => [source, "--output-new", output, "extra"],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        var result = await RunAsync(arguments);

        AssertFailure(result);
        Assert.Contains(Command, result.Error);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.Equal(new[] { source }, Directory.GetFiles(_root));
        Assert.Empty(Directory.GetDirectories(_root));
    }

    [Fact]
    public async Task MissingSourceDoesNotCreateTheRequestedOutput()
    {
        var output = Path.Combine(_root, "must-not-exist.decoded");

        var result = await RunAsync(Path.Combine(_root, "missing.decoded"), "--output-new", output);

        AssertFailure(result);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("packed-binary")]
    [InlineData("64-fields")]
    [InlineData("66-fields")]
    [InlineData("67-fields")]
    [InlineData("missing-trailer")]
    [InlineData("nul")]
    public async Task InvalidDecodedSchemasDoNotPublishAnOutput(string scenario)
    {
        var source = Path.Combine(_root, "invalid.decoded");
        var output = Path.Combine(_root, "must-not-exist.decoded");
        var bytes = scenario switch
        {
            "empty" => [],
            "packed-binary" => new byte[] { 0xEF, 0xFF, 0x32, 0x81, 0, 0xFE },
            "64-fields" => Row("1", "Name", "\n", 64),
            "66-fields" => Row("1", "Name", "\n", 66),
            "67-fields" => Row("1", "Name", "\n", 67),
            "missing-trailer" => Row("1", "Name", "", 65)[..^2],
            "nul" => Row("1", "Bad\0Name", "\n", 65),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        await File.WriteAllBytesAsync(source, bytes);

        var result = await RunAsync(source, "--output-new", output);

        AssertFailure(result);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
        Assert.False(File.Exists(output));
        Assert.Equal(new[] { source }, Directory.GetFiles(_root));
    }

    [Fact]
    public async Task InputLargerThanSixtyFourMiBIsRejectedBeforeAnyOutputIsCreated()
    {
        var source = Path.Combine(_root, "oversized.decoded");
        var output = Path.Combine(_root, "must-not-exist.decoded");
        const long oversizedLength = 64L * 1024 * 1024 + 1;
        using (var stream = new FileStream(source, FileMode.CreateNew, FileAccess.Write))
        {
            stream.SetLength(oversizedLength);
        }
        var originalWriteTime = File.GetLastWriteTimeUtc(source);

        var result = await RunAsync(source, "--output-new", output);

        AssertFailure(result);
        Assert.Contains("64", result.Error);
        Assert.Equal(oversizedLength, new FileInfo(source).Length);
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(source));
        Assert.False(File.Exists(output));
        Assert.Equal(new[] { source }, Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReparseAncestorsCannotRedirectSourceReadsOrOutputWrites(bool sourceThroughLink)
    {
        var target = Path.Combine(_root, "physical-target");
        var link = Path.Combine(_root, "junction");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "preserve.marker");
        await File.WriteAllBytesAsync(marker, [0x71, 0x72, 0x73]);
        var physicalSource = Path.Combine(sourceThroughLink ? target : _root, "source.decoded");
        var bytes = CatalogFixture();
        await File.WriteAllBytesAsync(physicalSource, bytes);
        await CreateDirectoryLinkAsync(link, target);
        try
        {
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            var source = sourceThroughLink ? Path.Combine(link, "source.decoded") : physicalSource;
            var output = Path.Combine(sourceThroughLink ? _root : link, "must-not-exist.decoded");

            var result = await RunAsync(source, "--output-new", output);

            AssertFailure(result);
            Assert.Contains("reparse", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(physicalSource));
            Assert.Equal(new byte[] { 0x71, 0x72, 0x73 }, await File.ReadAllBytesAsync(marker));
            Assert.False(File.Exists(Path.Combine(_root, "must-not-exist.decoded")));
            Assert.False(File.Exists(Path.Combine(target, "must-not-exist.decoded")));
        }
        finally
        {
            DeleteDirectoryLinkOnly(link);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReparseDirectoryAtTheExactSourceOrOutputPathIsExplicitlyRefused(bool sourceIsLink)
    {
        var target = Path.Combine(_root, "physical-target");
        var link = Path.Combine(_root, "linked.decoded");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "preserve.marker");
        await File.WriteAllBytesAsync(marker, [0x21, 0x22]);
        var physicalSource = Path.Combine(_root, "source.decoded");
        var bytes = CatalogFixture();
        await File.WriteAllBytesAsync(physicalSource, bytes);
        await CreateDirectoryLinkAsync(link, target);
        try
        {
            var source = sourceIsLink ? link : physicalSource;
            var output = sourceIsLink ? Path.Combine(_root, "must-not-exist.decoded") : link;

            var result = await RunAsync(source, "--output-new", output);

            AssertFailure(result);
            Assert.Contains("reparse", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(physicalSource));
            Assert.Equal(new byte[] { 0x21, 0x22 }, await File.ReadAllBytesAsync(marker));
            Assert.Equal(new[] { marker }, Directory.GetFiles(target));
            Assert.False(File.Exists(Path.Combine(_root, "must-not-exist.decoded")));
        }
        finally
        {
            DeleteDirectoryLinkOnly(link);
        }
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_root);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.StartsWith(temporaryRoot, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("wolfconquer-catalog-roundtrip-", Path.GetFileName(resolved));
        if (!Directory.Exists(resolved)) return;
        foreach (var directory in Directory.GetDirectories(resolved))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(directory);
            }
        }
        Directory.Delete(resolved, recursive: true);
    }

    private static byte[] CatalogFixture()
    {
        using var output = new MemoryStream();
        output.Write(Row("0", "First", "\r\n"));
        var rawNameRow = Row("00042", "GBKNAME", "\n");
        var nameOffset = Ascii("00042@@").Length;
        new byte[] { 0xD2, 0xA9, 0xC6, 0xB7, 0x81, 0x80, 0xFF }.CopyTo(rawNameRow, nameOffset);
        output.Write(rawNameRow);
        output.Write(Row("1001000", "Third", "\r"));
        output.Write(Row("4294967295", "Tail", ""));
        return output.ToArray();
    }

    private static byte[] Row(string itemId, string name, string ending, int fields = 65)
    {
        var values = Enumerable.Range(0, fields).Select(index => index == 0 ? itemId : index == 1 ? name : $"field{index:D2}");
        return Ascii(string.Join("@@", values) + "@@" + ending);
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

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
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
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
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "Could not start junction creation.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"Junction creation timed out. stdout: {await output} stderr: {await error}");
        }
        Assert.True(process.ExitCode == 0, $"Could not create junction. stdout: {await output} stderr: {await error}");
    }

    private static void DeleteDirectoryLinkOnly(string path)
    {
        Assert.True((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0);
        Directory.Delete(path);
    }

    private static void AssertSuccess(RunResult result)
    {
        Assert.True(result.ExitCode == 0, $"Exit {result.ExitCode}. stdout: {result.Output} stderr: {result.Error}");
        Assert.Equal(string.Empty, result.Error);
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
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        }
        throw new InvalidOperationException("Could not locate WolfConquer.sln.");
    }

    private sealed record RunResult(int ExitCode, string Output, string Error);
}
