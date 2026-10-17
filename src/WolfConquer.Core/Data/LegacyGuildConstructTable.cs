using System.Globalization;

namespace WolfConquer.Core.Data;

public sealed record LegacyGuildConstruct(
    uint Id,
    uint Level,
    ulong Experience,
    uint Beast,
    ulong V2,
    ulong V3,
    ulong V4)
{
    public int FormationType => checked((int)((Id * 100U) + Beast));
}

public static class LegacyGuildConstructTable
{
    private const int FieldsPerConstruct = 7;

    public static IReadOnlyList<LegacyGuildConstruct> ParseConstructLine(
        string line,
        string sourceFile = "",
        int lineNumber = 0)
    {
        if (!TryParseConstructLine(line, out var constructs))
        {
            var location = string.IsNullOrWhiteSpace(sourceFile)
                ? "legacy guild construct row"
                : $"{sourceFile} line {lineNumber}";
            throw new FormatException($"{location} is not a valid legacy guild construct row.");
        }

        return constructs;
    }

    public static bool TryParseConstructLine(
        string? line,
        out IReadOnlyList<LegacyGuildConstruct> constructs)
    {
        constructs = Array.Empty<LegacyGuildConstruct>();
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var fields = line.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length == 0 ||
            !uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            return false;
        }

        var rowCount = checked((int)count);
        var expectedFieldCount = 1 + checked(rowCount * FieldsPerConstruct);
        if (fields.Length != expectedFieldCount)
        {
            return false;
        }

        var rows = new LegacyGuildConstruct[rowCount];
        var offset = 1;
        for (var index = 0; index < rows.Length; index++)
        {
            if (!TryReadUInt32(fields, offset, out var id) ||
                !TryReadUInt32(fields, offset + 1, out var level) ||
                !TryReadUInt64(fields, offset + 2, out var experience) ||
                !TryReadUInt32(fields, offset + 3, out var beast) ||
                !TryReadUInt64(fields, offset + 4, out var v2) ||
                !TryReadUInt64(fields, offset + 5, out var v3) ||
                !TryReadUInt64(fields, offset + 6, out var v4))
            {
                constructs = Array.Empty<LegacyGuildConstruct>();
                return false;
            }

            rows[index] = new LegacyGuildConstruct(id, level, experience, beast, v2, v3, v4);
            offset += FieldsPerConstruct;
        }

        constructs = rows;
        return true;
    }

    public static bool TryLoadGuildConstructsByUsersRoot(
        string? usersRoot,
        uint guildId,
        out IReadOnlyList<LegacyGuildConstruct> constructs)
    {
        constructs = Array.Empty<LegacyGuildConstruct>();
        var guildsRoot = TryDeriveGuildsRootFromUsersRoot(usersRoot);
        return guildsRoot is not null &&
            TryLoadGuildConstructs(guildsRoot, guildId, out constructs);
    }

    public static bool TryLoadGuildConstructs(
        string? guildsRoot,
        uint guildId,
        out IReadOnlyList<LegacyGuildConstruct> constructs)
    {
        constructs = Array.Empty<LegacyGuildConstruct>();
        if (string.IsNullOrWhiteSpace(guildsRoot))
        {
            return false;
        }

        var path = Path.Combine(guildsRoot, guildId.ToString(CultureInfo.InvariantCulture) + ".txt");
        if (!File.Exists(path))
        {
            return false;
        }

        var lines = File.ReadAllLines(path);
        if (lines.Length >= 11 &&
            TryParseConstructLine(lines[10], out constructs))
        {
            return true;
        }

        foreach (var line in lines)
        {
            if (TryParseConstructLine(line, out constructs) &&
                constructs.Count > 0)
            {
                return true;
            }
        }

        constructs = Array.Empty<LegacyGuildConstruct>();
        return false;
    }

    public static string? TryDeriveGuildsRootFromUsersRoot(string? usersRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(usersRoot);
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        if (!string.Equals(Path.GetFileName(trimmed), "Users", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dbRoot = Path.GetDirectoryName(trimmed);
        return string.IsNullOrEmpty(dbRoot)
            ? null
            : Path.Combine(dbRoot, "Guilds");
    }

    public static bool TryFindConstruct(
        IEnumerable<LegacyGuildConstruct>? constructs,
        uint id,
        out LegacyGuildConstruct construct)
    {
        if (constructs is not null)
        {
            foreach (var candidate in constructs)
            {
                if (candidate.Id == id)
                {
                    construct = candidate;
                    return true;
                }
            }
        }

        construct = default!;
        return false;
    }

    private static bool TryReadUInt32(string[] fields, int index, out uint value)
    {
        return uint.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt64(string[] fields, int index, out ulong value)
    {
        return ulong.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
