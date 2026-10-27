using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    private sealed class PrestigeRebornGraphImage
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank>? current = Ranking.Ranks;
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank>? inspected;
        private readonly object? gate = Ranking.SynRoot;
        private readonly object? cacheGate;
        private readonly Ranking.Rank[]? cache;
        private readonly Ranking.Rank[] cacheMembers;
        private readonly bool? update;
        private readonly KeyValuePair<Ranking.Type, Ranking.Rank>[] pairs;
        private readonly (Ranking.Rank Owner, Ranking.Type Category, int Limit, PrestigeMutationRankImage Image)[] owners;

        // An optional escaped registry lets a null-registry refusal still witness
        // every original owned bucket rather than checking only the null slot.
        internal PrestigeRebornGraphImage(LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank>? escaped = null)
        {
            inspected = escaped ?? current;
            cacheGate = inspected?.SyncRoot;
            cache = inspected?.MyArray;
            cacheMembers = cache?.ToArray() ?? [];
            update = inspected?.Update;
            pairs = inspected?.ToArray() ?? [];
            owners = pairs.Select(pair => pair.Value).Where(rank => rank is not null).Distinct()
                .Select(rank => (rank, rank._Type, rank.MaxItems, new PrestigeMutationRankImage(rank))).ToArray();
        }

        internal void RequireUnchanged()
        {
            RequireRegistryAndCacheUnchanged();
            foreach (var owner in owners) owner.Image.RequireUnchanged();
        }

        internal void RequireOnlySelectedChanged(Ranking.Rank selected)
        {
            RequireRegistryAndCacheUnchanged();
            foreach (var owner in owners)
            {
                Assert.Equal(owner.Category, owner.Owner._Type);
                Assert.Equal(owner.Limit, owner.Owner.MaxItems);
                if (ReferenceEquals(selected, owner.Owner)) owner.Image.RequireEscapedContentsUnchanged();
                else owner.Image.RequireUnchanged();
            }
        }

        private void RequireRegistryAndCacheUnchanged()
        {
            Assert.Same(current, Ranking.Ranks);
            Assert.Same(gate, Ranking.SynRoot);
            if (inspected is null) return;
            Assert.Same(cacheGate, inspected.SyncRoot);
            Assert.Same(cache, inspected.MyArray);
            Assert.Equal(update, inspected.Update);
            Assert.Equal(cacheMembers.Length, inspected.MyArray.Length);
            for (var index = 0; index < cacheMembers.Length; index++) Assert.Same(cacheMembers[index], inspected.MyArray[index]);
            Assert.Equal(pairs.Select(pair => pair.Key), inspected.Keys);
            foreach (var pair in pairs) Assert.Same(pair.Value, inspected[pair.Key]);
        }
    }
}
