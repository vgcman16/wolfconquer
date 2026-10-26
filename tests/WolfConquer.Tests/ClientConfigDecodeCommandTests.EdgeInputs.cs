using System.Text;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigDecodeCommandTests
{
    [Fact]
    public async Task HardlinkedOutputAliasCannotModifyEitherNameOfTheEncodedInput()
    {
        await CreateHardLink(Output, Input);
        var before = CaptureFiles();

        var result = await RunAsync(Arguments());

        AssertFailure(result);
        AssertFiles(before);
        Assert.Equal(Convert.FromHexString(RawBlockHex), File.ReadAllBytes(Output));
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task NegativeResultBoundIsRejectedWithoutPublishingOrChangingInputs()
    {
        var before = CaptureFiles();

        var result = await RunAsync(Arguments(result: "-1"));

        AssertFailure(result);
        AssertFiles(before);
        Assert.False(File.Exists(Output));
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task TransformedBoundAboveTheOfflineMaximumIsRejectedIndependentlyOfResultBound()
    {
        var before = CaptureFiles();

        var result = await RunAsync(Arguments(transformed: "67108865"));

        AssertFailure(result);
        AssertFiles(before);
        Assert.False(File.Exists(Output));
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task ExponentFormOfAnOtherwiseValidPublicWordIsNotAdmittedAsDecimalUint32()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Words));
        var values = document.RootElement.EnumerateArray().Select(value => value.GetRawText()).ToArray();
        Assert.Equal(64, values.Length);
        // Numerically unchanged and valid JSON; the explicit word-file grammar
        // permits decimal integer tokens, not exponent notation.
        values[31] += "e0";
        File.WriteAllText(Words, "[" + string.Join(',', values) + "]", new UTF8Encoding(false));
        var before = CaptureFiles();

        var result = await RunAsync(Arguments(words: true));

        AssertFailure(result);
        AssertFiles(before);
        Assert.False(File.Exists(Output));
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task ZeroResultBoundCannotTurnAValidNonemptyRawDecodeIntoAnEmptyPublishedFile()
    {
        var before = CaptureFiles();

        var result = await RunAsync(Arguments(result: "0"));

        AssertFailure(result);
        AssertFiles(before);
        Assert.False(File.Exists(Output));
        Assert.Empty(Directory.GetDirectories(root));
    }
}
