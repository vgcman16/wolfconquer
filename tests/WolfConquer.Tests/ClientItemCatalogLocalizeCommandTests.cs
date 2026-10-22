using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientItemCatalogLocalizeCommandTests : IDisposable
{
    private const string Command = "client-item-catalog-localize";
    private const string ExpectedSummary = "Rows=3 SourceRows=7 MatchedNames=2 ChangedNames=1 UnmatchedRows=1 SkippedSourceNames=4 ExactRoundtrip=True";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolfconquer-catalog-localize-" + Guid.NewGuid().ToString("N"));

    public ClientItemCatalogLocalizeCommandTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task DefaultIsReadOnlyAndReportsMatchingChangedUnmatchedAndSkippedNames()
    {
        var fixture = await WriteFixtureAsync();
        var sourceTime = File.GetLastWriteTimeUtc(fixture.SourcePath);
        var donorTime = File.GetLastWriteTimeUtc(fixture.DonorPath);

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath);

        AssertSuccess(result, ExpectedSummary);
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.Equal(fixture.Donor, await File.ReadAllBytesAsync(fixture.DonorPath));
        Assert.Equal(sourceTime, File.GetLastWriteTimeUtc(fixture.SourcePath));
        Assert.Equal(donorTime, File.GetLastWriteTimeUtc(fixture.DonorPath));
        Assert.Equal(2, Directory.GetFiles(_root).Length);
        Assert.Empty(Directory.GetDirectories(_root));
    }

    [Fact]
    public async Task FreshPackedCandidateChangesOnlyMatchingNameBytesNeverRowsIdsOrStatistics()
    {
        var fixture = await WriteFixtureAsync();
        var output = Path.Combine(_root, "fresh-candidate.dat");

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--output-new", output);

        AssertSuccess(result, ExpectedSummary);
        var candidate = await File.ReadAllBytesAsync(output);
        var actualPlain = ClientItemCatalogCodec.Decode(candidate);
        Assert.Equal(ClientPlain(localized: true), actualPlain);
        Assert.Equal(candidate, ClientItemCatalogCodec.Encode(actualPlain));
        var before = ClientItemCatalogDocument.Parse(fixture.Plain);
        var after = ClientItemCatalogDocument.Parse(actualPlain);
        Assert.Equal(new uint[] { 1, 2, 3 }, after.Rows.Select(static row => row.ItemId));
        Assert.DoesNotContain(after.Rows, static row => row.ItemId == 999);
        for (var row = 0; row < before.Rows.Count; ++row)
        {
            Assert.Equal(before.Rows[row].LineEnding.ToArray(), after.Rows[row].LineEnding.ToArray());
            Assert.Equal(65, after.Rows[row].FieldCount);
            for (var field = 0; field < 65; ++field)
                if (field != 1) Assert.Equal(before.Rows[row].GetField(field).ToArray(), after.Rows[row].GetField(field).ToArray());
        }
        Assert.Equal(Ascii("English Potion"), after.Rows[0].GetField(1).ToArray());
        Assert.Equal(before.Rows[1].GetField(1).ToArray(), after.Rows[1].GetField(1).ToArray());
        Assert.Equal(before.Rows[2].GetField(1).ToArray(), after.Rows[2].GetField(1).ToArray());
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.Equal(fixture.Donor, await File.ReadAllBytesAsync(fixture.DonorPath));
        Assert.Equal(3, Directory.GetFiles(_root).Length);
    }

    [Fact]
    public async Task UnknownEnglishIdsAreNotInsertedAndZeroMatchesProduceAByteExactNoOp()
    {
        var fixture = await WriteFixtureAsync();
        var donor = Join(Row("999", Ascii("Unknown English"), 67, "\r\n"), Row("1", [0xB5, 0xA5], 67, ""));
        await File.WriteAllBytesAsync(fixture.DonorPath, donor);
        var output = Path.Combine(_root, "no-op-candidate.dat");

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--output-new", output);

        AssertSuccess(result, "Rows=3 SourceRows=2 MatchedNames=0 ChangedNames=0 UnmatchedRows=3 SkippedSourceNames=1 ExactRoundtrip=True");
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(output));
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.Equal(donor, await File.ReadAllBytesAsync(fixture.DonorPath));
    }

    [Theory]
    [InlineData("existing")]
    [InlineData("source")]
    [InlineData("donor")]
    public async Task CreateNewOutputCannotOverwriteExistingFilesIncludingEitherInput(string target)
    {
        var fixture = await WriteFixtureAsync();
        var existing = Path.Combine(_root, "preserved.dat");
        byte[] preserved = [0, 0xFE, 0xFF, 0x54];
        await File.WriteAllBytesAsync(existing, preserved);
        var output = target == "source" ? fixture.SourcePath : target == "donor" ? fixture.DonorPath : existing;

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--output-new", output);

        AssertFailure(result);
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.Equal(fixture.Donor, await File.ReadAllBytesAsync(fixture.DonorPath));
        Assert.Equal(preserved, await File.ReadAllBytesAsync(existing));
        Assert.Equal(3, Directory.GetFiles(_root).Length);
    }

    [Theory]
    [InlineData("no-arguments")]
    [InlineData("missing-source")]
    [InlineData("missing-donor-flag")]
    [InlineData("unknown-donor-flag")]
    [InlineData("missing-donor-value")]
    [InlineData("missing-output-value")]
    [InlineData("duplicate-donor-flag")]
    [InlineData("duplicate-output-flag")]
    public async Task InvalidArgumentShapesFailBeforeWrites(string scenario)
    {
        var fixture = await WriteFixtureAsync();
        var output = Path.Combine(_root, "must-not-exist.dat");
        string[] arguments = scenario switch
        {
            "no-arguments" => [],
            "missing-source" => ["--english-itemtype", fixture.DonorPath],
            "missing-donor-flag" => [fixture.SourcePath, fixture.DonorPath],
            "unknown-donor-flag" => [fixture.SourcePath, "--unknown", fixture.DonorPath],
            "missing-donor-value" => [fixture.SourcePath, "--english-itemtype"],
            "missing-output-value" => [fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--output-new"],
            "duplicate-donor-flag" => [fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--english-itemtype", fixture.DonorPath],
            "duplicate-output-flag" => [fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--output-new", output, "--output-new", output + ".other"],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        var result = await RunAsync(arguments);

        AssertFailure(result);
        Assert.Contains(Command, result.Error);
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.Equal(fixture.Donor, await File.ReadAllBytesAsync(fixture.DonorPath));
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Theory]
    [InlineData("native-65-fields")]
    [InlineData("68-fields")]
    [InlineData("empty")]
    [InlineData("nul")]
    [InlineData("duplicate-skipped-id")]
    public async Task InvalidDonorsAreRejectedRatherThanPartiallyApplied(string scenario)
    {
        var fixture = await WriteFixtureAsync();
        var donor = scenario switch
        {
            "native-65-fields" => Row("1", Ascii("English"), 65, "\n"),
            "68-fields" => Row("1", Ascii("English"), 68, "\n"),
            "empty" => [],
            "nul" => Row("1", Ascii("Eng\0lish"), 67, "\n"),
            "duplicate-skipped-id" => Join(Row("01", [], 67, "\r\n"), Row("1", Ascii("English"), 67, "")),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        await File.WriteAllBytesAsync(fixture.DonorPath, donor);
        var output = Path.Combine(_root, "must-not-exist.dat");

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--output-new", output);

        AssertFailure(result);
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.Equal(donor, await File.ReadAllBytesAsync(fixture.DonorPath));
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingEitherInputDoesNotPublishCandidate(bool sourceMissing)
    {
        var fixture = await WriteFixtureAsync();
        var source = sourceMissing ? Path.Combine(_root, "missing.dat") : fixture.SourcePath;
        var donor = sourceMissing ? fixture.DonorPath : Path.Combine(_root, "missing.txt");
        var output = Path.Combine(_root, "must-not-exist.dat");

        var result = await RunAsync(source, "--english-itemtype", donor, "--output-new", output);

        AssertFailure(result);
        Assert.False(File.Exists(output));
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.Equal(fixture.Donor, await File.ReadAllBytesAsync(fixture.DonorPath));
    }

    [Fact]
    public async Task DonorThroughReparseAncestorCannotRedirectReads()
    {
        var fixture = await WriteFixtureAsync();
        var target = Path.Combine(_root, "donor-target");
        var link = Path.Combine(_root, "donor-junction");
        Directory.CreateDirectory(target);
        var physicalDonor = Path.Combine(target, "donor.txt");
        await File.WriteAllBytesAsync(physicalDonor, fixture.Donor);
        await CreateDirectoryLinkAsync(link, target);
        try
        {
            var output = Path.Combine(_root, "must-not-exist.dat");

            var result = await RunAsync(fixture.SourcePath, "--english-itemtype", Path.Combine(link, "donor.txt"), "--output-new", output);

            AssertFailure(result);
            Assert.Contains("reparse", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(output));
            Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
            Assert.Equal(fixture.Donor, await File.ReadAllBytesAsync(physicalDonor));
        }
        finally
        {
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            Directory.Delete(link);
        }
    }

    [Fact]
    public async Task OversizedDonorIsRefusedBeforeCandidatePublication()
    {
        var fixture = await WriteFixtureAsync();
        const long length = 64L * 1024 * 1024 + 1;
        using (var file = new FileStream(fixture.DonorPath, FileMode.Truncate, FileAccess.Write)) file.SetLength(length);
        var output = Path.Combine(_root, "must-not-exist.dat");

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--output-new", output);

        AssertFailure(result);
        Assert.Contains("64", result.Error);
        Assert.Equal(length, new FileInfo(fixture.DonorPath).Length);
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.False(File.Exists(output));
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_root);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.StartsWith(temporaryRoot, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("wolfconquer-catalog-localize-", Path.GetFileName(resolved), StringComparison.Ordinal);
        if (!Directory.Exists(resolved)) return;
        foreach (var directory in Directory.GetDirectories(resolved))
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) Directory.Delete(directory);
        Directory.Delete(resolved, recursive: true);
    }

    private async Task<Fixture> WriteFixtureAsync()
    {
        var plain = ClientPlain(localized: false);
        var packed = ClientItemCatalogCodec.Encode(plain);
        var donor = Join(
            Row("1", Ascii("English Potion"), 67, "\r\n"),
            Row("2", Ascii("Already English"), 67, "\n"),
            Row("999", Ascii("Unknown English"), 67, "\r"),
            Row("3", [0xB5, 0xA5], 67, "\n"),
            Row("4", [], 67, "\n"),
            Row("5", Ascii("12345"), 67, "\n"),
            Row("6", Ascii("\tEnglish"), 67, ""));
        var sourcePath = Path.Combine(_root, "packed-source.dat");
        var donorPath = Path.Combine(_root, "english-source.txt");
        await File.WriteAllBytesAsync(sourcePath, packed);
        await File.WriteAllBytesAsync(donorPath, donor);
        return new Fixture(sourcePath, donorPath, plain, packed, donor);
    }

    private static byte[] ClientPlain(bool localized) => Join(
        Row("0001", localized ? Ascii("English Potion") : [0xB5, 0xA5], 65, "\r\n"),
        Row("2", Ascii("Already English"), 65, "\n"),
        Row("3", [0x81, 0x80, 0xFF], 65, ""));

    private static byte[] Row(string id, byte[] name, int fields, string ending)
    {
        using var output = new MemoryStream();
        for (var index = 0; index < fields; ++index)
        {
            // Donor statistics deliberately differ from every native field,
            // so copying/truncating whole 67-field donor rows cannot pass.
            output.Write(index == 0 ? Ascii(id) : index == 1 ? name :
                Ascii(fields == 67 ? $"donor{index:D2}" : $"value{index:D2}"));
            output.Write("@@"u8);
        }
        output.Write(Ascii(ending));
        return output.ToArray();
    }

    private static byte[] Join(params byte[][] parts)
    {
        using var output = new MemoryStream();
        foreach (var part in parts) output.Write(part);
        return output.ToArray();
    }

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);

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

    private static void AssertSuccess(RunResult result, string summary)
    {
        Assert.True(result.ExitCode == 0, $"Exit {result.ExitCode}. stdout: {result.Output} stderr: {result.Error}");
        Assert.Equal(string.Empty, result.Error);
        Assert.Contains(summary, result.Output);
        Assert.Contains("Name-field-only", result.Output);
        Assert.Contains("not a full English client", result.Output);
        Assert.Contains("67-field rows were not copied", result.Output);
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

    private sealed record Fixture(string SourcePath, string DonorPath, byte[] Plain, byte[] Packed, byte[] Donor);
    private sealed record RunResult(int ExitCode, string Output, string Error);
}
