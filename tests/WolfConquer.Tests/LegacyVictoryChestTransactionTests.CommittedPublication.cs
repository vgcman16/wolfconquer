using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyVictoryChestTransactionTests
{
    [Theory]
    [InlineData("finishSend")]
    [InlineData("itemSend")]
    [InlineData("replaceCurrent")]
    [InlineData("ended")]
    public Task RegisteredVictoryPostcommitPublicationCannotReopenOrTransferTheClaim(string fault) => RunAsync(async fixture =>
    {
        fixture.Seed();
        await fixture.OpenAsync(21_972);
        await fixture.AssertPostcommitPublicationAsync(fault);
    });

    private sealed partial class Fixture
    {
        // Observation/failure/actual-owner mutation only. No save/grant/UID authority.
        private Action<byte[], bool>? VictorySendObserver { get; set; }

        private ValueTask SendObservedVictoryFrame(ReadOnlyMemory<byte> frame)
        {
            var bytes = frame.ToArray();
            VictorySendObserver?.Invoke(bytes, false);
            Sent.Add(bytes);
            VictorySendObserver?.Invoke(bytes, true);
            return ValueTask.CompletedTask;
        }

        internal async Task AssertPostcommitPublicationAsync(string fault)
        {
            Assert.True(recovery.IsReadyFor(Users, Items, Quests));
            var before = Capture();
            var attempts = new List<(byte[] Frame, Preimage State)>();
            WorldSession? replacement = null;
            var injected = 0;
            VictorySendObserver = (bytes, accepted) =>
            {
                if (!accepted)
                {
                    // Capture real files and current Store before acceptance. Assertions
                    // run below, outside production's postcommit exception catch.
                    attempts.Add((bytes.ToArray(), Capture()));
                    if ((fault == "finishSend" && attempts.Count == 1) ||
                        (fault == "itemSend" && attempts.Count == 2))
                    {
                        injected++;
                        throw new IOException("Owned Victory postcommit send failure.");
                    }
                    return;
                }
                if (Sent.Count != 1) return;
                if (fault == "replaceCurrent")
                {
                    replacement = Current with { };
                    sessions.Set(Connection, replacement);
                    injected++;
                }
                else if (fault == "ended")
                {
                    lock (trades.SyncRoot) trades.MarkConnectionEnded(Connection);
                    injected++;
                }
            };

            // Existing helper tracks and observes the ACTUAL registered work.
            // Do not clear the observer on an unproven timeout while work could run.
            await ReplyAsync();
            VictorySendObserver = null;
            Assert.Equal(1, injected);
            Assert.Equal(fault == "itemSend" ? 2 : 1, attempts.Count);
            var committed = attempts[0].State;
            var frames = AssertDurablePostcommitReward(before, committed);
            for (var index = 0; index < attempts.Count; index++)
            {
                Assert.Equal(frames[index], attempts[index].Frame);
                Assert.Same(committed.Session, attempts[index].State.Session);
                Assert.Equal(committed.SessionBytes, attempts[index].State.SessionBytes);
                AssertVictoryFiles(attempts[index].State.Files);
            }
            Assert.Equal(fault == "finishSend" ? 0 : 1, Sent.Count);
            if (Sent.Count != 0) Assert.Equal(frames[0], Sent[0]);
            if (fault == "replaceCurrent")
            {
                Assert.NotNull(replacement);
                Assert.NotSame(committed.Session, replacement);
                Assert.Equal(committed.Session.Character.Uid, replacement.Character.Uid);
                Assert.Equal(committed.Session, replacement);
                Assert.Same(replacement, Current);
            }
            else Assert.Same(committed.Session, Current);
            Assert.Equal(fault == "ended", trades.HasConnectionEnded(Connection));
            Assert.DoesNotContain("WorldServer handled LegacyNpc VictoryChest unlock", Log.ToString());
            Assert.DoesNotContain("rejected without commit", Log.ToString());
            Assert.Contains("WorldServer persisted LegacyPlayerQuests", Log.ToString());

            if (fault == "ended")
            {
                var ended = Capture();
                Sent.Clear(); Log.GetStringBuilder().Clear();
                await ReplyAsync();
                AssertRejected(ended);
                Assert.True(trades.HasConnectionEnded(Connection));
            }
            else await AssertSameSessionAndReloadedReplayAsync();
        }

        private byte[][] AssertDurablePostcommitReward(Preimage before, Preimage committed)
        {
            var session = committed.Session;
            Assert.Equal(before.Session.PlayerItems.InventoryItems.Count + 1, session.PlayerItems.InventoryItems.Count);
            var actualReward = session.PlayerItems.InventoryItems[^1];
            Assert.Contains(actualReward.ItemId, new uint[] { 3_008_727, 3_009_002, 4_200_004 });
            Assert.NotEqual(0U, actualReward.Uid);
            var oldUids = LegacyDragonSkinInventory.Stores(before.Session.PlayerItems).SelectMany(store => store)
                .SelectMany(row => new[] { row.Uid }.Concat(row.DepositedItems.Select(child => child.Uid)));
            Assert.DoesNotContain(actualReward.Uid, oldUids);
            var exactReward = new PlayerItemSnapshotItem
            { Uid = actualReward.Uid, ItemId = actualReward.ItemId, StackSize = 1 };
            var expectedItems = before.Session.PlayerItems with
            { InventoryItems = before.Session.PlayerItems.InventoryItems.Concat(new[] { exactReward }).ToArray() };
            Assert.True(before.Session.Character.PlayerQuests.TryGetQuest(QuestId, out var oldQuest));
            var expectedQuests = before.Session.Character.PlayerQuests.WithQuest(oldQuest with { Status = PlayerQuestStatus.Finished });
            var itemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems);
            var questBytes = PlayerQuestSnapshotLoader.Serialize(expectedQuests);
            Assert.Equal(before.Session with
            {
                Character = before.Session.Character with { PlayerQuests = session.Character.PlayerQuests },
                PlayerItems = session.PlayerItems,
            }, session);
            Assert.Equal(itemBytes, committed.Items);
            Assert.Equal(questBytes, committed.Quests);
            Assert.Equal(itemBytes, committed.Files[ItemPath]);
            Assert.Equal(questBytes, committed.Files[QuestPath]);
            Assert.Equal(characterBefore, committed.Files[CharacterPath]);
            Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid)));
            Assert.Equal(questBytes, PlayerQuestSnapshotLoader.Serialize(new PlayerQuestSnapshotLoader(Quests).LoadByCharacterUid(CharacterUid)));
            Assert.Equal(before.SessionBytes, JsonSerializer.SerializeToUtf8Bytes(before.Session));
            Assert.Equal(before.Items, PlayerItemSnapshotLoader.ToLegacyBytes(before.Session.PlayerItems));
            Assert.Equal(before.Quests, PlayerQuestSnapshotLoader.Serialize(before.Session.Character.PlayerQuests));
            Assert.Equal(committed.SessionBytes, JsonSerializer.SerializeToUtf8Bytes(session));
            AssertVictoryFiles(committed.Files);
            AssertAllFiles();
            Assert.False(LegacyTradeJournal.HasPending(Users));
            Assert.False(recovery.IsBlocked);
            return
            [
                LegacyQuestListPacket.CreateSingleQuestFrame(LegacyQuestListMode.FinishQuest,
                    new LegacyQuestListItem(QuestId, LegacyQuestStatus.Finished, 12345)),
                LegacyGameItemPacket.FromSnapshotItem(exactReward).ToFrameBytes(),
            ];
        }

        private void AssertVictoryFiles(Dictionary<string, byte[]> expected)
        {
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal),
                Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach (var file in expected) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }
    }
}
