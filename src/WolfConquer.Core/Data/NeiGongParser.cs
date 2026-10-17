using System.Globalization;

namespace WolfConquer.Core.Data;

public static class NeiGongParser
{
    private const string HeaderSectionName = "NeiGong";

    public static NeiGongCatalog ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        return Parse(File.ReadAllText(path), Path.GetFileName(path));
    }

    public static NeiGongCatalog Parse(string text, string sourceFile = "NeiGongInfo.ini")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var sections = ParseSections(text, sourceFile);
        var headerSection = sections.FirstOrDefault(
            section => string.Equals(section.Name, HeaderSectionName, StringComparison.OrdinalIgnoreCase));
        if (headerSection is null)
        {
            throw new FormatException($"{sourceFile} is missing [{HeaderSectionName}] section.");
        }

        var header = BuildHeader(headerSection);
        var secrets = new List<NeiGongSecret>();
        var types = new List<NeiGongType>();

        foreach (var section in sections)
        {
            if (ReferenceEquals(section, headerSection))
            {
                continue;
            }

            if (TryParseSecretSectionName(section.Name, out var secretId))
            {
                secrets.Add(BuildSecret(section, secretId));
                continue;
            }

            if (TryParseTypeSectionName(section.Name, out var typeSecretId, out var slot))
            {
                types.Add(BuildType(section, typeSecretId, slot));
                continue;
            }

            throw new FormatException(
                $"{sourceFile} line {section.LineNumber} has invalid NeiGong section [{section.Name}].");
        }

        if (header.SecretCount != secrets.Count)
        {
            throw new FormatException(
                $"{sourceFile} [NeiGong] declares {header.SecretCount} secrets but parsed {secrets.Count}.");
        }

        foreach (var secret in secrets)
        {
            var childTypeCount = types.Count(type => type.SecretId == secret.Id);
            if (secret.NeiGongCount != childTypeCount)
            {
                throw new FormatException(
                    $"{sourceFile} section [{secret.RawSectionName}] declares {secret.NeiGongCount} types but parsed {childTypeCount}.");
            }
        }

        return new NeiGongCatalog(header, secrets.ToArray(), types.ToArray());
    }

    private static NeiGongHeader BuildHeader(NeiGongSection section)
    {
        var secretCount = ParseInt(GetRequiredEntry(section, "Num"), "NeiGong secret count");
        if (secretCount < 0)
        {
            throw new FormatException(
                $"{section.SourceFile} line {section.LineNumber} has negative NeiGong secret count.");
        }

        var attributeTips = new List<NeiGongAttributeTip>();
        foreach (var entry in section.Entries)
        {
            if (!entry.Key.StartsWith("Attri", StringComparison.OrdinalIgnoreCase)
                || !entry.Key.EndsWith("Tip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var attributeTypeText = entry.Key["Attri".Length..^"Tip".Length];
            if (!int.TryParse(attributeTypeText, NumberStyles.None, CultureInfo.InvariantCulture, out var attributeType))
            {
                throw new FormatException(
                    $"{entry.SourceFile} line {entry.LineNumber} has invalid NeiGong attribute tip key '{entry.Key}'.");
            }

            attributeTips.Add(new NeiGongAttributeTip(attributeType, entry.Value));
        }

        return new NeiGongHeader(
            secretCount,
            attributeTips.ToArray(),
            section.SourceFile,
            section.LineNumber,
            section.Name,
            CopyFields(section));
    }

    private static NeiGongSecret BuildSecret(NeiGongSection section, int sectionSecretId)
    {
        var id = ParseInt(GetRequiredEntry(section, "id"), "NeiGong secret id");
        if (id != sectionSecretId)
        {
            throw new FormatException(
                $"{section.SourceFile} line {section.LineNumber} section [{section.Name}] has id={id}; expected {sectionSecretId}.");
        }

        var attributeTypes = ParseHyphenIntList(GetRequiredEntry(section, "AttriType"), "NeiGong secret attribute type");
        var attributeValues = ParseHyphenIntList(GetRequiredEntry(section, "AttriValue"), "NeiGong secret attribute value");
        ValidateMatchingListCount(
            section,
            "secret attribute types",
            attributeTypes.Count,
            "secret attribute values",
            attributeValues.Count);

        var neiGongCount = ParseInt(GetRequiredEntry(section, "NeiGongNum"), "NeiGong type count");
        if (neiGongCount < 0)
        {
            throw new FormatException(
                $"{section.SourceFile} line {section.LineNumber} section [{section.Name}] has negative NeiGong type count.");
        }

        return new NeiGongSecret(
            id,
            GetRequiredEntry(section, "Name").Value,
            GetRequiredEntry(section, "AniSection").Value,
            neiGongCount,
            attributeTypes,
            attributeValues,
            GetRequiredEntry(section, "GetWayContent").Value,
            section.SourceFile,
            section.LineNumber,
            section.Name,
            CopyFields(section));
    }

    private static NeiGongType BuildType(NeiGongSection section, int secretId, int slot)
    {
        var maxLevel = ParseInt(GetRequiredEntry(section, "MaxLev"), "NeiGong max level");
        if (maxLevel < 0)
        {
            throw new FormatException(
                $"{section.SourceFile} line {section.LineNumber} section [{section.Name}] has negative NeiGong max level.");
        }

        var attributeTypes = ParseHyphenIntList(GetRequiredEntry(section, "AttriType"), "NeiGong type attribute type");
        var attributeValues = ParseHyphenIntList(GetRequiredEntry(section, "AttriValue"), "NeiGong type attribute value");
        ValidateMatchingListCount(
            section,
            "type attribute types",
            attributeTypes.Count,
            "type attribute values",
            attributeValues.Count);

        var cultureValues = ParseHyphenIntList(GetRequiredEntry(section, "NeiGongValue"), "NeiGong culture value");
        if (cultureValues.Count != maxLevel)
        {
            throw new FormatException(
                $"{section.SourceFile} line {section.LineNumber} section [{section.Name}] has {cultureValues.Count} NeiGong values; expected MaxLev={maxLevel}.");
        }

        return new NeiGongType(
            secretId,
            slot,
            ParseInt(GetRequiredEntry(section, "Type"), "NeiGong type id"),
            GetRequiredEntry(section, "Name").Value,
            GetRequiredEntry(section, "AniSection").Value,
            maxLevel,
            attributeTypes,
            attributeValues,
            cultureValues,
            GetRequiredEntry(section, "Condition").Value,
            ParseInt(GetRequiredEntry(section, "ReqLev"), "NeiGong required level"),
            ParseInt(GetRequiredEntry(section, "ReqNeiGongValue"), "NeiGong required value"),
            ParseInt(GetRequiredEntry(section, "ReqPreNeiGong"), "NeiGong prerequisite"),
            ParseInt(GetRequiredEntry(section, "ReqItemType"), "NeiGong required item type"),
            section.SourceFile,
            section.LineNumber,
            section.Name,
            CopyFields(section));
    }

    private static IReadOnlyList<NeiGongSection> ParseSections(string text, string sourceFile)
    {
        var sections = new List<NeiGongSectionBuilder>();
        NeiGongSectionBuilder? currentSection = null;

        using var reader = new StringReader(text);
        var lineNumber = 0;
        while (reader.ReadLine() is { } rawLine)
        {
            lineNumber++;

            var trimmedLine = rawLine.Trim();
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith(';') || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            if (trimmedLine.StartsWith('[') || trimmedLine.EndsWith(']'))
            {
                if (!trimmedLine.StartsWith('[') || !trimmedLine.EndsWith(']'))
                {
                    throw new FormatException($"{sourceFile} line {lineNumber} is a malformed NeiGong section line.");
                }

                var sectionName = trimmedLine[1..^1].Trim();
                if (sectionName.Length == 0)
                {
                    throw new FormatException($"{sourceFile} line {lineNumber} has an empty NeiGong section name.");
                }

                currentSection = new NeiGongSectionBuilder(sectionName, sourceFile, lineNumber);
                sections.Add(currentSection);
                continue;
            }

            if (currentSection is null)
            {
                throw new FormatException($"{sourceFile} line {lineNumber} has a NeiGong key before any section.");
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 0)
            {
                throw new FormatException($"{sourceFile} line {lineNumber} is not a NeiGong key/value row.");
            }

            var key = rawLine[..equalsIndex].Trim();
            if (key.Length == 0)
            {
                throw new FormatException($"{sourceFile} line {lineNumber} has an empty NeiGong key.");
            }

            currentSection.Add(new NeiGongEntry(
                key,
                rawLine[(equalsIndex + 1)..].Trim(),
                sourceFile,
                lineNumber));
        }

        return sections.Select(section => section.Build()).ToArray();
    }

    private static NeiGongEntry GetRequiredEntry(NeiGongSection section, string key)
    {
        for (var index = section.Entries.Count - 1; index >= 0; index--)
        {
            var entry = section.Entries[index];
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        throw new FormatException(
            $"{section.SourceFile} line {section.LineNumber} section [{section.Name}] is missing required key {key}.");
    }

    private static bool TryParseSecretSectionName(string sectionName, out int secretId)
    {
        return int.TryParse(sectionName, NumberStyles.None, CultureInfo.InvariantCulture, out secretId);
    }

    private static bool TryParseTypeSectionName(string sectionName, out int secretId, out int slot)
    {
        secretId = 0;
        slot = 0;

        var separatorIndex = sectionName.IndexOf('-');
        if (separatorIndex <= 0 || separatorIndex == sectionName.Length - 1)
        {
            return false;
        }

        return int.TryParse(sectionName[..separatorIndex], NumberStyles.None, CultureInfo.InvariantCulture, out secretId)
            && int.TryParse(sectionName[(separatorIndex + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out slot);
    }

    private static int ParseInt(NeiGongEntry entry, string name)
    {
        return int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException(
                $"{entry.SourceFile} line {entry.LineNumber} has invalid {name}: '{entry.Value}'.");
    }

    private static IReadOnlyList<int> ParseHyphenIntList(NeiGongEntry entry, string name)
    {
        if (entry.Value.Length == 0)
        {
            return Array.Empty<int>();
        }

        var fields = entry.Value.Split('-', StringSplitOptions.None);
        var values = new int[fields.Length];
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index].Trim();
            if (!int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException(
                    $"{entry.SourceFile} line {entry.LineNumber} has invalid {name} {index}: '{field}'.");
            }

            values[index] = value;
        }

        return values;
    }

    private static void ValidateMatchingListCount(
        NeiGongSection section,
        string leftName,
        int leftCount,
        string rightName,
        int rightCount)
    {
        if (leftCount == rightCount)
        {
            return;
        }

        throw new FormatException(
            $"{section.SourceFile} line {section.LineNumber} section [{section.Name}] has {leftCount} {leftName} but {rightCount} {rightName}.");
    }

    private static IReadOnlyDictionary<string, string> CopyFields(NeiGongSection section)
    {
        return new Dictionary<string, string>(section.Fields, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class NeiGongSectionBuilder
    {
        private readonly List<NeiGongEntry> _entries = [];
        private readonly Dictionary<string, string> _fields = new(StringComparer.OrdinalIgnoreCase);

        public NeiGongSectionBuilder(string name, string sourceFile, int lineNumber)
        {
            Name = name;
            SourceFile = sourceFile;
            LineNumber = lineNumber;
        }

        public string Name { get; }

        public string SourceFile { get; }

        public int LineNumber { get; }

        public void Add(NeiGongEntry entry)
        {
            _entries.Add(entry);
            _fields[entry.Key] = entry.Value;
        }

        public NeiGongSection Build()
        {
            return new NeiGongSection(
                Name,
                SourceFile,
                LineNumber,
                _entries.ToArray(),
                new Dictionary<string, string>(_fields, StringComparer.OrdinalIgnoreCase));
        }
    }

    private sealed record NeiGongSection(
        string Name,
        string SourceFile,
        int LineNumber,
        IReadOnlyList<NeiGongEntry> Entries,
        IReadOnlyDictionary<string, string> Fields);

    private sealed record NeiGongEntry(
        string Key,
        string Value,
        string SourceFile,
        int LineNumber);
}
