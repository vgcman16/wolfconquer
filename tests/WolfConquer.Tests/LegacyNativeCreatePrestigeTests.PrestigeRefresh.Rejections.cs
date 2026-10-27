using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    public static IEnumerable<object[]> PrestigeRefreshMalformedGraphs()
    {
        string[] faults =
        [
            "missing-world", "missing-target", "null-world", "null-old-class", "null-later-class",
            "wrong-world-type", "wrong-old-class-type", "aliased-rank-owner", "zero-old-class-limit",
            "negative-later-limit", "null-old-class-items", "null-later-items", "undefined-registry-key",
            "null-later-tail", "zero-key-later-tail", "mismatched-uid-later-tail",
            "incoming-uid-cross-class-duplicate", "other-uid-cross-class-duplicate",
        ];
        foreach (var fault in faults)
        foreach (var total in new uint[] { 0, 40_000 })
            yield return [fault, total];
    }

    [Theory]
    [MemberData(nameof(PrestigeRefreshMalformedGraphs))]
    public void PrestigeRefreshMalformedActualGraphRefusesBeforeAnyClientOrRankOutputChanges(string fault, uint total) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeRefreshClient(fixture);
            PublishPrestigeRefreshRow(42, 40_000);
            PublishPrestigeRefreshRow(77, 30_000, Ranking.Type.Thunderstriker, 9000);
            // Materialize the genuine cache before damaging actual owners; a
            // later scan must inspect the physical registry, not a valid prefix.
            _ = Ranking.Ranks.GetValues();
            SavedField(client.Player, "_class", 2000u);
            SetPrestigeRefreshScore(client, total);
            DamagePrestigeRefreshGraph(fault);

            RequirePrestigeRefreshRefusal(client);
        });

    [Theory]
    [InlineData(0u)]
    [InlineData(19_999u)]
    public void PrestigeRefreshAbsentLowUidCannotBypassAnInvalidLaterTail(uint total) =>
        RunPrestigeClientPreparation(fixture =>
        {
            PublishPrestigeRefreshRow(41, 50_000);
            var later = Ranking.Ranks[Ranking.Type.Thunderstriker];
            later.MaxItems = 1;
            later.Items.Add(77, RankPublicationRow(77, 30_000));
            later.Items.Add(99, null!);
            var prior = new PrestigeRefreshGraphImage();
            var incoming = RankPublicationRow(88, total);

            var error = Record.Exception(() => Ranking.PublishPrestigeEntry(incoming, includeClass: true));

            prior.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData(0u, 1u, 0u)]
    [InlineData(0u, 1u, 20_000u)]
    [InlineData(99u, 0u, 0u)]
    [InlineData(99u, 0u, 20_000u)]
    [InlineData(99u, 12u, 0u)]
    [InlineData(99u, 12u, 20_000u)]
    public void PrestigeRefreshInvalidIncomingIdentityOrCategoryRefusesEvenBelowThreshold(uint uid, uint category, uint total) =>
        RunPrestigeClientPreparation(fixture =>
        {
            PublishPrestigeRefreshRow(41, 50_000);
            var prior = new PrestigeRefreshGraphImage();
            var incoming = RankPublicationRow(uid, total);
            incoming.type = (Ranking.Type)category;

            var error = Record.Exception(() => Ranking.PublishPrestigeEntry(incoming, includeClass: true));

            prior.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    private static void DamagePrestigeRefreshGraph(string fault)
    {
        var oldClass = Ranking.Ranks[Ranking.Type.Trojan];
        var later = Ranking.Ranks[Ranking.Type.Thunderstriker];
        switch (fault)
        {
            case "missing-world": Ranking.Ranks.Remove(Ranking.Type.World); break;
            case "missing-target": Ranking.Ranks.Remove(Ranking.Type.Warrior); break;
            case "null-world": Ranking.Ranks[Ranking.Type.World] = null!; break;
            case "null-old-class": Ranking.Ranks[Ranking.Type.Trojan] = null!; break;
            case "null-later-class": Ranking.Ranks[Ranking.Type.Thunderstriker] = null!; break;
            case "wrong-world-type": Ranking.Ranks[Ranking.Type.World]._Type = Ranking.Type.Warrior; break;
            case "wrong-old-class-type": oldClass._Type = Ranking.Type.Warrior; break;
            case "aliased-rank-owner": Ranking.Ranks[Ranking.Type.Thunderstriker] = oldClass; break;
            case "zero-old-class-limit": oldClass.MaxItems = 0; break;
            case "negative-later-limit": later.MaxItems = -1; break;
            case "null-old-class-items": oldClass.Items = null!; break;
            case "null-later-items": later.Items = null!; break;
            case "undefined-registry-key":
                Ranking.Ranks[(Ranking.Type)12] = new((Ranking.Type)12) { MaxItems = 30 };
                break;
            case "null-later-tail": later.MaxItems = 1; later.Items.Add(99, null!); break;
            case "zero-key-later-tail":
                later.MaxItems = 1;
                later.Items.Add(0, RankPublicationRow(0, 1));
                break;
            case "mismatched-uid-later-tail":
                later.MaxItems = 1;
                later.Items.Add(99, RankPublicationRow(88, 1));
                break;
            case "incoming-uid-cross-class-duplicate":
                Ranking.Ranks[Ranking.Type.Archer].Items.Add(41, RankPublicationRow(41, 1));
                break;
            case "other-uid-cross-class-duplicate":
                Ranking.Ranks[Ranking.Type.Archer].Items.Add(77, RankPublicationRow(77, 1));
                break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
    }
}
