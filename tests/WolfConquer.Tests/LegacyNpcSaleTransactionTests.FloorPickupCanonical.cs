using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Fact]
    public async Task JoinedPickupAndQuestContinuationExposeActualCanonicalNativeAndHolderStateBeforeEachSend()
    {
        const uint uid = 1_300_271;
        var originalQuests = SeedPickupQuest(uid, PlayerQuestStatus.Accepted, new uint[] { 4, 9 });
        var expectedQuests = originalQuests.WithQuest(originalQuests.Quests.Single(row => row.Uid == 1311) with
            { Intentions = new uint[] { 5, 9 } });
        var owner = NewQuestPickupOwner();
        Player? player = null;
        WorldSession? assetReceipt = null, questReceipt = null;
        LegacyOrdinarySpellBinding? admittedBinding = null;
        LegacyNativeGameItemRuntime? originalKept = null, receivedNativeItem = null;
        byte[]? expectedItems = null, characterBefore = null;
        bool armed = false;
        int assets = 0, objectives = 0;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed) return;
            var frame = ReadFrame(bytes);
            if (LegacyGameItemPacket.TryParse(frame, out var item) && item.Uid == AdmissionPickedItemUid)
            {
                assets++;
                callbackFailure ??= Record.Exception(() =>
                {
                    assetReceipt = Current(owner, player!);
                    AssertCanonical(assetReceipt);
                    Assert.Equal(PlayerQuestSnapshotLoader.Serialize(originalQuests),
                        PlayerQuestSnapshotLoader.Serialize(assetReceipt.Character.PlayerQuests));
                    Assert.Equal(PlayerQuestSnapshotLoader.Serialize(originalQuests), File.ReadAllBytes(PickupQuestPath(uid)));
                    receivedNativeItem = player!.Connection.NativeGameClient!.Inventory!.ClientItems[AdmissionPickedItemUid];
                    Assert.Equal((ushort)3, receivedNativeItem.StackSize);
                    Assert.Equal((byte)0, receivedNativeItem.Plus);
                });
            }
            else if (frame.Header.Opcode == MsgQuestDataPacket.Opcode)
            {
                objectives++;
                callbackFailure ??= Record.Exception(() =>
                {
                    questReceipt = Current(owner, player!);
                    AssertCanonical(questReceipt);
                    Assert.NotNull(assetReceipt);
                    Assert.NotSame(assetReceipt, questReceipt);
                    Assert.Same(assetReceipt.PlayerItems, questReceipt.PlayerItems);
                    Assert.Same(receivedNativeItem, player!.Connection.NativeGameClient!.Inventory!.ClientItems[AdmissionPickedItemUid]);
                    Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expectedQuests),
                        PlayerQuestSnapshotLoader.Serialize(questReceipt.Character.PlayerQuests));
                    Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expectedQuests), File.ReadAllBytes(PickupQuestPath(uid)));
                });
            }
        });
        var before = Current(owner, player);
        var originalItems = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
        var client = player.Connection.NativeGameClient!;
        admittedBinding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
        originalKept = client.Inventory!.ClientItems[KeptUid];
        var incoming = SeedQuest1311Floor(owner, player);
        var incomingBefore = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot(new[] { incoming },
            Array.Empty<PlayerItemSnapshotItem>(), Array.Empty<PlayerItemSnapshotItem>(),
            Array.Empty<PlayerItemSnapshotItem>(), Array.Empty<PlayerItemSnapshotItem>()));
        expectedItems = ExpectedPickupInventoryBytes(before.PlayerItems, incoming.WithPosition(0), merge: false, stack: incoming.StackSize);
        characterBefore = File.ReadAllBytes(UserPath(uid));
        int first = player.Sent.Count;
        armed = true;

        await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);

        Assert.Null(callbackFailure); Assert.Equal(1, assets); Assert.Equal(1, objectives);
        Assert.NotNull(assetReceipt); Assert.NotNull(questReceipt);
        Assert.Same(questReceipt, Current(owner, player));
        AssertQuest1311PickupFrames(player.Sent.Skip(first).ToArray(), new uint[] { 5, 9 });
        Assert.Equal(originalItems, PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems));
        Assert.Equal(incomingBefore, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot(new[] { incoming },
            Array.Empty<PlayerItemSnapshotItem>(), Array.Empty<PlayerItemSnapshotItem>(),
            Array.Empty<PlayerItemSnapshotItem>(), Array.Empty<PlayerItemSnapshotItem>())));
        var finalItems = File.ReadAllBytes(ItemPath(uid)); var finalQuests = File.ReadAllBytes(PickupQuestPath(uid));
        int sent = player.Sent.Count;
        await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);
        Assert.Equal(sent, player.Sent.Count); Assert.Same(questReceipt, Current(owner, player));
        Assert.Equal(finalItems, File.ReadAllBytes(ItemPath(uid))); Assert.Equal(finalQuests, File.ReadAllBytes(PickupQuestPath(uid)));

        void AssertCanonical(WorldSession current)
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(player!.Connection.NativeGameClient!.OrdinarySpellBinding);
            Assert.Same(admittedBinding, binding);
            Assert.Same(current.PlayerItems, binding.ItemSnapshot);
            Assert.Same(current.PlayerItems, binding.Items.Image.FinalSnapshot);
            Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
            Assert.Same(originalKept, player.Connection.NativeGameClient.Inventory!.ClientItems[KeptUid]);
            Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath(uid)));
            Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Equal(characterBefore, File.ReadAllBytes(UserPath(uid)));
            Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
            Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        }
    }
}
