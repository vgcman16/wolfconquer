using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class QuizQuestionParserTests
{
    private const string FirstLiveRow = "In~which~channel~can~all~the~players~in~the~same~server~see~messages?#Team#Broadcast#Guild#Talk#2";
    private const string LastLiveRow = "Players~can~keep~the~attribute~restored~by~the~Protection~Pill~for~___~days.#1#2#3#4#2";

    [Fact]
    public void ParserReadsQuizRowsAndPreservesAnswerFields()
    {
        var entry = QuizQuestionParser.ParseLine("quizquestins.ini", 1, FirstLiveRow);

        Assert.Equal(1, entry.Position);
        Assert.Equal("In~which~channel~can~all~the~players~in~the~same~server~see~messages?", entry.Question);
        Assert.Equal(["Team", "Broadcast", "Guild", "Talk"], entry.Options);
        Assert.Equal(2, entry.CorrectOption);
        Assert.Equal("Broadcast", entry.CorrectAnswer);
        Assert.Equal(6, entry.Fields.Count);
        Assert.Equal("quizquestins.ini", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal(FirstLiveRow, entry.RawLine);
    }

    [Fact]
    public void ParseFileUsesFileNameAndNonEmptyRowPositions()
    {
        var path = Path.Combine(Path.GetTempPath(), "quizquestins-" + Guid.NewGuid() + ".ini");
        try
        {
            File.WriteAllText(path, FirstLiveRow + Environment.NewLine + Environment.NewLine + LastLiveRow + Environment.NewLine);

            var entries = QuizQuestionParser.ParseFile(path);

            Assert.Equal(2, entries.Count);
            Assert.All(entries, entry => Assert.Equal(Path.GetFileName(path), entry.SourceFile));
            Assert.Equal(1, entries[0].Position);
            Assert.Equal(2, entries[1].Position);
            Assert.Equal(3, entries[1].LineNumber);
            Assert.Equal("2", entries[1].Fields[^1]);
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
        var ex = Assert.Throws<FormatException>(() => QuizQuestionParser.ParseLine("quizquestins.ini", 1, "Question#A#B#C#1"));

        Assert.Contains("has 5 fields; expected 6", ex.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("5")]
    public void ParserRejectsCorrectOptionOutsideAnswerRange(string value)
    {
        var text = FirstLiveRow[..^1] + value;

        var ex = Assert.Throws<FormatException>(() => QuizQuestionParser.ParseLine("quizquestins.ini", 1, text));

        Assert.Contains("correct option", ex.Message);
    }

    [Fact]
    public void ParserRejectsMalformedCorrectOption()
    {
        var text = FirstLiveRow[..^1] + "x";

        var ex = Assert.Throws<FormatException>(() => QuizQuestionParser.ParseLine("quizquestins.ini", 1, text));

        Assert.Contains("field 6", ex.Message);
    }
}
