using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class Chinese7937ItemCatalogDecodeCommandTests : IDisposable
{
    private const string Command = "client-item-catalog7937-decode";
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-7937-decode-" + Guid.NewGuid().ToString("N"));
    public Chinese7937ItemCatalogDecodeCommandTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task MetadataDefaultValidatesCompleteSynthetic67InputAndWritesNoFiles()
    {
        var plain = Fixture(); var packed = ClientItemCatalogCodec.Encode(plain);
        var input = Path.Combine(root, "input.packed"); await File.WriteAllBytesAsync(input, packed);
        var stamp = File.GetLastWriteTimeUtc(input);
        var result = await Run(Command, input, "--expected-sha256", Pin(packed));
        Assert.Equal(0, result.Exit); Assert.Equal("", result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal(7937, json.RootElement.GetProperty("ClientVersion").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("Rows").GetInt32());
        Assert.Equal(67, json.RootElement.GetProperty("FieldsPerRow").GetInt32());
        Assert.True(json.RootElement.GetProperty("ExactInverse").GetBoolean());
        Assert.False(json.RootElement.GetProperty("OutputPublished").GetBoolean());
        Assert.Equal(Pin(plain), json.RootElement.GetProperty("DecodedSha256").GetString());
        Assert.Equal(packed, await File.ReadAllBytesAsync(input)); Assert.Equal(stamp, File.GetLastWriteTimeUtc(input));
        Assert.Equal(new[] { input }, Directory.GetFiles(root)); Assert.Empty(Directory.GetDirectories(root));
        Assert.DoesNotContain("opaque", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplicitFreshOutputIsExactDecodedDataAndTheOld65CommandStillRefuses67()
    {
        var plain = Fixture(); var packed = ClientItemCatalogCodec.Encode(plain);
        var input = Path.Combine(root, "input.packed"); var output = Path.Combine(root, "fresh.decoded");
        await File.WriteAllBytesAsync(input, packed);
        var result = await Run(Command, input, "--expected-sha256", Pin(packed), "--output-new", output);
        Assert.Equal(0, result.Exit); Assert.Equal("", result.Error);
        Assert.Equal(plain, await File.ReadAllBytesAsync(output)); Assert.Equal(packed, await File.ReadAllBytesAsync(input));
        using var json = JsonDocument.Parse(result.Output);
        Assert.True(json.RootElement.GetProperty("OutputPublished").GetBoolean());
        Assert.Equal(2, Directory.GetFiles(root).Length);
        var oldOutput = Path.Combine(root, "old-must-not-exist.decoded");
        var legacy = await Run("client-item-catalog-codec", "decode", input, "--output-new", oldOutput);
        Assert.Equal(1, legacy.Exit); Assert.NotEqual("", legacy.Error); Assert.False(File.Exists(oldOutput));
        Assert.Equal(plain, await File.ReadAllBytesAsync(output));
    }

    [Theory]
    [InlineData(65)] [InlineData(66)] [InlineData(68)]
    public async Task WrongWidthsNeverPublishOrChangeTheSource(int width)
    {
        var packed = ClientItemCatalogCodec.Encode(Row("101", width, "\r\n"));
        await RefusesPacked(packed);
    }

    [Theory]
    [InlineData("\n")] [InlineData("\r")] [InlineData("")]
    public async Task WrongOrMissingFinalEndingNeverPublishes(string ending) =>
        await RefusesPacked(ClientItemCatalogCodec.Encode(Row("101", 67, ending)));

    [Fact]
    public async Task DuplicateCanonicalIdentityAndNulAreNotAcceptedByACompleteEnvelope()
    {
        await RefusesPacked(ClientItemCatalogCodec.Encode(Row("0101", 67, "\r\n").Concat(Row("101", 67, "\r\n")).ToArray()));
        var nul = Row("101", 67, "\r\n"); nul[8] = 0;
        await RefusesPacked(ClientItemCatalogCodec.Encode(nul));
    }

    [Fact]
    public async Task PinMismatchExistingOutputAndInputOutputAliasPreserveOwnedData()
    {
        var packed = ClientItemCatalogCodec.Encode(Fixture()); var input = Path.Combine(root, "input.packed");
        var output = Path.Combine(root, "preserved.decoded"); byte[] marker = [9, 8, 7];
        await File.WriteAllBytesAsync(input, packed);
        AssertFailure(await Run(Command, input, "--expected-sha256", new string('0', 64), "--output-new", output));
        Assert.False(File.Exists(output));
        await File.WriteAllBytesAsync(output, marker);
        AssertFailure(await Run(Command, input, "--expected-sha256", Pin(packed), "--output-new", output));
        AssertFailure(await Run(Command, input, "--expected-sha256", Pin(packed), "--output-new", input));
        Assert.Equal(marker, await File.ReadAllBytesAsync(output)); Assert.Equal(packed, await File.ReadAllBytesAsync(input));
        Assert.Equal(2, Directory.GetFiles(root).Length);
    }

    [Theory]
    [InlineData("missing-pin")] [InlineData("short-pin")] [InlineData("nonhex-pin")]
    [InlineData("extra-mode")] [InlineData("duplicate-output")] [InlineData("unknown-option")]
    public async Task ClosedArgumentsRefuseWithoutPublishing(string scenario)
    {
        var packed = ClientItemCatalogCodec.Encode(Fixture()); var input = Path.Combine(root, "input.packed");
        var output = Path.Combine(root, "must-not-exist.decoded"); await File.WriteAllBytesAsync(input, packed);
        string[] arguments = scenario switch
        {
            "missing-pin" => [Command, input],
            "short-pin" => [Command, input, "--expected-sha256", "00"],
            "nonhex-pin" => [Command, input, "--expected-sha256", new string('z', 64)],
            "extra-mode" => [Command, "encode", input, "--expected-sha256", Pin(packed)],
            "duplicate-output" => [Command, input, "--expected-sha256", Pin(packed), "--output-new", output, "--output-new", output + ".other"],
            _ => [Command, input, "--expected-sha256", Pin(packed), "--other", output],
        };
        AssertFailure(await Run(arguments));
        Assert.Equal(packed, await File.ReadAllBytesAsync(input)); Assert.Equal(new[] { input }, Directory.GetFiles(root));
    }

    [Theory]
    [InlineData("relative.packed")]
    [InlineData("\\\\server\\share\\input.packed")]
    [InlineData("\\\\?\\C:\\input.packed")]
    public async Task NonordinaryPathSpellingsNeverLoadOrPublish(string input)
    {
        var output = Path.Combine(root, "must-not-exist.decoded");
        AssertFailure(await Run(Command, input, "--expected-sha256", new string('0', 64), "--output-new", output));
        Assert.Empty(Directory.GetFileSystemEntries(root));
    }

    [Fact]
    public async Task OversizedOrMissingInputAndMissingOutputParentCreateNothing()
    {
        var output = Path.Combine(root, "must-not-exist.decoded");
        AssertFailure(await Run(Command, Path.Combine(root, "missing.packed"), "--expected-sha256", new string('0', 64), "--output-new", output));
        var huge = Path.Combine(root, "oversized.packed");
        using (var file = new FileStream(huge, FileMode.CreateNew, FileAccess.Write)) file.SetLength(64L * 1024 * 1024 + 1);
        AssertFailure(await Run(Command, huge, "--expected-sha256", new string('0', 64), "--output-new", output));
        Assert.False(File.Exists(output));
        var packed = ClientItemCatalogCodec.Encode(Fixture()); var input = Path.Combine(root, "input.packed");
        await File.WriteAllBytesAsync(input, packed);
        AssertFailure(await Run(Command, input, "--expected-sha256", Pin(packed), "--output-new", Path.Combine(root, "missing", "new.decoded")));
        Assert.Empty(Directory.GetDirectories(root));
    }

    private async Task RefusesPacked(byte[] bytes)
    {
        var input = Path.Combine(root, Guid.NewGuid().ToString("N") + ".packed");
        var output = Path.Combine(root, "must-not-exist.decoded"); await File.WriteAllBytesAsync(input, bytes);
        AssertFailure(await Run(Command, input, "--expected-sha256", Pin(bytes), "--output-new", output));
        Assert.False(File.Exists(output)); Assert.Equal(bytes, await File.ReadAllBytesAsync(input));
        Assert.DoesNotContain(Directory.GetFiles(root), file => file.EndsWith(".tmp", StringComparison.Ordinal));
    }

    private static byte[] Fixture() => Row("0101", 67, "\r\n").Concat(Row("202", 67, "\r\n")).ToArray();
    private static byte[] Row(string id, int fields, string ending)
    {
        using var output = new MemoryStream();
        for (var index = 0; index < fields; index++)
        {
            output.Write(index == 0 ? Encoding.ASCII.GetBytes(id) : Encoding.ASCII.GetBytes("opaque" + index));
            output.Write("@@"u8);
        }
        output.Write(Encoding.ASCII.GetBytes(ending)); return output.ToArray();
    }
    private static string Pin(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void AssertFailure(Result result)
    {
        Assert.Equal(1, result.Exit); Assert.NotEqual("", result.Error); Assert.DoesNotContain("Unhandled exception", result.Error);
        Assert.Contains("OutputPublished=False", result.Error, StringComparison.Ordinal);
    }
    private async Task<Result> Run(params string[] arguments)
    {
        using var process = TestProjectProcess.Create(Repository(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = root;
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start()); var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); Assert.Fail("Owned7937 decode command timed out."); }
        return new(process.ExitCode, await output, await error);
    }
    private static string Repository([CallerFilePath] string source = "")
    {
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(source)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository root missing.");
    }
    public void Dispose()
    {
        var full = Path.GetFullPath(root); var name = Path.GetFileName(full); const string prefix = "wolf-7937-decode-";
        if (!string.Equals(Path.GetDirectoryName(full), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _) ||
            (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Invalid owned decode cleanup root.");
        Directory.Delete(full, recursive: true);
    }
    private sealed record Result(int Exit, string Output, string Error);
}
