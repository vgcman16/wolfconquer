using System.Globalization;

namespace WolfConquer.Core.Data;

public static class QuizQuestionParser
{
    private const int ExpectedFieldCount = 6;
    private const int CorrectOptionFieldNumber = 6;
    private const int OptionCount = 4;

    public static QuizQuestionEntry ParseLine(string sourceFile, int lineNumber, string line)
    {
        return ParseLine(sourceFile, lineNumber, line, lineNumber);
    }

    public static IReadOnlyList<QuizQuestionEntry> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var sourceFile = Path.GetFileName(path);
        var entries = new List<QuizQuestionEntry>();
        using var reader = File.OpenText(path);
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Trim().Length == 0)
            {
                continue;
            }

            entries.Add(ParseLine(sourceFile, lineNumber, line, entries.Count + 1));
        }

        return entries;
    }

    private static QuizQuestionEntry ParseLine(string sourceFile, int lineNumber, string line, int position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(line);
        ValidateLineNumber(lineNumber);
        ValidatePosition(position);

        var fields = line.Split('#', StringSplitOptions.None);
        if (fields.Length != ExpectedFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {ExpectedFieldCount}.");
        }

        var options = new[] { fields[1], fields[2], fields[3], fields[4] };
        var correctOption = ParseCorrectOption(sourceFile, lineNumber, fields[5]);

        return new QuizQuestionEntry(
            position,
            fields[0],
            options,
            correctOption,
            options[correctOption - 1],
            sourceFile,
            lineNumber,
            line,
            fields);
    }

    private static int ParseCorrectOption(string sourceFile, int lineNumber, string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var correctOption))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} field {CorrectOptionFieldNumber} has invalid integer: '{value}'.");
        }

        if (correctOption is < 1 or > OptionCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} correct option must be 1 through {OptionCount}: '{value}'.");
        }

        return correctOption;
    }

    private static void ValidateLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }

    private static void ValidatePosition(int position)
    {
        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be positive.");
        }
    }
}
