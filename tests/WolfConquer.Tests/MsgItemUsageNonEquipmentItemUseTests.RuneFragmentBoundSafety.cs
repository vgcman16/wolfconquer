using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Theory]
    [InlineData(3_306_366U, 0, 1)]
    [InlineData(3_306_369U, 1, 0)]
    public async Task HandlerRuneFragmentConsumesOnlyMatchingBoundStacksEvenWhenOtherTypeSortsFirst(
        uint fragmentId, byte requiredBound, byte otherBound)
    {
        const uint otherUid = 5_338_100;
        const uint matchingUid = 5_338_101;
        const uint rewardId = 4_012_001;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(otherUid, fragmentId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 25, bound: otherBound),
            PlayerItemSnapshotLoaderTests.DbItem(matchingUid, fragmentId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 25, bound: requiredBound));
        var worldData = CreateWorldDataCatalog(
            CreateItemType(fragmentId, "RedRuneFragment", 1),
            CreateItemType(rewardId, "BoundSafetyRune", 610));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);
        var original = GetSession(handler, connection).PlayerItems;
        var other = Assert.Single(original.InventoryItems, row => row.Uid == otherUid);
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        int before = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(matchingUid).ToFrameBytes()));

        Assert.Equal(before + 22, sent.Count);
        var debits = sent.Skip(before).Take(20).Select(ParseRuneMixedBoundItem).ToArray();
        Assert.All(debits, row =>
        {
            Assert.Equal(matchingUid, row.Uid);
            Assert.Equal(requiredBound, row.Bound);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, row.Mode);
        });
        Assert.Equal(Enumerable.Range(5, 20).Reverse().Select(value => (ushort)value),
            debits.Select(row => row.StackSize));
        var rewardPacket = ParseRuneMixedBoundItem(sent[before + 20]);
        Assert.Equal(rewardId, rewardPacket.ItemId);
        Assert.Equal(requiredBound, rewardPacket.Bound);
        Assert.Equal((ushort)610, rewardPacket.Durability);
        Assert.Equal((ushort)1, rewardPacket.StackSize);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[before + 21]), out var message));
        Assert.Contains("You have got 1 BoundSafetyRune" + (requiredBound == 0 ? "!." : "(B)!."), message.Content);
        var updated = GetSession(handler, connection).PlayerItems;
        Assert.Same(other, Assert.Single(updated.InventoryItems, row => row.Uid == otherUid));
        Assert.Equal((ushort)25, other.StackSize);
        Assert.Equal((ushort)5, Assert.Single(updated.InventoryItems, row => row.Uid == matchingUid).StackSize);
        Assert.Equal(3, updated.InventoryItems.Count);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(updated),
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600)));
    }

    [Theory]
    [InlineData(3_306_366U, 0, 1)]
    [InlineData(3_306_369U, 1, 0)]
    public async Task HandlerRuneFragmentCannotSubstituteOtherBoundForMissingTwentiethUnit(
        uint fragmentId, byte requiredBound, byte otherBound)
    {
        const uint otherUid = 5_338_110;
        const uint matchingUid = 5_338_111;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(otherUid, fragmentId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 25, bound: otherBound),
            PlayerItemSnapshotLoaderTests.DbItem(matchingUid, fragmentId, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 19, bound: requiredBound));
        var worldData = CreateWorldDataCatalog(
            CreateItemType(fragmentId, "RedRuneFragment", 1),
            CreateItemType(4_012_001, "BoundSafetyRune", 610));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);
        var original = GetSession(handler, connection);
        byte[] saved = File.ReadAllBytes(Path.Combine(_itemsRoot, "1293600.bin"));
        int before = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(matchingUid).ToFrameBytes()));

        Assert.Same(original, GetSession(handler, connection));
        Assert.Equal(before + 1, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[before]), out var message));
        Assert.Contains("You don't have 20 RedRuneFragment" + (requiredBound == 0 ? "!." : "(B)!."), message.Content);
        Assert.Equal(saved, File.ReadAllBytes(Path.Combine(_itemsRoot, "1293600.bin")));
        Assert.Equal(saved, PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems));
    }
}
