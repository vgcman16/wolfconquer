using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

public static class SubProfessionParser
{
    private static readonly Encoding SubProfessionEncoding = CreateSubProfessionEncoding();

    public static SubProfessionCatalog ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var text = SubProfessionEncoding.GetString(File.ReadAllBytes(path));
        return Parse(text, Path.GetFileName(path));
    }

    public static SubProfessionCatalog Parse(
        string content,
        string sourceFile = "SubProfessionInfo.ini")
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var sections = ParseSections(content, sourceFile);
        var entries = new List<SubProfessionEntry>(sections.Count);
        var seenIds = new HashSet<int>();

        foreach (var section in sections)
        {
            if (!int.TryParse(section.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                throw new FormatException(
                    $"{sourceFile} line {section.LineNumber}: invalid sub-profession section id '{section.Name}'.");
            }

            if (!seenIds.Add(id))
            {
                throw new FormatException(
                    $"{sourceFile} line {section.LineNumber}: duplicate sub-profession section id {id}.");
            }

            entries.Add(BuildEntry(section, id));
        }

        return new SubProfessionCatalog(entries.ToArray(), sourceFile);
    }

    private static SubProfessionEntry BuildEntry(SubProfessionSection section, int id)
    {
        return new SubProfessionEntry(
            id,
            GetRequiredEntry(section, "title").Value,
            GetRequiredEntry(section, "intro").Value,
            ParseInt(GetRequiredEntry(section, "req_lv"), "sub-profession required level", section),
            ParseInt(GetRequiredEntry(section, "req_meto"), "sub-profession required metempsychosis", section),
            ParseRequiredProfessions(GetOptionalEntry(section, "req_Profession"), section),
            GetRequiredEntry(section, "Promotion").Value,
            ParseInt(GetRequiredEntry(section, "suit"), "sub-profession suit", section),
            ParseInt(GetRequiredEntry(section, "UplimitLev"), "sub-profession level limit", section),
            ParseOptionalInt(section, "req_joblv", "sub-profession required job level"),
            ParseOptionalInt(section, "EffectProportion", "sub-profession effect proportion"),
            GetOptionalValue(section, "UpLevEffect"),
            GetOptionalValue(section, "UpStepEffect"),
            GetOptionalValue(section, "UpStepScreenEffect"),
            GetOptionalValue(section, "UpEffect"),
            GetOptionalValue(section, "SubSpecial"),
            ParseOptionalInt(section, "suitpower", "sub-profession suit power"),
            ParseOptionalInt(section, "BigClass", "sub-profession big class"),
            BuildRoute(
                section,
                "Accession",
                "AccessionMapID",
                "AccessionNpcID",
                "AccessionPosX",
                "AccessionPosY"),
            BuildRoute(
                section,
                "StepUp",
                "StepUpMapID",
                "StepUpNpcID",
                "StepUpPosX",
                "StepUpPosY"),
            GetOptionalValue(section, "AniSection"),
            section.SourceFile,
            section.LineNumber,
            section.Name,
            section.Entries);
    }

    private static IReadOnlyList<SubProfessionSection> ParseSections(string content, string sourceFile)
    {
        var sections = new List<SubProfessionSectionBuilder>();
        SubProfessionSectionBuilder? currentSection = null;

        using var reader = new StringReader(content);
        var lineNumber = 0;
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;
            var trimmed = rawLine.Trim();
            if (IsIgnoredLine(trimmed))
            {
                continue;
            }

            if (trimmed.StartsWith('[') || trimmed.EndsWith(']'))
            {
                if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
                {
                    throw new FormatException($"{sourceFile} line {lineNumber}: malformed sub-profession section line.");
                }

                var sectionName = trimmed[1..^1].Trim();
                if (sectionName.Length == 0)
                {
                    throw new FormatException($"{sourceFile} line {lineNumber}: sub-profession section name cannot be empty.");
                }

                currentSection = new SubProfessionSectionBuilder(sectionName, sourceFile, lineNumber);
                sections.Add(currentSection);
                continue;
            }

            if (currentSection is null)
            {
                throw new FormatException($"{sourceFile} line {lineNumber}: key/value row appears before any sub-profession section.");
            }

            var separator = rawLine.IndexOf('=');
            if (separator <= 0)
            {
                throw new FormatException(
                    $"{sourceFile} line {lineNumber} section [{currentSection.Name}]: expected key=value row.");
            }

            var key = rawLine[..separator].Trim();
            if (key.Length == 0)
            {
                throw new FormatException(
                    $"{sourceFile} line {lineNumber} section [{currentSection.Name}]: sub-profession key cannot be empty.");
            }

            currentSection.Add(new SubProfessionRawEntry(
                key,
                rawLine[(separator + 1)..].Trim(),
                sourceFile,
                lineNumber,
                rawLine));
        }

        return sections.Select(section => section.Build()).ToArray();
    }

    private static bool IsIgnoredLine(string trimmed)
    {
        return trimmed.Length == 0
            || trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith(';')
            || trimmed.StartsWith('#');
    }

    private static SubProfessionRawEntry GetRequiredEntry(SubProfessionSection section, string key)
    {
        var entry = GetOptionalEntry(section, key);
        if (entry is not null)
        {
            return entry;
        }

        throw new KeyNotFoundException(
            $"{section.SourceFile} line {section.LineNumber} section [{section.Name}] is missing required key {key}.");
    }

    private static SubProfessionRawEntry? GetOptionalEntry(SubProfessionSection section, string key)
    {
        for (var index = section.Entries.Count - 1; index >= 0; index--)
        {
            var entry = section.Entries[index];
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static string? GetOptionalValue(SubProfessionSection section, string key)
    {
        return GetOptionalEntry(section, key)?.Value;
    }

    private static int? ParseOptionalInt(SubProfessionSection section, string key, string name)
    {
        var entry = GetOptionalEntry(section, key);
        return entry is null ? null : ParseInt(entry, name, section);
    }

    private static int ParseInt(SubProfessionRawEntry entry, string name, SubProfessionSection section)
    {
        return int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException(
                $"{entry.SourceFile} line {entry.LineNumber} section [{section.Name}] has invalid {name}: '{entry.Value}'.");
    }

    private static IReadOnlyList<int> ParseRequiredProfessions(
        SubProfessionRawEntry? entry,
        SubProfessionSection section)
    {
        if (entry is null || entry.Value.Length == 0)
        {
            return Array.Empty<int>();
        }

        var fields = entry.Value.Split(',', StringSplitOptions.None);
        var values = new int[fields.Length];
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index].Trim();
            if (!int.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException(
                    $"{entry.SourceFile} line {entry.LineNumber} section [{section.Name}] has invalid sub-profession required profession {index}: '{field}'.");
            }

            values[index] = value;
        }

        return values;
    }

    private static SubProfessionNpcRoute? BuildRoute(
        SubProfessionSection section,
        string routeName,
        string mapKey,
        string npcKey,
        string xKey,
        string yKey)
    {
        var mapEntry = GetOptionalEntry(section, mapKey);
        var npcEntry = GetOptionalEntry(section, npcKey);
        var xEntry = GetOptionalEntry(section, xKey);
        var yEntry = GetOptionalEntry(section, yKey);

        var presentCount = CountPresent(mapEntry, npcEntry, xEntry, yEntry);
        if (presentCount == 0)
        {
            return null;
        }

        if (presentCount != 4)
        {
            throw new FormatException(
                $"{section.SourceFile} line {section.LineNumber} section [{section.Name}] has a partial {routeName} route; expected {mapKey}, {npcKey}, {xKey}, and {yKey}.");
        }

        return new SubProfessionNpcRoute(
            ParseInt(mapEntry!, $"{routeName} route map id", section),
            ParseInt(npcEntry!, $"{routeName} route npc id", section),
            ParseInt(xEntry!, $"{routeName} route X", section),
            ParseInt(yEntry!, $"{routeName} route Y", section));
    }

    private static int CountPresent(params SubProfessionRawEntry?[] entries)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            if (entry is not null)
            {
                count++;
            }
        }

        return count;
    }

    private static Encoding CreateSubProfessionEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    private sealed class SubProfessionSectionBuilder
    {
        private readonly List<SubProfessionRawEntry> _entries = [];

        public SubProfessionSectionBuilder(string name, string sourceFile, int lineNumber)
        {
            Name = name;
            SourceFile = sourceFile;
            LineNumber = lineNumber;
        }

        public string Name { get; }

        public string SourceFile { get; }

        public int LineNumber { get; }

        public void Add(SubProfessionRawEntry entry)
        {
            _entries.Add(entry);
        }

        public SubProfessionSection Build()
        {
            return new SubProfessionSection(
                Name,
                SourceFile,
                LineNumber,
                _entries.ToArray());
        }
    }

    private sealed record SubProfessionSection(
        string Name,
        string SourceFile,
        int LineNumber,
        IReadOnlyList<SubProfessionRawEntry> Entries);
}
