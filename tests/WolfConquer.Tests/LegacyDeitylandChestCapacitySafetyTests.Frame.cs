using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualFrameChestCommitsUnderIndependentSharedReaderAndReleasesItsLease(bool epic) => RunChestFrameAsync(async fixture =>
    {
        fixture.SeedFrameChest(epic);
        await fixture.HoldIndependentChestFrameReaderAsync();
        await fixture.OpenChestFrameDialogAsync(epic);
        fixture.ObserveChestFrameCommittedSends();

        await fixture.HandleChestFrameAsync(ReadFrame(new LegacyNpcDialogRequestPacket(
            NpcId: 0, Mesh: 0, OptionId: 1, InteractType: 1, Action: 0, Input: "").ToFrameBytes()));

        fixture.AssertChestFrameCommittedSends();
        fixture.AssertChestFrameReward(epic);
        await fixture.ProveChestFrameSharedLeaseReleasedAsync();
        Assert.DoesNotContain("PARITY-BUG", fixture.Log.ToString());
        Assert.DoesNotContain("PARITY-GAP", fixture.Log.ToString());
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualFrameChestRejectsMissingRequiredItemsWithoutRewardAndReleasesItsLease(bool epic) => RunChestFrameAsync(async fixture =>
    {
        fixture.SeedFrameChest(epic);
        await fixture.HoldIndependentChestFrameReaderAsync();
        await fixture.OpenChestFrameDialogAsync(epic);
        fixture.CorruptRequiredChestSaveBaseline("itemsMissing");
        var before = fixture.CaptureRequiredChestSavePreimage();
        var dialog = fixture.Sent.Select(frame => frame.ToArray()).ToArray();

        await fixture.HandleChestFrameAsync(ReadFrame(new LegacyNpcDialogRequestPacket(
            NpcId: 0, Mesh: 0, OptionId: 1, InteractType: 1, Action: 0, Input: "").ToFrameBytes()));

        fixture.AssertChestFrameRefused(before, dialog);
        await fixture.ProveChestFrameSharedLeaseReleasedAsync();
        Assert.DoesNotContain("PARITY-BUG", fixture.Log.ToString());
        Assert.DoesNotContain("PARITY-GAP", fixture.Log.ToString());
    });

    private sealed partial class Fixture
    {
        internal void SeedFrameChest(bool epic)
        {
            var keyId = epic ? 3_303_974U : KeyItemId;
            Seed(40, firstStack: 1, secondStack: 2, firstBound: 1,
                firstKeyItemId: keyId, secondKeyItemId: keyId,
                npcId: epic ? 22_607U : 22_598U, accountId: 301);
            AttachChestFrameOwner();
        }

        internal async Task OpenChestFrameDialogAsync(bool epic)
        {
            var before = Current;
            await HandleChestFrameAsync(ReadFrame(LegacyNpcServerRequestPacket.Script(
                epic ? 22_607U : 22_598U, optionId: 0, interactType: 1).ToFrameBytes()));
            Assert.Equal(before with { ActiveNpcId = epic ? 22_607U : 22_598U }, Current);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath));
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            Assert.Equal(8, Sent.Count);
            var texts = epic ? new[]
            {
                "\t\t\t\t\t\t\t\t\t*<tip desc=This#divine#chest#contains#fabulous#treasures#of#the#ancient#deities.#However,#it`s#locked.>Epic Deityland Chest</tip>*\n",
                "----------------------------------------------------------\n",
                "Requirement: An <tip color=0xfff05050 desc=The#devils#in#the#Deityland#may#carry#the#Epic#Chest#Key.>Epic Chest Key</tip>\n",
                "Rewards: A chance to get 200,000 CPs, Frozen Chi Pill Scrap, Anima p5, P7 Dragon Soul, etc.\n",
                "----------------------------------------------------------\n",
            } : new[]
            {
                "\t\t\t\t\t\t\t\t\t*<tip desc=This#delicate#chest#with#vivid#dragon#pattern#contains#valuable#items~crafted~by~the~ancient~deities.#However,#it`s#locked.>Rare Deityland Chest</tip>*\n",
                "----------------------------------------------------------\n",
                "Requirement: A <tip color=0xfff05050 desc=The#devils#in#the#Deityland#may#carry#the#Rare#Chest#Key.>Rare Chest Key</tip>\n",
                "Rewards: A chance to get Chasm Star Stone Pack, points, etc.\n",
                "----------------------------------------------------------\n",
            };
            for (var index = 0; index < texts.Length; index++)
                Assert.Equal(LegacyNpcReplyPacket.ForDialog(texts[index]).ToFrameBytes(), Sent[index]);
            Assert.Equal(LegacyNpcReplyPacket.ForOption("I~own~the~key!", 1).ToFrameBytes(), Sent[5]);
            Assert.Equal(LegacyNpcReplyPacket.ForClosedOption("I~see.").ToFrameBytes(), Sent[6]);
            Assert.Equal(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), Sent[7]);
        }

        internal void AssertChestFrameReward(bool epic)
        {
            Assert.Equal(10, Sent.Count);
            Assert.Equal(new MsgItemUsagePacket(900, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), Sent[8]);
            var debit = ExpectedDebit();
            if (epic && LegacyMsgUpdatePacket.TryParse(ReadFrame(Sent[9]), out var update))
            {
                AssertItems(debit);
                Assert.Equal(Session with
                {
                    Character = Session.Character with { ConquerPoints = 200_055 },
                    PlayerItems = Current.PlayerItems,
                }, Current);
                Assert.Equal(new LegacyMsgUpdatePacket((uint)CharacterUid,
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, 200_055)]).ToFrameBytes(), Sent[9]);
                Assert.Equal((uint)CharacterUid, update.Uid);
                AssertCharacterCpSaved();
                Assert.Equal(39, Current.PlayerItems.InventoryItems.Count);
            }
            else
            {
                Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(Sent[9]), out var item));
                Assert.Contains(item.ItemId, epic ? new uint[] { 4_200_005, 3_007_269, 3_004_247 }
                    : new uint[] { 3_306_362, 3_303_480, 3_003_124 });
                var reward = new PlayerItemSnapshotItem { Uid = 5001, ItemId = item.ItemId, StackSize = 1 };
                Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(), Sent[9]);
                AssertItems(debit with { InventoryItems = debit.InventoryItems.Concat([reward]).ToArray() });
                Assert.Equal(Session with { PlayerItems = Current.PlayerItems }, Current);
                Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
                Assert.Equal(40, Current.PlayerItems.InventoryItems.Count);
            }
            Assert.Contains(epic ? "source=NpcHandler.EpicDeitylandChests" : "source=NpcHandler.RareDeitylandChests", Log.ToString());
            Assert.False(LegacyTradeJournal.HasPending(Users));
        }

        internal void AssertChestFrameRefused(RequiredChestSavePreimage before, byte[][] dialog)
        {
            Assert.Same(before.Session, Current);
            Assert.Equal(before.SessionBytes, JsonSerializer.SerializeToUtf8Bytes(Current));
            Assert.Equal(before.PhysicalItems, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(before.PhysicalItems, PlayerItemSnapshotLoader.ToLegacyBytes(Session.PlayerItems));
            Assert.Equal(dialog.Length, Sent.Count);
            for (var index = 0; index < dialog.Length; index++) Assert.Equal(dialog[index], Sent[index]);
            Assert.Equal(before.Files.Keys.Order(StringComparer.Ordinal),
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach (var file in before.Files) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            Assert.False(File.Exists(ItemPath));
            Assert.Contains("reason=deityChestSnapshotMissing.", Log.ToString());
            Assert.Contains("rejected without commit", Log.ToString());
            Assert.DoesNotContain("chest item reward", Log.ToString());
            Assert.DoesNotContain("chest CP reward", Log.ToString());
        }
    }
}
