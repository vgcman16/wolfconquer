using WolfConquer.Core.Data;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task SuccessfulRankRefreshKeepsPhysicalOwnersAndEscapedOldMapsAndRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedRankingRows(scope);
        var owners = RankingCategories.Select(scope.Rank).ToArray();
        var oldMaps = owners.Select(owner => owner.RankingList).ToArray();
        var oldRows = oldMaps.Select(map => map[17]).ToArray();
        var otherRows = oldMaps.Select(map => map[18]).ToArray();
        scope.Client.Player.Name = "Current";
        scope.Ninja.FireMastery = scope.Ninja.WaterMastery = scope.Ninja.EarthMastery = 1001;
        scope.Ninja.WindMastery = scope.Ninja.LightningMastery = 1001;

        scope.Ninja.UpdateRank();

        var entries = new List<LegacyNativeNinjaRank.Entry>();
        for (var index = 0; index < owners.Length; index++)
        {
            var owner = scope.Rank((RankType)index);
            Assert.Same(owners[index], owner);
            Assert.NotSame(oldMaps[index], owner.RankingList);
            Assert.Same(oldRows[index], oldMaps[index][17]);
            Assert.Equal(2000u, oldRows[index].TotalPoints);
            Assert.Equal("Former", oldRows[index].Name);
            Assert.Same(otherRows[index], owner.RankingList[18]);
            Assert.Equal((RankType)index, owner.RankingList[17].Type);
            Assert.Equal(index == 0 ? 5005u : 1001u, owner.RankingList[17].TotalPoints);
            entries.Add(owner.RankingList[17]);
        }
        Assert.Equal(6, entries.Distinct(ReferenceEqualityComparer.Instance).Count());
        AssertNoRankingPackets(scope);
    });

    [Fact]
    public Task ElementDeclineRemovesStaleMembershipWhileOverallRemainsEligible() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.FireMastery = 2000;
        scope.Ninja.WindMastery = 1001;
        scope.Ninja.UpdateRank();
        scope.Ninja.FireMastery = 999;
        scope.Ninja.WaterMastery = 1000;
        scope.Ninja.WindMastery = 1000;
        scope.Ninja.UpdateRank();

        Assert.Equal(2999u, scope.Rank(RankType.Overall).RankingList[17].TotalPoints);
        Assert.Empty(scope.Rank(RankType.Fire).RankingList);
        Assert.Empty(scope.Rank(RankType.Wind).RankingList);
        Assert.Equal(1000u, scope.Rank(RankType.Water).RankingList[17].TotalPoints);
        AssertNoRankingPackets(scope);
    });

    [Fact]
    public Task CustomPositiveCapRetainsReplacementTiePositionAndDoesNotBackfillDiscardedRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        foreach (var category in new[] { RankType.Overall, RankType.Fire })
        {
            var rank = scope.Rank(category);
            rank.MaxItems = 2;
            rank.RankingList[18] = new() { UID = 18, Type = category, TotalPoints = 2000 };
            rank.RankingList[17] = new() { UID = 17, Type = category, TotalPoints = 1000 };
            rank.RankingList[19] = new() { UID = 19, Type = category, TotalPoints = 1000 };
        }
        scope.Ninja.FireMastery = 1000;
        scope.Ninja.UpdateRank();
        Assert.Equal(new uint[] { 18, 17 }, scope.Rank(RankType.Fire).RankingList.Keys);
        scope.Ninja.FireMastery = 999;
        scope.Ninja.UpdateRank();
        Assert.Equal(18u, Assert.Single(scope.Rank(RankType.Fire).RankingList).Key);
        AssertNoRankingPackets(scope);
    });

    [Fact]
    public Task RepresentableScoreBoundaryUsesGenuineMetadataAndCheckedSourceOperationOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Item(100, 100, 1);
        scope.Meta(7, 100, 1, score: 3);
        scope.Ninja.Fire = uint.MaxValue / 4;
        scope.Ninja.UpdateRank();
        Assert.Equal(uint.MaxValue, scope.Rank(RankType.Overall).RankingList[17].TotalPoints);
        Assert.Equal(uint.MaxValue, scope.Rank(RankType.Fire).RankingList[17].TotalPoints);
        AssertNoRankingPackets(scope);
    });

    [Fact]
    public Task DistinctTypedOwnersCanSafelyShareAnEscapedEmptySourceMap() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var shared = new Dictionary<uint, LegacyNativeNinjaRank.Entry>();
        foreach (var category in RankingCategories) scope.Rank(category).RankingList = shared;
        scope.Ninja.FireMastery = 1000;
        scope.Ninja.UpdateRank();
        var overall = scope.Rank(RankType.Overall).RankingList;
        var fire = scope.Rank(RankType.Fire).RankingList;
        Assert.Empty(shared);
        Assert.NotSame(shared, overall); Assert.NotSame(shared, fire); Assert.NotSame(overall, fire);
        Assert.Equal(RankType.Overall, overall[17].Type); Assert.Equal(RankType.Fire, fire[17].Type);
        AssertNoRankingPackets(scope);
    });

    [Fact]
    public Task RefreshConsumesActualRegistryOwnerWithoutConsultingItsStaleValueCache() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var cache = LegacyNativeNinjaRank.Ranks.GetValues();
        var oldFire = scope.Rank(RankType.Fire);
        var current = new LegacyNativeNinjaRank.Rank(RankType.Fire) { MaxItems = 2 };
        long priorRevision = LegacyNativeNinjaRank.Ranks.Revision;
        LegacyNativeNinjaRank.Ranks[RankType.Fire] = current;
        long currentRevision = LegacyNativeNinjaRank.Ranks.Revision;
        Assert.Equal(priorRevision + 1, currentRevision);
        Assert.Contains(oldFire, cache); Assert.True(LegacyNativeNinjaRank.Ranks.Update);
        scope.Ninja.FireMastery = 1000;
        scope.Ninja.UpdateRank();
        Assert.Equal(cache, LegacyNativeNinjaRank.Ranks.MyArray);
        Assert.Equal(currentRevision, LegacyNativeNinjaRank.Ranks.Revision);
        Assert.True(LegacyNativeNinjaRank.Ranks.Update);
        Assert.Same(current, LegacyNativeNinjaRank.Ranks[RankType.Fire]);
        var actual = LegacyNativeNinjaRank.Ranks.GetValues();
        Assert.Contains(current, actual); Assert.DoesNotContain(oldFire, actual);
        Assert.False(LegacyNativeNinjaRank.Ranks.Update);
        Assert.Empty(oldFire.RankingList);
        Assert.Equal(1000u, current.RankingList[17].TotalPoints);
        AssertNoRankingPackets(scope);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AbsentLowUidIsANoopOnlyAfterCompleteGraphAdmissionWithoutUnusedAppearance(bool corruptTail) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Equipment = null;
        scope.Client.Player.BitVector = null!;
        scope.Client.Player.Name = null!;
        scope.SetLevel(300);
        if (corruptTail) scope.Rank(RankType.Lightning).RankingList[99] = null!;
        var before = new RankingGraphWitness();
        if (corruptTail) Assert.Throws<InvalidOperationException>(() => scope.Ninja.UpdateRank());
        else scope.Ninja.UpdateRank();
        before.RequireUnchanged();
        AssertNoRankingPackets(scope);
    });
}
