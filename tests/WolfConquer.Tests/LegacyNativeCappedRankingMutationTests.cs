using Ninja = WolfConquer.Core.Data.LegacyNativeNinjaRank;
using Weapons = WolfConquer.Core.Data.LegacyNativeHWRank;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCappedRankingMutationTests
{
    [Fact]
    public void TypedConstructorsRetainDistinctPhysicalFieldsAndDefaults()
    {
        var ninja = new Ninja.Rank(Ninja.Type.Fire);
        var weapons = new Weapons.Rank(Weapons.Type.Judge);
        Assert.Equal(Ninja.Type.Fire, ninja._Type); Assert.Equal(Weapons.Type.Judge, weapons._Type);
        Assert.Equal(0, ninja.MaxItems); Assert.Equal(0, weapons.MaxItems);
        Assert.NotSame(ninja.SynRoot, weapons.SynRoot);
        Assert.IsType<Dictionary<uint, Ninja.Entry>>(ninja.RankingList);
        Assert.IsType<Dictionary<uint, Weapons.Entry>>(weapons.RankingList);
        Assert.Empty(ninja.RankingList); Assert.Empty(weapons.RankingList);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NinjaUpdateIsSingleProbeUpsertUnderItsActualGate(bool replacing)
    {
        var rank = new Ninja.Rank(Ninja.Type.Overall) { MaxItems = 3 };
        var probe = new MutationProbe(rank.SynRoot);
        var survivor = new Ninja.Entry { UID = 9, TotalPoints = 100 };
        var old = new Ninja.Entry { UID = 5, TotalPoints = 10 };
        var added = new Ninja.Entry { UID = 5, TotalPoints = 200 };
        rank.RankingList = Book(probe, survivor, old, replacing);
        VerifyUpsert(rank.RankingList, () => rank.RankingList, () => rank.UpdateItem(added), probe, survivor, old, added);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WeaponsUpdateIsSingleProbeUpsertUnderItsActualGate(bool replacing)
    {
        var rank = new Weapons.Rank(Weapons.Type.Main) { MaxItems = 3 };
        var probe = new MutationProbe(rank.SynRoot);
        var survivor = new Weapons.Entry { UID = 9, TotalPoints = 100 };
        var old = new Weapons.Entry { UID = 5, TotalPoints = 10 };
        var added = new Weapons.Entry { UID = 5, TotalPoints = 200 };
        rank.RankingList = Book(probe, survivor, old, replacing);
        VerifyUpsert(rank.RankingList, () => rank.RankingList, () => rank.UpdateItem(added), probe, survivor, old, added);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void NinjaTrimRetainsLiteralUnsignedOrderStableTiesCapAndRowReferences(int cap)
    {
        var rank = new Ninja.Rank(Ninja.Type.Overall) { MaxItems = cap };
        var rows = new[]
        {
            new Ninja.Entry { UID = 9, TotalPoints = uint.MaxValue },
            new Ninja.Entry { UID = 7, TotalPoints = 50 },
            new Ninja.Entry { UID = 8, TotalPoints = 50 },
            new Ninja.Entry { UID = 6, TotalPoints = 50 },
        };
        foreach (var row in rows.Take(3)) rank.RankingList.Add(row.UID, row);
        rank.UpdateItem(rows[3]);
        AssertTrim(rank.RankingList, rows, cap);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void WeaponsTrimRetainsLiteralUnsignedOrderStableTiesCapAndRowReferences(int cap)
    {
        var rank = new Weapons.Rank(Weapons.Type.Main) { MaxItems = cap };
        var rows = new[]
        {
            new Weapons.Entry { UID = 9, TotalPoints = uint.MaxValue },
            new Weapons.Entry { UID = 7, TotalPoints = 50 },
            new Weapons.Entry { UID = 8, TotalPoints = 50 },
            new Weapons.Entry { UID = 6, TotalPoints = 50 },
        };
        foreach (var row in rows.Take(3)) rank.RankingList.Add(row.UID, row);
        rank.UpdateItem(rows[3]);
        AssertTrim(rank.RankingList, rows, cap);
    }

    [Fact]
    public void NinjaUpdateReadsTheRealCapAfterThePhysicalUpsertNotAtWrapperEntry()
    {
        var rank = new Ninja.Rank(Ninja.Type.Overall) { MaxItems = 3 };
        var probe = new MutationProbe(rank.SynRoot) { OnHash = _ => rank.MaxItems = 1 };
        rank.RankingList = new(probe) { [9] = new() { UID = 9, TotalPoints = 100 } };
        var added = new Ninja.Entry { UID = 5, TotalPoints = 200 };
        probe.Armed = true; rank.UpdateItem(added); probe.Armed = false;
        Assert.Equal(new uint[] { 5 }, probe.Keys); Assert.Equal(1, rank.MaxItems);
        Assert.Same(added, Assert.Single(rank.RankingList).Value);
    }

    [Fact]
    public void WeaponsUpdateReadsTheRealCapAfterThePhysicalUpsertNotAtWrapperEntry()
    {
        var rank = new Weapons.Rank(Weapons.Type.Main) { MaxItems = 3 };
        var probe = new MutationProbe(rank.SynRoot) { OnHash = _ => rank.MaxItems = 1 };
        rank.RankingList = new(probe) { [9] = new() { UID = 9, TotalPoints = 100 } };
        var added = new Weapons.Entry { UID = 5, TotalPoints = 200 };
        probe.Armed = true; rank.UpdateItem(added); probe.Armed = false;
        Assert.Equal(new uint[] { 5 }, probe.Keys); Assert.Equal(1, rank.MaxItems);
        Assert.Same(added, Assert.Single(rank.RankingList).Value);
    }
}
