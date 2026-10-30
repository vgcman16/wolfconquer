using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Fact]
    public async Task RepeatedStackSalesPersistEachSingleUnitBeforeUpdateOrRemovalAndPayOnlyThreeTimes()
    {
        const uint uid = 1_299_121;
        SeedStack(uid);
        var owner = NewOwner();
        Player? player = null;
        WorldSession? expected = null;
        var armed = false;
        var observed = 0;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed || (!IsRemoval(bytes) && !IsStackUpdate(bytes))) return;
            observed++;
            callbackFailure ??= Record.Exception(() =>
            {
                Assert.NotNull(expected);
                AssertSaved(uid, expected);
                Assert.Equal(expected.Character.Money, Current(owner, player!).Character.Money);
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems),
                    PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, player!).PlayerItems));
                Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
            });
        });
        armed = true;
        for (ushort remaining = 2; ; remaining--)
        {
            var before = Current(owner, player);
            expected = StackSaleResult(before, remaining, before.Character.Money + 300);
            var first = player.Sent.Count;

            await Sell(owner, player);

            Assert.Null(callbackFailure);
            AssertSaved(uid, expected);
            Assert.Equal(expected.Character.Money, Current(owner, player).Character.Money);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems),
                PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, player).PlayerItems));
            Assert.Equal(2, MoneyUpdateCount(player, first));
            if (remaining == 0)
            {
                Assert.Single(player.Sent.Skip(first), IsRemoval);
                Assert.Empty(StackUpdates(player, first));
                break;
            }
            var update = Assert.Single(StackUpdates(player, first));
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
            Assert.Equal(remaining, update.StackSize);
            Assert.DoesNotContain(player.Sent.Skip(first), IsRemoval);
        }
        Assert.Equal(3, observed);
        Assert.Equal(1_900, Current(owner, player).Character.Money);
        Assert.Equal(KeptUid, Assert.Single(Current(owner, player).PlayerItems.InventoryItems).Uid);
        await AssertReplayCannotPay(owner, player);
        Assert.Equal(3, observed);
    }

    [Theory]
    [InlineData((ushort)39)]
    [InlineData((ushort)40)]
    public async Task MalformedInventoryPositionCannotReplaceOwnedGraphOrAuthorizeAStackSale(ushort position)
    {
        const uint uid = 1_299_131;
        SeedStack(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var initial = Current(owner, player);
        var characters = File.ReadAllBytes(UserPath(uid));
        var items = File.ReadAllBytes(ItemPath(uid));
        var inventory = Assert.IsType<LegacyNativeInventoryRuntime>(player.Connection.NativeGameClient!.Inventory);
        var originalItem = inventory.ClientItems[SoldUid];
        var controlledItems = initial.PlayerItems with
        {
            InventoryItems = initial.PlayerItems.InventoryItems.Select(item =>
                item.Uid == SoldUid ? item.WithPosition(position) : item).ToArray(),
        };
        int first = player.Sent.Count;

        Assert.Throws<InvalidDataException>(() =>
            owner.Sessions.Set(player.Connection, initial with { PlayerItems = controlledItems }));

        Assert.Same(initial, Current(owner, player));
        Assert.Same(originalItem, inventory.ClientItems[SoldUid]);
        Assert.Equal((ushort)0, originalItem.Position);
        Assert.Equal(characters, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(first, player.Sent.Count);
        // The failed malformed replacement must not poison the real owner. A
        // subsequent valid stack sale still pays once and keeps the whole graph.
        var expected = StackSaleResult(initial, 2, 1_300);
        await Sell(owner, player);
        AssertSaved(uid, expected);
        Assert.Equal(1_300, Current(owner, player).Character.Money);
        Assert.Equal(2, MoneyUpdateCount(player, first));
        var update = Assert.Single(StackUpdates(player, first));
        Assert.Equal((byte)0, update.Position);
        Assert.Equal((ushort)2, update.StackSize);
        Assert.DoesNotContain(player.Sent.Skip(first), IsRemoval);
    }

    [Fact]
    public async Task ZeroDurabilityStackLosesOneUnitWithoutMoneyNotifications()
    {
        const uint uid = 1_299_141;
        SeedStack(uid, durability: 0);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var expected = StackSaleResult(Current(owner, player), 2, 1_000);
        var first = player.Sent.Count;

        await Sell(owner, player);

        AssertSaved(uid, expected);
        Assert.Equal(1_000, Current(owner, player).Character.Money);
        Assert.Equal((ushort)2, Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == SoldUid).StackSize);
        var update = Assert.Single(StackUpdates(player, first));
        Assert.Equal((ushort)2, update.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
        Assert.DoesNotContain(player.Sent.Skip(first), IsRemoval);
        Assert.Equal(0, MoneyUpdateCount(player, first));
    }

    [WindowsSecondaryPasswordPersistenceFact]
    public async Task LockedItemReplacementPreservesWholeStackUntilOneSuccessfulRetryDecrementsOneUnit()
    {
        const uint uid = 1_299_151;
        SeedStack(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        using (File.Open(ItemPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Sell(owner, player);
            Assert.Same(original, Current(owner, player));
            Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
            Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
            Assert.DoesNotContain(player.Sent.Skip(first), IsSaleSuccessFrame);
            Assert.Empty(StackUpdates(player, first));
            Assert.Equal((ushort)3, Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == SoldUid).StackSize);
        }
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));

        await Sell(owner, player);

        AssertSaved(uid, StackSaleResult(original, 2, 1_300));
        Assert.Equal(1_300, Current(owner, player).Character.Money);
        var update = Assert.Single(StackUpdates(player, first));
        Assert.Equal((ushort)2, update.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
        Assert.DoesNotContain(player.Sent.Skip(first), IsRemoval);
        Assert.Equal(2, MoneyUpdateCount(player, first));
    }

    private void SeedStack(uint uid, ushort durability = 10)
    {
        Seed(uid, durability);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var items = loader.LoadByCharacterUid(uid);
        loader.SaveByCharacterUid(uid, items with
        {
            InventoryItems = items.InventoryItems.Select(item => item.Uid == SoldUid ? item.WithStackSize(3) : item).ToArray(),
        });
    }

    private static WorldSession StackSaleResult(WorldSession original, ushort remaining, long money)
    {
        var updated = SaleResult(original, money, 50);
        return remaining == 0 ? updated : updated with
        {
            PlayerItems = original.PlayerItems with
            {
                InventoryItems = original.PlayerItems.InventoryItems.Select(item =>
                    item.Uid == SoldUid ? item.WithStackSize(remaining) : item).ToArray(),
            },
        };
    }

    private static bool IsStackUpdate(byte[] bytes) => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Uid == SoldUid && packet.Mode == LegacyGameItemPacket.UpdateItemMode;

    private static LegacyGameItemPacket[] StackUpdates(Player player, int first) => player.Sent.Skip(first)
        .Where(IsStackUpdate).Select(bytes =>
        {
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
            return packet;
        }).ToArray();
}
