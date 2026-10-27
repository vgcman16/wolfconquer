using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    public static IEnumerable<object[]> PrestigeTargetUpperSuffixes() => PrestigeTargetClassFixture.ExtendedSuffixes();

    public static IEnumerable<object[]> UnsupportedPrestigeRebuildClasses()
    {
        foreach (var id in new uint[] { 0, uint.MaxValue, 1058 }.Concat(PrestigeTargetClassFixture.ObservedUnmodeled))
        foreach (var foreignServer in new[] { false, true }) yield return [id, foreignServer];
    }

    [Theory]
    [MemberData(nameof(PrestigeTargetUpperSuffixes))]
    public void ClassResolutionRealRebuildPublishesKnownSuffix52And57IntoTheActualFamily(uint characterClass, uint category) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            var old = client.PrestrigeEntry!;
            var oldPoints = client.PrestigePoints;
            var oldValues = oldPoints.ToArray();
            SetNextPrestigeClientInputs(client);
            SavedField(client.Player, "_class", characterClass);

            client.CreatePrestigePoints();

            var current = client.PrestrigeEntry!;
            Assert.Equal(characterClass, current.Class);
            Assert.Equal((Ranking.Type)category, current.type);
            Assert.Equal(40_000u, current.TotalPoints);
            Assert.Same(current, Ranking.Ranks[(Ranking.Type)category].Items[41]);
            Assert.Same(current, Ranking.Ranks[Ranking.Type.World].Items[41]);
            Assert.All(Ranking.Ranks.Where(pair => pair.Key != Ranking.Type.World && pair.Key != (Ranking.Type)category),
                pair => Assert.False(pair.Value.Items.ContainsKey(41)));
            Assert.Equal(20_000u, old.TotalPoints);
            Assert.Equal(oldValues, oldPoints);
            Assert.NotSame(old, current);
            Assert.NotSame(client.PrestigePoints, current.Points);
            Assert.Equal(client.PrestigePoints, current.Points);
            Assert.Equal(40_000u, client.Player.MyGuildMember!.PrestigePoints);
        });

    [Theory]
    [MemberData(nameof(UnsupportedPrestigeRebuildClasses))]
    public void ClassResolutionUnsupportedCurrentClassRefusesBeforeAnyLocalOrForeignOutput(uint characterClass, bool foreignServer) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            SavedField(client.Player, "_class", characterClass);
            if (foreignServer) client.Player.ServerID = 3;

            RequirePrestigeClientPreparationRefusal(client);
        });

    public static IEnumerable<object[]> InconsistentPrestigePublicationClasses()
    {
        (uint Class, uint Category, bool Include)[] cases =
        [
            (0, 8, true), (uint.MaxValue, 8, true), (11002, 8, true),
            (1000, 2, true), (1000, 1, false), (1052, 8, true), (13057, 9, true),
            (10000, 8, false), (10000, 0, true), (10002, 8, true),
        ];
        foreach (var value in cases)
        foreach (var total in new uint[] { 0, 40_000 })
            yield return [value.Class, value.Category, value.Include, total];
    }

    [Theory]
    [MemberData(nameof(InconsistentPrestigePublicationClasses))]
    public void ClassResolutionPublisherRefusesUnsupportedOrInconsistentMetadataBeforeAnyPublication(
        uint characterClass, uint category, bool includeClass, uint total) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            var incoming = RankPublicationRow(99, total);
            incoming.Class = characterClass;
            incoming.type = (Ranking.Type)category;
            var incomingValues = ClientPreparationEntryValues(incoming);
            var incomingPoints = incoming.Points;
            var incomingPointValues = incoming.Points!.ToArray();
            var before = new PrestigeClientPreparationImage(client);

            var error = Record.Exception(() => Ranking.PublishPrestigeEntry(incoming, includeClass));

            before.RequireUnchanged();
            Assert.Equal(incomingValues, ClientPreparationEntryValues(incoming));
            Assert.Same(incomingPoints, incoming.Points);
            Assert.Equal(incomingPointValues, incoming.Points);
            Assert.IsType<InvalidOperationException>(error);
        });
}
