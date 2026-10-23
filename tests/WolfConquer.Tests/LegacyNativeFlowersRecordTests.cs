using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeFlowersRecordTests
{
    [Fact]
    public void CompleteConstructorAndRecordPreserveFourDistinctOwnersAndNativeEnumerationOrder()
    {
        using var scope = new FlowersScope();
        var owner = new LegacyNativeGameClientRuntime();
        Assert.Null(owner.Player.Flowers);
        var flowers = new LegacyNativeFlowersRuntime(17, "Wolf");
        Assert.Equal(1u, flowers.FreeFlowers); Assert.Equal(0, flowers.Day);
        Assert.Equal(4, flowers.Distinct().Count());
        Assert.Equal(new[] { flowers.RedRoses, flowers.Lilies, flowers.Orchids, flowers.Tulips }, flowers.ToArray());
        Assert.Empty(LegacyNativeFlowersRuntime.ClientPoll); // Constructor does not publish.
        var nongeneric = ((System.Collections.IEnumerable)flowers).Cast<LegacyNativeFlowersRuntime.Flower>().ToArray();
        Assert.Equal(flowers.ToArray(), nongeneric);
        uint amount = 10;
        foreach (var flower in flowers)
        {
            Assert.Equal(17u, flower.UID); Assert.Equal("Wolf", flower.Name);
            flower.Amount = amount++; flower.Amount2day = amount++;
        }
        Assert.Equal("1/10/11/12/13/14/15/16/17/", flowers.ToString());
        Assert.Equal(56u, flowers.AllFlowersToday());
        foreach (var flower in flowers) flower.Amount2day = uint.MaxValue;
        Assert.Equal(4294967292u, flowers.AllFlowersToday());
    }

    [Theory]
    [InlineData(0u, (byte)1)] [InlineData(1u, (byte)4)]
    [InlineData(2u, (byte)3)] [InlineData(3u, (byte)2)]
    [InlineData(4u, (byte)0)] [InlineData(uint.MaxValue, (byte)0)]
    public void FlowerGradeAndOperatorsRetainActualIdentityAndBothUnsignedWraps(uint type, byte grade)
    {
        var flower = new LegacyNativeFlowersRuntime.Flower((LegacyNativeMsgFlower.FlowersType)type, 17, "Wolf")
        { Amount = uint.MaxValue, Amount2day = uint.MaxValue };
        Assert.Equal(grade, flower.GetGrade());
        Assert.Same(flower, flower + 2);
        Assert.Equal(1u, flower.Amount); Assert.Equal(1u, flower.Amount2day);
        uint converted = flower;
        Assert.Equal(1u, converted);
    }

    [Theory]
    [InlineData(0, (ushort)0)] [InlineData(1, (ushort)0)]
    [InlineData(2, (ushort)10000)] [InlineData(3, (ushort)20000)]
    [InlineData(4, (ushort)30000)] [InlineData(100, (ushort)30000)]
    [InlineData(-1, (ushort)0)]
    public void IconTierRetainsEveryNativeRankPartition(int rank, ushort expected)
    {
        var flowers = new LegacyNativeFlowersRuntime(17, "Wolf");
        Assert.Equal(expected, flowers.GetRank(rank));
        Assert.Equal((uint)(30000001 + expected), flowers.CreateFlowerIcon(null!, true, (byte)Math.Max(0, rank)));
    }

    [Theory]
    [InlineData(0, 0u)] [InlineData(1, 30000202u)] [InlineData(2, 30010202u)]
    [InlineData(3, 30020202u)] [InlineData(4, 30030202u)] [InlineData(100, 30030202u)]
    [InlineData(101, 0u)] [InlineData(-1, 30000202u)]
    public void FemaleIconRetainsNativeNegativeRankAndTypeWidthQuirks(int rank, uint expected)
    {
        var flowers = new LegacyNativeFlowersRuntime(17, "Wolf");
        var flower = flowers.Orchids; flower.Rank = rank;
        Assert.Equal(expected, flowers.CreateFlowerIcon(flower));
        Assert.Equal((uint)(30000602 + flowers.GetRank(rank)), flowers.CreateBoyIcon(flower));
    }

    [Fact]
    public void TodayRankingDoesNotReplaceExistingAmountsAndCapsAtThirtyActualEntries()
    {
        var rank = new LegacyNativeFlowersRuntime.FlowersRankingToday();
        rank.UpdateRank(1, 10); rank.UpdateRank(1, 100);
        Assert.Equal(10u, rank.Flowers[1]); // Source Calculate never updates an existing scalar.
        for (uint uid = 2; uid <= 31; uid++) rank.UpdateRank(uid, uid);
        Assert.Equal(30, rank.Flowers.Count);
        Assert.Equal(31u, rank.Flowers.First().Value);
        Assert.False(rank.Flowers.ContainsKey(2));
        var before = rank.Flowers.ToArray();
        rank.UpdateRank(99, 0);
        Assert.Equal(before, rank.Flowers.ToArray());
    }

    [Fact]
    public void WholeFlowerRankPreservesOldSameUidObjectAndNativeCandidateRankResetOnOverflow()
    {
        using var scope = new FlowersScope();
        var rank = new LegacyNativeFlowersRuntime.FlowerRanking(false);
        var first = new LegacyNativeFlowersRuntime.Flower(LegacyNativeMsgFlower.FlowersType.Rouse, 1, "one") { Amount = 1 };
        rank.UpdateRank(first, first.Type);
        var replacement = new LegacyNativeFlowersRuntime.Flower(first.Type, 1, "new") { Amount = 1000 };
        rank.UpdateRank(replacement, replacement.Type);
        Assert.Same(first, rank.RedRoses[1]); Assert.Equal(1u, first.Amount);
        Assert.Equal(1, replacement.Rank);
        for (uint uid = 2; uid <= 100; uid++)
            rank.UpdateRank(new(first.Type, uid, "row") { Amount = uid }, first.Type);
        var candidate = new LegacyNativeFlowersRuntime.Flower(first.Type, 999, "top") { Amount = 1000 };
        rank.UpdateRank(candidate, candidate.Type);
        Assert.Equal(100, rank.RedRoses.Count);
        Assert.Same(candidate, rank.RedRoses.First().Value);
        Assert.False(rank.RedRoses.ContainsKey(1));
        Assert.Equal(0, first.Rank);
        Assert.Equal(0, candidate.Rank); // Native dropped-row else resets the input candidate too.
        var rejected = new LegacyNativeFlowersRuntime.Flower(first.Type, 1000, "low") { Amount = 0, Rank = 77 };
        rank.UpdateRank(rejected, rejected.Type);
        Assert.False(rank.RedRoses.ContainsKey(1000)); Assert.Equal(77, rejected.Rank);
    }

    [Theory]
    [InlineData(0u)] [InlineData(1u)] [InlineData(2u)] [InlineData(3u)]
    public void FullGenderRankingDispatchesEveryNativeFlowerTypeAndDefaultGirlsUsesRealOfflinePool(uint type)
    {
        using var scope = new FlowersScope();
        var rank = new LegacyNativeFlowersRuntime.FlowerRanking();
        var row = new LegacyNativeFlowersRuntime.Flower((LegacyNativeMsgFlower.FlowersType)type, 17, "Wolf") { Amount = 1 };
        rank.UpdateRank(row, row.Type);
        var lists = new[] { rank.RedRoses, rank.Lilies, rank.Orchids, rank.Tulips };
        Assert.Same(row, Assert.Single(lists[type]).Value);
        Assert.Equal(1, row.Rank);
        Assert.Equal(1, lists.Sum(list => list.Count));
        rank.UpdateRank(null!, (LegacyNativeMsgFlower.FlowersType)99); // Unknown type does not dereference.
        Assert.True(rank.Girls);
    }

    private sealed class FlowersScope : IDisposable
    {
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? clients = LegacyNativePoolClients.GamePoll;
        private readonly ConcurrentDictionary<uint, LegacyNativeFlowersRuntime> flowers = LegacyNativeFlowersRuntime.ClientPoll;
        private readonly LegacyNativeFlowersRuntime.FlowersRankingToday today = LegacyNativePoolFlowers.FlowersRankToday;
        internal FlowersScope()
        {
            LegacyNativePoolClients.GamePoll = new();
            LegacyNativeFlowersRuntime.ClientPoll = new();
            LegacyNativePoolFlowers.FlowersRankToday = new();
        }
        public void Dispose()
        {
            LegacyNativePoolClients.GamePoll = clients;
            LegacyNativeFlowersRuntime.ClientPoll = flowers;
            LegacyNativePoolFlowers.FlowersRankToday = today;
        }
    }
}
