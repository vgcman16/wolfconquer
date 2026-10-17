using System.Globalization;

namespace WolfConquer.Core.Data;

public static class RefineryParser
{
    private const int RefineryBoxFieldCount = 4;
    private const int RefineryBoxNamedFieldCount = 1;
    private const int RefineryMaterialParameterStartField = 3;
    private const int RefineryMaterialParameterEndField = 53;
    private const int RefineryMaterialTargetField = 54;
    private const int RefineryMaterialDescriptionField = 55;
    private static readonly HashSet<int> RefineryMaterialFieldCounts = [61, 65, 66];
    private const int RefinerySoulFieldCount = 2;

    public static IReadOnlyList<RefineryBox> ParseBoxesFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseBoxes(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<RefineryBox> ParseBoxes(string text, string sourceFile = "databaserefineryboxes.txt")
    {
        return ParseCountedRows(text, sourceFile, ParseBoxRow);
    }

    public static RefineryBox ParseBoxRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(row);
        ValidateLineNumber(lineNumber);

        var fields = row.Split(',', StringSplitOptions.None);
        if (fields.Length != RefineryBoxFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {RefineryBoxFieldCount}.");
        }

        var values = new int[RefineryBoxFieldCount - RefineryBoxNamedFieldCount];
        for (var index = RefineryBoxNamedFieldCount; index < fields.Length; index++)
        {
            values[index - RefineryBoxNamedFieldCount] = ParseInt(fields[index], $"refinery box value {index - RefineryBoxNamedFieldCount}");
        }

        return new RefineryBox(
            ParseInt(fields[0], "refinery box item type"),
            values,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<RefineryMaterial> ParseMaterialsFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseMaterials(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<RefineryMaterial> ParseMaterials(string text, string sourceFile = "Rifinery.txt")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var materials = new List<RefineryMaterial>();
        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Trim().Length == 0)
            {
                continue;
            }

            materials.Add(ParseMaterialRow(sourceFile, lineNumber, line));
        }

        return materials;
    }

    public static RefineryMaterial ParseMaterialRow(string sourceFile, int lineNumber, string row)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(row);
        ValidateLineNumber(lineNumber);

        var fields = row.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!RefineryMaterialFieldCounts.Contains(fields.Length))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected 61, 65, or 66.");
        }

        var parameters = new int[RefineryMaterialParameterEndField - RefineryMaterialParameterStartField + 1];
        for (var fieldNumber = RefineryMaterialParameterStartField; fieldNumber <= RefineryMaterialParameterEndField; fieldNumber++)
        {
            parameters[fieldNumber - RefineryMaterialParameterStartField] = ParseMaterialInt(sourceFile, lineNumber, fieldNumber, fields[fieldNumber - 1]);
        }

        var tailValues = new int[fields.Length - RefineryMaterialDescriptionField];
        for (var fieldNumber = RefineryMaterialDescriptionField + 1; fieldNumber <= fields.Length; fieldNumber++)
        {
            tailValues[fieldNumber - RefineryMaterialDescriptionField - 1] = ParseMaterialInt(sourceFile, lineNumber, fieldNumber, fields[fieldNumber - 1]);
        }

        return new RefineryMaterial(
            ParseMaterialInt(sourceFile, lineNumber, 1, fields[0]),
            fields[1],
            parameters[0],
            parameters[1],
            parameters,
            fields[RefineryMaterialTargetField - 1],
            fields[RefineryMaterialDescriptionField - 1],
            tailValues,
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    public static IReadOnlyList<RefinerySoul> ParseSoulsFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return ParseSouls(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<RefinerySoul> ParseSouls(string text, string sourceFile)
    {
        var soulLevel = ParseSoulLevel(sourceFile);
        return ParseCountedRows(
            text,
            sourceFile,
            (rowSourceFile, lineNumber, row) => ParseSoulRow(rowSourceFile, lineNumber, row, soulLevel));
    }

    public static RefinerySoul ParseSoulRow(string sourceFile, int lineNumber, string row, int soulLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentNullException.ThrowIfNull(row);
        ValidateLineNumber(lineNumber);
        ValidateSoulLevel(sourceFile, soulLevel);

        var fields = row.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != RefinerySoulFieldCount)
        {
            throw new FormatException($"{sourceFile} line {lineNumber} has {fields.Length} fields; expected {RefinerySoulFieldCount}.");
        }

        return new RefinerySoul(
            soulLevel,
            fields[0],
            ParseInt(fields[1], "refinery soul item type"),
            sourceFile,
            lineNumber,
            row,
            fields);
    }

    private static IReadOnlyList<T> ParseCountedRows<T>(
        string text,
        string sourceFile,
        Func<string, int, string, T> parseRow)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        int? expectedCount = null;
        var rows = new List<T>();
        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (expectedCount is null)
            {
                expectedCount = ParseCountHeader(sourceFile, lineNumber, trimmed);
                continue;
            }

            rows.Add(parseRow(sourceFile, lineNumber, line));
        }

        if (expectedCount is null)
        {
            throw new FormatException($"{sourceFile} is missing Count header.");
        }

        if (expectedCount.Value != rows.Count)
        {
            throw new FormatException($"{sourceFile} Count is {expectedCount.Value}, but parsed {rows.Count} rows.");
        }

        return rows;
    }

    private static int ParseCountHeader(string sourceFile, int lineNumber, string line)
    {
        const string prefix = "Count=";
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"{sourceFile} line {lineNumber} is missing Count header.");
        }

        var value = line[prefix.Length..].Trim();
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"{sourceFile} line {lineNumber} has invalid Count: '{value}'.");
    }

    private static int ParseSoulLevel(string sourceFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var name = Path.GetFileNameWithoutExtension(sourceFile);
        var suffixStart = name.Length;
        while (suffixStart > 0 && char.IsDigit(name[suffixStart - 1]))
        {
            suffixStart--;
        }

        if (suffixStart == name.Length)
        {
            throw new FormatException($"{sourceFile} does not end with a soul level suffix.");
        }

        var suffix = name[suffixStart..];
        var soulLevel = int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"{sourceFile} has invalid soul level suffix '{suffix}'.");

        ValidateSoulLevel(sourceFile, soulLevel);
        return soulLevel;
    }

    private static void ValidateSoulLevel(string sourceFile, int soulLevel)
    {
        if (soulLevel is < 1 or > 7)
        {
            throw new FormatException($"{sourceFile} has soul level {soulLevel}; expected 1 through 7.");
        }
    }

    private static void ValidateLineNumber(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be positive.");
        }
    }

    private static int ParseInt(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }

    private static int ParseMaterialInt(string sourceFile, int lineNumber, int fieldNumber, string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"{sourceFile} line {lineNumber} field {fieldNumber} is not numeric: '{value}'.");
    }
}
