using System.Globalization;

namespace WolfConquer.Core.World;

public static class LegacyFamilyOccupyState
{
    public static LegacyFamilyOccupyShowInformation CreateShowInformation(string? usersRoot, uint npcUid)
    {
        if (!LegacyFamilyOccupyPacket.TryGetSourceClanWarInformationCity(
                npcUid,
                out var city,
                out var useBestWinner))
        {
            return LegacyFamilyOccupyPacket.CreateSourceDefaultShowInformation(npcUid);
        }

        var score = LegacyFamilyOccupyClanWarScore.None;
        var dbRoot = TryDeriveDbRoot(usersRoot);
        if (dbRoot is not null)
        {
            var clanWarPath = Path.Combine(dbRoot, "ClanWar", city + ".txt");
            if (TryReadClanWarFile(clanWarPath, out var winner, out var bestWinner))
            {
                score = useBestWinner ? bestWinner : winner;
            }
        }

        return new LegacyFamilyOccupyShowInformation(
            ClanId: score.ClanId,
            NpcUid: npcUid,
            Unknown5: 5,
            ClanName: string.IsNullOrWhiteSpace(score.Name) ? "None" : score.Name,
            MapName: city,
            Alive: false,
            UnknownByte: 1,
            UnknownShort: 1,
            OccupationDays: score.OccupationDays,
            Reward: score.Reward,
            NextReward: score.NextReward);
    }

    private static string? TryDeriveDbRoot(string? usersRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return null;
        }

        var trimmed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(usersRoot));
        return string.Equals(Path.GetFileName(trimmed), "Users", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(trimmed)
            : trimmed;
    }

    private static bool TryReadClanWarFile(
        string path,
        out LegacyFamilyOccupyClanWarScore winner,
        out LegacyFamilyOccupyClanWarScore bestWinner)
    {
        winner = LegacyFamilyOccupyClanWarScore.None;
        bestWinner = LegacyFamilyOccupyClanWarScore.None;

        string? row;
        try
        {
            row = File.ReadLines(path)
                .FirstOrDefault(line => !line.StartsWith("Count=", StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(row))
        {
            return false;
        }

        var parts = row.Split('/', StringSplitOptions.None);
        if (parts.Length < 12)
        {
            return false;
        }

        winner = ReadScore(parts, 0);
        bestWinner = ReadScore(parts, 6);
        return true;
    }

    private static LegacyFamilyOccupyClanWarScore ReadScore(string[] parts, int offset)
    {
        return new LegacyFamilyOccupyClanWarScore(
            ClanId: ReadUInt32(parts, offset),
            Name: NormalizeName(parts[offset + 1]),
            ClaimReward: ReadUInt32(parts, offset + 2),
            NextReward: ReadUInt32(parts, offset + 3),
            OccupationDays: ReadUInt32(parts, offset + 4),
            Reward: ReadUInt32(parts, offset + 5));
    }

    private static uint ReadUInt32(string[] parts, int index)
    {
        return index < parts.Length &&
            uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "None" : value;
    }

    private readonly record struct LegacyFamilyOccupyClanWarScore(
        uint ClanId,
        string Name,
        uint ClaimReward,
        uint NextReward,
        uint OccupationDays,
        uint Reward)
    {
        public static readonly LegacyFamilyOccupyClanWarScore None = new(
            ClanId: 0,
            Name: "None",
            ClaimReward: 0,
            NextReward: 0,
            OccupationDays: 0,
            Reward: 0);
    }
}
