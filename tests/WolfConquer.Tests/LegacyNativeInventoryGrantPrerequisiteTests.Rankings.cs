using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryGrantPrerequisiteTests
{
    [Fact]
    public Task EmptyRankUpdatesReplaceOnlyTodayArraysAndRetainRealPoolAndOtherRankOwners() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var aPool = LegacyNativeArenaRankings.ArenaPoll; var tPool = LegacyNativeTeamArenaRankings.ArenaPoll;
        var aToday = LegacyNativeArenaRankings.Top1000Today; var tToday = LegacyNativeTeamArenaRankings.Top1000Today;
        var a10 = LegacyNativeArenaRankings.Top10; var a1000 = LegacyNativeArenaRankings.Top1000;
        var t10 = LegacyNativeTeamArenaRankings.Top10; var t1000 = LegacyNativeTeamArenaRankings.Top1000;
        LegacyNativeArenaRankings.UpdateRank(); LegacyNativeTeamArenaRankings.UpdateRank();
        Assert.Same(aPool, LegacyNativeArenaRankings.ArenaPoll); Assert.Same(tPool, LegacyNativeTeamArenaRankings.ArenaPoll);
        Assert.NotSame(aToday, LegacyNativeArenaRankings.Top1000Today);
        Assert.NotSame(tToday, LegacyNativeTeamArenaRankings.Top1000Today);
        Assert.Equal(1000, LegacyNativeArenaRankings.Top1000Today.Length);
        Assert.Equal(1000, LegacyNativeTeamArenaRankings.Top1000Today.Length);
        Assert.All(LegacyNativeArenaRankings.Top1000Today, Assert.Null);
        Assert.All(LegacyNativeTeamArenaRankings.Top1000Today, Assert.Null);
        Assert.Same(a10, LegacyNativeArenaRankings.Top10); Assert.Same(a1000, LegacyNativeArenaRankings.Top1000);
        Assert.Same(t10, LegacyNativeTeamArenaRankings.Top10); Assert.Same(t1000, LegacyNativeTeamArenaRankings.Top1000);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task BothWholeRankUpdatesSortCurrentPointsCapAtOneThousandAndClearEveryOldRank() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint uid = 1; uid <= 1002; uid++)
        {
            var arena = new LegacyNativeArenaUser { UID = uid, LastSeasonRank = 77 };
            arena.Info.ArenaPoints = uid; arena.Info.TodayRank = 9999;
            var team = new LegacyNativeTeamArenaUser { UID = uid, LastSeasonRank = 88 };
            team.Info.ArenaPoints = uid; team.Info.TodayRank = 9999;
            Assert.True(LegacyNativeArenaRankings.ArenaPoll.TryAdd(uid, arena));
            Assert.True(LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(uid, team));
        }
        LegacyNativeArenaRankings.UpdateRank(); LegacyNativeTeamArenaRankings.UpdateRank();
        for (int index = 0; index < 1000; index++)
        {
            uint expectedUid = (uint)(1002 - index);
            var arena = LegacyNativeArenaRankings.Top1000Today[index];
            var team = LegacyNativeTeamArenaRankings.Top1000Today[index];
            Assert.Same(LegacyNativeArenaRankings.ArenaPoll[expectedUid], arena);
            Assert.Same(LegacyNativeTeamArenaRankings.ArenaPoll[expectedUid], team);
            Assert.Equal((uint)(index + 1), arena.Info.TodayRank);
            Assert.Equal((uint)(index + 1), team.Info.TodayRank);
            Assert.Equal(77u, arena.LastSeasonRank); Assert.Equal(88u, team.LastSeasonRank);
        }
        Assert.Equal(0u, LegacyNativeArenaRankings.ArenaPoll[1].Info.TodayRank);
        Assert.Equal(0u, LegacyNativeArenaRankings.ArenaPoll[2].Info.TodayRank);
        Assert.Equal(0u, LegacyNativeTeamArenaRankings.ArenaPoll[1].Info.TodayRank);
        Assert.Equal(0u, LegacyNativeTeamArenaRankings.ArenaPoll[2].Info.TodayRank);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task RankingReadsCurrentInfoAndDoesNotNormalizeKeysOrDeduplicatePhysicalAliases() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var arena = new LegacyNativeArenaUser { UID = 777 }; var team = new LegacyNativeTeamArenaUser { UID = 888 };
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(10, arena); LegacyNativeArenaRankings.ArenaPoll.TryAdd(11, arena);
        LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(12, team); LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(13, team);
        arena.Info = new LegacyNativeMsgArenaInfo { ArenaPoints = 99 };
        team.Info = new LegacyNativeMsgTeamArenaInfo { ArenaPoints = 100 };
        LegacyNativeArenaRankings.UpdateRank(); LegacyNativeTeamArenaRankings.UpdateRank();
        Assert.Same(arena, LegacyNativeArenaRankings.Top1000Today[0]);
        Assert.Same(arena, LegacyNativeArenaRankings.Top1000Today[1]);
        Assert.Same(team, LegacyNativeTeamArenaRankings.Top1000Today[0]);
        Assert.Same(team, LegacyNativeTeamArenaRankings.Top1000Today[1]);
        Assert.Equal(2u, arena.Info.TodayRank); Assert.Equal(2u, team.Info.TodayRank);
        Assert.False(LegacyNativeArenaRankings.ArenaPoll.ContainsKey(777));
        Assert.False(LegacyNativeTeamArenaRankings.ArenaPoll.ContainsKey(888));
        Assert.Null(LegacyNativeArenaRankings.Top1000Today[2]); Assert.Null(LegacyNativeTeamArenaRankings.Top1000Today[2]);
    });

    [Fact]
    public Task NullInfoRankFaultOccursAfterNewArrayPublicationWithoutRemovingTheBadRow() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var arena = new LegacyNativeArenaUser { Info = null! }; var team = new LegacyNativeTeamArenaUser { Info = null! };
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(1, arena); LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(1, team);
        var oldA = LegacyNativeArenaRankings.Top1000Today; var oldT = LegacyNativeTeamArenaRankings.Top1000Today;
        Assert.Throws<NullReferenceException>(LegacyNativeArenaRankings.UpdateRank);
        Assert.Throws<NullReferenceException>(LegacyNativeTeamArenaRankings.UpdateRank);
        Assert.NotSame(oldA, LegacyNativeArenaRankings.Top1000Today); Assert.NotSame(oldT, LegacyNativeTeamArenaRankings.Top1000Today);
        Assert.All(LegacyNativeArenaRankings.Top1000Today, Assert.Null);
        Assert.All(LegacyNativeTeamArenaRankings.Top1000Today, Assert.Null);
        Assert.Same(arena, LegacyNativeArenaRankings.ArenaPoll[1]); Assert.Same(team, LegacyNativeTeamArenaRankings.ArenaPoll[1]);
    });
}
