using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientStringFileUtf16Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolfconquer-string-utf16-" + Guid.NewGuid().ToString("N"));

    public ClientStringFileUtf16Tests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(ClientTextEncodingKind.CodePage936)]
    [InlineData(ClientTextEncodingKind.Utf8)]
    [InlineData(ClientTextEncodingKind.Utf16LeBom)]
    public void ExplicitReplacementIndentationIsStableForEqualsAndWhitespaceSeparators(ClientTextEncodingKind kind)
    {
        var encoding = ClientTextEncodings.GetEncoding(kind);
        var file = ClientStringFile.Parse(encoding.GetBytes("10006=    Old\r\n10007    Old\n"), encoding);
        Assert.True(file.SetValue("10006", "    Enter Silver"));
        Assert.True(file.SetValue("10007", "    Confirm Removal"));
        var first = file.ToBytes();
        Assert.True(file.SetValue("10006", "    Enter Silver"));
        Assert.True(file.SetValue("10007", "    Confirm Removal"));
        Assert.Equal(first, file.ToBytes());
        Assert.Equal(encoding.GetBytes("10006=    Enter Silver\r\n10007    Confirm Removal\n"), first);
    }

    [Fact]
    public void ExplicitUtf16LeEncodingReadsIndependentAsciiCjkAndSupplementaryBytesExactly()
    {
        // Literal little-endian UTF-16 for "Name=中🚀\r\n", including its BOM.
        byte[] bytes =
        [
            0xFF, 0xFE, 0x4E, 0x00, 0x61, 0x00, 0x6D, 0x00, 0x65, 0x00,
            0x3D, 0x00, 0x2D, 0x4E, 0x3D, 0xD8, 0x80, 0xDE, 0x0D, 0x00, 0x0A, 0x00,
        ];
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf16LeBom);
        var payload = bytes[2..];

        var file = ClientStringFile.Parse(payload, encoding);

        Assert.Equal(1200, encoding.CodePage);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, encoding.GetPreamble());
        Assert.Equal(new byte[] { 0x41, 0x00 }, encoding.GetBytes("A"));
        Assert.True(file.TryGetValue("Name", out var value));
        Assert.Equal("\u4E2D\U0001F680", value);
        Assert.Equal("Name=\u4E2D\U0001F680", Assert.Single(file.Lines).Text);
        Assert.Equal("\r\n", file.Lines[0].LineEnding);
        Assert.Equal(payload, file.ToBytes());
        Array.Fill(payload, (byte)'X');
        Assert.Equal(bytes[2..], file.ToBytes());
    }

    [Theory]
    [InlineData("Name=\u4E2D\U0001F680\r\n")]
    [InlineData("")]
    public void PatchPipelineNoOpPreservesTheExactUtf16LeBomAndPayload(string text)
    {
        var bytes = Utf16LeWithBom(text);
        // Even a no-op must have explicit caller-supplied operations in the
        // public API. No bundled catalog or empty-catalog fallback is used.
        var patch = text.Length == 0
            ? new ClientPatchFile(Path.Combine("ini", "utf16-fixture.ini"), ClientTextEncodingKind.Utf16LeBom, [],
                FullTextPatch: new("", EnsureFinalNewline: false))
            : CreatePatch(ClientTextEncodingKind.Utf16LeBom, [new("Name", "\u4E2D\U0001F680")]);
        var path = WriteFixture(patch, bytes);
        var originalWriteTime = File.GetLastWriteTimeUtc(path);

        var result = ApplyFixture(patch);

        Assert.Equal(1, result.Applied);
        Assert.Equal(0, result.Missing);
        Assert.False(result.Changed);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void SectionDuplicateEditRetainsEveryUnrelatedUtf16UnitNumericIdAndMixedLineEnding()
    {
        const string text = ";comment \u4E2D\r\n[0015]\n\tName = \u7B2C\u4E00\U0001F680\r" +
            "Name=\u7B2C\u4E8C\nCount=0007\r\n[0016]\r\nName=\u4FDD\u6301\r" +
            "[0015]\nName=\u7B2C\u4E09\r\n0xF00 = 4294967295\nTail=\u5C3E";
        const string expectedText = ";comment \u4E2D\r\n[0015]\n\tName = \u7B2C\u4E00\U0001F680\r" +
            "Name=English \U0001F680\nCount=0007\r\n[0016]\r\nName=\u4FDD\u6301\r" +
            "[0015]\nName=\u7B2C\u4E09\r\n0xF00 = 4294967295\nTail=\u5C3E";
        var patch = CreatePatch(ClientTextEncodingKind.Utf16LeBom,
            [new ClientTextReplacement("0015.Name", "English \U0001F680", occurrence: 2)]);
        var path = WriteFixture(patch, Utf16LeWithBom(text));

        var first = ApplyFixture(patch);
        var changed = File.ReadAllBytes(path);
        var second = ApplyFixture(patch);

        Assert.Equal(1, first.Applied);
        Assert.Equal(0, first.Missing);
        Assert.True(first.Changed);
        Assert.Equal(Utf16LeWithBom(expectedText), changed);
        Assert.Equal(1, second.Applied);
        Assert.Equal(0, second.Missing);
        Assert.False(second.Changed);
        Assert.Equal(changed, File.ReadAllBytes(path));
        var explicitBody = ClientStringFile.Parse(changed[2..], ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf16LeBom));
        Assert.False(explicitBody.SetSectionValue("15", "Name", "Wrong section"));
        Assert.Equal(changed[2..], explicitBody.ToBytes());
    }

    [Fact]
    public void NumericResourceKeysAndWhitespaceSeparatorsRemainExactWhenEditingUtf16Body()
    {
        const string text = "[0]\r\n1001=\u65E7\u503C\n1001=\u91CD\u590D\r1002 \u8865\u5145\U0001F680";
        const string expected = "[0]\r\n1001=\u65E7\u503C\n1001=English Duplicate\r1002 Supplementary \U0001F680";
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf16LeBom);
        var file = ClientStringFile.Parse(Utf16LeWithBom(text)[2..], encoding);

        Assert.True(file.SetSectionLine("0", "1001", "1001=English Duplicate", occurrence: 2));
        Assert.True(file.SetSectionValue("0", "1002", "Supplementary \U0001F680"));

        Assert.Equal(Utf16LeWithBom(expected)[2..], file.ToBytes());
        Assert.True(file.SetSectionLine("0", "1001", "1001=English Duplicate", occurrence: 2));
        Assert.Equal(Utf16LeWithBom(expected)[2..], file.ToBytes());
    }

    [Theory]
    [InlineData("odd-payload")]
    [InlineData("unpaired-high")]
    [InlineData("unpaired-low")]
    [InlineData("high-then-ascii")]
    [InlineData("reversed-pair")]
    public void MalformedUtf16FailsStrictlyWithoutFallbackOrChangingTheFixture(string scenario)
    {
        byte[] payload = scenario switch
        {
            "odd-payload" => [0x41],
            "unpaired-high" => [0x00, 0xD8],
            "unpaired-low" => [0x00, 0xDC],
            "high-then-ascii" => [0x00, 0xD8, 0x41, 0x00],
            "reversed-pair" => [0x00, 0xDC, 0x00, 0xD8],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf16LeBom);
        var bytes = new byte[] { 0xFF, 0xFE }.Concat(payload).ToArray();
        var patch = CreatePatch(ClientTextEncodingKind.Utf16LeBom, [new("Name", "Synthetic output")]);
        var path = WriteFixture(patch, bytes);

        Assert.Throws<DecoderFallbackException>(() => ClientStringFile.Parse(payload, encoding));
        Assert.Throws<DecoderFallbackException>(() => ApplyFixture(patch));

        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void InvalidSurrogateReplacementFailsBeforeThePipelineWritesAnyBytes()
    {
        var bytes = Utf16LeWithBom("Name=Original\r\n");
        var patch = CreatePatch(ClientTextEncodingKind.Utf16LeBom, [new ClientTextReplacement("Name", "\uD800")]);
        var path = WriteFixture(patch, bytes);

        Assert.Throws<EncoderFallbackException>(() => ApplyFixture(patch));

        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void RetainedUtf8BomPipelineStillEditsTheFirstKeyAndPreservesOneBom()
    {
        // Independent literal UTF-8 BOM + "Name=旧\n"; UTF-8 BOM had no
        // dedicated baseline test, while CP936 is covered by ClientStringFileTests.
        var bytes = Convert.FromHexString("EFBBBF4E616D653DE697A70A");
        var expected = Convert.FromHexString("EFBBBF4E616D653D456E676C6973680A");
        var patch = CreatePatch(ClientTextEncodingKind.Utf8Bom, [new ClientTextReplacement("Name", "English")]);
        var path = WriteFixture(patch, bytes);

        var first = ApplyFixture(patch);
        var second = ApplyFixture(patch);

        Assert.Equal(1, first.Applied);
        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(expected, File.ReadAllBytes(path));
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_root);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.StartsWith(temporaryRoot, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("wolfconquer-string-utf16-", Path.GetFileName(resolved), StringComparison.Ordinal);
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }

    private static ClientPatchFile CreatePatch(ClientTextEncodingKind kind, IReadOnlyList<ClientTextReplacement> replacements) =>
        new(Path.Combine("ini", "utf16-fixture.ini"), kind, replacements);

    private string WriteFixture(ClientPatchFile patch, byte[] bytes)
    {
        var path = Path.Combine(_root, patch.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private ClientPatchFileResult ApplyFixture(ClientPatchFile patch) =>
        Assert.Single(EnglishClientPatch.Apply(_root, [patch]).Files);

    // Encode UTF-16 code units directly, without the production Encoding API.
    private static byte[] Utf16LeWithBom(string text)
    {
        var bytes = new byte[2 + text.Length * 2];
        bytes[0] = 0xFF;
        bytes[1] = 0xFE;
        for (var index = 0; index < text.Length; ++index)
        {
            bytes[2 + index * 2] = (byte)(text[index] & 0xFF);
            bytes[3 + index * 2] = (byte)(text[index] >> 8);
        }
        return bytes;
    }
}
