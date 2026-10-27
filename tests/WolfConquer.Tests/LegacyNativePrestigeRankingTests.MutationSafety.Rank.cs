using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    public static IEnumerable<object[]> PrestigeDetachedMalformedCases()
    {
        foreach (var fault in new[] { "type", "zero-limit", "negative-limit", "null-items", "null-tail", "zero-key", "uid-tail" })
        foreach (var operation in new[] { "add", "low-add", "absent-low-add", "remove", "absent-remove" })
            yield return [fault, operation];
    }

    [Theory]
    [MemberData(nameof(PrestigeDetachedMalformedCases))]
    public void MutationSafetyDetachedRankValidatesItsCompletePhysicalOwnerBeforeAnyChange(string fault, string operation) =>
        RunPrestigeMutationSafety(() =>
        {
            var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 1 };
            var prior = Entry(41, 50_000);
            rank.AddItem(prior);
            switch (fault)
            {
                case "type": rank._Type = (Ranking.Type)12; break;
                case "zero-limit": rank.MaxItems = 0; break;
                case "negative-limit": rank.MaxItems = -1; break;
                case "null-items": rank.Items = null!; break;
                case "null-tail": rank.Items.Add(77, null!); break;
                case "zero-key": rank.Items.Add(0, Entry(0, 1)); break;
                case "uid-tail": rank.Items.Add(77, Entry(88, 1)); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            var saved = new PrestigeMutationRankImage(rank);
            var graph = new PrestigeMutationRegistryImage();

            var error = Record.Exception(() =>
            {
                switch (operation)
                {
                    case "add": rank.AddItem(Entry(41, 60_000)); break;
                    case "low-add": rank.AddItem(Entry(41, 0)); break;
                    case "absent-low-add": rank.AddItem(Entry(99, 0)); break;
                    case "remove": rank.Remove(41); break;
                    case "absent-remove": rank.Remove(99); break;
                    default: throw new ArgumentOutOfRangeException(nameof(operation));
                }
            });

            saved.RequireUnchanged();
            graph.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MutationSafetyDetachedRankDeclineOrRemovalSelectsTheNextChampionWithoutAnyGlobalOwners(bool decline) =>
        RunPrestigeMutationSafety(() =>
        {
            Assert.Empty(Ranking.Ranks); // A detached Rank must not require a global World owner.
            var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 2 };
            var best = Entry(41, 50_000);
            var next = Entry(42, 40_000);
            rank.AddItem(best);
            rank.AddItem(next);
            var saved = new PrestigeMutationRankImage(rank);
            var graph = new PrestigeMutationRegistryImage();

            if (decline) rank.AddItem(Entry(41, 19_999));
            else rank.Remove(41);

            Assert.Same(next, Assert.Single(rank.Items).Value);
            Assert.Same(next, rank.BestOfClass);
            saved.RequireEscapedContentsUnchanged();
            graph.RequireUnchanged();
        });

    [Theory]
    [InlineData(0u)]
    [InlineData(20_000u)]
    public void MutationSafetyDetachedRankRejectsAnIncomingZeroUidBeforeReplacingAnyOutput(uint points) =>
        RunPrestigeMutationSafety(() =>
        {
            var rank = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 3 };
            rank.AddItem(Entry(41, 50_000));
            var saved = new PrestigeMutationRankImage(rank);

            var error = Record.Exception(() => rank.AddItem(Entry(0, points)));

            saved.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });
}
