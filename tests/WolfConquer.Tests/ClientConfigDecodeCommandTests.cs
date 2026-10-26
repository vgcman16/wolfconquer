namespace WolfConquer.Tests;

public sealed partial class ClientConfigDecodeCommandTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ActualCommandDecodesIndependentSignedRawAndGzipVectorsWithEitherExplicitPublicInput(bool gzip, bool words)
    {
        File.WriteAllBytes(Input, Convert.FromHexString(gzip ? GzipBlocksHex : RawBlockHex));
        var before = CaptureFiles();
        byte[] expected = gzip ? [97, 98, 99] : [0x10, 0x20, 0, 0x80, 0xFF];
        var inputLength = new FileInfo(Input).Length;

        var result = await RunAsync(Arguments(words));

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains($"Profile=d13-default InputBytes={inputLength} ResultBytes={expected.Length} OutputPublished=True", result.Output);
        Assert.Contains("Byte output only", result.Output);
        Assert.DoesNotContain(root, result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ModulusHex, result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expected, File.ReadAllBytes(Output));
        before.Add(Path.GetFileName(Output), (expected, File.GetLastWriteTimeUtc(Output)));
        AssertFiles(before);
        Assert.Empty(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task PublicWordsWithUtf8BomUseTheSameExactRecipe()
    {
        File.WriteAllBytes(Words, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(File.ReadAllBytes(Words)).ToArray());
        var before = CaptureFiles();

        var result = await RunAsync(Arguments(words: true));

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        byte[] expected = [0x10, 0x20, 0, 0x80, 0xFF];
        Assert.Equal(expected, File.ReadAllBytes(Output));
        before.Add(Path.GetFileName(Output), (expected, File.GetLastWriteTimeUtc(Output)));
        AssertFiles(before);
    }

    [Fact]
    public async Task ASecondInvocationCannotOverwriteTheFirstDecodedOutput()
    {
        Assert.Equal(0, (await RunAsync(Arguments())).ExitCode);
        var committed = CaptureFiles();

        var result = await RunAsync(Arguments());

        AssertFailure(result);
        AssertFiles(committed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalShortChunkAndIndependentAccumulationPreserveEveryBinaryByte(bool shortFinal)
    {
        File.WriteAllBytes(Input, Convert.FromHexString(RawBlockHex + (shortFinal ? "1A541A1B9B65374B641C3A6DF60C87" : RawBlockHex)));
        var before = CaptureFiles();
        byte[] expected = shortFinal ? [0x10, 0x20, 0, 0x80, 0xFF, 65] : [0x10, 0x20, 0, 0x80, 0xFF, 0x10, 0x20, 0, 0x80, 0xFF];

        var result = await RunAsync(Arguments(result: expected.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(expected, File.ReadAllBytes(Output));
        before.Add(Path.GetFileName(Output), (expected, File.GetLastWriteTimeUtc(Output)));
        AssertFiles(before);
    }
}
