namespace WolfConquer.Core.Data;

public sealed class IniDocument
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections;

    private IniDocument(Dictionary<string, Dictionary<string, string>> sections)
    {
        _sections = sections;
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Sections =>
        _sections.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, string>)pair.Value, StringComparer.OrdinalIgnoreCase);

    public static IniDocument Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? currentSection = null;

        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var sectionName = line[1..^1].Trim();
                if (sectionName.Length == 0)
                {
                    throw new FormatException("INI section name cannot be empty.");
                }

                if (!sections.TryGetValue(sectionName, out currentSection))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sections[sectionName] = currentSection;
                }

                continue;
            }

            if (currentSection is null)
            {
                throw new FormatException($"INI key appears before any section: '{line}'.");
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
            {
                throw new FormatException($"Invalid INI key/value line: '{line}'.");
            }

            var key = line[..equalsIndex].Trim();
            var value = line[(equalsIndex + 1)..].Trim();
            currentSection[key] = value;
        }

        return new IniDocument(sections);
    }

    public IReadOnlyDictionary<string, string> GetSection(string name)
    {
        return _sections.TryGetValue(name, out var section)
            ? section
            : throw new KeyNotFoundException($"INI section '{name}' was not found.");
    }
}
