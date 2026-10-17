using System.Globalization;

namespace WolfConquer.Core.Data;

public static class MapMetadataParser
{
    public static MapMetadata Parse(IniDocument document, string sourceRelativePath = "")
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sourceRelativePath);

        var attributes = new Dictionary<string, string>(document.GetSection("info"), StringComparer.OrdinalIgnoreCase);
        var typeRaw = GetRequired(attributes, "type");
        var colorRaw = GetRequired(attributes, "color");
        var hasPortalX = attributes.TryGetValue("portal0_x", out var portalX);
        var hasPortalY = attributes.TryGetValue("portal0_y", out var portalY);

        if (hasPortalX != hasPortalY)
        {
            throw new FormatException("Map metadata must contain both portal0_x and portal0_y when either is present.");
        }

        return new MapMetadata(
            ParseRequiredInt(attributes, "id"),
            sourceRelativePath,
            attributes.TryGetValue("name", out var name) ? name : null,
            ParseRequiredInt(attributes, "mapdoc"),
            typeRaw,
            ParseLong(typeRaw, "type"),
            hasPortalX ? ParseInt(portalX!, "portal0_x") : null,
            hasPortalY ? ParseInt(portalY!, "portal0_y") : null,
            ParseRequiredInt(attributes, "reborn_map"),
            ParseRequiredInt(attributes, "reborn_portal"),
            ParseRequiredInt(attributes, "res_lev"),
            ParseRequiredInt(attributes, "link_map"),
            ParseRequiredInt(attributes, "link_x"),
            ParseRequiredInt(attributes, "link_y"),
            colorRaw,
            ParseLong(colorRaw, "color"),
            attributes);
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return attributes.TryGetValue(key, out var value)
            ? value
            : throw new FormatException($"Missing map metadata key '{key}'.");
    }

    private static int ParseRequiredInt(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return ParseInt(GetRequired(attributes, key), key);
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid map metadata key '{fieldName}': '{value}'.");
        }

        return parsed;
    }

    private static long ParseLong(string value, string fieldName)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid map metadata key '{fieldName}': '{value}'.");
        }

        return parsed;
    }
}
