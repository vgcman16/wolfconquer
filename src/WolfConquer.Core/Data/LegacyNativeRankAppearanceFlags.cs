using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Core.Data;

// Existing appearance-code priorities, resolved before any live Entry copy.
// This does not authorize the supplied client, rank membership or publication.
internal static class LegacyNativeRankAppearanceFlags
{
    internal static uint? Read(LegacyNativePlayerRuntime player, bool ninjaPriorities)
    {
        var vector = player.BitVector;
        if (vector is null)
            throw new InvalidDataException("Rank appearance flag membership is unavailable without its physical vector.");

        lock (vector.SyncRoot)
        {
            if (!ReferenceEquals(player.BitVector, vector)) throw ChangedVector();
            uint? selected = ReadPriorities(vector, ninjaPriorities);
            if (!ReferenceEquals(player.BitVector, vector)) throw ChangedVector();
            return selected;
        }
    }

    private static InvalidDataException ChangedVector() =>
        new("Rank appearance flag membership changed its physical vector during preparation.");

    private static uint? ReadPriorities(LegacyNativeStatusFlagsBigVector32 vector, bool ninjaPriorities)
    {
        ReadOnlySpan<(Flag Flag, uint Appearance)> common =
        [
            (Flag.WeeklyPKChampion, 37), (Flag.TopTrojan, 39), (Flag.TopWarrior, 38),
            (Flag.TopArcher, 40), (Flag.TopNinja, 43), (Flag.TopMonk, 114),
            (Flag.TopPirate, 122), (Flag.TopDragonLee, 154),
            (Flag.TopFireTaoist, 42), (Flag.TopWaterTaoist, 41),
        ];
        uint? selected = ReadFirst(vector, common);
        if (selected.HasValue) return selected;

        if (ninjaPriorities)
        {
            ReadOnlySpan<(Flag Flag, uint Appearance)> ninja =
            [
                (Flag.TopSuperGuildWarFiveStars, 151), (Flag.TopSuperGuildWarThreeStars, 152),
                (Flag.TopSuperGuildWarOneStar, 153), (Flag.TopMrsConquer, 167), (Flag.TopMrConquer, 166),
            ];
            selected = ReadFirst(vector, ninja);
            if (selected.HasValue) return selected;
        }

        ReadOnlySpan<(Flag Flag, uint Appearance)> leaders =
        [
            (Flag.TopDeputyLeader, 35), (Flag.TopGuildLeader, 34),
            (Flag.TopWindWalker, 203), (Flag.TopThunderstriker, 232),
        ];
        return ReadFirst(vector, leaders);
    }

    private static uint? ReadFirst(LegacyNativeStatusFlagsBigVector32 vector,
        ReadOnlySpan<(Flag Flag, uint Appearance)> priorities)
    {
        foreach (var priority in priorities)
            if (vector.ContainFlag((int)priority.Flag)) return priority.Appearance;
        return null;
    }
}
