using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData((byte)1, (ushort)0, false)]
    [InlineData((byte)0, (ushort)1, false)]
    [InlineData((byte)1, (ushort)1, false)]
    [InlineData((byte)0, (ushort)0, true)]
    public async Task LockedInscribedOrSpecialInventoryUidSendsOneEnglishNativeSystemDenialWithoutMutation(
        byte locked, ushort inscribed, bool specialUid)
    {
        const uint uid = 1_299_171;
        const string denial = "This item is inscribed by your guild or locked.";
        var requestedUid = specialUid ? 2_100_075U : SoldUid;
        Seed(uid);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var seeded = loader.LoadByCharacterUid(uid);
        var originalItem = seeded.InventoryItems.Single(item => item.Uid == SoldUid);
        var selected = new PlayerItemSnapshotItem
        {
            Uid = requestedUid, ItemId = originalItem.ItemId,
            Durability = originalItem.Durability, MaximumDurability = originalItem.MaximumDurability,
            Position = originalItem.Position, StackSize = originalItem.StackSize,
            Plus = originalItem.Plus, Color = originalItem.Color, Locked = locked, Inscribed = inscribed,
        };
        loader.SaveByCharacterUid(uid, seeded with
        {
            InventoryItems = seeded.InventoryItems.Select(item => item.Uid == SoldUid ? selected : item).ToArray(),
        });
        var owner = NewOwner();
        var barrier = ActualOperationBarrier(owner);
        var armed = false;
        var observedDenials = 0;
        var exclusiveAvailable = false;
        Task probeCleanup = Task.CompletedTask;
        var player = await Login(owner, uid, bytes =>
        {
            if (!armed || !LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) ||
                packet.Content.Count <= 3 || packet.Content[3] != denial) return;
            observedDenials++;
            using var cancellation = new CancellationTokenSource();
            var acquisition = barrier.EnterExclusiveAsync(cancellation.Token);
            exclusiveAvailable = acquisition.IsCompletedSuccessfully;
            if (exclusiveAvailable) acquisition.Result.Dispose();
            else
            {
                cancellation.Cancel();
                probeCleanup = DisposeCancelledProbe(acquisition);
            }
            // Assertions stay outside this callback: dispatch catches send errors.
        });
        var before = Current(owner, player);
        Assert.Equal(ItemId, Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == requestedUid).ItemId);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var sessionItemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
        var first = player.Sent.Count;
        armed = true;

        await owner.Handler.HandleFrameAsync(player.Connection,
            ReadFrame(new MsgItemUsagePacket(NpcUid, requestedUid, MsgItemUsageAction.SellItem, 0, 0, 0, 0).ToFrameBytes()),
            player.Lifetime.Token);
        await probeCleanup.WaitAsync(TimeSpan.FromSeconds(5));

        var reply = Assert.Single(player.Sent.Skip(first));
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(reply), out var message));
        Assert.Equal(2_005U, message.ChatType);
        Assert.Equal(16_711_680U, message.Color);
        Assert.Equal(new[] { "SYSTEM", "ALL", string.Empty, denial, string.Empty, string.Empty, string.Empty }, message.Content);
        Assert.Equal(0U, message.MessageUid1);
        Assert.Equal(0U, message.MessageUid2);
        Assert.Equal(0U, message.ServerId);
        Assert.Equal(0U, message.Mesh);
        Assert.Equal(0U, message.UnionType);
        Assert.Equal(1U, message.Count);
        Assert.Equal(0U, message.New1);
        Assert.Equal(0U, message.New2);
        Assert.Equal(1, observedDenials);
        Assert.True(exclusiveAvailable, owner.Log.ToString());
        Assert.Same(before, Current(owner, player));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(sessionItemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, player).PlayerItems));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }
}
