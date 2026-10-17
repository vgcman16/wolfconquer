using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class MagicTypeOperationParserTests
{
    private const string FirstLiveRow = "1,1,1,1,2,1360,1270,1045,1046,5030,1260,1250,1290,5050,5020,5010,1300,5040,7000,7010,7020,7030,1115,1190,7040,11000,11005,11190,11960,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0";
    private const string ZeroReferenceRow = "77,0,9,9,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0";

    [Fact]
    public void ParserReadsFixedWidthRowsAndPreservesReferences()
    {
        var entry = MagicTypeOperationParser.ParseLine("magictypeop.txt", 1, FirstLiveRow);

        Assert.Equal(1, entry.Id);
        Assert.Equal(1, entry.Mode);
        Assert.Equal(1, entry.Group);
        Assert.Equal(1, entry.Level);
        Assert.Equal(2, entry.Flag);
        Assert.Equal(65, entry.Fields.Count);
        Assert.Equal(65, entry.Values.Count);
        Assert.Equal(24, entry.ReferencedMagicTypeIds.Count);
        Assert.Equal([1360, 1270, 1045, 1046, 5030], entry.ReferencedMagicTypeIds.Take(5).ToArray());
        Assert.Equal(11960, entry.ReferencedMagicTypeIds[^1]);
        Assert.Equal("magictypeop.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal(FirstLiveRow, entry.RawLine);
    }

    [Fact]
    public void ParserPreservesRowsWithoutReferences()
    {
        var entry = MagicTypeOperationParser.ParseLine("magictypeop.txt", 77, ZeroReferenceRow);

        Assert.Equal(77, entry.Id);
        Assert.Empty(entry.ReferencedMagicTypeIds);
        Assert.Equal(0, entry.Values[^1]);
    }

    [Fact]
    public void ParseFileUsesFileNameAsSource()
    {
        var path = Path.Combine(Path.GetTempPath(), "magictypeop-" + Guid.NewGuid() + ".txt");
        try
        {
            File.WriteAllText(path, FirstLiveRow + Environment.NewLine + ZeroReferenceRow + Environment.NewLine);

            var entries = MagicTypeOperationParser.ParseFile(path);

            Assert.Equal(2, entries.Count);
            Assert.All(entries, entry => Assert.Equal(Path.GetFileName(path), entry.SourceFile));
            Assert.Equal(2, entries[1].LineNumber);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ParserRejectsWrongFieldCount()
    {
        var ex = Assert.Throws<FormatException>(() => MagicTypeOperationParser.ParseLine("magictypeop.txt", 1, "1,2,3"));

        Assert.Contains("has 3 fields; expected 65", ex.Message);
    }

    [Fact]
    public void ParserRejectsMalformedInteger()
    {
        var text = FirstLiveRow.Replace("5030", "x");

        var ex = Assert.Throws<FormatException>(() => MagicTypeOperationParser.ParseLine("magictypeop.txt", 1, text));

        Assert.Contains("field 10", ex.Message);
    }
}
