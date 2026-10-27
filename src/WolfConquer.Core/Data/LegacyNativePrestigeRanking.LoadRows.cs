using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    private const int PrestigeLoadFixedFieldCount = 22;
    private static readonly UTF8Encoding PrestigeLoadTextEncoding = new(false, true);

    private readonly record struct PrestigeLoadLocation(string Path, long LineNumber);

    private static List<Entry> ReadPrestigeLoadRows(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var encoding = ReadPrestigeLoadEncoding(stream);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false);
        var headerLocation = new PrestigeLoadLocation(path, 1);
        var count = ReadPrestigeLoadCount(ReadPrestigeLoadLine(reader, headerLocation), headerLocation);
        var rows = new List<Entry>();
        var identities = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            var location = new PrestigeLoadLocation(path, index + 2L);
            var line = ReadPrestigeLoadLine(reader, location)
                ?? throw InvalidPrestigeLoad(location, "ends before its declared row count");
            var entry = ParsePrestigeLoadRow(line, location);
            if (!identities.Add(entry.UID))
            {
                throw InvalidPrestigeLoad(location, "duplicates a character UID");
            }

            rows.Add(entry);
        }

        var finalLocation = new PrestigeLoadLocation(path, count + 2L);
        if (ReadPrestigeLoadLine(reader, finalLocation) is not null)
        {
            throw InvalidPrestigeLoad(finalLocation, "contains data after its declared row count");
        }

        return rows;
    }

    private static string? ReadPrestigeLoadLine(StreamReader reader, PrestigeLoadLocation location)
    {
        try
        {
            return reader.ReadLine();
        }
        catch (DecoderFallbackException error)
        {
            // Buffered decoding can encounter a later byte while satisfying this
            // read. Report the read context, not an invented exact bad-byte line.
            throw InvalidPrestigeLoad(location, "cannot decode input while reading this line", error);
        }
    }

    private static int ReadPrestigeLoadCount(string? header, PrestigeLoadLocation location)
    {
        const string prefix = "Count=";
        if (header is null || !header.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw InvalidPrestigeLoad(location, "requires a Count header");
        }

        return ReadPrestigeLoadInt32(header[prefix.Length..], location, "row count");
    }

    private static Entry ParsePrestigeLoadRow(string line, PrestigeLoadLocation location)
    {
        var fields = line.Split('/', StringSplitOptions.None);
        var pointCount = ReadPrestigeLoadPointCount(fields, location);
        Span<uint> values = stackalloc uint[20];
        for (var index = 0; index < values.Length; index++)
        {
            if (index != 1)
            {
                values[index] = ReadPrestigeLoadUInt32(fields, index, location);
            }
        }

        if (values[0] == 0 || values[2] is < 1 or > 11 || values[4] > byte.MaxValue)
        {
            throw InvalidPrestigeLoad(location, "has an invalid UID, saved class category, or byte level");
        }

        ValidatePrestigeLoadText(fields[1], location, "character name", allowEmpty: false);
        ValidatePrestigeLoadText(fields[20], location, "guild name", allowEmpty: true);
        // Validate every actual component and the matching count before allocating
        // a point array. Header/declared counts never determine unchecked capacity.
        for (var index = 0; index < pointCount; index++)
        {
            _ = ReadPrestigeLoadUInt32(fields, PrestigeLoadFixedFieldCount + index, location);
        }

        var points = new uint[pointCount];
        for (var index = 0; index < points.Length; index++)
        {
            points[index] = ReadPrestigeLoadUInt32(fields, PrestigeLoadFixedFieldCount + index, location);
        }

        return new Entry
        {
            UID = values[0],
            Name = fields[1],
            type = (Type)values[2],
            Class = values[3],
            Level = (byte)values[4],
            Mesh = values[5],
            TotalPoints = values[6],
            HairStyle = values[7],
            Head = values[8],
            Garment = values[9],
            LeftWeapon = values[10],
            LefttWeaponAccessory = values[11],
            RightWeapon = values[12],
            RightWeaponAccessory = values[13],
            MountArmor = values[14],
            Armor = values[15],
            Wing = values[16],
            WingPlus = values[17],
            Title = values[18],
            Flag = values[19],
            GuildName = fields[20],
            Points = points,
        };
    }

    private static int ReadPrestigeLoadPointCount(IReadOnlyList<string> fields, PrestigeLoadLocation location)
    {
        if (fields.Count < PrestigeLoadFixedFieldCount + 1 || fields[^1].Length != 0)
        {
            throw InvalidPrestigeLoad(location, "requires complete fields and a final slash delimiter");
        }

        var count = ReadPrestigeLoadInt32(fields[21], location, "component count");
        if (count != fields.Count - PrestigeLoadFixedFieldCount - 1)
        {
            throw InvalidPrestigeLoad(location, "has a component count inconsistent with its complete row");
        }

        return count;
    }

    private static int ReadPrestigeLoadInt32(string value, PrestigeLoadLocation location, string name)
    {
        RequirePrestigeLoadDigits(value, location, name);
        try
        {
            return int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
        }
        catch (Exception error) when (error is FormatException or OverflowException)
        {
            throw InvalidPrestigeLoad(location, $"has an invalid nonnegative Int32 {name}", error);
        }
    }

    private static uint ReadPrestigeLoadUInt32(IReadOnlyList<string> fields, int index, PrestigeLoadLocation location)
    {
        var value = fields[index];
        RequirePrestigeLoadDigits(value, location, $"UInt32 field at index {index}");
        try
        {
            return uint.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
        }
        catch (Exception error) when (error is FormatException or OverflowException)
        {
            throw InvalidPrestigeLoad(location, $"has an invalid UInt32 field at index {index}", error);
        }
    }

    private static void RequirePrestigeLoadDigits(string value, PrestigeLoadLocation location, string name)
    {
        if (value.Length == 0 || value.Any(character => character is < '0' or > '9'))
        {
            throw InvalidPrestigeLoad(location, $"requires nonempty ASCII decimal digits for its {name}");
        }
    }

    private static void ValidatePrestigeLoadText(string value, PrestigeLoadLocation location, string name, bool allowEmpty)
    {
        if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) || value.Any(char.IsControl))
        {
            throw InvalidPrestigeLoad(location, $"has an invalid {name}");
        }

        try
        {
            _ = PrestigeLoadTextEncoding.GetByteCount(value);
        }
        catch (EncoderFallbackException error)
        {
            throw InvalidPrestigeLoad(location, $"has invalid Unicode in its {name}", error);
        }
    }

    private static FormatException InvalidPrestigeLoad(PrestigeLoadLocation location, string reason, Exception? cause = null)
        => new($"Prestige ranking load '{location.Path}' at line {location.LineNumber} {reason}.", cause);
}
