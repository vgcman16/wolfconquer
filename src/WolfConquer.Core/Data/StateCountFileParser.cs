using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

public static class StateCountFileParser
{
    private const string DefaultSourceFile = "StateCount.txt";
    private static readonly Encoding StateCountEncoding = CreateStateCountEncoding();

    public static StateCountFileData ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var text = StateCountEncoding.GetString(File.ReadAllBytes(path));
        return Parse(Path.GetFileName(path), text);
    }

    public static StateCountFileData Parse(string sourceFile, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(text);

        var rawLines = new List<string>();
        var entries = new List<StateCountFileEntry>();
        int? declaredCount = null;

        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            rawLines.Add(line);

            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (declaredCount is null)
            {
                declaredCount = ParseCountHeader(sourceFile, lineNumber, trimmed);
                continue;
            }

            entries.Add(new StateCountFileEntry(
                lineNumber,
                line,
                SplitFields(line)));
        }

        if (declaredCount is null)
        {
            throw new FormatException(
                $"{sourceFile} line {Math.Max(lineNumber, 1)} is missing Count header.");
        }

        if (declaredCount.Value != entries.Count)
        {
            throw new FormatException(
                $"{sourceFile} declares {declaredCount.Value} entries but parsed {entries.Count}.");
        }

        return new StateCountFileData(
            sourceFile,
            declaredCount.Value,
            entries.ToArray(),
            rawLines.ToArray());
    }

    public static StateCountFileData Parse(string text)
    {
        return Parse(DefaultSourceFile, text);
    }

    private static int ParseCountHeader(string sourceFile, int lineNumber, string line)
    {
        const string prefix = "Count=";
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} is missing Count header.");
        }

        var value = line[prefix.Length..].Trim();
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has invalid Count: '{value}'.");
        }

        if (count < 0)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has negative Count: {count}.");
        }

        return count;
    }

    private static IReadOnlyList<string> SplitFields(string line)
    {
        return line.Contains('/', StringComparison.Ordinal)
            ? line.Split('/', StringSplitOptions.None)
            : line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static Encoding CreateStateCountEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }
}
