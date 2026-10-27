using Gear = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Ninja = WolfConquer.Core.Data.LegacyNativeNinjaRank;
using Weapons = WolfConquer.Core.Data.LegacyNativeHWRank;
using Prestige = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeRankingRemovalApiTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CombatGearRemovalUsesOneLookupAndRetainsValidMissingOrSortedCappedSemantics(int mode)
    {
        var probes = new RemovalHashProbes();
        var rank = new Gear.Rank(Gear.Type.Overall_Warrior) { MaxItems = 2 };
        rank.RankingList = Book(probes, mode,
            new Gear.Entry { UID = 3, TotalPoints = 10 }, new Gear.Entry { UID = 42, TotalPoints = 20 },
            new Gear.Entry { UID = 1, TotalPoints = uint.MaxValue }, new Gear.Entry { UID = 2, TotalPoints = 10 });
        Verify(rank.RankingList, () => rank.RankingList, rank.Remove, probes, mode);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void NinjaRemovalUsesOneLookupAndRetainsValidMissingOrSortedCappedSemantics(int mode)
    {
        var probes = new RemovalHashProbes();
        var rank = new Ninja.Rank(Ninja.Type.Overall) { MaxItems = 2 };
        rank.RankingList = Book(probes, mode,
            new Ninja.Entry { UID = 3, TotalPoints = 10 }, new Ninja.Entry { UID = 42, TotalPoints = 20 },
            new Ninja.Entry { UID = 1, TotalPoints = uint.MaxValue }, new Ninja.Entry { UID = 2, TotalPoints = 10 });
        Verify(rank.RankingList, () => rank.RankingList, rank.Remove, probes, mode);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void HundredWeaponsRemovalUsesOneLookupAndRetainsValidMissingOrSortedCappedSemantics(int mode)
    {
        var probes = new RemovalHashProbes();
        var rank = new Weapons.Rank(Weapons.Type.Main) { MaxItems = 2 };
        rank.RankingList = Book(probes, mode,
            new Weapons.Entry { UID = 3, TotalPoints = 10 }, new Weapons.Entry { UID = 42, TotalPoints = 20 },
            new Weapons.Entry { UID = 1, TotalPoints = uint.MaxValue }, new Weapons.Entry { UID = 2, TotalPoints = 10 });
        Verify(rank.RankingList, () => rank.RankingList, rank.Remove, probes, mode);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PrestigeRemovalRetainsValidMissingOrSortedCappedSemanticsWithoutMutatingEscapedItems(int mode)
    {
        var probes = new RemovalHashProbes();
        var rank = new Prestige.Rank(Prestige.Type.Ninja) { MaxItems = 2 };
        rank.Items = Book(probes, mode,
            new Prestige.Entry { UID = 3, TotalPoints = 10 }, new Prestige.Entry { UID = 42, TotalPoints = 20 },
            new Prestige.Entry { UID = 1, TotalPoints = uint.MaxValue }, new Prestige.Entry { UID = 2, TotalPoints = 10 });
        VerifyPrestige(rank, mode);
    }

    private static void VerifyPrestige(Prestige.Rank rank, int mode)
    {
        var original = rank.Items;
        var initial = original.ToArray();
        var oldChampion = initial.LastOrDefault().Value;
        rank.BestOfClass = oldChampion;
        uint uid = mode == 2 ? 42u : 99u;
        rank.Remove(uid);
        // Prestige now validates the complete owner and prepares a private map.
        // The other three families retain their separate one-lookup assertion.
        Assert.Equal(initial.Select(pair => pair.Key), original.Keys);
        foreach (var pair in initial) Assert.Same(pair.Value, original[pair.Key]);
        if (mode == 2)
        {
            Assert.NotSame(original, rank.Items);
            Assert.Equal(new uint[] { 1, 3 }, rank.Items.Keys);
            Assert.Same(initial.Single(pair => pair.Key == 1).Value, rank.BestOfClass);
        }
        else
        {
            Assert.Same(original, rank.Items);
            Assert.Same(oldChampion, rank.BestOfClass);
        }
        foreach (var pair in rank.Items)
            Assert.Same(initial.Single(saved => saved.Key == pair.Key).Value, pair.Value);
    }

    private static Dictionary<uint, T> Book<T>(RemovalHashProbes probes, int mode,
        T low, T removed, T high, T tied) where T : class
    {
        var book = new Dictionary<uint, T>(probes);
        if (mode == 0) return book;
        book.Add(3, low); book.Add(42, removed); book.Add(1, high); book.Add(2, tied);
        return book;
    }

    private static void Verify<T>(Dictionary<uint, T> original, Func<Dictionary<uint, T>> current,
        Action<uint> actualRemove, RemovalHashProbes probes, int mode) where T : class
    {
        // Delegates only bind the genuine owners' whole Remove methods and actual field reads.
        // They do not stand in for gameplay, ranking, persistence or publication behavior.
        var initial = original.ToArray();
        uint uid = mode == 2 ? 42u : 99u;
        probes.Keys.Clear();
        actualRemove(uid);
        Assert.Equal(mode == 0 ? Array.Empty<uint>() : new[] { uid }, probes.Keys);
        var after = current();
        if (mode == 2)
        {
            Assert.NotSame(original, after);
            Assert.Equal(new uint[] { 1, 3 }, after.Keys); // Stable equal10 order and native Take(2).
            Assert.False(original.ContainsKey(42));
        }
        else
        {
            Assert.Same(original, after);
            Assert.Equal(initial.Select(pair => pair.Key), after.Keys);
        }
        foreach (var pair in after)
            Assert.Same(initial.Single(saved => saved.Key == pair.Key).Value, pair.Value);
    }
}

internal sealed class RemovalHashProbes : IEqualityComparer<uint>
{
    internal List<uint> Keys { get; } = [];
    public bool Equals(uint first, uint second) => first == second;
    public int GetHashCode(uint value)
    {
        Keys.Add(value);
        return value.GetHashCode();
    }
}
