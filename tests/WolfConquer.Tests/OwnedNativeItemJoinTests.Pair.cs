using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeItemJoinTests
{
    [Fact]
    public async Task ActualStorePairAndRealPairWriterPublishBothNativeGraphsWithTheirCanonicalHolders()
    {
        const uint itemUid = 910001;
        var item = new PlayerItemSnapshotItem { Uid = itemUid, ItemId = 132009,
            Durability = 1, MaximumDurability = 1, StackSize = 1 };
        var saved = PlayerItemSnapshot.Empty with { InventoryItems = new[] { item } };
        await WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(async f =>
        {
            string itemsRoot = Path.Combine(f.Root, "PlayersItems");
            await f.LoginAsync(); var second = await f.AdmitSecondAsync();
            var firstBefore = f.Current; var secondBefore = second.Session;
            var firstBinding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var secondBinding = Assert.IsType<LegacyOrdinarySpellBinding>(second.Native.OrdinarySpellBinding);
            var firstInventory = f.Native.Inventory; var secondInventory = second.Native.Inventory;
            var transferred = Assert.Single(firstBefore.PlayerItems.InventoryItems);
            var firstAfter = firstBefore with { PlayerItems = firstBefore.PlayerItems with { InventoryItems = Array.Empty<PlayerItemSnapshotItem>() } };
            var secondAfter = secondBefore with { PlayerItems = secondBefore.PlayerItems with { InventoryItems = new[] { transferred } } };
            var reason = string.Empty; bool durable = false;
            bool Writer()
            {
                durable = LegacyVendorPurchasePersistence.TryPersist(firstBefore, firstAfter, secondBefore, secondAfter,
                    itemsRoot, out reason);
                return durable;
            }

            Assert.True(f.Store.TryCommitPair(f.Connection, firstBefore, firstAfter,
                second.Connection, secondBefore, secondAfter, Writer, out var finalFirst, out var finalSecond), reason);

            Assert.True(durable, reason); Assert.Same(finalFirst, f.Current);
            Assert.True(f.Store.TryGet(second.Connection, out var actualSecond)); Assert.Same(finalSecond, actualSecond);
            Assert.Empty(firstInventory!.ClientItems); Assert.Equal(itemUid, Assert.Single(secondInventory!.ClientItems).Key);
            Assert.Same(firstInventory, f.Native.Inventory); Assert.Same(secondInventory, second.Native.Inventory);
            Assert.Same(f.Current.PlayerItems, firstBinding.Items.Image.FinalSnapshot);
            Assert.Same(finalSecond!.PlayerItems, secondBinding.Items.Image.FinalSnapshot);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(firstBinding)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(secondBinding));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(finalFirst!.PlayerItems),
                File.ReadAllBytes(Path.Combine(itemsRoot, firstBefore.Character.Uid + ".bin")));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(finalSecond.PlayerItems),
                File.ReadAllBytes(Path.Combine(itemsRoot, secondBefore.Character.Uid + ".bin")));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        }, lineage: false, playerItems: saved);
    }
}
