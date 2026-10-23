using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaLoadTests
{
    [Fact]
    public Task BothRealInstanceRankMethodsReplaceArraysOnEmptyPoolWithoutTouchingTodayRanks() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var arena = new LegacyNativeArenaRuntime();
        var pool = LegacyNativeArenaRankings.ArenaPoll;
        var ten = LegacyNativeArenaRankings.Top10; var thousand = LegacyNativeArenaRankings.Top1000;
        var today = LegacyNativeArenaRankings.Top1000Today;
        arena.CreateRankTop10(); arena.CreateRankTop1000();
        Assert.NotSame(ten, LegacyNativeArenaRankings.Top10);
        Assert.NotSame(thousand, LegacyNativeArenaRankings.Top1000);
        Assert.Same(pool, LegacyNativeArenaRankings.ArenaPoll);
        Assert.Same(today, LegacyNativeArenaRankings.Top1000Today);
        Assert.Equal(10, LegacyNativeArenaRankings.Top10.Length);
        Assert.Equal(1000, LegacyNativeArenaRankings.Top1000.Length);
        Assert.All(LegacyNativeArenaRankings.Top10, Assert.Null);
        Assert.All(LegacyNativeArenaRankings.Top1000, Assert.Null);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task FullInstanceRankMethodsUseDifferentRealScoresAndCapAtTenAndOneThousand() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var arena = new LegacyNativeArenaRuntime();
        for (uint uid = 1; uid <= 1002; uid++)
        {
            var row = new LegacyNativeArenaUser { UID = uid, LastSeasonArenaPoints = uid };
            row.Info.CurrentHonor = 1003 - uid; row.Info.ArenaPoints = uid * 3; row.Info.TodayRank = 88;
            Assert.True(LegacyNativeArenaRankings.ArenaPoll.TryAdd(uid, row));
        }
        arena.CreateRankTop10(); arena.CreateRankTop1000();
        for (int index = 0; index < 10; index++)
            Assert.Same(LegacyNativeArenaRankings.ArenaPoll[(uint)(1002 - index)], LegacyNativeArenaRankings.Top10[index]);
        for (int index = 0; index < 1000; index++)
            Assert.Same(LegacyNativeArenaRankings.ArenaPoll[(uint)(index + 1)], LegacyNativeArenaRankings.Top1000[index]);
        Assert.All(LegacyNativeArenaRankings.ArenaPoll.Values, row => Assert.Equal(88u, row.Info.TodayRank));
        Assert.All(LegacyNativeArenaRankings.Top1000Today, Assert.Null);
    });

    [Fact]
    public Task MultipleRealInstancesShareTheSameRankingsAndRetainDuplicatePhysicalRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = new LegacyNativeArenaRuntime(); var second = new LegacyNativeArenaRuntime();
        var row = new LegacyNativeArenaUser { UID = 777, LastSeasonArenaPoints = 12 };
        row.Info.CurrentHonor = 56;
        Assert.True(LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, row));
        Assert.True(LegacyNativeArenaRankings.ArenaPoll.TryAdd(2, row));
        first.CreateRankTop10(); second.CreateRankTop1000();
        Assert.Same(row, LegacyNativeArenaRankings.Top10[0]); Assert.Same(row, LegacyNativeArenaRankings.Top10[1]);
        Assert.Same(row, LegacyNativeArenaRankings.Top1000[0]); Assert.Same(row, LegacyNativeArenaRankings.Top1000[1]);
        Assert.Null(LegacyNativeArenaRankings.Top10[2]); Assert.Null(LegacyNativeArenaRankings.Top1000[2]);
        Assert.False(LegacyNativeArenaRankings.ArenaPoll.ContainsKey(777));
        Assert.Empty(first.Registered); Assert.Empty(second.Registered);
        Assert.Null(LegacyNativeMsgSchedules.Arena); // Direct genuine instance calls do not manufacture startup.
    });

    [Fact]
    public Task NullInfoOnlyFaultsTheHonorRankAfterPublishingItsNewArrayWithoutCleaningTheRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var arena = new LegacyNativeArenaRuntime();
        var row = new LegacyNativeArenaUser { UID = 3, LastSeasonArenaPoints = 4, Info = null! };
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(3, row);
        arena.CreateRankTop10(); Assert.Same(row, LegacyNativeArenaRankings.Top10[0]);
        var oldHonor = LegacyNativeArenaRankings.Top1000;
        Assert.Throws<NullReferenceException>(arena.CreateRankTop1000);
        Assert.NotSame(oldHonor, LegacyNativeArenaRankings.Top1000);
        Assert.All(LegacyNativeArenaRankings.Top1000, Assert.Null);
        Assert.Same(row, LegacyNativeArenaRankings.ArenaPoll[3]); Assert.Null(row.Info);
    });

    [Fact]
    public Task HonorRanksReadTheCurrentInfoObjectAndNeitherInstanceMethodRewritesTodayRank() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var arena = new LegacyNativeArenaRuntime();
        var a = Row(1); var b = Row(2); a.Info.CurrentHonor = 1000; b.Info.CurrentHonor = 1;
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, a); LegacyNativeArenaRankings.ArenaPoll.TryAdd(2, b);
        arena.CreateRankTop1000(); Assert.Same(a, LegacyNativeArenaRankings.Top1000[0]);
        b.Info = new LegacyNativeMsgArenaInfo { CurrentHonor = 2000, TodayRank = 99 };
        arena.CreateRankTop1000(); Assert.Same(b, LegacyNativeArenaRankings.Top1000[0]);
        Assert.Equal(3u, a.Info.TodayRank); Assert.Equal(99u, b.Info.TodayRank);
    });
}
