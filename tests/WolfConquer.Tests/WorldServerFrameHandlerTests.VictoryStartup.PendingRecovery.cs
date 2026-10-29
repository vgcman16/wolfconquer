using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public Task ActualD13VictoryStartupRecoversPendingV3BeforeLoginAndRefusesDuplicateNpcClaim() =>
        RunVictoryStartupAsync(quests: true, async fixture =>
        {
            await fixture.AssertPendingVictoryRecoveryAsync();
        });

    [Fact]
    public Task ActualD13VictoryStartupUnknownQuestImageKeepsPendingV3AndRefusesAdmission() =>
        RunVictoryStartupAsync(quests: true, async fixture =>
        {
            await fixture.AssertUnknownVictoryRecoveryImageAsync();
        });

    private sealed partial class VictoryStartupFixture
    {
        private sealed record RecoveryImages(byte[] ItemsAfter, byte[] QuestsAfter,
            PlayerItemSnapshotItem Reward);

        internal async Task AssertPendingVictoryRecoveryAsync()
        {
            Seed();
            var images = PreparePendingVictoryRecord();
            // Only fixture EXPECTATIONS change here. Disk is still the asserted
            // mixed prefix and no handler, session or recovery owner exists yet.
            itemSeed = images.ItemsAfter;
            questSeed = images.QuestsAfter;
            await EnterSeededAsync(); // Actual first Frame performs recovery, then normal login/SetLocation.
            Assert.False(LegacyTradeJournal.HasPending(Users));
            Assert.True(recovery.IsReadyFor(Users, Items, Quests));
            AssertRecoveredVictoryImage(images);

            await HoldIndependentReaderAsync();
            await OpenAsync(21_972);
            await AssertReplayAsync(); // Actual Script/Reply sees Finished, not a new grant.
            await AssertReplayAsync();
            AssertRecoveredVictoryImage(images);
            Assert.DoesNotContain("WorldServer handled LegacyNpc VictoryChest unlock", log.ToString());
            await ProveSharedLeasesReturnedAsync();
        }

        internal async Task AssertUnknownVictoryRecoveryImageAsync()
        {
            Seed();
            var images = PreparePendingVictoryRecord();
            var beforeQuests = new PlayerQuestSnapshotLoader(Quests).LoadByCharacterUid(Uid);
            Assert.True(beforeQuests.TryGetQuest(QuestId, out var claim));
            var unknown = beforeQuests.WithQuest(claim with { Time = claim.Time + 1 });
            var unknownBytes = PlayerQuestSnapshotLoader.Serialize(unknown);
            Assert.False(unknownBytes.AsSpan().SequenceEqual(questSeed));
            Assert.False(unknownBytes.AsSpan().SequenceEqual(images.QuestsAfter));
            new PlayerQuestSnapshotLoader(Quests).SaveByCharacterUid(Uid, unknown);
            Assert.Equal(unknownBytes, File.ReadAllBytes(QuestPath));
            var evidence = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
            Assert.Equal(4, evidence.Count); // Three targets and the genuine pending journal, no staging leftovers.

            // The same actual constructor/first Frame as the positive fixture,
            // stopping before positive-login assertions that must not hold here.
            await RequestSeededAdmissionAsync();
            Assert.Empty(sent);
            Assert.False(sessions.TryGet(connection, out _));
            Assert.True(recovery.IsBlocked);
            Assert.False(recovery.IsReadyFor(Users, Items, Quests));
            Assert.True(LegacyTradeJournal.HasPending(Users));
            Assert.Contains("WorldServer trade recovery blocked world admission/actions: " +
                "tradeJournalRecoveryRequired:rollForwardFailed:IOException", log.ToString());
            Assert.DoesNotContain("questsRootMissing", log.ToString());
            Assert.DoesNotContain("WorldServer snapshot Success", log.ToString());
            Assert.DoesNotContain("WorldServer handled LegacyNpc VictoryChest unlock", log.ToString());
            AssertFileSet(evidence);
            foreach (var file in evidence) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            Assert.Equal(characterSeed, File.ReadAllBytes(CharacterPath));
            Assert.Equal(images.ItemsAfter, File.ReadAllBytes(ItemPath));
            Assert.Equal(unknownBytes, File.ReadAllBytes(QuestPath));
        }

        private RecoveryImages PreparePendingVictoryRecord()
        {
            Assert.Null(handler);
            AssertSeedFiles();
            var items = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid);
            var quests = new PlayerQuestSnapshotLoader(Quests).LoadByCharacterUid(Uid);
            Assert.True(quests.TryGetQuest(QuestId, out var claim));
            Assert.Equal(PlayerQuestStatus.Accepted, claim.Status);
            Assert.Equal(new uint[] { 100 }, claim.Intentions);
            // Explicit recovery-image data, NOT a result from a grant allocator.
            var reward = new PlayerItemSnapshotItem { Uid = 50_001, ItemId = 3_008_727, StackSize = 1 };
            var oldUids = LegacyDragonSkinInventory.Stores(items).SelectMany(store => store)
                .SelectMany(row => new[] { row.Uid }.Concat(row.DepositedItems.Select(child => child.Uid)));
            Assert.DoesNotContain(reward.Uid, oldUids);
            var afterItems = items with { InventoryItems = items.InventoryItems.Concat(new[] { reward }).ToArray() };
            var afterQuests = quests.WithQuest(claim with { Status = PlayerQuestStatus.Finished });
            var images = new RecoveryImages(PlayerItemSnapshotLoader.ToLegacyBytes(afterItems),
                PlayerQuestSnapshotLoader.Serialize(afterQuests), reward);
            using var interruption = LegacyJournalInterruption.StopAfter(2);
            Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests,
            [
                new(LegacyTradeJournalFileKind.Character, Uid, characterSeed, characterSeed),
                new(LegacyTradeJournalFileKind.Items, Uid, itemSeed, images.ItemsAfter),
                new(LegacyTradeJournalFileKind.Quests, Uid, questSeed, images.QuestsAfter),
            ], interruption, out var reason));
            Assert.Equal(new[] { 0, 1, 2 }, interruption.Visited);
            Assert.Equal("tradeJournalRecoveryRequired:interrupted", reason);
            Assert.True(LegacyTradeJournal.HasPending(Users));
            Assert.Equal(characterSeed, File.ReadAllBytes(CharacterPath));
            Assert.Equal(images.ItemsAfter, File.ReadAllBytes(ItemPath));
            Assert.Equal(questSeed, File.ReadAllBytes(QuestPath));
            return images;
        }

        private void AssertRecoveredVictoryImage(RecoveryImages images)
        {
            Assert.True(Current.Character.PlayerQuests.TryGetQuest(QuestId, out var quest));
            Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
            Assert.Equal(12345U, quest.Time);
            Assert.Equal(new uint[] { 100 }, quest.Intentions);
            var reward = Assert.Single(Current.PlayerItems.InventoryItems, item => item.Uid == images.Reward.Uid);
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(images.Reward).ToFrameBytes(),
                LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes());
            Assert.Equal(images.ItemsAfter, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(images.QuestsAfter, PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests));
            Assert.Equal(images.ItemsAfter,
                PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid)));
            Assert.Equal(images.QuestsAfter,
                PlayerQuestSnapshotLoader.Serialize(new PlayerQuestSnapshotLoader(Quests).LoadByCharacterUid(Uid)));
            AssertSeedFiles();
            Assert.False(LegacyTradeJournal.HasPending(Users));
            AssertFileSet(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CharacterPath] = characterSeed,
                [ItemPath] = images.ItemsAfter,
                [QuestPath] = images.QuestsAfter,
            });
        }
    }
}
