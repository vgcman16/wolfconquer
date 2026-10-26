using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class SourceBackedStrResUnicodeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolfconquer-strres-unicode-" + Guid.NewGuid().ToString("N"));
    private readonly Encoding _targetEncoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    public SourceBackedStrResUnicodeTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ExplicitUnicodeResourceSourcePreservesTargetIdsOrderFormattingAndCodePageBytes()
    {
        const string before = ";keep \u4e2d\r\n[0]\n  10016 = \u8bb0\u5f55\u786e\u5b9a\r\n10019=\u7ecf\u9a8c:%0.3f%%\n90001=\u4fdd\u6301\rTail=ASCII";
        const string expected = ";keep \u4e2d\r\n[0]\n  10016 = Confirm Saving\r\n10019=Experience:%0.3f%%\n90001=\u4fdd\u6301\rTail=ASCII";
        var target = Write("StrRes.ini", _targetEncoding.GetBytes(before));
        // First numeric ID follows the exact BOM; no synthetic header masks BOM stripping.
        var sourceBytes = Unicode("10016=Confirm Saving\r\n10019=Experience:%0.3f%%\r\n99999=Not inserted\r\n");
        var source = Write("UStrRes.ini", sourceBytes);

        var result = Patch(target, source);

        Assert.Equal(2, result.ChangedRows);
        Assert.Equal(3, result.SourceKeys);
        Assert.Equal(_targetEncoding.GetBytes(expected), File.ReadAllBytes(target));
        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
        var ids = ClientStringFile.Parse(File.ReadAllBytes(target), _targetEncoding).KeyValues().Select(row => row.Key);
        Assert.Equal(new[] { "10016", "10019", "90001", "Tail" }, ids);
        Assert.False(Patch(target, source).Changed);
        Assert.Equal(_targetEncoding.GetBytes(expected), File.ReadAllBytes(target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingUnmarkedCodePageAndUtf8CSharpSourcesRetainCustomKeySupport(bool csharp)
    {
        var target = Write("StrRes.ini", _targetEncoding.GetBytes("strres.custom=\u4e2d\r\n"));
        var text = csharp ? "new(\"strres.custom\", \"Custom Text\"),\r\n" : "strres.custom=Custom Text\r\n";
        var bytes = csharp ? Encoding.UTF8.GetBytes(text) : _targetEncoding.GetBytes(text);
        var source = Write(csharp ? "source.cs" : "source.ini", bytes);

        Assert.Equal(1, Patch(target, source).ChangedRows);
        Assert.Equal(_targetEncoding.GetBytes("strres.custom=Custom Text\r\n"), File.ReadAllBytes(target));
        Assert.Equal(bytes, File.ReadAllBytes(source));
    }

    [Theory]
    [InlineData("https://example.invalid/update")]
    [InlineData("www.example.invalid")]
    [InlineData("127.0.0.1:9958")]
    [InlineData(@"C:\Client\Play.exe")]
    [InlineData(@"OpenC:\Client\Play.exe")]
    [InlineData(@"\\host\share\client")]
    [InlineData("Reward \U0001F680")]
    [InlineData("Reward\u2000")]
    public void UnicodeSourcesCannotRetargetRoutesAdmitConfigKeysOrPublishEncodingReplacementCharacters(string unsafeValue)
    {
        const string before = "90001=\u4e2d\r\n90002=\u67e5\u770b https://example.invalid/keep\r\nstrres.custom=\u4fdd\u6301\r\n90003=\u4e2d\r\n";
        const string expected = "90001=\u4e2d\r\n90002=\u67e5\u770b https://example.invalid/keep\r\nstrres.custom=\u4fdd\u6301\r\n90003=Clean Text\r\n";
        var target = Write("StrRes.ini", _targetEncoding.GetBytes(before));
        var source = Write("UStrRes.ini", Unicode($"90001={unsafeValue}\r\n90002=Retargeted\r\nstrres.custom=Config Changed\r\n90003=Clean Text\r\n"));

        var result = Patch(target, source);

        Assert.Equal(1, result.ChangedRows);
        Assert.Equal(3, result.UnmatchedRows);
        Assert.Equal(_targetEncoding.GetBytes(expected), File.ReadAllBytes(target));
        Assert.DoesNotContain((byte)'?', File.ReadAllBytes(target));
    }

    [Fact]
    public void UnicodeRowsRetainExactConflictAndOrderedFormatTokenGates()
    {
        const string before = "90001=\u7b49\u7ea7%d\r\n90002=\u6807\u9898\\n\u5185\u5bb9\n90003=\u4e2d\r\n90004=\u4e2d";
        var target = Write("StrRes.ini", _targetEncoding.GetBytes(before));
        var source = Write("UStrRes.ini", Unicode("90001=Level %s\r\n90002=Title Content\r\n90003=First\r\n90004=Accepted\r\n"));
        var conflict = Write("conflict.ini", _targetEncoding.GetBytes("90003=Second\r\n"));

        var result = Patch(target, source, conflict);

        Assert.Equal(1, result.ChangedRows);
        Assert.Equal(2, result.PlaceholderMismatchRows);
        Assert.Equal(1, result.ConflictedSourceKeys);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(_targetEncoding.GetBytes(before.Replace("90004=\u4e2d", "90004=Accepted", StringComparison.Ordinal)), File.ReadAllBytes(target));
    }

    [Fact]
    public void EmbeddedDrivePathInTargetRemainsByteExactWhileOrdinaryTextStillTranslates()
    {
        const string before = "90001=\u6253\u5f00C:\\Client\\Play.exe\r\n90002=\u4e2d\n";
        const string expected = "90001=\u6253\u5f00C:\\Client\\Play.exe\r\n90002=Clean Text\n";
        var target = Write("StrRes.ini", _targetEncoding.GetBytes(before));
        var sourceBytes = Unicode("90001=Do not replace this path-bearing row\r\n90002=Clean Text\r\n");
        var source = Write("UStrRes.ini", sourceBytes);

        var result = Patch(target, source);

        Assert.Equal(1, result.ChangedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(0, result.PlaceholderMismatchRows);
        Assert.Equal(_targetEncoding.GetBytes(expected), File.ReadAllBytes(target));
        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
        Assert.False(Patch(target, source).Changed);
        Assert.Equal(_targetEncoding.GetBytes(expected), File.ReadAllBytes(target));
    }

    [Theory]
    [MemberData(nameof(MalformedUnicode))]
    public void MalformedExplicitUnicodeRejectsBeforeAnyTargetWriteEvenAfterAnEligibleSource(byte[] badBytes)
    {
        var original = _targetEncoding.GetBytes("90001=\u4e2d\r\n");
        var target = Write("StrRes.ini", original);
        var valid = Write("valid.ini", _targetEncoding.GetBytes("90001=Eligible\r\n"));
        var bad = Write("UStrRes.ini", badBytes);
        var timestamp = File.GetLastWriteTimeUtc(target);

        var error = Record.Exception(() => Patch(target, valid, bad));

        Assert.True(error is DecoderFallbackException or InvalidDataException, error?.ToString() ?? "Expected strict decoding refusal.");
        Assert.Equal(original, File.ReadAllBytes(target));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(target));
        Assert.Equal(badBytes, File.ReadAllBytes(bad));
        Assert.Equal(3, Directory.GetFiles(_root).Length);
        Assert.Empty(Directory.GetDirectories(_root));
    }

    public static IEnumerable<object[]> MalformedUnicode()
    {
        yield return new object[] { new byte[] { 0xFF, 0xFE, 0x41 } };
        yield return new object[] { new byte[] { 0xFF, 0xFE, 0x00, 0xD8 } };
        yield return new object[] { new byte[] { 0xFF, 0xFE, 0x00, 0xDC } };
        yield return new object[] { new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x41, 0x00, 0x00, 0x00 } };
        yield return new object[] { new byte[] { 0xFE, 0xFF, 0x00, 0x41 } };
        yield return new object[] { Unicode("90001=Bad\0Text\r\n") };
    }

    [Fact]
    public void MissingUnicodeBomDoesNotEnableEncodingInference()
    {
        var original = _targetEncoding.GetBytes("90001=\u4e2d\r\n");
        var target = Write("StrRes.ini", original);
        var source = Write("UStrRes.ini", Encoding.Unicode.GetBytes("90001=Not inferred\r\n"));

        Assert.False(Patch(target, source).Changed);
        Assert.Equal(original, File.ReadAllBytes(target));
    }

    private SourceBackedStrResTranslationResult Patch(string target, params string[] sources) =>
        SourceBackedStrResTranslator.PatchFile(target, sources, includeTargetAsSource: false, catalogReplacements: null);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Unicode(string text) => new byte[] { 0xFF, 0xFE }.Concat(new UnicodeEncoding(false, false, true).GetBytes(text)).ToArray();

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
