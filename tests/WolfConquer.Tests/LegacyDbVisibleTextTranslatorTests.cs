using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyDbVisibleTextTranslatorTests
{
    private static LegacyDbVisibleTextReplacement Row(string section = "sample", string key = "Name") =>
        new("sample.ini", section, key, "\u6d4b\u8bd5", "Synthetic label", "Authored test fixture");

    [Fact]
    public void ExplicitReplacementsPreserveUnrelatedFieldsAndLineEndings()
    {
        const string input = "[sample]\r\n  Name = \u6d4b\u8bd5\r\nCount=17\n[other]\nName=unchanged\n";
        var result = LegacyDbVisibleTextTranslator.Translate(input, [Row()], out var updated);
        Assert.Equal(1, result.ChangedRows);
        Assert.Equal(1, result.MatchedSourceRows);
        Assert.Equal("[sample]\r\n  Name = Synthetic label\r\nCount=17\n[other]\nName=unchanged\n", updated);
        var repeated = LegacyDbVisibleTextTranslator.Translate(updated, [Row()], out var unchanged);
        Assert.False(repeated.Changed);
        Assert.Equal(1, repeated.AlreadyTranslatedRows);
        Assert.Equal(updated, unchanged);
    }

    [Fact]
    public void MismatchedSourceIsNotOverwritten()
    {
        const string input = "[sample]\nName=\u5176\u4ed6\n";
        var result = LegacyDbVisibleTextTranslator.Translate(input, [Row()], out var updated);
        Assert.False(result.Changed);
        Assert.Equal(1, result.UnmatchedCjkRows);
        Assert.Equal(input, updated);
    }

    [Fact]
    public void DelimitedRowsChangeOnlyTheSuppliedField()
    {
        const string input = "sample@@\u6d4b\u8bd5@@17@@keep\r\nother@@unchanged@@23@@stay\r\n";
        var result = LegacyDbVisibleTextTranslator.Translate(input, [Row(key: "Field1")], out var updated);
        Assert.Equal(1, result.ChangedRows);
        Assert.Equal("sample@@Synthetic label@@17@@keep\r\nother@@unchanged@@23@@stay\r\n", updated);
    }

    [Fact]
    public void CatalogAndFilePatchUseOnlyExplicitSyntheticData()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-db-text-synthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var catalogPath = Path.Combine(root, "catalog.json");
            File.WriteAllText(catalogPath, """
                [{"relativePath":"sample.ini","section":"sample","key":"Name",
                  "sourceValue":"\u6d4b\u8bd5","replacementValue":"Synthetic label","sourceEvidence":"Authored test fixture"}]
                """);
            var rows = LegacyDbVisibleTextCatalog.Load(catalogPath);
            var target = Path.Combine(root, "sample.ini");
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            File.WriteAllBytes(target, encoding.GetBytes("[sample]\r\nName=\u6d4b\u8bd5\r\n"));
            Assert.True(LegacyDbVisibleTextTranslator.PatchFile(target, rows).Changed);
            Assert.Equal(encoding.GetBytes("[sample]\r\nName=Synthetic label\r\n"), File.ReadAllBytes(target));
            Assert.False(LegacyDbVisibleTextTranslator.PatchFile(target, rows).Changed);
            File.WriteAllText(catalogPath, "[]");
            Assert.Throws<ArgumentException>(() => LegacyDbVisibleTextCatalog.Load(catalogPath));
            File.WriteAllText(catalogPath, """
                [{"relativePath":"../outside.ini","section":"sample","key":"Name",
                  "sourceValue":"old","replacementValue":"new","sourceEvidence":"Authored test fixture"}]
                """);
            Assert.Throws<ArgumentException>(() => LegacyDbVisibleTextCatalog.Load(catalogPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
