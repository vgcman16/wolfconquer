using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private static int RankingScoreGroup(uint id) => id switch
    {
        >= 100 and <= 105 => 0, >= 200 and <= 205 => 1, >= 300 and <= 305 => 2,
        >= 400 and <= 405 => 3, >= 500 and <= 505 => 4, >= 600 and <= 605 => 5,
        _ => -1,
    };

    private static RankingMetadata MatchingRankingMetadata(RankingMetadata[] rows, uint id, uint level)
    {
        RankingMetadata? match = null;
        foreach (var row in rows)
        {
            if (row.Id != id || row.Level != level) continue;
            if (match is not null) throw UnavailableRanking();
            match = row;
        }
        return match ?? throw UnavailableRanking();
    }

    private static uint[] PrepareRankingScores(PersistenceState state, RankingItem[] items, RankingMetadata[] metadata)
    {
        try
        {
            checked
            {
                uint[] mastery = [state.FireMastery, state.WaterMastery, state.EarthMastery,
                    state.WindMastery, state.LightningMastery, 0];
                uint[] element = [state.Fire, state.Water, state.Earth, state.Wind, state.Lightning, 0];
                var groups = new uint[6];
                foreach (var item in items)
                {
                    var group = RankingScoreGroup(item.Id);
                    if (group < 0) continue;
                    var score = MatchingRankingMetadata(metadata, item.Id, item.Level).Score;
                    groups[group] += score + element[group] * 4;
                }
                for (var group = 0; group < groups.Length; group++) groups[group] += mastery[group];
                uint total = 0;
                foreach (var group in groups) total += group;
                return [total, groups[0], groups[1], groups[2], groups[3], groups[4]];
            }
        }
        catch (OverflowException error)
        {
            throw new InvalidOperationException("Ninja ranking score exceeds its declared UInt32 width.", error);
        }
    }

    private static LegacyNativeNinjaRank.Entry?[] PrepareRankingEntries(
        LegacyNativeNinjaRank.Entry? appearance, uint[] scores)
    {
        var entries = new LegacyNativeNinjaRank.Entry?[(int)LegacyNativeNinjaRank.Type.Count];
        if (appearance is null) return entries;
        for (var index = 0; index < entries.Length; index++)
        {
            var eligible = index >= (int)LegacyNativeNinjaRank.Type.Wind ? scores[index] > 1000 : scores[index] >= 1000;
            if (!eligible) continue;
            var entry = appearance.ShallowCopy();
            entry.Type = (LegacyNativeNinjaRank.Type)index;
            entry.TotalPoints = scores[index];
            entries[index] = entry;
        }
        return entries;
    }
}
