using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGearRankBonusTests
{
    [Theory]
    [InlineData(1, 0, 10)]
    [InlineData(10, 0, 1)]
    [InlineData(0, 3, 8)]
    [InlineData(1, 7, 4)]
    [InlineData(9, 2, 9)]
    [InlineData(0, 0, 0)]
    [InlineData(11, 0, 0)]
    [InlineData(3, 11, 8)]
    public void PirateGrantRetainsOverallFallbackAndWeeklyPriorityWithoutInventingLevelZero(
        int overall, int weekly, int expectedLevel)
    {
        using var scope = new NativeCombatGearQueryScope();
        LegacyNativeCombatGearRankTable.Create();
        Fill(LegacyNativeCombatGearRankTable.Type.Overall_Pirate, overall);
        Fill(LegacyNativeCombatGearRankTable.Type.WEEK_Pirate, weekly);
        var rank = LegacyNativeCombatGear.GetValueRank(17, 7051)!;
        Assert.Equal(expectedLevel == 0 ? (ushort)0 : (ushort)21930, rank.Magic);
        Assert.Equal(checked((ushort)expectedLevel), rank.MagicLevel);
        if (rank.Magic != 0) Assert.InRange(rank.MagicLevel, (ushort)1, (ushort)10);
    }

    private static void Fill(LegacyNativeCombatGearRankTable.Type type, int position)
    {
        var list = LegacyNativeCombatGearRankTable.Ranks[type].RankingList;
        for (int index = 1; index <= position; index++)
        {
            uint uid = index == position ? 17U : checked((uint)(1000 + index));
            // Effect capture reads the actual ordered loaded dictionary. These
            // source-data rows are not a live ranking publication capability.
            list.Add(uid, new LegacyNativeCombatGearRankTable.Entry
                { UID = uid, Type = type, Class = 7051, TotalPoints = checked((uint)(100 - index)) });
        }
    }
}
