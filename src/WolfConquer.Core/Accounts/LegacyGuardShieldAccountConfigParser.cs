namespace WolfConquer.Core.Accounts;

public static class LegacyGuardShieldAccountConfigParser
{
    public static LegacyGuardShieldAccountConfig ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ParseLines(File.ReadLines(path));
    }

    public static LegacyGuardShieldAccountConfig ParseLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var sections = ParseIniSections(lines);

        return new LegacyGuardShieldAccountConfig(
            GetValue(sections, "LoaderSystem", "ServerIP"),
            GetValue(sections, "LoaderSystem", "DbLocation"),
            GetValue(sections, "MySql", "Host"),
            GetValue(sections, "MySql", "Username"),
            GetValue(sections, "MySql", "Password"),
            GetValue(sections, "MySql", "Database"));
    }

    private static Dictionary<string, Dictionary<string, string>> ParseIniSections(IEnumerable<string> lines)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var section = string.Empty;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                if (!sections.ContainsKey(section))
                {
                    sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (!sections.TryGetValue(section, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[section] = values;
            }

            values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return sections;
    }

    private static string GetValue(
        Dictionary<string, Dictionary<string, string>> sections,
        string section,
        string key)
    {
        return sections.TryGetValue(section, out var values) &&
            values.TryGetValue(key, out var value)
                ? value
                : string.Empty;
    }
}
