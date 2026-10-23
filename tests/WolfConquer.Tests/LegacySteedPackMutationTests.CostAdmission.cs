using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySteedPackMutationTests
{
    public static TheoryData<uint, uint, byte, bool, string> AdmittedSteedCostCases => new()
    {
        { Steed1PackNpcId, Steed1PackItemId, 2, true, "Sorry, you don`t have a Steed+1Pack." },
        { Steed1NpcId, Steed1ItemId, 1, false, "Sorry, you don`t have a Steed+1." },
        { RareSteedPack6NpcId, RareSteedPack6ItemId, 1, false, "Sorry, you don`t have a NobleSteedPack" },
    };

    [Theory]
    [MemberData(nameof(AdmittedSteedCostCases))]
    public async Task CostAdmissionSkipsZeroOfTheRequiredSteedBindingClass(uint npcId, uint packId,
        byte optionId, bool bound, string _)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var costBound = bound ? (byte)1 : (byte)0;
        var zero = CreateInventoryItem(10, packId, stackSize: 0, bound: costBound);
        var paid = CreateInventoryItem(20, packId, stackSize: 2, bound: costBound);
        var unrelated = CreateInventoryItem(30, 720_001);
        unrelated = unrelated.WithMaximumDurabilityAndPlus(unrelated.MaximumDurability, 3);
        unrelated.Signature = "No cost";
        unrelated.OwnerName = "Retained";
        var original = CreatePlayerItems(zero, paid, unrelated);
        var result = await InvokeAsync(npcId, optionId, original,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)), itemsRoot);
        var rows = result.UpdatedSession.PlayerItems.InventoryItems;

        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(CreatePlayerItems(zero), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 10)));
        AssertPlayerItemsEqual(CreatePlayerItems(paid.WithStackSize(1)), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 20)));
        AssertPlayerItemsEqual(CreatePlayerItems(unrelated), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 30)));
        Assert.Equal(bound ? 3 : 2, result.Sent.Count);
        if (bound) AssertBoundPackTalk(result.Sent[0], 1, optionId);
        var debit = AssertGameItem(result.Sent[bound ? 1 : 0]);
        Assert.Equal(20U, debit.Uid);
        Assert.Equal(costBound, debit.Bound);
        Assert.Equal((ushort)1, debit.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        var reward = AssertGameItem(result.Sent[^1]);
        Assert.Equal(GrantedSteedItemId, reward.ItemId);
        Assert.Equal(costBound, reward.Bound);
        AssertPersistedEquals(itemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Theory]
    [MemberData(nameof(AdmittedSteedCostCases))]
    public async Task CostAdmissionUsesFirstEligibleSteedRowRatherThanLowestUid(uint npcId, uint packId,
        byte optionId, bool bound, string _)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var costBound = bound ? (byte)1 : (byte)0;
        var first = CreateInventoryItem(20, packId, stackSize: 2, bound: costBound);
        var second = CreateInventoryItem(10, packId, bound: costBound);
        second.Signature = "Second eligible";
        var result = await InvokeAsync(npcId, optionId, CreatePlayerItems(first, second),
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)), itemsRoot);
        var rows = result.UpdatedSession.PlayerItems.InventoryItems;

        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(CreatePlayerItems(second), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 10)));
        AssertPlayerItemsEqual(CreatePlayerItems(first.WithStackSize(1)), CreatePlayerItems(Assert.Single(rows, row => row.Uid == 20)));
        Assert.Equal(bound ? 3 : 2, result.Sent.Count);
        var debit = AssertGameItem(result.Sent[bound ? 1 : 0]);
        Assert.Equal(20U, debit.Uid);
        Assert.Equal((ushort)1, debit.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        Assert.Equal(GrantedSteedItemId, AssertGameItem(result.Sent[^1]).ItemId);
        AssertPersistedEquals(itemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Theory]
    [MemberData(nameof(AdmittedSteedCostCases))]
    public async Task ForeignSteedSingletonCannotFalselyFreeCapacity(uint npcId, uint packId,
        byte optionId, bool bound, string _)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var costBound = bound ? (byte)1 : (byte)0;
        var wrongBound = bound ? (byte)0 : (byte)1;
        var rows = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, packId, bound: wrongBound),
            CreateInventoryItem(20, packId, stackSize: 2, bound: costBound),
        };
        rows.AddRange(Enumerable.Range(100, 38).Select(uid => CreateInventoryItem((uint)uid, (uint)(410_000 + uid))));
        var original = CreatePlayerItems(rows.ToArray());
        var preimage = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var result = await InvokeAsync(npcId, optionId, original,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)), itemsRoot);

        AssertMutationFree(result, original);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        Assert.Equal(preimage, PlayerItemSnapshotLoader.ToLegacyBytes(result.UpdatedSession.PlayerItems));
        AssertMessageBox(result.Sent, "Please make one more space in your inventory.");
        Assert.Contains("inventory-full-after-planned-pack-removal", result.Log);
        Assert.Equal(preimage, File.ReadAllBytes(Path.Combine(itemsRoot, CharacterUid + ".bin")));
        AssertPersistedEquals(itemsRoot, original);
    }

    [Theory]
    [MemberData(nameof(AdmittedSteedCostCases))]
    public async Task ZeroAndForeignSteedRowsCannotAuthorizeReward(uint npcId, uint packId,
        byte optionId, bool bound, string missingText)
    {
        var itemsRoot = CreatePlayerItemsRoot();
        var original = CreatePlayerItems(
            CreateInventoryItem(10, packId, stackSize: 0, bound: bound ? (byte)1 : (byte)0),
            CreateInventoryItem(20, packId, bound: bound ? (byte)0 : (byte)1));
        var result = await InvokeAsync(npcId, optionId, original,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)), itemsRoot);

        AssertMutationFree(result, original);
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        AssertMessageBox(result.Sent, missingText);
        AssertPersistedEquals(itemsRoot, original);
    }
}
