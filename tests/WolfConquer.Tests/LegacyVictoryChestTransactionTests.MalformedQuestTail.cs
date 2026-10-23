using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyVictoryChestTransactionTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("duplicate")]
    public Task RegisteredVictoryValidClaimBeforeMalformedTailDoesNotAuthorizeAnyChange(string tail) => RunAsync(async fixture =>
    {
        fixture.Seed();
        await fixture.OpenAsync(21_972);
        await fixture.AssertMalformedQuestTailAsync(tail);
    });

    private sealed partial class Fixture
    {
        internal async Task AssertMalformedQuestTailAsync(string tail)
        {
            Assert.True(recovery.IsReadyFor(Users, Items, Quests));
            var healthy = Capture();
            Assert.True(healthy.Session.Character.PlayerQuests.TryGetQuest(QuestId, out var claim));
            var rows = healthy.Session.Character.PlayerQuests.Quests.Concat(new[]
            {
                tail == "duplicate" ? claim with { Time = 54321 } :
                    new PlayerQuestSnapshotEntry(805, PlayerQuestStatus.Available, 0, Array.Empty<uint>()),
            }).ToArray();
            var malformed = new PlayerQuestSnapshot(rows);
            // Build the real cache while the tail is valid, then corrupt its actual
            // nonaccepted backing row. The constructor itself cannot accept null.
            if (tail == "null") rows[^1] = null!;
            sessions.Set(Connection, healthy.Session with
            { Character = healthy.Session.Character with { PlayerQuests = malformed } });
            var expected = Current;
            var graph = expected.Character.PlayerQuests;
            var accepted = graph.AcceptedQuests;
            var acceptedReferences = accepted.ToArray();
            var entryReferences = rows.ToArray();
            var intentionReferences = rows.Select(row => row?.Intentions).ToArray();
            var rawSession = JsonSerializer.SerializeToUtf8Bytes(expected);

            // The selected Accepted/100 row precedes the malformed tail. Neither
            // TryGetQuest's early success nor a permissive serializer grants it.
            await ReplyAsync();
            Assert.Same(expected, Current);
            Assert.Same(graph, Current.Character.PlayerQuests);
            Assert.Same(rows, graph.Quests);
            Assert.Same(accepted, graph.AcceptedQuests);
            Assert.Equal(acceptedReferences.Length, accepted.Count);
            for (var index = 0; index < acceptedReferences.Length; index++)
                Assert.Same(acceptedReferences[index], accepted[index]);
            Assert.Equal(entryReferences.Length, graph.Quests.Count);
            for (var index = 0; index < entryReferences.Length; index++)
            {
                Assert.Same(entryReferences[index], graph.Quests[index]);
                Assert.Same(intentionReferences[index], graph.Quests[index]?.Intentions);
            }
            Assert.Equal(rawSession, JsonSerializer.SerializeToUtf8Bytes(Current));
            Assert.Same(healthy.Session.PlayerItems, Current.PlayerItems);
            Assert.Equal(healthy.Items, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(healthy.SessionBytes, JsonSerializer.SerializeToUtf8Bytes(healthy.Session));
            AssertVictoryFiles(healthy.Files);
            Assert.Empty(Sent);
            Assert.Contains("reason=victoryQuestSnapshotInvalid", Log.ToString());
            Assert.DoesNotContain("WorldServer handled LegacyNpc VictoryChest unlock", Log.ToString());
            Assert.DoesNotContain("WorldServer persisted LegacyPlayerQuests", Log.ToString());

            // Restore only the fixture-injected live graph, not files or owners.
            // The SAME ready owners then complete the genuine positive claim.
            sessions.Set(Connection, healthy.Session);
            Sent.Clear(); Log.GetStringBuilder().Clear();
            var repaired = Capture();
            await ReplyAsync();
            AssertCommitted(repaired);
            await AssertSameSessionAndReloadedReplayAsync();
        }
    }
}
