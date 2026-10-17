using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static uint ReadAttributeUInt32(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        var value = ReadAttributeUInt64(attributes, keys);
        return value > uint.MaxValue ? 0 : (uint)value;
    }

    private static ushort ReadAttributeUInt16(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value) ||
            !ushort.TryParse(value, out var parsed))
        {
            return 0;
        }

        return parsed;
    }

    private static ushort ReadAttributeUInt16OrZero(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        var value = ReadAttributeUInt64(attributes, keys);
        return value > ushort.MaxValue ? (ushort)0 : (ushort)value;
    }

    private static CharacterSnapshot WithCharacterAttribute(
        CharacterSnapshot character,
        string key,
        ulong value)
    {
        var attributes = CopyCharacterAttributes(character.Attributes);
        attributes[key] = value.ToString(CultureInfo.InvariantCulture);

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithCharacterAttribute(
        CharacterSnapshot character,
        string key,
        string value)
    {
        var attributes = CopyCharacterAttributes(character.Attributes);
        attributes[key] = value;

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithoutCharacterAttribute(
        CharacterSnapshot character,
        string key)
    {
        if (!character.Attributes.ContainsKey(key))
        {
            return character;
        }

        var attributes = CopyCharacterAttributes(character.Attributes);
        attributes.Remove(key);
        return character with { Attributes = attributes };
    }

    private static Dictionary<string, string> CopyCharacterAttributes(
        IReadOnlyDictionary<string, string> attributes)
    {
        var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in attributes)
        {
            copy[key] = value;
        }

        return copy;
    }

    private static bool TryReadInt64Attribute(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out long value)
    {
        value = 0;
        return attributes.TryGetValue(key, out var raw) &&
            long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt32Attribute(
        IReadOnlyDictionary<string, string> attributes,
        out uint value,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var raw) &&
                uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static int? TryReadAttributeModulo(
        IReadOnlyDictionary<string, string> attributes,
        params string[] keys)
    {
        return TryReadUInt32Attribute(attributes, out var value, keys)
            ? checked((int)(value % 3))
            : null;
    }

    private static int? TryReadAttributeInt(
        IReadOnlyDictionary<string, string> attributes,
        params string[] keys)
    {
        return TryReadUInt32Attribute(attributes, out var value, keys)
            ? checked((int)value)
            : null;
    }

    private static IReadOnlyList<int>? ReadAttributeIntList(
        IReadOnlyDictionary<string, string> attributes,
        params string[] keys)
    {
        var raw = ReadAttributeString(attributes, keys);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var values = new List<int>();
        foreach (var token in raw.Split(
            [',', ';', '|', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                values.Add(parsed);
            }
        }

        return values.Count == 0 ? null : values;
    }

    private static ulong ReadAttributeUInt64(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static bool TryReadUInt64(
        IReadOnlyDictionary<string, string> attributes,
        out ulong parsed,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return true;
            }
        }

        parsed = 0;
        return false;
    }

    private static bool TryReadDoubleAttribute(
        IReadOnlyDictionary<string, string> attributes,
        out double parsed,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return true;
            }
        }

        parsed = 0;
        return false;
    }

    private static bool ReadAttributeBool(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        var raw = ReadAttributeString(attributes, keys);
        return raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value != 0);
    }

    private static bool TryReadAttributeBool(
        IReadOnlyDictionary<string, string> attributes,
        out bool parsed,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!attributes.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                parsed = true;
                return true;
            }

            if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                parsed = false;
                return true;
            }

            if (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                parsed = value != 0;
                return true;
            }
        }

        parsed = false;
        return false;
    }

    private static string ReadAttributeString(
        IReadOnlyDictionary<string, string> attributes,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return "";
    }
}
