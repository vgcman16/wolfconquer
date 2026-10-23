using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualChestRejectsSelectedPositiveKeyWithDepositedChild(bool cpReward) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound: 1,
            accountId: 301, firstKeyHasDepositedChild: true);
        fixture.AssertRequiredChestSaveOwnersReady();
        var (key, child) = fixture.AssertSeededChestKeyWithDeposit();
        var before = fixture.CaptureRequiredChestSavePreimage();

        await fixture.UnlockSelectedAsync(
            rewardItemId: cpReward ? 0U : 3_306_362U, conquerPointsReward: cpReward ? 200_000U : 0U);

        fixture.AssertRequiredChestSaveRejected(before);
        Assert.Same(before.Session.PlayerItems, fixture.Current.PlayerItems);
        var (retainedKey, retainedChild) = fixture.AssertSeededChestKeyWithDeposit();
        Assert.Same(key, retainedKey);
        Assert.Same(child, retainedChild);
        Assert.Contains("deityChestKeyHasDeposits", fixture.Log.ToString());
    });

    private sealed partial class Fixture
    {
        internal (PlayerItemSnapshotItem Key, PlayerItemSnapshotItem Child) AssertSeededChestKeyWithDeposit()
        {
            var key = Assert.Single(Current.PlayerItems.InventoryItems, item => item.Uid == 900);
            Assert.Same(Current.PlayerItems.InventoryItems[0], key);
            Assert.Equal(KeyItemId, key.ItemId);
            Assert.Equal((ushort)1, key.StackSize);
            Assert.Equal((byte)1, key.Bound); // Deposits, not binding, make this selected cost unsafe.
            var child = Assert.Single(key.DepositedItems);
            Assert.Equal(1900U, child.Uid);
            Assert.Equal(1_001_900U, child.ItemId);
            Assert.Equal((ushort)2, child.StackSize);
            Assert.Empty(child.DepositedItems);

            // The initial Seed serialized and reloaded the child before real
            // recovery initialization. Verify its full native row/perfection
            // image along with every other physical store, not only IDs/counts.
            var expectedRows = Current.PlayerItems.InventoryItems.ToArray();
            expectedRows[0] = Item(900, KeyItemId, 1).WithBound(1)
                .WithDepositedItems([Item(1900, 1_001_900, 2)]);
            var expected = Current.PlayerItems with { InventoryItems = expectedRows };
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(expected));
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            var loaded = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(loaded));
            return (key, child);
        }
    }
}
