using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyVictoryChestTransactionTests
{
    private sealed partial class Fixture
    {
        internal sealed record Preimage(WorldSession Session, byte[] SessionBytes, byte[] Items, byte[] Quests,
            Dictionary<string, byte[]> Files);

        internal Preimage Capture() => new(Current, JsonSerializer.SerializeToUtf8Bytes(Current),
            PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems),
            PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests),
            Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal));

        internal void AssertRejected(Preimage before)
        {
            AssertStateAndFiles(before);
            Assert.Empty(Sent);
            Assert.DoesNotContain("WorldServer handled LegacyNpc VictoryChest unlock", Log.ToString());
        }

        private void AssertStateAndFiles(Preimage before)
        {
            Assert.Same(before.Session, Current);
            Assert.Same(before.Session.PlayerItems, Current.PlayerItems);
            Assert.Same(before.Session.Character.PlayerQuests, Current.Character.PlayerQuests);
            Assert.Equal(before.SessionBytes, JsonSerializer.SerializeToUtf8Bytes(Current));
            Assert.Equal(before.Items, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(before.Items, PlayerItemSnapshotLoader.ToLegacyBytes(before.Session.PlayerItems));
            Assert.Equal(before.Quests, PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests));
            Assert.Equal(before.Quests, PlayerQuestSnapshotLoader.Serialize(before.Session.Character.PlayerQuests));
            Assert.Equal(before.Files.Keys.Order(StringComparer.Ordinal),
                Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach (var file in before.Files) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }

        internal void AssertCommitted(Preimage before)
        {
            Assert.Equal(2, Sent.Count);
            Assert.True(LegacyQuestListPacket.TryParse(ReadFrame(Sent[0]), out var finish));
            Assert.Equal(new LegacyQuestListPacket(LegacyQuestListMode.FinishQuest, 1, 0, 0,
                QuestId, LegacyQuestStatus.Finished, 12345), finish);
            Assert.Equal(LegacyQuestListPacket.CreateSingleQuestFrame(LegacyQuestListMode.FinishQuest,
                new LegacyQuestListItem(QuestId, LegacyQuestStatus.Finished, 12345)), Sent[0]);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(Sent[1]), out var packet));
            Assert.Contains(packet.ItemId, new uint[] { 3_008_727, 3_009_002, 4_200_004 });
            Assert.NotEqual(0U, packet.Uid);
            var oldUids = LegacyDragonSkinInventory.Stores(before.Session.PlayerItems)
                .SelectMany(store => store).SelectMany(row => new[] { row.Uid }.Concat(row.DepositedItems.Select(child => child.Uid))).ToArray();
            Assert.DoesNotContain(packet.Uid, oldUids);
            // Optional metadata is absent, matching the retained CURRENT owner.
            // This is the real returned UID, not a substitute allocator/max helper.
            var reward = new PlayerItemSnapshotItem { Uid = packet.Uid, ItemId = packet.ItemId, StackSize = 1 };
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(), Sent[1]);
            var expectedItems = before.Session.PlayerItems with
            { InventoryItems = before.Session.PlayerItems.InventoryItems.Concat(new[] { reward }).ToArray() };
            Assert.True(before.Session.Character.PlayerQuests.TryGetQuest(QuestId, out var oldQuest));
            var expectedQuests = before.Session.Character.PlayerQuests.WithQuest(oldQuest with { Status = PlayerQuestStatus.Finished });
            Assert.Equal(before.Session with
            {
                Character = before.Session.Character with { PlayerQuests = Current.Character.PlayerQuests },
                PlayerItems = Current.PlayerItems,
            }, Current);
            var expectedItemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems);
            var expectedQuestBytes = PlayerQuestSnapshotLoader.Serialize(expectedQuests);
            Assert.Equal(expectedItemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(expectedItemBytes, File.ReadAllBytes(ItemPath));
            Assert.Equal(expectedItemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid)));
            Assert.Equal(expectedQuestBytes, PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests));
            Assert.Equal(expectedQuestBytes, File.ReadAllBytes(QuestPath));
            Assert.Equal(expectedQuestBytes, PlayerQuestSnapshotLoader.Serialize(new PlayerQuestSnapshotLoader(Quests).LoadByCharacterUid(CharacterUid)));
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            Assert.Equal(before.SessionBytes, JsonSerializer.SerializeToUtf8Bytes(before.Session));
            Assert.Equal(before.Items, PlayerItemSnapshotLoader.ToLegacyBytes(before.Session.PlayerItems));
            Assert.Equal(before.Quests, PlayerQuestSnapshotLoader.Serialize(before.Session.Character.PlayerQuests));
            AssertAllFiles();
            Assert.Contains("WorldServer handled LegacyNpc VictoryChest unlock", Log.ToString());
            Assert.Contains("WorldServer persisted LegacyPlayerQuests", Log.ToString());
            Assert.Contains("source=NpcHandler.VictoryChest", Log.ToString());
            Assert.DoesNotContain("PARITY-GAP", Log.ToString());
        }

        internal async Task AssertSameSessionAndReloadedReplayAsync()
        {
            var before = Capture();
            Sent.Clear(); Log.GetStringBuilder().Clear();
            await ReplyAsync();
            AssertAlreadyClaimed();
            AssertStateAndFiles(before);
            // Controlled file reload into the actual Store, no ledger reset,
            // accepting provider or invocation of an alternative private owner.
            var character = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath) with
            { PlayerQuests = new PlayerQuestSnapshotLoader(Quests).LoadByCharacterUid(CharacterUid) };
            var items = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid);
            Assert.True(character.PlayerQuests.TryGetQuest(QuestId, out var saved));
            Assert.Equal(PlayerQuestStatus.Finished, saved.Status);
            sessions.Set(Connection, Current with { Character = character, PlayerItems = items });
            var reloaded = Capture();
            Sent.Clear(); Log.GetStringBuilder().Clear();
            await ReplyAsync();
            AssertAlreadyClaimed();
            AssertStateAndFiles(reloaded);
        }

        private void AssertAlreadyClaimed()
        {
            Assert.Equal(3, Sent.Count);
            Assert.Equal(LegacyNpcReplyPacket.ForDialog("You`ve already unlocked this chest today. Please try again tomorrow.").ToFrameBytes(), Sent[0]);
            Assert.Equal(LegacyNpcReplyPacket.ForClosedOption("Okay.").ToFrameBytes(), Sent[1]);
            Assert.Equal(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), Sent[2]);
            Assert.DoesNotContain("WorldServer handled LegacyNpc VictoryChest unlock", Log.ToString());
        }

        private void AssertDialog()
        {
            var texts = new[]
            {
                "\t\t\t\t\t\t\t\t\t*<tip desc=Heroes#who#have#killed#100#devils#in#the#Deityland#can#unlock#this#chest#and#get#wonderful#rewards.>Victory Chest</tip>*\n",
                "----------------------------------------------------------\n",
                "Requirement: Kill 100 devils in the Deityland, once a day.\n",
                "Rewards: 1 Bright Star Stone for sure, and a shot to win +2 Stone, Blazing CP Fragment, 150 Chi, etc.\n",
                "----------------------------------------------------------\n",
            };
            Assert.Equal(8, Sent.Count);
            for (var index = 0; index < texts.Length; index++) Assert.Equal(LegacyNpcReplyPacket.ForDialog(texts[index]).ToFrameBytes(), Sent[index]);
            Assert.Equal(LegacyNpcReplyPacket.ForOption("Unlock~this~chest.", 1).ToFrameBytes(), Sent[5]);
            Assert.Equal(LegacyNpcReplyPacket.ForClosedOption("I~see.").ToFrameBytes(), Sent[6]);
            Assert.Equal(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), Sent[7]);
        }

        private void AssertAllFiles() => Assert.Equal(new[] { CharacterPath, ItemPath, QuestPath }.Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
    }
}
