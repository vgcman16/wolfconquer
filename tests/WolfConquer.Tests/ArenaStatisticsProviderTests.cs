using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class ArenaStatisticsProviderTests
{
    [Fact]
    public void ProviderComputesTodayRankFromCurrentArenaPoints()
    {
        var provider = new ArenaStatisticsProvider(
        [
            ArenaRecord(1001, "Second", points: 4500, currentHonor: 500, historyHonor: 600, todayBattles: 5, todayWin: 4, totalLose: 3, totalWin: 12),
            ArenaRecord(1002, "First", points: 6000),
            ArenaRecord(1003, "Third", points: 4000),
        ]);

        var stats = provider.GetForCharacter(1001);

        Assert.Equal(2U, stats.TodayRank);
        Assert.Equal(4500U, stats.ArenaPoints);
        Assert.Equal(12U, stats.TotalWin);
        Assert.Equal(3U, stats.TotalLose);
        Assert.Equal(4U, stats.TodayWin);
        Assert.Equal(5U, stats.TodayBattles);
        Assert.Equal(600U, stats.HistoryHonor);
        Assert.Equal(500U, stats.CurrentHonor);
    }

    [Fact]
    public void ProviderReturnsLegacyDefaultForMissingArenaRecord()
    {
        var provider = new ArenaStatisticsProvider([]);

        var stats = provider.GetForCharacter(1293600);

        Assert.Equal(0U, stats.TodayRank);
        Assert.Equal(4000U, stats.ArenaPoints);
    }

    private static ArenaRecord ArenaRecord(
        int playerId,
        string name,
        int points,
        int currentHonor = 0,
        int historyHonor = 0,
        int todayBattles = 0,
        int todayWin = 0,
        int totalLose = 0,
        int totalWin = 0)
    {
        return new ArenaRecord(
            playerId,
            name,
            [140, 7051, 1008, points, currentHonor, historyHonor, todayBattles, todayWin, totalLose, totalWin, 4000, 0, 0, 999],
            "Arena.ini",
            playerId,
            $"{playerId}/{name}/140/7051/1008/{points}/{currentHonor}/{historyHonor}/{todayBattles}/{todayWin}/{totalLose}/{totalWin}/4000/0/0/999/",
            [$"{playerId}", name, "140", "7051", "1008", $"{points}", $"{currentHonor}", $"{historyHonor}", $"{todayBattles}", $"{todayWin}", $"{totalLose}", $"{totalWin}", "4000", "0", "0", "999", ""]);
    }
}
