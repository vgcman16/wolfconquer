using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPhoenixCupServicesMutationTests
{
    [Theory]
    [InlineData((byte)1, false)]
    [InlineData((byte)1, true)]
    [InlineData((byte)2, false)]
    [InlineData((byte)2, true)]
    public async Task SpiritCostKeepsForeignAndZeroRowsOutsideEligibleRoundRobin(
        byte foreignBound, bool foreignFirst)
    {
        var foreign = Item(5, SpiritItemId, stackSize: 2, bound: foreignBound)
            .WithMaximumDurabilityAndPlus(4321, 7).WithSocketProgress(123456);
        foreign.OwnerName = "foreign";
        foreign.Signature = "preserve";
        var zero = Item(10, SpiritItemId, stackSize: 0).WithSocketProgress(654321);
        var first = Item(20, SpiritItemId, stackSize: 3);
        var second = Item(30, SpiritItemId, stackSize: 3);
        var unrelated = Item(40, 720_001).WithMaximumDurabilityAndPlus(5432, 8);
        var original = foreignFirst
            ? Items(foreign, zero, first, second, unrelated)
            : Items(first, second, foreign, zero, unrelated);
        var preservedRows = new[] { foreign, zero, unrelated }.Select(item =>
            (item.Uid, Bytes: PlayerItemSnapshotLoader.ToLegacyBytes(Items(item)))).ToArray();
        var result = await InvokeAsync(6, original, Catalog(FragmentType(SilverFragmentItemId)),
            rewardRoll: 1, rewardAmount: 2);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(20_000, result.UpdatedSession.Character.ConquerPoints);
        foreach (var preserved in preservedRows)
        {
            Assert.Equal(preserved.Bytes, PlayerItemSnapshotLoader.ToLegacyBytes(Items(Assert.Single(
                result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == preserved.Uid))));
        }
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20);
        Assert.Equal(1, Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 30).StackSize);
        Assert.Equal(7, result.Sent.Count);
        AssertItemUpdate(result.Sent[0], 20, 2);
        AssertItemUpdate(result.Sent[1], 30, 2);
        AssertItemUpdate(result.Sent[2], 20, 1);
        AssertItemUpdate(result.Sent[3], 30, 1);
        AssertRemoval(result.Sent[4], 20);
        AssertSystemTalk(result.Sent[5], "You received SilverPhoenixCupFragment*2!");
        var reward = AssertGameItem(result.Sent[6]);
        Assert.Equal(SilverFragmentItemId, reward.ItemId);
        Assert.Equal((ushort)2, reward.StackSize);
        Assert.Equal((byte)0, reward.Bound);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, reward.Mode);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Theory]
    [InlineData(20U, 10U)]
    [InlineData(10U, 20U)]
    public async Task SpiritCostPreservesEligiblePhysicalRoundRobinInsteadOfUidSorting(
        uint firstUid, uint secondUid)
    {
        var original = Items(Item(firstUid, SpiritItemId, stackSize: 3), Item(secondUid, SpiritItemId, stackSize: 3));
        var result = await InvokeAsync(6, original, Catalog(FragmentType(GoldenFragmentItemId)),
            rewardRoll: 2, rewardAmount: 1);

        Assert.Equal(7, result.Sent.Count);
        AssertItemUpdate(result.Sent[0], firstUid, 2);
        AssertItemUpdate(result.Sent[1], secondUid, 2);
        AssertItemUpdate(result.Sent[2], firstUid, 1);
        AssertItemUpdate(result.Sent[3], secondUid, 1);
        AssertRemoval(result.Sent[4], firstUid);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == firstUid);
        Assert.Equal(1, Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == secondUid).StackSize);
        AssertSystemTalk(result.Sent[5], "You received GoldenPhoenixCupFragment*1!");
        Assert.Equal(GoldenFragmentItemId, AssertGameItem(result.Sent[6]).ItemId);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    [InlineData((ushort)3)]
    [InlineData((ushort)4)]
    public async Task SpiritCostRefusesInsufficientEligibleStockDespiteForeignAndZeroRows(ushort eligibleCount)
    {
        var original = Items(Item(5, SpiritItemId, stackSize: 100, bound: 1),
            Item(10, SpiritItemId, stackSize: 0), Item(20, SpiritItemId, stackSize: eligibleCount));
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var result = await InvokeAsync(6, original, Catalog(FragmentType(SilverFragmentItemId)));

        AssertMutationFree(result, original, conquerPoints: 20_000);
        AssertExactDialogText(result.Sent,
            "You should give me a Spirit of Legend or 15,000 CPs before trying to make a phoenix cup.");
        AssertPlayerItemsEqual(original, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        var invocationRoot = Assert.Single(Directory.EnumerateDirectories(_root));
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(result.UpdatedSession.PlayerItems));
        Assert.Equal(before, File.ReadAllBytes(
            Path.Combine(invocationRoot, "PlayersItems", CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin")));
        Assert.Equal(20_000, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
        Assert.DoesNotContain(result.Sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
    }
}
