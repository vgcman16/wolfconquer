using System.Text;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigDecodeCommandTests
{
    [Theory]
    [InlineData("missing-profile")]
    [InlineData("wrong-profile")]
    [InlineData("unknown-option")]
    [InlineData("duplicate-option")]
    [InlineData("both-public-inputs")]
    [InlineData("missing-output")]
    [InlineData("relative-input")]
    [InlineData("relative-output")]
    [InlineData("missing-bound")]
    public async Task CommandRequiresEveryExplicitInputAndNeverFallsBackToMachineDefaults(string kind)
    {
        var args = Arguments();
        args = kind switch
        {
            "missing-profile" => args.Skip(2).ToArray(),
            "wrong-profile" => Replace(args, "--profile", "auto"),
            "unknown-option" => args.Select(value => value == "--input" ? "--discover-input" : value).ToArray(),
            "duplicate-option" => args.Select(value => value == "--input" ? "--profile" : value).ToArray(),
            "both-public-inputs" => args.Concat(new[] { "--encoded-words-file", Words }).ToArray(),
            "missing-output" => args.Where((_, index) => index is not 6 and not 7).ToArray(),
            "relative-input" => Replace(args, "--input", Path.GetFileName(Input)),
            "relative-output" => Replace(args, "--output-new", Path.GetFileName(Output)),
            _ => args.Take(args.Length - 2).ToArray(),
        };
        var before = CaptureFiles();

        var result = await RunAsync(args);

        AssertFailure(result);
        AssertFiles(before);
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("object")]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("negative")]
    [InlineData("overflow")]
    [InlineData("fraction")]
    [InlineData("null")]
    [InlineData("trailing")]
    [InlineData("invalid-json")]
    public async Task MalformedEncodedWordFilesCannotProduceAnOutputOrLeakTheirText(string kind)
    {
        var values = Enumerable.Repeat("0", kind == "short" ? 63 : kind == "long" ? 65 : 64).ToArray();
        if (kind is "negative" or "overflow" or "fraction" or "null") values[31] = kind switch
        { "negative" => "-1", "overflow" => "4294967296", "fraction" => "1.5", _ => "null" };
        var json = "[" + string.Join(',', values) + "]";
        json = kind switch
        {
            "empty" => string.Empty,
            "object" => "{\"words\":" + json + "}",
            "trailing" => json + " 0",
            "invalid-json" => "DO-NOT-ECHO-OWNED-SYNTHETIC-WORDS",
            _ => json,
        };
        File.WriteAllText(Words, json, new UTF8Encoding(false));
        var before = CaptureFiles();

        var result = await RunAsync(Arguments(words: true));

        AssertFailure(result);
        Assert.DoesNotContain("DO-NOT-ECHO-OWNED-SYNTHETIC-WORDS", result.Error);
        AssertFiles(before);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("zero")]
    [InlineData("integer-too-large")]
    [InlineData("late-invalid")]
    [InlineData("truncated-final")]
    public async Task InvalidAndLateInvalidSignedInputsCannotLeaveAnEarlierDecodedPrefix(string kind)
    {
        var bytes = kind switch
        {
            "empty" => Array.Empty<byte>(),
            "zero" => new byte[16],
            "integer-too-large" => Convert.FromHexString(ModulusHex),
            "late-invalid" => Convert.FromHexString(RawBlockHex).Concat(new byte[16]).ToArray(),
            _ => Convert.FromHexString(RawBlockHex).Concat(new byte[] { 0 }).ToArray(),
        };
        File.WriteAllBytes(Input, bytes);
        var before = CaptureFiles();

        var result = await RunAsync(Arguments());

        AssertFailure(result);
        AssertFiles(before);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("even")]
    [InlineData("short")]
    [InlineData("oversized")]
    public async Task InvalidExplicitPublicModulusCannotSelectAFallback(string kind)
    {
        var bytes = kind switch
        {
            "empty" => Array.Empty<byte>(),
            "even" => Convert.FromHexString("FFFFFFFFFFFFFF720000000000001320"),
            "short" => new byte[] { 3 },
            _ => new byte[2049],
        };
        File.WriteAllBytes(Modulus, bytes);
        var before = CaptureFiles();

        var result = await RunAsync(Arguments());

        AssertFailure(result);
        AssertFiles(before);
    }

    [Theory]
    [InlineData(false, "4", "64")]
    [InlineData(false, "64", "4")]
    [InlineData(true, "22", "64")]
    [InlineData(true, "64", "2")]
    [InlineData(false, "0", "64")]
    [InlineData(false, "64", "67108865")]
    public async Task IndependentFiniteTransformAndResultBoundsRefuseBeforeAnyWrite(bool gzip, string transformed, string resultBound)
    {
        if (gzip) File.WriteAllBytes(Input, Convert.FromHexString(GzipBlocksHex));
        var before = CaptureFiles();

        var result = await RunAsync(Arguments(transformed: transformed, result: resultBound));

        AssertFailure(result);
        AssertFiles(before);
    }

    [Theory]
    [InlineData("encoded")]
    [InlineData("words")]
    public async Task OversizedFilesAreRejectedBeforePayloadAllocationOrOutput(string kind)
    {
        var target = kind == "encoded" ? Input : Words;
        var size = kind == "encoded" ? 64L * 1024 * 1024 + 1 : 4097;
        using (var file = new FileStream(target, FileMode.Truncate, FileAccess.Write)) file.SetLength(size);
        var time = File.GetLastWriteTimeUtc(target);

        var result = await RunAsync(Arguments(words: kind == "words"));

        AssertFailure(result);
        Assert.Equal(size, new FileInfo(target).Length);
        Assert.Equal(time, File.GetLastWriteTimeUtc(target));
        Assert.False(File.Exists(Output));
        Assert.Equal(3, Directory.GetFiles(root).Length);
        Assert.Empty(Directory.GetDirectories(root));
    }
}
