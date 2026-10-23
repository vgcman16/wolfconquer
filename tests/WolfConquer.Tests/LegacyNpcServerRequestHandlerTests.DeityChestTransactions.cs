using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcServerRequestHandlerTests
{
    [Fact]
    public async Task HandlerUnlocksRareDeitylandChestWithSourceReward()
    {
        await RunDeityChestAsync(async fixture =>
        {
            var log = fixture.Log;
            var sent = fixture.Sent;
            var connection = fixture.Connection;
            var baseSession = CreateDragonIslandSession();
            var key = new PlayerItemSnapshotItem
            {
                Uid = 90,
                ItemId = 3_303_973,
                StackSize = 1,
            };
            var session = baseSession with
            {
                PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [key] },
            };
            fixture.Seed(session);
            var sessions = fixture.Sessions;

            await fixture.HandleAsync(ReadFrame(LegacyNpcServerRequestPacket.Script(22_598, optionId: 0, interactType: 1).ToFrameBytes()));

            Assert.Equal(8, sent.Count);
            var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
            Assert.Contains("Rare Deityland Chest", dialog.Text);
            var keyOption = AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Option);
            Assert.Equal(1, keyOption.OptionId);
            Assert.Equal("I~own~the~key!", keyOption.Text);

            Assert.True(sessions.TryGet(connection, out var activeSession));
            Assert.NotNull(activeSession);
            fixture.AssertDialogOnly(22_598);

            await fixture.HandleAsync(ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

            Assert.Equal(10, sent.Count);
            AssertRemoveInventory(sent[8], key.Uid);
            var reward = AssertGameItemIn(sent[9], [3_306_362U, 3_303_480U, 3_003_124U]);
            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == key.ItemId);
            Assert.Contains(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == reward.ItemId);
            Assert.Contains("source=NpcHandler.RareDeitylandChests", log.ToString());
            Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
            fixture.AssertCommitted(22_598, key.Uid);
        });
    }

    [Fact]
    public async Task HandlerUnlocksEpicDeitylandChestWithEpicKey()
    {
        await RunDeityChestAsync(async fixture =>
        {
            var log = fixture.Log;
            var sent = fixture.Sent;
            var connection = fixture.Connection;
            var baseSession = CreateDragonIslandSession();
            var epicDialogKey = new PlayerItemSnapshotItem
            {
                Uid = 91,
                ItemId = 3_303_974,
                StackSize = 1,
            };
            var sourceUnlockKey = new PlayerItemSnapshotItem
            {
                Uid = 92,
                ItemId = 3_303_973,
                StackSize = 1,
            };
            var session = baseSession with
            {
                PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [epicDialogKey, sourceUnlockKey] },
            };
            fixture.Seed(session);
            var sessions = fixture.Sessions;

            await fixture.HandleAsync(ReadFrame(LegacyNpcServerRequestPacket.Script(22_607, optionId: 0, interactType: 1).ToFrameBytes()));

            Assert.Equal(8, sent.Count);
            var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
            Assert.Contains("Epic Deityland Chest", dialog.Text);
            var keyOption = AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Option);
            Assert.Equal(1, keyOption.OptionId);
            Assert.Equal("I~own~the~key!", keyOption.Text);

            Assert.True(sessions.TryGet(connection, out var activeSession));
            Assert.NotNull(activeSession);
            fixture.AssertDialogOnly(22_607);

            await fixture.HandleAsync(ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

            Assert.Equal(10, sent.Count);
            AssertRemoveInventory(sent[8], epicDialogKey.Uid);
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[9]), out var update))
            {
                Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, 200_000)], update.Updates);
            }
            else
            {
                AssertGameItemIn(sent[9], [4_200_005U, 3_007_269U, 3_004_247U]);
            }

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == epicDialogKey.ItemId);
            Assert.Contains(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == sourceUnlockKey.ItemId);
            Assert.Contains("source=NpcHandler.EpicDeitylandChests", log.ToString());
            Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
            fixture.AssertCommitted(22_607, epicDialogKey.Uid);
        });
    }
}
