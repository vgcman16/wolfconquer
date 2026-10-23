using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyP7SacredTreasuresMutationTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public async Task SavageBoneCostSkipsZeroStackAndDebitsOnlyPositiveUnboundRow(byte zeroBound)
    {
        var zero = Item(5, SavageBoneItemId, stackSize: 0, bound: zeroBound);
        var paid = Item(20, SavageBoneItemId, stackSize: 2);
        var unrelated = Item(30, 720_001);
        unrelated = unrelated.WithMaximumDurabilityAndPlus(unrelated.MaximumDurability, 3);
        unrelated.Signature = "No cost";
        unrelated.OwnerName = "Retained";
        var result = await InvokeAsync(10, Items(zero, paid, unrelated), Catalog(ItemType(WeaponSoulPackItemId)));
        var rows = result.UpdatedSession.PlayerItems.InventoryItems;

        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(20_000L, result.UpdatedSession.Character.ConquerPoints);
        AssertPlayerItemsEqual(Items(zero), Items(Assert.Single(rows, row => row.Uid == 5)));
        AssertPlayerItemsEqual(Items(paid.WithStackSize(1)), Items(Assert.Single(rows, row => row.Uid == 20)));
        AssertPlayerItemsEqual(Items(unrelated), Items(Assert.Single(rows, row => row.Uid == 30)));
        Assert.Equal(3, result.Sent.Count);
        var debit = AssertGameItem(result.Sent[0]);
        Assert.Equal(20U, debit.Uid);
        Assert.Equal((ushort)1, debit.StackSize);
        Assert.Equal((byte)0, debit.Bound);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        Assert.Equal("You received a P7 Weapon Soul Pack.", AssertMessageBox(result.Sent[1]).Text);
        Assert.Equal(WeaponSoulPackItemId, AssertGameItem(result.Sent[2]).ItemId);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Equal(20_000L, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)2)]
    public async Task SavageBoneCostKeepsPhysicalEligibleOrder(ushort firstStack)
    {
        var first = Item(20, SavageBoneItemId, firstStack);
        var second = Item(10, SavageBoneItemId);
        second.Signature = "Second eligible";
        var result = await InvokeAsync(10, Items(first, second), Catalog(ItemType(WeaponSoulPackItemId)));
        var rows = result.UpdatedSession.PlayerItems.InventoryItems;

        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(20_000L, result.UpdatedSession.Character.ConquerPoints);
        AssertPlayerItemsEqual(Items(second), Items(Assert.Single(rows, row => row.Uid == 10)));
        if (firstStack == 1)
        {
            Assert.DoesNotContain(rows, row => row.Uid == 20);
            AssertRemoval(result.Sent[0], 20);
        }
        else
        {
            AssertPlayerItemsEqual(Items(first.WithStackSize(1)), Items(Assert.Single(rows, row => row.Uid == 20)));
            var debit = AssertGameItem(result.Sent[0]);
            Assert.Equal(20U, debit.Uid);
            Assert.Equal((ushort)1, debit.StackSize);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        }
        Assert.Equal(3, result.Sent.Count);
        Assert.Equal("You received a P7 Weapon Soul Pack.", AssertMessageBox(result.Sent[1]).Text);
        Assert.Equal(WeaponSoulPackItemId, AssertGameItem(result.Sent[2]).ItemId);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Fact]
    public async Task ForeignBoneSingletonCannotFalselyFreeRewardCapacity()
    {
        var rows = new List<PlayerItemSnapshotItem> { Item(10, SavageBoneItemId, bound: 1), Item(20, SavageBoneItemId, stackSize: 2) };
        rows.AddRange(Enumerable.Range(100, 38).Select(uid => Item((uint)uid, (uint)(810_000 + uid))));
        var original = Items(rows.ToArray());
        var preimage = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var result = await InvokeAsync(10, original, Catalog(ItemType(WeaponSoulPackItemId)));

        AssertMutationFree(result, original, conquerPoints: 20_000);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(preimage, PlayerItemSnapshotLoader.ToLegacyBytes(result.UpdatedSession.PlayerItems));
        Assert.Empty(result.Sent);
        Assert.Contains("inventory-full-after-planned-Savage-Bone-removal", result.Log);
        var invocation = Assert.Single(Directory.EnumerateDirectories(_root));
        Assert.Equal(preimage, File.ReadAllBytes(Path.Combine(invocation, "PlayersItems", CharacterUid + ".bin")));
        AssertPlayerItemsEqual(original, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Equal(20_000L, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
    }

    [Fact]
    public async Task ZeroUnboundAndPositiveForeignBonesCannotAuthorizeExploration()
    {
        var original = Items(Item(10, SavageBoneItemId, stackSize: 0), Item(20, SavageBoneItemId, bound: 1));
        var result = await InvokeAsync(10, original, Catalog(ItemType(WeaponSoulPackItemId)));

        AssertMutationFree(result, original, conquerPoints: 20_000);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        AssertExactDialog(result.Sent, "Sorry, you don`t have a Savage Bone.", "My~bad.", avatarId: 200);
        AssertPlayerItemsEqual(original, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }
}
