using System.Globalization;

namespace WolfConquer.Core.Data;

public static class MonsterParser
{
    public static Monster Parse(IniDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var attributes = new Dictionary<string, string>(document.GetSection("cq_monstertype"), StringComparer.OrdinalIgnoreCase);
        var id = ParseInt(GetRequired(attributes, "id"), "monster id");
        var name = GetRequired(attributes, "name");
        var specialDrops = ParseSpecialDrops(document);

        return LegacyMonsterFamilyLoadRules.ApplySourceLoadRules(new Monster(id, name, attributes, specialDrops));
    }

    private static IReadOnlyList<SpecialDrop> ParseSpecialDrops(IniDocument document)
    {
        if (!document.Sections.TryGetValue("SpecialDrop", out var section))
        {
            return [];
        }

        var count = ParseInt(GetRequired(section, "Count"), "special drop count");
        var drops = new List<SpecialDrop>(count);
        for (var index = 0; index < count; index++)
        {
            if (!section.TryGetValue(index.ToString(CultureInfo.InvariantCulture), out var value))
            {
                throw new FormatException($"Missing SpecialDrop entry {index}.");
            }

            var parts = value.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                throw new FormatException($"SpecialDrop entry {index} must contain item id and chance/count.");
            }

            drops.Add(new SpecialDrop(ParseInt(parts[0], "special drop item id"), ParseInt(parts[1], "special drop chance/count")));
        }

        return drops;
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> section, string key)
    {
        return section.TryGetValue(key, out var value)
            ? value
            : throw new FormatException($"Missing monster key '{key}'.");
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
