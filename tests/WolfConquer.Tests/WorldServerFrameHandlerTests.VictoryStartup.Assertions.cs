using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private sealed partial class VictoryStartupFixture
    {
        internal sealed record Before(WorldSession Session, byte[] SessionImage, byte[] Items, byte[] Quests,
            Dictionary<string, byte[]> Files);
        private bool observeCommittedPublication;
        private int completedPublicationObservations;
        private Before? publicationBefore;

        internal Before Capture() => new(Current, JsonSerializer.SerializeToUtf8Bytes(Current),
            PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems),
            PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests),
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal));

        internal async Task OpenAsync(uint npc)
        {
            var before = Current;
            await FrameAsync(LegacyNpcServerRequestPacket.Script(npc, 0, 1).ToFrameBytes());
            Assert.Equal(before with { ActiveNpcId = npc }, Current);
            string[] texts =
            [
                "\t\t\t\t\t\t\t\t\t*<tip desc=Heroes#who#have#killed#100#devils#in#the#Deityland#can#unlock#this#chest#and#get#wonderful#rewards.>Victory Chest</tip>*\n",
                "----------------------------------------------------------\n",
                "Requirement: Kill 100 devils in the Deityland, once a day.\n",
                "Rewards: 1 Bright Star Stone for sure, and a shot to win +2 Stone, Blazing CP Fragment, 150 Chi, etc.\n",
                "----------------------------------------------------------\n",
            ];
            Assert.Equal(explicitQuestRoot ? 8 : 7, sent.Count);
            for (var index = 0; index < texts.Length; index++)
                Assert.Equal(LegacyNpcReplyPacket.ForDialog(texts[index]).ToFrameBytes(), sent[index]);
            var option = 5;
            if (explicitQuestRoot)
                Assert.Equal(LegacyNpcReplyPacket.ForOption("Unlock~this~chest.", 1).ToFrameBytes(), sent[option++]);
            Assert.Equal(LegacyNpcReplyPacket.ForClosedOption("I~see.").ToFrameBytes(), sent[option++]);
            Assert.Equal(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), sent[option]);
            AssertSeedFiles();
            sent.Clear();
        }

        internal Task ReplyAsync() => FrameAsync(new LegacyNpcDialogRequestPacket(0, 0, 1, 1, 0, "").ToFrameBytes());

        internal FileStream LeaseRequiredQuestLeaf() => new(QuestPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        internal void AssertQuestSharingRefused(Before before)
        {
            AssertUnchanged(before);
            Assert.Empty(sent);
            Assert.False(recovery.IsBlocked);
            Assert.Contains("reason=victoryChestWriteRejected:", log.ToString());
            Assert.DoesNotContain("WorldServer handled LegacyNpc VictoryChest unlock", log.ToString());
        }

        internal void AssertMissingQuestAuthorityRefused(Before before)
        {
            AssertOneCloseDialog("You need to kill 100 devils in the Deityland before you can unlock this chest.");
            AssertUnchanged(before);
            Assert.False(recovery.IsReadyFor(Users, Items, Quests));
            Assert.DoesNotContain("WorldServer handled LegacyNpc VictoryChest unlock", log.ToString());
        }

        internal void ObserveCommit(Before before)
        { publicationBefore = before; completedPublicationObservations = 0; observeCommittedPublication = true; }

        private void AssertDurableCommit(Before before)
        {
            Assert.NotSame(before.Session, Current);
            Assert.True(Current.Character.PlayerQuests.TryGetQuest(QuestId, out var quest));
            Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
            Assert.Equal(1234L, Current.Character.Money);
            Assert.Equal(55L, Current.Character.ConquerPoints);
            Assert.Equal(characterSeed, File.ReadAllBytes(CharacterPath));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems), File.ReadAllBytes(ItemPath));
            Assert.Equal(PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests), File.ReadAllBytes(QuestPath));
            Assert.False(LegacyTradeJournal.HasPending(Users));
        }

        internal void AssertCommitted(Before before)
        {
            observeCommittedPublication = false;
            Assert.Equal(2, completedPublicationObservations); // Send assertions cannot be silently swallowed.
            Assert.Equal(2, sent.Count);
            Assert.Equal(LegacyQuestListPacket.CreateSingleQuestFrame(LegacyQuestListMode.FinishQuest,
                new LegacyQuestListItem(QuestId, LegacyQuestStatus.Finished, 12345)), sent[0]);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[1]), out var packet));
            Assert.Contains(packet.ItemId, new uint[] { 3_008_727, 3_009_002, 4_200_004 });
            Assert.Equal(5001U, packet.Uid); // Actual CURRENT creator over this complete all-store image.
            var reward = new PlayerItemSnapshotItem { Uid = packet.Uid, ItemId = packet.ItemId, StackSize = 1 };
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(), sent[1]);
            var items = before.Session.PlayerItems with
            { InventoryItems = before.Session.PlayerItems.InventoryItems.Concat([reward]).ToArray() };
            Assert.True(before.Session.Character.PlayerQuests.TryGetQuest(QuestId, out var oldQuest));
            var quests = before.Session.Character.PlayerQuests.WithQuest(oldQuest with { Status = PlayerQuestStatus.Finished });
            Assert.Equal(before.Session with
            {
                Character = before.Session.Character with { PlayerQuests = Current.Character.PlayerQuests },
                PlayerItems = Current.PlayerItems,
            }, Current);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(items), PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(PlayerQuestSnapshotLoader.Serialize(quests), PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests));
            for (var index = 0; index < before.Session.PlayerItems.InventoryItems.Count; index++)
                Assert.Same(before.Session.PlayerItems.InventoryItems[index], Current.PlayerItems.InventoryItems[index]);
            var beforeStores = LegacyDragonSkinInventory.Stores(before.Session.PlayerItems);
            var afterStores = LegacyDragonSkinInventory.Stores(Current.PlayerItems);
            for (var index = 1; index < beforeStores.Length; index++) Assert.Same(beforeStores[index], afterStores[index]);
            Assert.True(before.Session.Character.PlayerQuests.TryGetQuest(804, out var oldUnrelated));
            Assert.True(Current.Character.PlayerQuests.TryGetQuest(804, out var unrelated));
            Assert.Same(oldUnrelated, unrelated);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(items),
                PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid)));
            Assert.Equal(PlayerQuestSnapshotLoader.Serialize(quests),
                PlayerQuestSnapshotLoader.Serialize(new PlayerQuestSnapshotLoader(Quests).LoadByCharacterUid(Uid)));
            AssertDurableCommit(before);
            Assert.Equal(before.SessionImage, JsonSerializer.SerializeToUtf8Bytes(before.Session));
            Assert.Equal(before.Items, PlayerItemSnapshotLoader.ToLegacyBytes(before.Session.PlayerItems));
            Assert.Equal(before.Quests, PlayerQuestSnapshotLoader.Serialize(before.Session.Character.PlayerQuests));
            AssertFileSet(before.Files);
            foreach (var file in before.Files)
                if (file.Key != ItemPath && file.Key != QuestPath) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            Assert.Contains("WorldServer handled LegacyNpc VictoryChest unlock", log.ToString());
            Assert.False(recovery.IsBlocked);
        }

        internal void AssertUnchanged(Before before)
        {
            Assert.Same(before.Session, Current);
            Assert.Same(before.Session.PlayerItems, Current.PlayerItems);
            Assert.Same(before.Session.Character.PlayerQuests, Current.Character.PlayerQuests);
            Assert.Equal(before.SessionImage, JsonSerializer.SerializeToUtf8Bytes(Current));
            Assert.Equal(before.Items, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(before.Quests, PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests));
            AssertFileSet(before.Files);
            foreach (var file in before.Files) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            Assert.False(LegacyTradeJournal.HasPending(Users));
        }

        internal async Task AssertReplayAsync()
        {
            var before = Capture(); sent.Clear();
            await ReplyAsync();
            AssertOneCloseDialog("You`ve already unlocked this chest today. Please try again tomorrow.");
            AssertUnchanged(before);
        }

        private void AssertOneCloseDialog(string text)
        {
            Assert.Equal(3, sent.Count);
            Assert.Equal(LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(), sent[0]);
            Assert.Equal(LegacyNpcReplyPacket.ForClosedOption("Okay.").ToFrameBytes(), sent[1]);
            Assert.Equal(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), sent[2]);
        }

        private void AssertFileSet(Dictionary<string, byte[]> files) => Assert.Equal(files.Keys.Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));

        private void AssertSeedFiles()
        {
            Assert.Equal(characterSeed, File.ReadAllBytes(CharacterPath));
            Assert.Equal(itemSeed, File.ReadAllBytes(ItemPath));
            Assert.Equal(questSeed, File.ReadAllBytes(QuestPath));
        }
    }
}
