using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyRuneRankMessage
{
    public static LegacyMsgMessagePacket CreateStartup(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        IReadOnlyList<RuneRankingRecord>? rankings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(playerItems);

        var score = LegacyRuneStoragePacket.ComputeStorageScore(playerItems);
        var rank = FindRank(snapshot.Uid, rankings ?? []);
        var text = rank is null
            ? $"My ranking: Unranked; Current rune pack score: {score}"
            : $"My ranking: {rank.Value}; Current rune pack score: {score}";

        return LegacyMsgMessagePacket.SystemRuneRank(text);
    }

    private static int? FindRank(long uid, IReadOnlyList<RuneRankingRecord> rankings)
    {
        for (var index = 0; index < rankings.Count; index++)
        {
            if (rankings[index].CharacterId == uid)
            {
                return index + 1;
            }
        }

        return null;
    }
}
