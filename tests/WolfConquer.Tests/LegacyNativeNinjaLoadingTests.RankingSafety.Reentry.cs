using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(false, "state")]
    [InlineData(true, "state")]
    [InlineData(false, "ninja")]
    [InlineData(true, "ninja")]
    [InlineData(false, "metadata")]
    [InlineData(true, "metadata")]
    [InlineData(false, "item")]
    [InlineData(true, "item")]
    [InlineData(false, "name")]
    [InlineData(true, "name")]
    [InlineData(false, "level")]
    [InlineData(true, "level")]
    [InlineData(false, "mesh")]
    [InlineData(true, "mesh")]
    public Task LastRealAppearanceLookupCannotPublishOrOverwriteChangedOwnedInputs(bool addItem, string mutation) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedRankingRows(scope);
            var item = scope.Item(100, 100, 1);
            scope.Meta(7, 100, 1); scope.Meta(8, 101, 1);
            scope.Ninja.FireMastery = 1000;
            var items = scope.Ninja.Items;
            var vector = scope.Client.Player.BitVector;
            var comparer = new RankingMutationComparer(vector, () =>
            {
                switch (mutation)
                {
                    case "state": scope.Ninja.FireMastery = 2000; break; // Real gated persistence-state setter.
                    case "ninja": scope.Client.MyNinja = new LegacyNativeNinja(scope.Client); break;
                    case "metadata": scope.Metadata[7].Score = 100; break;
                    case "item": item.Exp = 1; break;
                    case "name": scope.Client.Player.Name = "Changed Ninja"; break;
                    case "level": scope.SetLevel(300); break;
                    case "mesh": scope.Client.Player.Body = 2001; break;
                }
            });
            vector.ArrayFlags = new ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>(comparer)
            {
                [(int)Flag.WeeklyPKChampion] = new((int)Flag.WeeklyPKChampion, 10, false, 0),
            };
            vector.Add((int)Flag.WeeklyPKChampion);
            var before = new RankingGraphWitness();
            comparer.Armed = true;

            Assert.Throws<InvalidOperationException>(() =>
            {
                if (addItem) scope.Ninja.AddItem(101, 1, 0, 0, 0);
                else scope.Ninja.UpdateRank();
            });

            Assert.True(comparer.Triggered);
            Assert.Equal(6, comparer.Reads);
            Assert.Same(items, scope.Ninja.Items);
            Assert.Same(item, Assert.Single(items).Value);
            if (mutation == "state") Assert.Equal(2000u, scope.Ninja.FireMastery); // Do not roll back an external input change.
            before.RequireUnchanged();
            AssertNoRankingPackets(scope);
        });

    [Fact]
    public Task GenuineNonmutatingAppearanceComparerRemainsAValidPositiveLookup() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.FireMastery = 1000;
        var vector = scope.Client.Player.BitVector;
        var comparer = new RankingMutationComparer(vector, () => { });
        vector.ArrayFlags = new(comparer)
        {
            [(int)Flag.WeeklyPKChampion] = new((int)Flag.WeeklyPKChampion, 10, false, 0),
        };
        vector.Add((int)Flag.WeeklyPKChampion);
        comparer.Armed = true;
        scope.Ninja.UpdateRank();
        Assert.True(comparer.Triggered);
        Assert.Equal(37u, scope.Rank(WolfConquer.Core.Data.LegacyNativeNinjaRank.Type.Overall).RankingList[17].Flag);
        AssertNoRankingPackets(scope);
    });

    private sealed class RankingMutationComparer(LegacyNativeStatusFlagsBigVector32 vector, Action mutate) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal bool Triggered;
        internal int Reads;
        public bool Equals(int first, int second) => first == second;
        public int GetHashCode(int value)
        {
            // Two real Weekly lookups per appearance: final call6 is the last
            // membership candidate in the third, outer precommit appearance read.
            if (Armed && value == (int)Flag.WeeklyPKChampion && ++Reads == 6)
            {
                Assert.True(Monitor.IsEntered(vector.SyncRoot));
                Armed = false;
                Triggered = true;
                mutate();
            }
            return value.GetHashCode();
        }
    }
}
