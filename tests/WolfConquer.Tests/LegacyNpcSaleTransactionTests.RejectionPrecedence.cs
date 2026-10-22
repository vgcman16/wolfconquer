using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData((byte)1, (ushort)0)]
    [InlineData((byte)0, (ushort)1)]
    [InlineData((byte)1, (ushort)1)]
    public async Task KnownNoSellItemPrecedesLockedOrInscribedFeedback(byte locked, ushort inscribed)
    {
        const uint uid = 1_299_201;
        SeedRejectionItem(uid, 2_100_075, locked, inscribed);
        var owner = NewOwner();
        var player = await Login(owner, uid);

        // Native noSell MessageBox guard/timer ownership is deferred. This verifies
        // the current silent fence, not a fabricated popup or full MessageBox parity.
        await AssertSilentSaleRejection(owner, player, NpcUid, "legacy no-sell list");
    }

    [Fact]
    public async Task InvisibleNpcPrecedesLockedOrdinaryItemFeedback()
    {
        const uint uid = 1_299_211;
        SeedRejectionItem(uid, ItemId, locked: 1, inscribed: 0);
        var owner = NewOwner();
        var player = await Login(owner, uid);

        await AssertSilentSaleRejection(owner, player, NpcUid + 1, "visible NPC was not found");
    }

    [Fact]
    public async Task UnverifiedPhysicalPlayerPrecedesLockedItemFeedbackWithoutChangingItsOwner()
    {
        const uint uid = 1_299_221;
        SeedRejectionItem(uid, ItemId, locked: 1, inscribed: 0);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var physical = player.Connection.NativeGameClient!.Player;
        Assert.True(physical.IsCheckedPass);
        // Alter the real initialized field, not a copied account attribute. The
        // private issuer rejects this stale verification binding before item work.
        physical.IsCheckedPass = false;

        await AssertSilentSaleRejection(owner, player, NpcUid, "secondaryPasswordUnverifiedOrOwnerInvalid");

        Assert.Same(physical, player.Connection.NativeGameClient.Player);
        Assert.False(physical.IsCheckedPass);
    }

    private void SeedRejectionItem(uint uid, uint itemId, byte locked, ushort inscribed)
    {
        Seed(uid);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var items = loader.LoadByCharacterUid(uid);
        var seeded = Assert.Single(items.InventoryItems, item => item.Uid == SoldUid);
        // Copy the explicit Seed fixture's fields; unrelated inventory and warehouse
        // entries remain the same real-file records used by ordinary sale tests.
        var rejected = new PlayerItemSnapshotItem
        {
            Uid = seeded.Uid, ItemId = itemId, Durability = seeded.Durability,
            MaximumDurability = seeded.MaximumDurability, StackSize = seeded.StackSize,
            Color = seeded.Color, Plus = seeded.Plus, Locked = locked, Inscribed = inscribed,
        };
        loader.SaveByCharacterUid(uid, items with
        {
            InventoryItems = items.InventoryItems.Select(item => item.Uid == SoldUid ? rejected : item).ToArray(),
        });
    }

    private async Task AssertSilentSaleRejection(Owner owner, Player player, uint npcUid, string expectedReason)
    {
        var original = Current(owner, player);
        var character = File.ReadAllBytes(UserPath(player.Uid));
        var items = File.ReadAllBytes(ItemPath(player.Uid));
        var first = player.Sent.Count;
        var logStart = owner.Log.GetStringBuilder().Length;

        await owner.Handler.HandleFrameAsync(player.Connection,
            ReadFrame(new MsgItemUsagePacket(npcUid, SoldUid, MsgItemUsageAction.SellItem, 0, 0, 0, 0).ToFrameBytes()),
            player.Lifetime.Token);

        Assert.Same(original, Current(owner, player));
        Assert.Equal(character, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(items, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Empty(player.Sent.Skip(first));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        var resultLog = owner.Log.ToString()[logStart..];
        Assert.Contains(expectedReason, resultLog, StringComparison.Ordinal);
        Assert.DoesNotContain("locked or inscribed items cannot be sold", resultLog, StringComparison.Ordinal);
    }
}
