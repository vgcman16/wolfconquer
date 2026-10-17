using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class SocialAssociationParserTests
{
    private const string EmptyNoteRow = "1293600/1/600/0/0/1293116/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//14048/";
    private const string NoteRow = "1293116/4/0/0/0/1293600/0/0/0/0/Coder[GM]/0/0/0/0/1541008/100/0/hey/7000/";

    [Fact]
    public void ParseReadsCountedLiveLikeRowsAndPreservesMetadata()
    {
        var records = SocialAssociationParser.Parse(
            $"Count=2\n{EmptyNoteRow}\n{NoteRow}\n",
            "Associate.txt");

        Assert.Equal(2, records.Count);

        var emptyNote = records[0];
        Assert.Equal(1_293_600, emptyNote.OwnerId);
        Assert.Equal(1, emptyNote.RelationType);
        Assert.Equal(1_293_116, emptyNote.TargetId);
        Assert.Equal("Tester[PM]", emptyNote.TargetName);
        Assert.Equal("", emptyNote.Note);
        Assert.Equal(14_048, emptyNote.TailValue);
        Assert.Equal(21, emptyNote.Fields.Count);
        Assert.Equal("", emptyNote.Fields[18]);
        Assert.Equal("14048", emptyNote.Fields[19]);
        Assert.Equal("", emptyNote.Fields[^1]);
        Assert.Equal(
            [600, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1_671_008, 140, 5],
            emptyNote.Values);
        Assert.Equal("Associate.txt", emptyNote.SourceFile);
        Assert.Equal(2, emptyNote.LineNumber);
        Assert.Equal(EmptyNoteRow, emptyNote.RawLine);

        var note = records[1];
        Assert.Equal(1_293_116, note.OwnerId);
        Assert.Equal(4, note.RelationType);
        Assert.Equal(1_293_600, note.TargetId);
        Assert.Equal("Coder[GM]", note.TargetName);
        Assert.Equal("hey", note.Note);
        Assert.Equal(7_000, note.TailValue);
        Assert.Equal("hey", note.Fields[18]);
        Assert.Equal("", note.Fields[^1]);
        Assert.Equal("Associate.txt", note.SourceFile);
        Assert.Equal(3, note.LineNumber);
        Assert.Equal(NoteRow, note.RawLine);
    }

    [Fact]
    public void ParseFileReadsFromDiskUsingFileNameAsSource()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Associate-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, $"Count=1\n{EmptyNoteRow}\n");

        try
        {
            var record = Assert.Single(SocialAssociationParser.ParseFile(path));

            Assert.Equal(Path.GetFileName(path), record.SourceFile);
            Assert.Equal(2, record.LineNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseRejectsDeclaredCountMismatch()
    {
        var ex = Assert.Throws<FormatException>(() => SocialAssociationParser.Parse(
            $"Count=2\n{EmptyNoteRow}\n",
            "Associate.txt"));

        Assert.Contains("Count=2", ex.Message);
        Assert.Contains("1 rows", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1293600/1/600/0/0/1293116/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//14048/\n")]
    [InlineData("Total=1\n1293600/1/600/0/0/1293116/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//14048/\n")]
    [InlineData("Count=abc\n1293600/1/600/0/0/1293116/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//14048/\n")]
    [InlineData("Count=-1\n")]
    public void ParseRejectsMissingMalformedOrNegativeCount(string text)
    {
        Assert.Throws<FormatException>(() => SocialAssociationParser.Parse(text, "Associate.txt"));
    }

    [Fact]
    public void ParseRowRejectsWrongFieldCountOrMissingTrailingSlash()
    {
        Assert.Throws<FormatException>(() => SocialAssociationParser.ParseRow(
            "Associate.txt",
            2,
            "1293600/1/600/0/0/1293116/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//14048"));

        Assert.Throws<FormatException>(() => SocialAssociationParser.ParseRow(
            "Associate.txt",
            2,
            $"{EmptyNoteRow}extra/"));
    }

    [Theory]
    [InlineData("bad/1/600/0/0/1293116/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//14048/")]
    [InlineData("1293600/bad/600/0/0/1293116/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//14048/")]
    [InlineData("1293600/1/600/0/0/bad/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//14048/")]
    [InlineData("1293600/1/600/0/0/1293116/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//bad/")]
    [InlineData("1293600/1/600/0/nope/1293116/0/0/0/0/Tester[PM]/0/0/0/0/1671008/140/5//14048/")]
    public void ParseRowRejectsInvalidNumericFields(string row)
    {
        Assert.Throws<FormatException>(() => SocialAssociationParser.ParseRow("Associate.txt", 2, row));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ParseRowRejectsInvalidLineNumber(int lineNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SocialAssociationParser.ParseRow(
            "Associate.txt",
            lineNumber,
            EmptyNoteRow));
    }
}
