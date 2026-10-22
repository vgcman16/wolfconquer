using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientItemCatalogTextFieldsCommandTests : IDisposable
{
    private const string Command = "client-item-catalog-localize";
    private const string NameSummary = "Rows=3 SourceRows=5 MatchedNames=2 ChangedNames=1 UnmatchedRows=1 SkippedSourceNames=2 ExactRoundtrip=True";
    private const string TextSummary = "TextFields=52,53 MatchedCategories=2 ChangedCategories=1 MatchedDescriptions=2 ChangedDescriptions=2 SkippedFormatDescriptions=0";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolfconquer-catalog-text-fields-" + Guid.NewGuid().ToString("N"));

    public ClientItemCatalogTextFieldsCommandTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task OptInPreviewIsReadOnlyAndReportsEachFieldIndependently()
    {
        var fixture = await WriteFixtureAsync();

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--include-text-fields");

        AssertSuccess(result);
        Assert.Contains(NameSummary, result.Output);
        Assert.Contains(TextSummary, result.Output);
        Assert.Contains("Text-field-only", result.Output);
        AssertInputsUnchanged(fixture);
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyExplicitOptInChangesCategoryAndDescriptionWhileAllOtherFieldsStayNative(bool includeText)
    {
        var fixture = await WriteFixtureAsync();
        var output = Path.Combine(_root, "fresh-candidate.dat");
        var arguments = new List<string> { fixture.SourcePath, "--english-itemtype", fixture.DonorPath };
        if (includeText) arguments.Add("--include-text-fields");
        arguments.AddRange(["--output-new", output]);

        var result = await RunAsync(arguments.ToArray());

        AssertSuccess(result);
        Assert.Contains(NameSummary, result.Output);
        if (includeText) Assert.Contains(TextSummary, result.Output);
        else
        {
            Assert.DoesNotContain("TextFields=", result.Output);
            Assert.Contains("Name-field-only", result.Output);
        }
        var packed = await File.ReadAllBytesAsync(output);
        var decoded = ClientItemCatalogCodec.Decode(packed);
        Assert.Equal(ClientPlain(localizedNames: true, localizedText: includeText), decoded);
        Assert.Equal(packed, ClientItemCatalogCodec.Encode(decoded));
        var original = ClientItemCatalogDocument.Parse(fixture.Plain);
        var changed = ClientItemCatalogDocument.Parse(decoded);
        Assert.Equal(new uint[] { 1, 2, 3 }, changed.Rows.Select(static row => row.ItemId));
        for (var row = 0; row < original.Rows.Count; ++row)
        {
            Assert.Equal(original.Rows[row].LineEnding.ToArray(), changed.Rows[row].LineEnding.ToArray());
            for (var field = 0; field < 65; ++field)
                if (field != 1 && (!includeText || field is not (52 or 53)))
                    Assert.Equal(original.Rows[row].GetField(field).ToArray(), changed.Rows[row].GetField(field).ToArray());
        }
        AssertInputsUnchanged(fixture);
    }

    [Fact]
    public async Task UnchangedAndIneligibleTextProducesAByteExactNoOpCandidate()
    {
        var fixture = await WriteFixtureAsync();
        var donor = Render(Fields("1", "Old One", "", "Old Description One", 67), "\n");
        await File.WriteAllBytesAsync(fixture.DonorPath, donor);
        var output = Path.Combine(_root, "no-op-candidate.dat");

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--include-text-fields", "--output-new", output);

        AssertSuccess(result);
        Assert.Contains("Rows=3 SourceRows=1 MatchedNames=1 ChangedNames=0 UnmatchedRows=2 SkippedSourceNames=0 ExactRoundtrip=True", result.Output);
        Assert.Contains("TextFields=52,53 MatchedCategories=0 ChangedCategories=0 MatchedDescriptions=1 ChangedDescriptions=0 SkippedFormatDescriptions=0", result.Output);
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(output));
        Assert.Equal(fixture.Packed, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.Equal(donor, await File.ReadAllBytesAsync(fixture.DonorPath));
    }

    [Theory]
    [InlineData("Native %s", "Donor Description")]
    [InlineData("Native Description", "Donor %d")]
    [InlineData("Literal %% text", "Literal %% text")]
    public async Task PercentDescriptionsRemainUnchangedAndAreCountedAsSkippedWhileOtherTextStillApplies(string nativeDescription, string donorDescription)
    {
        var nativeFields = Fields("0001", "Old Name", "Old Category", nativeDescription, 65);
        var plain = Render(nativeFields, "\r\n");
        var packed = ClientItemCatalogCodec.Encode(plain);
        var donor = Render(Fields("1", "English Name", "English Category", donorDescription, 67), "");
        var sourcePath = Path.Combine(_root, "packed-source.dat");
        var donorPath = Path.Combine(_root, "english-source.txt");
        var output = Path.Combine(_root, "fresh-candidate.dat");
        await File.WriteAllBytesAsync(sourcePath, packed);
        await File.WriteAllBytesAsync(donorPath, donor);

        var result = await RunAsync(sourcePath, "--english-itemtype", donorPath, "--include-text-fields", "--output-new", output);

        AssertSuccess(result);
        Assert.Contains("Rows=1 SourceRows=1 MatchedNames=1 ChangedNames=1 UnmatchedRows=0 SkippedSourceNames=0 ExactRoundtrip=True", result.Output);
        Assert.Contains("TextFields=52,53 MatchedCategories=1 ChangedCategories=1 MatchedDescriptions=0 ChangedDescriptions=0 SkippedFormatDescriptions=1", result.Output);
        nativeFields[1] = Ascii("English Name");
        nativeFields[52] = Ascii("English Category");
        var actual = ClientItemCatalogCodec.Decode(await File.ReadAllBytesAsync(output));
        Assert.Equal(Render(nativeFields, "\r\n"), actual);
        Assert.Equal(Ascii(nativeDescription), ClientItemCatalogDocument.Parse(actual).Rows[0].GetField(53).ToArray());
        Assert.Equal(packed, await File.ReadAllBytesAsync(sourcePath));
        Assert.Equal(donor, await File.ReadAllBytesAsync(donorPath));
    }

    [Fact]
    public async Task OptInCannotOverwriteAnExistingOutput()
    {
        var fixture = await WriteFixtureAsync();
        var output = Path.Combine(_root, "preserved.dat");
        byte[] preserved = [0xFE, 0, 0xFF];
        await File.WriteAllBytesAsync(output, preserved);

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--include-text-fields", "--output-new", output);

        AssertFailure(result);
        Assert.Equal(preserved, await File.ReadAllBytesAsync(output));
        AssertInputsUnchanged(fixture);
    }

    [Theory]
    [InlineData("duplicate-text-flag")]
    [InlineData("late-text-flag")]
    [InlineData("unknown-text-flag")]
    [InlineData("duplicate-output")]
    [InlineData("missing-output-value")]
    public async Task NewFlagRequiresItsExactPositionAndCannotBeRepeated(string scenario)
    {
        var fixture = await WriteFixtureAsync();
        var output = Path.Combine(_root, "must-not-exist.dat");
        var arguments = new List<string> { fixture.SourcePath, "--english-itemtype", fixture.DonorPath };
        arguments.AddRange(scenario switch
        {
            "duplicate-text-flag" => ["--include-text-fields", "--include-text-fields"],
            "late-text-flag" => ["--output-new", output, "--include-text-fields"],
            "unknown-text-flag" => ["--all-text"],
            "duplicate-output" => ["--include-text-fields", "--output-new", output, "--output-new", output + ".other"],
            "missing-output-value" => ["--include-text-fields", "--output-new"],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        });

        var result = await RunAsync(arguments.ToArray());

        AssertFailure(result);
        AssertInputsUnchanged(fixture);
        Assert.False(File.Exists(output));
        Assert.Equal(2, Directory.GetFiles(_root).Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MalformedDonorOrPackedSourceStillFailsBeforePublicationWithOptIn(bool badDonor)
    {
        var fixture = await WriteFixtureAsync();
        var source = badDonor ? fixture.Packed : new byte[] { 1, 2, 3 };
        var donor = badDonor ? Render(Fields("1", "Name", "Category", "Description", 65), "\n") : fixture.Donor;
        await File.WriteAllBytesAsync(fixture.SourcePath, source);
        await File.WriteAllBytesAsync(fixture.DonorPath, donor);
        var output = Path.Combine(_root, "must-not-exist.dat");

        var result = await RunAsync(fixture.SourcePath, "--english-itemtype", fixture.DonorPath, "--include-text-fields", "--output-new", output);

        AssertFailure(result);
        Assert.Equal(source, await File.ReadAllBytesAsync(fixture.SourcePath));
        Assert.Equal(donor, await File.ReadAllBytesAsync(fixture.DonorPath));
        Assert.False(File.Exists(output));
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_root);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.StartsWith(temporaryRoot, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("wolfconquer-catalog-text-fields-", Path.GetFileName(resolved), StringComparison.Ordinal);
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }

    private async Task<Fixture> WriteFixtureAsync()
    {
        var plain = ClientPlain(localizedNames: false, localizedText: false);
        var packed = ClientItemCatalogCodec.Encode(plain);
        var donorTwo = Fields("2", "Already English", "Unchanged Category", "", 67);
        donorTwo[53] = [0xB5, 0xA5];
        var donorThree = Fields("3", "", "12345", "Third Description", 67);
        donorThree[1] = [0xB5, 0xA5];
        var donor = Join(
            Render(Fields("1", "English One", "Consumable", "Restores Hit Points", 67), "\r\n"),
            Render(donorTwo, "\n"),
            Render(donorThree, "\r"),
            Render(Fields("999", "Unknown Name", "Unknown Category", "Unknown Description", 67), "\n"),
            Render(Fields("4", "", "", "", 67), ""));
        var sourcePath = Path.Combine(_root, "packed-source.dat");
        var donorPath = Path.Combine(_root, "english-source.txt");
        await File.WriteAllBytesAsync(sourcePath, packed);
        await File.WriteAllBytesAsync(donorPath, donor);
        return new Fixture(sourcePath, donorPath, plain, packed, donor);
    }

    private static byte[] ClientPlain(bool localizedNames, bool localizedText) => Join(
        Render(Fields("0001", localizedNames ? "English One" : "Old One",
            localizedText ? "Consumable" : "Old Category One",
            localizedText ? "Restores Hit Points" : "Old Description One", 65), "\r\n"),
        Render(Fields("2", "Already English", "Unchanged Category", "Old Description Two", 65), "\n"),
        Render(Fields("3", "Old Three", "Old Category Three",
            localizedText ? "Third Description" : "Old Description Three", 65), ""));

    private static byte[][] Fields(string id, string name, string category, string description, int count)
    {
        var fields = Enumerable.Range(0, count).Select(index => Ascii(index == 0 ? id :
            count == 67 ? $"donor-stat{index:D2}" : $"native-stat{index:D2}")).ToArray();
        fields[1] = Ascii(name);
        fields[52] = Ascii(category);
        fields[53] = Ascii(description);
        return fields;
    }

    private static byte[] Render(IReadOnlyList<byte[]> fields, string ending)
    {
        using var output = new MemoryStream();
        foreach (var field in fields)
        {
            output.Write(field);
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

    private static void AssertInputsUnchanged(Fixture fixture)
    {
        Assert.Equal(fixture.Packed, File.ReadAllBytes(fixture.SourcePath));
        Assert.Equal(fixture.Donor, File.ReadAllBytes(fixture.DonorPath));
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
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Could not locate WolfConquer.sln.");
    }

    private sealed record Fixture(string SourcePath, string DonorPath, byte[] Plain, byte[] Packed, byte[] Donor);
    private sealed record RunResult(int ExitCode, string Output, string Error);
}
