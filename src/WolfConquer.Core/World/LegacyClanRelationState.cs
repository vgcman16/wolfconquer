namespace WolfConquer.Core.World;

public sealed class LegacyClanRelationState
{
    private const string CountLine = "Count=3";

    public LegacyClanRelationState(
        uint clanId,
        string name,
        string leaderName,
        byte level,
        uint donation,
        string bulletin,
        byte battlePower,
        IReadOnlyList<uint> allyIds,
        IReadOnlyList<uint> enemyIds,
        string? coreLine = null)
    {
        ClanId = clanId;
        Name = string.IsNullOrWhiteSpace(name) ? "None" : name;
        LeaderName = string.IsNullOrWhiteSpace(leaderName) ? "None" : leaderName;
        Level = level;
        Donation = donation;
        Bulletin = string.IsNullOrWhiteSpace(bulletin) ? "None" : bulletin;
        BattlePower = battlePower;
        AllyIds = allyIds.ToArray();
        EnemyIds = enemyIds.ToArray();
        _coreLine = coreLine;
    }

    public uint ClanId { get; }

    public string Name { get; }

    public string LeaderName { get; }

    public byte Level { get; }

    public uint Donation { get; }

    public string Bulletin { get; }

    public byte BattlePower { get; }

    public IReadOnlyList<uint> AllyIds { get; }

    public IReadOnlyList<uint> EnemyIds { get; }

    public static bool TryRead(string path, out LegacyClanRelationState state)
    {
        state = default!;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return TryParse(lines, out state);
    }

    public static bool TryParse(IEnumerable<string> lines, out LegacyClanRelationState state)
    {
        state = default!;

        var logicalLines = lines
            .Where(line => line is not null && !line.StartsWith("Count=", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (logicalLines.Length == 0)
        {
            return false;
        }

        if (!TryParseCoreLine(logicalLines[0], out var clanId, out var name, out var leaderName, out var level, out var donation, out var bulletin, out var battlePower))
        {
            return false;
        }

        var allyIds = ParseRelationIds(logicalLines.ElementAtOrDefault(1));
        var enemyIds = ParseRelationIds(logicalLines.ElementAtOrDefault(2));
        state = new LegacyClanRelationState(
            clanId,
            name,
            leaderName,
            level,
            donation,
            bulletin,
            battlePower,
            allyIds,
            enemyIds,
            logicalLines[0]);
        return true;
    }

    public LegacyClanRelationState WithRelations(
        IReadOnlyCollection<uint> allyIds,
        IReadOnlyCollection<uint> enemyIds)
    {
        return new LegacyClanRelationState(
            ClanId,
            Name,
            LeaderName,
            Level,
            Donation,
            Bulletin,
            BattlePower,
            allyIds.ToArray(),
            enemyIds.ToArray(),
            _coreLine ?? ToCoreLine());
    }

    public void WriteTo(string path)
    {
        File.WriteAllLines(
            path,
            [
                CountLine,
                _coreLine ?? ToCoreLine(),
                FormatRelationLine(AllyIds),
                FormatRelationLine(EnemyIds),
            ]);
    }

    public string ToCoreLine()
    {
        return string.Join(
            '/',
            [
                ClanId.ToString(),
                Name,
                LeaderName,
                Level.ToString(),
                Donation.ToString(),
                Bulletin,
                BattlePower.ToString(),
                "0",
                "0",
                "0",
                string.Empty,
            ]);
    }

    private static bool TryParseCoreLine(
        string line,
        out uint clanId,
        out string name,
        out string leaderName,
        out byte level,
        out uint donation,
        out string bulletin,
        out byte battlePower)
    {
        clanId = 0;
        name = "None";
        leaderName = "None";
        level = 0;
        donation = 0;
        bulletin = "None";
        battlePower = 0;

        var parts = line.Split('/', StringSplitOptions.None);
        if (parts.Length < 7 ||
            !uint.TryParse(parts[0], out clanId) ||
            !byte.TryParse(parts[3], out level) ||
            !uint.TryParse(parts[4], out donation) ||
            !byte.TryParse(parts[6], out battlePower))
        {
            return false;
        }

        name = NormalizeLegacyText(parts[1]);
        leaderName = NormalizeLegacyText(parts[2]);
        bulletin = NormalizeLegacyText(parts[5]);
        return true;
    }

    private static IReadOnlyList<uint> ParseRelationIds(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return Array.Empty<uint>();
        }

        var parts = line.Split('/', StringSplitOptions.None);
        if (parts.Length == 0 || !int.TryParse(parts[0], out var count) || count <= 0)
        {
            return Array.Empty<uint>();
        }

        var ids = new List<uint>(count);
        for (var index = 0; index < count; index++)
        {
            ids.Add(ReadUInt32(parts, index + 1));
        }

        return ids;
    }

    private static uint ReadUInt32(string[] parts, int index)
    {
        return index < parts.Length && uint.TryParse(parts[index], out var value)
            ? value
            : 0;
    }

    private static string NormalizeLegacyText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "None" : value;
    }

    private static string FormatRelationLine(IEnumerable<uint> ids)
    {
        var values = ids.ToArray();
        var parts = new List<string>(values.Length + 2)
        {
            values.Length.ToString(),
        };

        parts.AddRange(values.Select(value => value.ToString()));
        parts.Add(string.Empty);
        return string.Join('/', parts);
    }

    private readonly string? _coreLine;
}
