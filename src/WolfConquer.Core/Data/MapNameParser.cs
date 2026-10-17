using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

public static class MapNameParser
{
    private static readonly Encoding MapNameEncoding = CreateMapNameEncoding();

    public static IReadOnlyList<MapName> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(MapNameEncoding.GetString(File.ReadAllBytes(path)), Path.GetFileName(path));
    }

    public static IReadOnlyList<MapName> ParseFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var rows = new List<MapName>();
        var seenMapIds = new HashSet<int>();
        var isPrimaryPath = true;
        foreach (var path in paths)
        {
            var fileRows = isPrimaryPath
                ? ParseFile(path)
                : ParseFileFirstWins(path);
            isPrimaryPath = false;
            foreach (var row in fileRows)
            {
                if (seenMapIds.Add(row.MapId))
                {
                    rows.Add(row);
                }
            }
        }

        return rows;
    }

    public static IReadOnlyList<MapName> Parse(string text, string sourceFile = "GameMapEx.ini")
    {
        return Parse(text, sourceFile, DuplicateMapNameSectionHandling.Throw);
    }

    private static IReadOnlyList<MapName> ParseFileFirstWins(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(
            MapNameEncoding.GetString(File.ReadAllBytes(path)),
            Path.GetFileName(path),
            DuplicateMapNameSectionHandling.FirstWins);
    }

    private static IReadOnlyList<MapName> Parse(
        string text,
        string sourceFile,
        DuplicateMapNameSectionHandling duplicateSectionHandling)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var rows = new List<MapName>();
        var seenMapIds = new HashSet<int>();
        int? currentMapId = null;
        var currentSectionLine = 0;
        string? currentSourceSection = null;
        string? currentName = null;
        string? currentRawNameLine = null;
        var currentNameLine = 0;
        var skippingDuplicateSection = false;

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

            if (trimmed.StartsWith(';') || trimmed.StartsWith('#') || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                FlushCurrentSection();
                var section = trimmed[1..^1].Trim();
                if (!int.TryParse(section, NumberStyles.None, CultureInfo.InvariantCulture, out var mapId))
                {
                    throw new FormatException($"{sourceFile} line {lineNumber}: invalid map name section id '{section}'.");
                }

                if (!seenMapIds.Add(mapId))
                {
                    if (duplicateSectionHandling == DuplicateMapNameSectionHandling.FirstWins)
                    {
                        currentMapId = null;
                        currentSectionLine = 0;
                        currentSourceSection = null;
                        currentName = null;
                        currentRawNameLine = null;
                        currentNameLine = 0;
                        skippingDuplicateSection = true;
                        continue;
                    }

                    throw new FormatException($"{sourceFile} line {lineNumber}: duplicate map name section '{section}'.");
                }

                currentMapId = mapId;
                currentSectionLine = lineNumber;
                currentSourceSection = section;
                currentName = null;
                currentRawNameLine = null;
                currentNameLine = 0;
                skippingDuplicateSection = false;
                continue;
            }

            if (skippingDuplicateSection)
            {
                continue;
            }

            if (currentMapId is null || currentSourceSection is null)
            {
                throw new FormatException($"{sourceFile} line {lineNumber}: key/value row appears before any map section.");
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                throw new FormatException($"{sourceFile} line {lineNumber}: expected key=value inside a map name section.");
            }

            var key = line[..separator].Trim();
            if (!string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException($"{sourceFile} line {lineNumber}: unexpected map name key '{key}'.");
            }

            if (currentName is not null)
            {
                throw new FormatException($"{sourceFile} line {lineNumber}: duplicate Name row in section '{currentSourceSection}'.");
            }

            currentName = line[(separator + 1)..].Trim();
            currentRawNameLine = line;
            currentNameLine = lineNumber;
        }

        FlushCurrentSection();
        return rows;

        void FlushCurrentSection()
        {
            if (currentMapId is null || currentSourceSection is null)
            {
                return;
            }

            if (currentName is null || currentRawNameLine is null)
            {
                throw new FormatException($"{sourceFile} line {currentSectionLine}: map name section '{currentSourceSection}' is missing Name.");
            }

            rows.Add(new MapName(
                currentMapId.Value,
                currentName,
                sourceFile,
                currentSectionLine,
                currentNameLine,
                currentSourceSection,
                currentRawNameLine));
        }
    }

    private enum DuplicateMapNameSectionHandling
    {
        Throw,
        FirstWins
    }

    private static Encoding CreateMapNameEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }
}
