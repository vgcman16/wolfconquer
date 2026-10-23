using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaLoadTests
{
    [Fact]
    public Task WholeResetCapturesTheOldSeasonAwardsHonorThenResetsAndRebuildsAllThreeRanks() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row();
        row.AcceptBox = true; row.Cheers = 4; row.Damage = 5;
        row.Info.Status = LegacyNativeMsgArenaInfo.Action.WaitingForOpponent;
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, row);
        LegacyNativeMsgSchedules.Arena = new LegacyNativeArenaRuntime();
        new LegacyNativeArenaTable().ResetArena();
        Assert.Equal(100u, row.LastSeasonArenaPoints); Assert.Equal(5u, row.LastSeasonWin);
        Assert.Equal(6u, row.LastSeasonLose); Assert.Equal(3u, row.LastSeasonRank);
        Assert.Equal(700u, row.Info.CurrentHonor); Assert.Equal(700u, row.Info.HistoryHonor);
        Assert.Equal(4000u, row.Info.ArenaPoints); Assert.Equal(0u, row.Info.TodayWin);
        Assert.Equal(0u, row.Info.TodayBattles); Assert.Equal(0u, row.Info.TotalLose);
        Assert.Equal(1u, row.Info.TodayRank); // Whole method calls UpdateRank after zeroing this field.
        Assert.Equal(7u, row.Info.TotalWin); Assert.True(row.AcceptBox);
        Assert.Equal(4u, row.Cheers); Assert.Equal(5u, row.Damage);
        Assert.Equal(LegacyNativeMsgArenaInfo.Action.WaitingForOpponent, row.Info.Status);
        Assert.Same(row, LegacyNativeArenaRankings.Top10[0]); Assert.Same(row, LegacyNativeArenaRankings.Top1000[0]);
        Assert.Same(row, LegacyNativeArenaRankings.Top1000Today[0]); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0u, 100u)]
    [InlineData(1u, 4000u)]
    [InlineData(0u, 4000u)]
    public Task NoHonorAwardOccursWhenOldRankIsZeroOrPointsEqualFourThousand(uint rank, uint points) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row();
        row.Info.TodayRank = rank; row.Info.ArenaPoints = points;
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, row); LegacyNativeMsgSchedules.Arena = new LegacyNativeArenaRuntime();
        new LegacyNativeArenaTable().ResetArena();
        Assert.Equal(points, row.LastSeasonArenaPoints); Assert.Equal(rank, row.LastSeasonRank);
        Assert.Equal(200u, row.Info.CurrentHonor); Assert.Equal(300u, row.Info.HistoryHonor);
        Assert.Equal(4000u, row.Info.ArenaPoints); Assert.Equal(1u, row.Info.TodayRank);
    });

    [Fact]
    public Task HonorAwardKeepsNativeUncheckedUIntArithmeticAndComparesHistoryAfterOverflow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row();
        row.Info.ArenaPoints = uint.MaxValue; row.Info.CurrentHonor = 10; row.Info.HistoryHonor = 9;
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, row); LegacyNativeMsgSchedules.Arena = new LegacyNativeArenaRuntime();
        new LegacyNativeArenaTable().ResetArena();
        Assert.Equal(uint.MaxValue, row.LastSeasonArenaPoints);
        Assert.Equal(5u, row.Info.CurrentHonor); Assert.Equal(9u, row.Info.HistoryHonor);
        Assert.Equal(4000u, row.Info.ArenaPoints); Assert.Equal(1u, row.Info.TodayRank);
    });

    [Fact]
    public Task DefaultNullArenaFaultHappensAfterEveryRecordMutationButBeforeRankArrayReplacement() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row();
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, row);
        var ten = LegacyNativeArenaRankings.Top10; var thousand = LegacyNativeArenaRankings.Top1000;
        var today = LegacyNativeArenaRankings.Top1000Today;
        Assert.Throws<NullReferenceException>(() => new LegacyNativeArenaTable().ResetArena());
        Assert.Equal(100u, row.LastSeasonArenaPoints); Assert.Equal(5u, row.LastSeasonWin);
        Assert.Equal(6u, row.LastSeasonLose); Assert.Equal(3u, row.LastSeasonRank);
        Assert.Equal(700u, row.Info.CurrentHonor); Assert.Equal(700u, row.Info.HistoryHonor);
        Assert.Equal(4000u, row.Info.ArenaPoints); Assert.Equal(0u, row.Info.TodayRank);
        Assert.Equal(0u, row.Info.TodayWin); Assert.Equal(0u, row.Info.TodayBattles); Assert.Equal(0u, row.Info.TotalLose);
        Assert.Same(ten, LegacyNativeArenaRankings.Top10); Assert.Same(thousand, LegacyNativeArenaRankings.Top1000);
        Assert.Same(today, LegacyNativeArenaRankings.Top1000Today); Assert.Null(LegacyNativeMsgSchedules.Arena);
    });

    [Fact]
    public Task RepeatedResetUsesTheNewSeasonSnapshotAndDoesNotReawardFourThousandPointRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row();
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, row); LegacyNativeMsgSchedules.Arena = new LegacyNativeArenaRuntime();
        var table = new LegacyNativeArenaTable(); table.ResetArena(); table.ResetArena();
        Assert.Equal(4000u, row.LastSeasonArenaPoints); Assert.Equal(0u, row.LastSeasonWin);
        Assert.Equal(0u, row.LastSeasonLose); Assert.Equal(1u, row.LastSeasonRank);
        Assert.Equal(700u, row.Info.CurrentHonor); Assert.Equal(700u, row.Info.HistoryHonor);
        Assert.Equal(7u, row.Info.TotalWin); Assert.Equal(1u, row.Info.TodayRank);
    });

    [Fact]
    public Task EmptyWholeResetStillCallsBothInstanceRankMethodsAndStaticTodayRankMethod() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativeMsgSchedules.Arena = new LegacyNativeArenaRuntime();
        var ten = LegacyNativeArenaRankings.Top10; var thousand = LegacyNativeArenaRankings.Top1000;
        var today = LegacyNativeArenaRankings.Top1000Today;
        new LegacyNativeArenaTable().ResetArena();
        Assert.NotSame(ten, LegacyNativeArenaRankings.Top10); Assert.NotSame(thousand, LegacyNativeArenaRankings.Top1000);
        Assert.NotSame(today, LegacyNativeArenaRankings.Top1000Today);
        Assert.All(LegacyNativeArenaRankings.Top10, Assert.Null);
        Assert.All(LegacyNativeArenaRankings.Top1000, Assert.Null);
        Assert.All(LegacyNativeArenaRankings.Top1000Today, Assert.Null); Assert.Empty(scope.Queue);
    });
}
