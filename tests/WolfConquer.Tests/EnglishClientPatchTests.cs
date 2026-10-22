using System.Text;
using System.Text.Json;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class EnglishClientPatchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-patch-synthetic-" + Guid.NewGuid().ToString("N"));

    public EnglishClientPatchTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(ClientTextEncodingKind.Utf8)]
    [InlineData(ClientTextEncodingKind.Utf8Bom)]
    [InlineData(ClientTextEncodingKind.CodePage936)]
    [InlineData(ClientTextEncodingKind.Utf16LeBom)]
    public void ExplicitCatalogPreservesSectionsDuplicateKeysAndEncoding(ClientTextEncodingKind kind)
    {
        var encoding = ClientTextEncodings.GetEncoding(kind);
        const string original = "[Sample]\r\nName=\u6d4b\u8bd5\r\nLabel=first\r\nLabel=second\r\n[Other]\r\nName=unchanged\r\n";
        const string expected = "[Sample]\r\nName=Sample name\r\nLabel=first\r\nLabel=Updated second\r\n[Other]\r\nName=unchanged\r\n";
        var path = Write("ini/sample.ini", original, encoding);
        ClientPatchFile[] files = [new("ini/sample.ini", kind,
            [new("Sample.Name", "Sample name"), new("Sample.Label", "Updated second", occurrence: 2)])];

        var first = EnglishClientPatch.Apply(_root, files);
        Assert.Equal(2, first.Applied);
        Assert.Equal(0, first.Missing);
        Assert.Equal(1, first.ChangedFiles);
        Assert.Equal(WithPreamble(encoding, expected), File.ReadAllBytes(path));
        var second = EnglishClientPatch.Apply(_root, files);
        Assert.Equal(2, second.Applied);
        Assert.Equal(0, second.ChangedFiles);
        Assert.Equal(WithPreamble(encoding, expected), File.ReadAllBytes(path));
    }

    [Fact]
    public void ReplacesCommentsAndReportsMissingKeys()
    {
        var path = Write("sample.ini", "; note=old\nValue=old\n", new UTF8Encoding(false));
        var result = EnglishClientPatch.Apply(_root,
            [new("sample.ini", ClientTextEncodingKind.Utf8,
                [new("; note=old", "; note=new", replaceLine: true), new("Value", "new"), new("Absent", "unused")], [";"])]);
        Assert.Equal(2, result.Applied);
        Assert.Equal(1, result.Missing);
        Assert.Equal("; note=new\nValue=new\n", File.ReadAllText(path));
    }

    [Fact]
    public void SuppliedFullTextNormalizesNewlinesAndRemainsIdempotent()
    {
        var path = Write("notes.txt", "old", Encoding.UTF8);
        ClientPatchFile[] files = [new("notes.txt", ClientTextEncodingKind.Utf8, [],
            FullTextPatch: new("Synthetic first line\nSynthetic second line\n", EnsureFinalNewline: true))];
        Assert.Equal(1, EnglishClientPatch.Apply(_root, files).ChangedFiles);
        Assert.Equal("Synthetic first line\r\nSynthetic second line\r\n", File.ReadAllText(path));
        Assert.Equal(0, EnglishClientPatch.Apply(_root, files).ChangedFiles);
    }

    [Fact]
    public void CallerValidationCanRejectOriginalWithoutChangingIt()
    {
        var path = Write("sample.ini", "Value=old", new UTF8Encoding(false));
        var patch = new ClientPatchFile("sample.ini", ClientTextEncodingKind.Utf8, [new("Value", "new")],
            ValidateOriginal: (_, _, _) => throw new InvalidDataException("Synthetic validator rejected input."));
        Assert.Throws<InvalidDataException>(() => EnglishClientPatch.Apply(_root, [patch]));
        Assert.Equal("Value=old", File.ReadAllText(path));
    }

    [Fact]
    public void MissingAndEmptyCatalogsCannotReturnSuccessfulNoOp()
    {
        Assert.Throws<ArgumentNullException>(() => EnglishClientPatch.Apply(_root, null!));
        Assert.Throws<ArgumentException>(() => EnglishClientPatch.Apply(_root, []));
        Assert.Throws<ArgumentException>(() => EnglishClientPatch.Apply(_root,
            [new("sample.ini", ClientTextEncodingKind.Utf8, [])]));
        Assert.Throws<FileNotFoundException>(() => EnglishClientPatch.Apply(_root,
            [new("missing.ini", ClientTextEncodingKind.Utf8, [new("Value", "new")])]));
    }

    [Theory]
    [InlineData("../outside.ini")]
    [InlineData("..\\outside.ini")]
    [InlineData("/outside.ini")]
    [InlineData("C:\\outside.ini")]
    [InlineData("ini/../outside.ini")]
    public void InvalidLaterTargetIsRejectedBeforeEarlierFileChanges(string invalidPath)
    {
        var path = Write("sample.ini", "Value=old", new UTF8Encoding(false));
        Assert.Throws<ArgumentException>(() => EnglishClientPatch.Apply(_root,
            [new("sample.ini", ClientTextEncodingKind.Utf8, [new("Value", "new")]),
             new(invalidPath, ClientTextEncodingKind.Utf8, [new("Value", "new")])]));
        Assert.Equal("Value=old", File.ReadAllText(path));
    }

    [Fact]
    public void JsonCatalogLoadsExplicitSyntheticValuesAndRejectsEmptyOrUnknownFields()
    {
        var catalogPath = Path.Combine(_root, "catalog.json");
        File.WriteAllText(catalogPath, """
            [{"relativePath":"sample.ini","encodingKind":"Utf8","replacements":[
              {"key":"Value","value":"Synthetic output","occurrence":1}]}]
            """);
        var files = ClientPatchCatalog.Load(catalogPath);
        Write("sample.ini", "Value=old", new UTF8Encoding(false));
        Assert.Equal(1, EnglishClientPatch.Apply(_root, files).Applied);
        Assert.Equal("Value=Synthetic output", File.ReadAllText(Path.Combine(_root, "sample.ini")));

        File.WriteAllText(catalogPath, "[]");
        Assert.Throws<ArgumentException>(() => ClientPatchCatalog.Load(catalogPath));
        File.WriteAllText(catalogPath, """
            [{"relativePath":"sample.ini","encodingKind":"Utf8","unexpected":true}]
            """);
        Assert.Throws<JsonException>(() => ClientPatchCatalog.Load(catalogPath));
    }

    private string Write(string relativePath, string content, Encoding encoding)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, WithPreamble(encoding, content));
        return path;
    }

    private static byte[] WithPreamble(Encoding encoding, string text) =>
        [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
}
