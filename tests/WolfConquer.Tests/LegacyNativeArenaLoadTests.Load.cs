using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaLoadTests
{
    [Fact]
    public Task WholeLoadReadsActualNativeRowsThenPublishesAllThreeRankArraysUsingTheExistingArena() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        File.WriteAllLines(files.ArenaFile, new[] { "Count=1", NativeRow });
        var arena = new LegacyNativeArenaRuntime(); LegacyNativeMsgSchedules.Arena = arena;
        var pool = LegacyNativeArenaRankings.ArenaPoll;
        new LegacyNativeArenaTable().Load();
        Assert.Same(pool, LegacyNativeArenaRankings.ArenaPoll); Assert.Single(pool);
        var row = pool[1];
        Assert.Equal("Alpha", row.Name); Assert.Equal((ushort)130, row.Level);
        Assert.Equal(16004u, row.Class); Assert.Equal(1003u, row.Mesh);
        Assert.Equal(100u, row.Info.ArenaPoints); Assert.Equal(200u, row.Info.CurrentHonor);
        Assert.Equal(300u, row.Info.HistoryHonor); Assert.Equal(4u, row.Info.TodayBattles);
        Assert.Equal(7u, row.Info.TodayWin); Assert.Equal(0u, row.Info.TotalWin); // Native duplicate load destination.
        Assert.Equal(6u, row.Info.TotalLose); Assert.Equal(8u, row.LastSeasonArenaPoints);
        Assert.Equal(9u, row.LastSeasonWin); Assert.Equal(10u, row.LastSeasonLose); Assert.Equal(11u, row.LastSeasonRank);
        Assert.Equal(1u, row.Info.TodayRank);
        Assert.Same(row, LegacyNativeArenaRankings.Top10[0]);
        Assert.Same(row, LegacyNativeArenaRankings.Top1000[0]);
        Assert.Same(row, LegacyNativeArenaRankings.Top1000Today[0]);
        Assert.Same(arena, LegacyNativeMsgSchedules.Arena);
        Assert.Empty(arena.Registered); Assert.Empty(arena.MatchesRegistered);
        Assert.Empty(LegacyNativeMsgSchedules.Tournaments); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task WholeLoadDoesNotClearExistingPoolOrReplaceADuplicateUidWithAFileRecord() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        var existing = Row(); existing.Name = "KeepExisting"; existing.Info.ArenaPoints = 999;
        var unrelated = Row(9); unrelated.LastSeasonArenaPoints = 9999;
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, existing); LegacyNativeArenaRankings.ArenaPoll.TryAdd(9, unrelated);
        File.WriteAllLines(files.ArenaFile, new[] { "Count=2", NativeRow, "2/Beta/90/2002/1004/2000/500/600/1/2/3/4/5/6/7/8/" });
        LegacyNativeMsgSchedules.Arena = new LegacyNativeArenaRuntime();
        new LegacyNativeArenaTable().Load();
        Assert.Equal(3, LegacyNativeArenaRankings.ArenaPoll.Count);
        Assert.Same(existing, LegacyNativeArenaRankings.ArenaPoll[1]); Assert.Equal("KeepExisting", existing.Name);
        Assert.Same(unrelated, LegacyNativeArenaRankings.ArenaPoll[9]);
        Assert.Equal("Beta", LegacyNativeArenaRankings.ArenaPoll[2].Name);
        Assert.Same(unrelated, LegacyNativeArenaRankings.Top10[0]);
        Assert.Same(LegacyNativeArenaRankings.ArenaPoll[2], LegacyNativeArenaRankings.Top1000Today[0]);
        Assert.Equal(2u, existing.Info.TodayRank);
    });

    [Fact]
    public Task DefaultNullArenaFailsAfterRowPublicationWithoutCreatingArenaOrReplacingRankArrays() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        File.WriteAllLines(files.ArenaFile, new[] { "Count=1", NativeRow });
        var ten = LegacyNativeArenaRankings.Top10; var thousand = LegacyNativeArenaRankings.Top1000;
        var today = LegacyNativeArenaRankings.Top1000Today;
        Assert.Throws<NullReferenceException>(() => new LegacyNativeArenaTable().Load());
        Assert.Single(LegacyNativeArenaRankings.ArenaPoll);
        Assert.Equal("Alpha", LegacyNativeArenaRankings.ArenaPoll[1].Name);
        Assert.Equal(0u, LegacyNativeArenaRankings.ArenaPoll[1].Info.TodayRank);
        Assert.Null(LegacyNativeMsgSchedules.Arena); Assert.Empty(LegacyNativeMsgSchedules.Tournaments);
        Assert.Same(ten, LegacyNativeArenaRankings.Top10); Assert.Same(thousand, LegacyNativeArenaRankings.Top1000);
        Assert.Same(today, LegacyNativeArenaRankings.Top1000Today);
    });

    [Theory]
    [InlineData("Count=0")]
    [InlineData("not-a-count-header")]
    [InlineData("Count=bad")]
    [InlineData("Count=-1")]
    public Task FalseReaderResultStillRunsTheThreeWholeRankMethods(string header) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        File.WriteAllLines(files.ArenaFile, new[] { header });
        var existing = Row(7); LegacyNativeArenaRankings.ArenaPoll.TryAdd(7, existing);
        var ten = LegacyNativeArenaRankings.Top10; var thousand = LegacyNativeArenaRankings.Top1000;
        var today = LegacyNativeArenaRankings.Top1000Today;
        LegacyNativeMsgSchedules.Arena = new LegacyNativeArenaRuntime();
        new LegacyNativeArenaTable().Load();
        Assert.Single(LegacyNativeArenaRankings.ArenaPoll); Assert.Same(existing, LegacyNativeArenaRankings.ArenaPoll[7]);
        Assert.NotSame(ten, LegacyNativeArenaRankings.Top10); Assert.NotSame(thousand, LegacyNativeArenaRankings.Top1000);
        Assert.NotSame(today, LegacyNativeArenaRankings.Top1000Today);
        Assert.Same(existing, LegacyNativeArenaRankings.Top10[0]);
        Assert.Same(existing, LegacyNativeArenaRankings.Top1000[0]);
        Assert.Same(existing, LegacyNativeArenaRankings.Top1000Today[0]);
        Assert.Equal(1u, existing.Info.TodayRank);
    });

    [Fact]
    public Task MissingFileStillRanksPreexistingRowsButDoesNotCreateAFileOrRepairSchedulerIdentity() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        var row = Row(); LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, row);
        var arena = new LegacyNativeArenaRuntime(); LegacyNativeMsgSchedules.Arena = arena;
        new LegacyNativeArenaTable().Load();
        Assert.False(File.Exists(files.ArenaFile)); Assert.Same(arena, LegacyNativeMsgSchedules.Arena);
        Assert.Same(row, LegacyNativeArenaRankings.Top10[0]); Assert.Same(row, LegacyNativeArenaRankings.Top1000[0]);
        Assert.Same(row, LegacyNativeArenaRankings.Top1000Today[0]); Assert.Equal(1u, row.Info.TodayRank);
    });

    [Fact]
    public Task TruncatedDeclaredFileCreatesTheActualEmptyLoadedRowRatherThanSkippingIt() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        File.WriteAllLines(files.ArenaFile, new[] { "Count=2", NativeRow });
        LegacyNativeMsgSchedules.Arena = new LegacyNativeArenaRuntime();
        new LegacyNativeArenaTable().Load();
        Assert.Equal(2, LegacyNativeArenaRankings.ArenaPoll.Count);
        var empty = LegacyNativeArenaRankings.ArenaPoll[0];
        Assert.Equal("None", empty.Name); Assert.Equal(0u, empty.Class); Assert.Equal(0u, empty.Info.ArenaPoints);
        Assert.Equal(2u, empty.Info.TodayRank); Assert.Equal(0u, empty.Info.TotalWin);
        Assert.Same(empty, LegacyNativeArenaRankings.Top1000Today[1]);
    });

    [Fact]
    public Task SaveThenWholeLoadUsesNativeOwnedFilesAndKeepsTheHistoricalDuplicateWinBug() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FileScope();
        var original = Row(); LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, original);
        var table = new LegacyNativeArenaTable(); table.Save();
        Assert.Equal(new[] { "Count=1", NativeRow }, File.ReadAllLines(files.ArenaFile));
        LegacyNativeArenaRankings.ArenaPoll.Clear(); LegacyNativeMsgSchedules.Arena = new LegacyNativeArenaRuntime();
        table.Load();
        var loaded = LegacyNativeArenaRankings.ArenaPoll[1];
        Assert.NotSame(original, loaded); Assert.Equal(5u, original.Info.TodayWin); Assert.Equal(7u, original.Info.TotalWin);
        Assert.Equal(7u, loaded.Info.TodayWin); Assert.Equal(0u, loaded.Info.TotalWin);
        Assert.Equal(1u, loaded.Info.TodayRank); Assert.Equal(11u, loaded.LastSeasonRank);
        Assert.Same(loaded, LegacyNativeArenaRankings.Top1000Today[0]); Assert.Empty(scope.Queue);
    });
}
