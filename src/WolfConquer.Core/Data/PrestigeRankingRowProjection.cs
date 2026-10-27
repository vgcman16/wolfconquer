using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

internal static class PrestigeRankingRowProjection
{
    private const int FieldCount = 66;
    private const int RankingFieldCount = FieldCount - 3;
    private const int PointsStart = 22;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static PrestigeRankingEntry[] ParseRows(IReadOnlyList<PrestigeRankEntry> rows)
    {
        var result = new PrestigeRankingEntry[rows.Count];
        var uids = new HashSet<uint>();
        for (var index = 0; index < result.Length; index++)
        {
            var row = rows[index];
            if (row is null)
            {
                throw new FormatException($"Prestige snapshot row {index + 1} is unavailable.");
            }

            var entry = ParseRow(row);
            if (!uids.Add(entry.UID))
            {
                throw Invalid(row, "duplicates a character UID");
            }

            result[index] = entry;
        }

        return result;
    }

    private static PrestigeRankingEntry ParseRow(PrestigeRankEntry row)
    {
        var fields = CopyAndValidateFields(row);
        var uid = ReadUInt32(row, fields, 0);
        if (uid == 0 || row.CharacterId <= 0 || uid != (uint)row.CharacterId)
        {
            throw Invalid(row, "has an invalid or inconsistent character UID");
        }

        ValidateText(row, fields[1], "character name", allowEmpty: false);
        ValidateText(row, fields[20], "guild name", allowEmpty: true);
        var category = ReadUInt32(row, fields, 2);
        // Save omits World, which Load reconstructs from the per-class rows.
        if (category is < 1 or > 11)
        {
            throw Invalid(row, "must contain a saved class category from 1 through 11");
        }

        var level = ReadUInt32(row, fields, 4);
        if (level > byte.MaxValue)
        {
            throw Invalid(row, "has a level outside its saved byte width");
        }

        var points = ReadPoints(row, fields);
        return new PrestigeRankingEntry(
            uid,
            fields[1],
            (PrestigeRankCategory)category,
            ReadUInt32(row, fields, 3),
            (byte)level,
            ReadUInt32(row, fields, 6),
            ReadAppearance(row, fields),
            points,
            row.SourceFile,
            row.LineNumber);
    }

    private static string[] CopyAndValidateFields(PrestigeRankEntry row)
    {
        if (string.IsNullOrWhiteSpace(row.SourceFile) || row.LineNumber <= 0 ||
            row.Fields is null || row.Fields.Count != FieldCount ||
            row.RankingFields is null || row.RankingFields.Count != RankingFieldCount)
        {
            throw Invalid(row, "has incomplete saved row fields or provenance");
        }

        var fields = new string[FieldCount];
        for (var index = 0; index < fields.Length; index++)
        {
            fields[index] = row.Fields[index]
                ?? throw Invalid(row, $"has an unavailable field at index {index}");
        }

        if (fields[^1].Length != 0 ||
            !string.Equals(fields[1], row.CharacterName, StringComparison.Ordinal) ||
            !string.Equals(string.Join('/', fields), row.RawLine, StringComparison.Ordinal))
        {
            throw Invalid(row, "has inconsistent saved row text, name, or trailing delimiter");
        }

        for (var index = 0; index < RankingFieldCount; index++)
        {
            if (!string.Equals(row.RankingFields[index], fields[index + 2], StringComparison.Ordinal))
            {
                throw Invalid(row, $"has inconsistent ranking field at index {index}");
            }
        }

        return fields;
    }

    private static uint[] ReadPoints(PrestigeRankEntry row, IReadOnlyList<string> fields)
    {
        if (ReadUInt32(row, fields, 21) != PrestigeRankingSnapshot.PointCount)
        {
            throw Invalid(row, "must contain exactly 43 saved point components");
        }

        var points = new uint[PrestigeRankingSnapshot.PointCount];
        for (var index = 0; index < points.Length; index++)
        {
            points[index] = ReadUInt32(row, fields, PointsStart + index);
        }

        return points;
    }

    private static PrestigeRankingAppearance ReadAppearance(PrestigeRankEntry row, IReadOnlyList<string> fields)
        => new(
            ReadUInt32(row, fields, 5),
            ReadUInt32(row, fields, 7),
            ReadUInt32(row, fields, 8),
            ReadUInt32(row, fields, 9),
            ReadUInt32(row, fields, 10),
            ReadUInt32(row, fields, 11),
            ReadUInt32(row, fields, 12),
            ReadUInt32(row, fields, 13),
            ReadUInt32(row, fields, 14),
            ReadUInt32(row, fields, 15),
            ReadUInt32(row, fields, 16),
            ReadUInt32(row, fields, 17),
            ReadUInt32(row, fields, 18),
            ReadUInt32(row, fields, 19),
            fields[20]);

    private static uint ReadUInt32(PrestigeRankEntry row, IReadOnlyList<string> fields, int index)
    {
        if (!uint.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw Invalid(row, $"has invalid UInt32 field at index {index}");
        }

        return value;
    }

    private static void ValidateText(PrestigeRankEntry row, string value, string name, bool allowEmpty)
    {
        if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) ||
            value.Contains('/') || value.Any(char.IsControl))
        {
            throw Invalid(row, $"has an invalid saved {name}");
        }

        try
        {
            _ = StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException error)
        {
            throw Invalid(row, $"has invalid Unicode in its saved {name}", error);
        }
    }

    private static FormatException Invalid(PrestigeRankEntry row, string reason, Exception? innerException = null)
        => new($"Prestige snapshot {row.SourceFile} line {row.LineNumber} {reason}.", innerException);
}
