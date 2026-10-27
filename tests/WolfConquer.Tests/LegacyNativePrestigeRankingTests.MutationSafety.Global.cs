using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    public static IEnumerable<object[]> PrestigeGlobalMalformedCases()
    {
        foreach (var fault in new[]
        {
            "null-owner", "wrong-type", "rank-alias", "zero-limit", "negative-limit", "null-items",
            "undefined-key", "null-tail", "zero-key", "uid-tail", "target-class-duplicate", "other-class-duplicate",
        })
        foreach (var uid in new uint[] { 41, 99 })
            yield return [fault, uid];
    }

    [Theory]
    [MemberData(nameof(PrestigeGlobalMalformedCases))]
    public void MutationSafetyGlobalRemoveRejectsAnInvalidLaterOwnerOrTailBeforeDeletingAnyPrefix(string fault, uint uid) =>
        RunPrestigeMutationSafety(() =>
        {
            Ranking.Create();
            var target = Entry(41, 50_000);
            var survivor = Entry(42, 40_000);
            Ranking.Ranks[Ranking.Type.World].AddItem(target);
            Ranking.Ranks[Ranking.Type.Ninja].AddItem(target);
            Ranking.Ranks[Ranking.Type.World].AddItem(survivor);
            Ranking.Ranks[Ranking.Type.Ninja].AddItem(survivor);
            var later = Ranking.Ranks[Ranking.Type.Thunderstriker];
            var laterRow = Entry(77, 30_000);
            laterRow.type = Ranking.Type.Thunderstriker;
            later.AddItem(laterRow);
            _ = Ranking.Ranks.GetValues();
            switch (fault)
            {
                case "null-owner": Ranking.Ranks[Ranking.Type.Thunderstriker] = null!; break;
                case "wrong-type": later._Type = Ranking.Type.Warrior; break;
                case "rank-alias": Ranking.Ranks[Ranking.Type.Thunderstriker] = Ranking.Ranks[Ranking.Type.Ninja]; break;
                case "zero-limit": later.MaxItems = 0; break;
                case "negative-limit": later.MaxItems = -1; break;
                case "null-items": later.Items = null!; break;
                case "undefined-key": Ranking.Ranks[(Ranking.Type)12] = new((Ranking.Type)12) { MaxItems = 30 }; break;
                case "null-tail": later.MaxItems = 1; later.Items.Add(88, null!); break;
                case "zero-key": later.MaxItems = 1; later.Items.Add(0, Entry(0, 1)); break;
                case "uid-tail": later.MaxItems = 1; later.Items.Add(88, Entry(89, 1)); break;
                case "target-class-duplicate": later.Items.Add(41, Entry(41, 1)); break;
                case "other-class-duplicate": Ranking.Ranks[Ranking.Type.Archer].Items.Add(77, Entry(77, 1)); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            var prior = new PrestigeMutationRegistryImage();

            var error = Record.Exception(() => Ranking.Remove(uid));

            prior.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData("empty")]
    [InlineData("class-only")]
    [InlineData("world-only")]
    [InlineData("separate-mirrors")]
    [InlineData("shared-items")]
    public void MutationSafetyGlobalRemoveAcceptsValidSparseOwnersAndWorldClassMirrorsWithoutCreatingBuckets(string shape) =>
        RunPrestigeMutationSafety(() =>
        {
            var best = Entry(41, 50_000);
            var second = Entry(42, 40_000);
            Ranking.Rank? world = null;
            Ranking.Rank? classRank = null;
            if (shape is "world-only" or "separate-mirrors" or "shared-items")
                world = AddPrestigeMutationOwner(Ranking.Type.World, best, second);
            if (shape is "class-only" or "separate-mirrors" or "shared-items")
                classRank = AddPrestigeMutationOwner(Ranking.Type.Ninja, best, second);
            if (shape == "separate-mirrors")
            {
                var distinct = Entry(41, 50_000);
                distinct.Mesh = 999;
                classRank!.Items[41] = distinct;
                classRank.BestOfClass = distinct;
            }
            if (shape == "shared-items") classRank!.Items = world!.Items;
            _ = Ranking.Ranks.GetValues();
            var prior = new PrestigeMutationRegistryImage();

            Ranking.Remove(41);

            foreach (var rank in Ranking.Ranks.Values)
            {
                Assert.Same(second, Assert.Single(rank.Items).Value);
                Assert.Same(second, rank.BestOfClass);
            }
            Assert.Null(Ranking.GetInfo(41));
            prior.RequireRegistryAndCacheUnchanged();
            prior.RequireEscapedContentsUnchanged();
        });

    [Fact]
    public void MutationSafetyGlobalAbsentUidLeavesActualMapsAndUnrelatedHistoricalChampionsExactlyUnchanged() =>
        RunPrestigeMutationSafety(() =>
        {
            Ranking.Create();
            var best = Entry(41, 50_000);
            Ranking.Ranks[Ranking.Type.Ninja].AddItem(best);
            Ranking.Ranks[Ranking.Type.World].AddItem(best);
            Assert.True(Ranking.Ranks[Ranking.Type.Ninja].Items.Remove(41));
            Assert.True(Ranking.Ranks[Ranking.Type.World].Items.Remove(41));
            _ = Ranking.Ranks.GetValues();
            var prior = new PrestigeMutationRegistryImage();

            Ranking.Remove(99);

            prior.RequireUnchanged();
        });

    private static Ranking.Rank AddPrestigeMutationOwner(Ranking.Type category, params Ranking.Entry[] entries)
    {
        var owner = new Ranking.Rank(category) { MaxItems = category == Ranking.Type.World ? 100 : 30 };
        foreach (var entry in entries) owner.AddItem(entry);
        Ranking.Ranks.Add(category, owner);
        return owner;
    }
}
