using System.Globalization;

namespace WolfConquer.Core.Data;

public static class PortalParser
{
    public static IReadOnlyList<Portal> Parse(IniDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var portals = new List<Portal>();

        foreach (var (sectionName, section) in document.Sections)
        {
            var count = ParseInt(GetRequired(section, "Count", sectionName), $"portal count for section {sectionName}");
            for (var index = 0; index < count; index++)
            {
                var enter = ParseTriplet(GetRequired(section, $"PortalEnter{index}", sectionName), $"PortalEnter{index}");
                var exit = ParseTriplet(GetPortalExit(section, index, sectionName), $"PortalExit{index}");
                portals.Add(new Portal(enter.MapId, enter.X, enter.Y, exit.MapId, exit.X, exit.Y));
            }
        }

        return portals;
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> section, string key, string sectionName)
    {
        return section.TryGetValue(key, out var value)
            ? value
            : throw new FormatException($"Missing key '{key}' in portal section '{sectionName}'.");
    }

    private static string GetPortalExit(IReadOnlyDictionary<string, string> section, int index, string sectionName)
    {
        var key = $"PortalExit{index}";
        if (section.TryGetValue(key, out var value))
        {
            return value;
        }

        var legacyTypoKey = $"PortalExt{index}";
        if (section.TryGetValue(legacyTypoKey, out value))
        {
            return value;
        }

        var offByOneKey = $"PortalExit{index + 1}";
        return index == 0 && section.TryGetValue("Count", out var count) && count == "1" && section.TryGetValue(offByOneKey, out value)
            ? value
            : throw new FormatException($"Missing key '{key}' in portal section '{sectionName}'.");
    }

    private static (int MapId, int X, int Y) ParseTriplet(string value, string fieldName)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            throw new FormatException($"{fieldName} must contain map, x, and y.");
        }

        return (ParseInt(parts[0], $"{fieldName} map"), ParseInt(parts[1], $"{fieldName} x"), ParseInt(parts[2], $"{fieldName} y"));
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {fieldName}: '{value}'.");
        }

        return parsed;
    }
}
