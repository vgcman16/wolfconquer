using System.Reflection;
using WolfConquer.Core.World;
namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public void EliteFighterDefaultConstructorRetainsNativeNullsAndFaults()
    {
        var stats = new LegacyNativeEliteGroupRuntime.FighterStats();
        Assert.Null(stats.Name); Assert.Null(stats.Wagers); Assert.False(stats.Advanced);
        Assert.Equal(LegacyNativeEliteGroupRuntime.FighterStats.StatusFlag.None, stats.Flag);
        Assert.Throws<NullReferenceException>(() => stats.Reset());
        Assert.Throws<NullReferenceException>(() => stats.ToString());
    }

    [Fact]
    public Task EliteFighterCloneUsesTrueConstructorAndCopiesOnlyNativeSubset()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var stats = new LegacyNativeEliteGroupRuntime.FighterStats(17, "Wolf", 1008, 2, 100017)
            {
                CrossEliteRank = 9, Wager = 30, Cheers = 7, Points = 1234, ClaimReward = 1,
                Flag = LegacyNativeEliteGroupRuntime.FighterStats.StatusFlag.Qualified
            };
            stats.Wagers!.Add(18, scope.Client);
            stats.Flag = LegacyNativeEliteGroupRuntime.FighterStats.StatusFlag.Lost;
            Assert.True(stats.Advanced); Assert.Equal("Wolf/17/1008/1/", stats.ToString());
            var clone = stats.Clone();
            Assert.NotSame(stats, clone); Assert.Equal(17u, clone.UID); Assert.Equal(100017u, clone.RealUID);
            Assert.Equal("Wolf", clone.Name); Assert.Equal(1008u, clone.Mesh); Assert.Equal(2u, clone.ServerID);
            Assert.Equal(30u, clone.Wager); Assert.Equal(1234u, clone.Points);
            Assert.Equal(stats.Flag, clone.Flag); Assert.False(clone.Advanced);
            Assert.Equal(0u, clone.CrossEliteRank); Assert.Equal(0u, clone.Cheers); Assert.Equal(0, clone.ClaimReward);
            Assert.NotSame(stats.Wagers, clone.Wagers); Assert.Empty(clone.Wagers!);
            stats.Reset(true);
            Assert.Empty(stats.Wagers!); Assert.Equal(0u, stats.Wager); Assert.Equal(0u, stats.Points); Assert.Equal(0u, stats.Cheers);
            Assert.Equal(LegacyNativeEliteGroupRuntime.FighterStats.StatusFlag.None, stats.Flag);
            Assert.True(stats.Advanced); Assert.Equal(1, stats.ClaimReward); Assert.Equal(9u, stats.CrossEliteRank);
        });
    }

    [Theory]
    [InlineData(0, 100000u)]
    [InlineData(1, 200000u)]
    [InlineData(2, 300000u)]
    [InlineData(3, 400000u)]
    public void EliteGroupUsesWholeRealConstructorAndNativeCounter(int kindValue, uint counter)
    {
        var kind = (LegacyNativeEliteTournamentGroupType)kindValue;
        var group = new LegacyNativeEliteGroupRuntime(kind);
        Assert.Equal(LegacyNativeProcesType.Dead, group.Proces); Assert.Equal(kind, group.GroupTyp);
        Assert.Empty(group.Players); Assert.Null(group.Map); Assert.Null(group.Matches);
        Assert.Null(group.Top4Matches); Assert.Null(group.ThreeQualiferMatch); Assert.Null(group.FinalMatch);
        Assert.Equal(8, group.Top8.Length);
        Assert.All(group.Top8, row => { Assert.Equal("None", row.Name); Assert.Equal(0u, row.UID); Assert.Empty(row.Wagers!); });
        Assert.NotSame(group.Top8[0], group.Top8[1]);
        var field = typeof(LegacyNativeEliteGroupRuntime).GetField("MatchCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(counter, ((LegacyNativeCounter)field.GetValue(group)!).Count);
        group.SubscribeTimer(); Assert.Equal(LegacyNativeProcesType.Idle, group.Proces);
    }

    [Fact]
    public Task EliteRewardClaimsRealNativeRowOnceAndReturnsExistingRankAgain()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var group = new LegacyNativeEliteGroupRuntime(LegacyNativeEliteTournamentGroupType.EPK_Lvl130Plus);
            group.Top8[5] = new LegacyNativeEliteGroupRuntime.FighterStats(scope.Client.Player.UID, "Owner", 1008, 2, 17);
            Assert.True(group.GetReward(scope.Client, out byte rank)); Assert.Equal(6, rank);
            Assert.Equal(1, group.Top8[5].ClaimReward);
            Assert.False(group.GetReward(scope.Client, out rank)); Assert.Equal(6, rank);
            group.Top8 = new LegacyNativeEliteGroupRuntime.FighterStats[7];
            Assert.False(group.GetReward(scope.Client, out rank)); Assert.Equal(0, rank);
        });
    }

    [Fact]
    public Task EliteMatchConstructorRequiresRealMapAndConsumesRealDynamicCounter()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var map = scope.Map(700);
            var group = new LegacyNativeEliteGroupRuntime(LegacyNativeEliteTournamentGroupType.EPK_Lvl130Plus);
            var match = new LegacyNativeEliteGroupRuntime.Match(group);
            Assert.Same(group, match.elitepkgroup); Assert.Equal(10000002u, match.DinamicID);
            Assert.Empty(match.Players); Assert.Empty(match.MatchStats); Assert.Empty(match.Watchers);
            Assert.Empty(match.Cheerers); Assert.Null(match.PlayerWaiting);
            Assert.Equal(LegacyNativeEliteGroupRuntime.Match.StatusFlag.AcceptingWagers, match.Flag);
            Assert.False(match.IsFinishd());
            var mapField = typeof(LegacyNativeEliteGroupRuntime.Match).GetField("Map", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.Same(map, mapField.GetValue(match));
            Assert.Equal(0, match.Count); Assert.Equal(-1, match.GroupID);
            match.ID = 400001; Assert.Equal(3, match.GroupID);
            var nullGroup = new LegacyNativeEliteGroupRuntime.Match(null);
            Assert.Equal(10000003u, nullGroup.DinamicID);
            Assert.Throws<NullReferenceException>(() => _ = nullGroup.GetMatchStats);
        });
    }

    [Fact]
    public Task EliteMatchMissingMapFaultDoesNotRunSyntheticFallbackOrAdvanceCounter()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.True(LegacyNativePoolMaps.ServerMaps!.Add(700, null!));
            Assert.Throws<NullReferenceException>(() => new LegacyNativeEliteGroupRuntime.Match(null));
            Assert.Equal(10000001u, LegacyNativeGameMap.DinamicIDS.Count);
        });
    }

    [Fact]
    public Task EliteClientPropertyRetainsSourceNameReadBeforeAssignment()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Map(700);
            var match = new LegacyNativeEliteGroupRuntime.Match(null);
            Assert.Null(scope.Client.ElitePkMatch); scope.Client.ElitePkMatch = match;
            Assert.Same(match, scope.Client.ElitePkMatch);
            var player = scope.Client.Player; scope.Client.Player = null!;
            try
            {
                Assert.Throws<NullReferenceException>(() => scope.Client.ElitePkMatch = null);
                Assert.Same(match, scope.Client.ElitePkMatch);
            }
            finally { scope.Client.Player = player; }
        });
    }
}
