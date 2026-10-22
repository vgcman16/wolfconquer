using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const uint RuneComposePrefixTargetUid = 5_348_700;
    private const uint RuneComposePrefixMaterialAUid = 5_348_701;
    private const uint RuneComposePrefixMaterialBUid = 5_348_702;
    private const uint RuneComposePrefixMissingUid = 5_348_799;
    private const uint RuneComposePrefixTargetId = 4_020_501;
    private const uint RuneComposePrefixMaterialId = 4_040_001;

    [Fact]
    public async Task HandlerRuneComposeDeclaredOneIgnoresValidSurplusAndReloads()
    {
        await AssertRuneComposePrefixSuccessAsync(
            declaredCount: 1,
            offeredUids: [RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialBUid],
            selectedUids: [RuneComposePrefixMaterialAUid],
            expectedItemId: RuneComposePrefixTargetId + 1,
            expectedExperience: 50);
    }

    [Fact]
    public async Task HandlerRuneComposeDeclaredOneIgnoresMissingSurplusAndReloads()
    {
        await AssertRuneComposePrefixSuccessAsync(
            declaredCount: 1,
            offeredUids: [RuneComposePrefixMaterialAUid, RuneComposePrefixMissingUid],
            selectedUids: [RuneComposePrefixMaterialAUid],
            expectedItemId: RuneComposePrefixTargetId + 1,
            expectedExperience: 50);
    }

    [Fact]
    public async Task HandlerRuneComposeDeclaredTwoConsumesBothAndReloads()
    {
        await AssertRuneComposePrefixSuccessAsync(
            declaredCount: 2,
            offeredUids: [RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialBUid],
            selectedUids: [RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialBUid],
            expectedItemId: RuneComposePrefixTargetId + 2,
            expectedExperience: 0);
    }

    private async Task AssertRuneComposePrefixSuccessAsync(
        uint declaredCount,
        uint[] offeredUids,
        uint[] selectedUids,
        uint expectedItemId,
        uint expectedExperience,
        bool expectSerializedChange = true)
    {
        var catalog = WriteRuneComposePrefixScenario();
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, catalog);
        var originalSession = GetSession(handler, connection);
        var original = originalSession.PlayerItems;
        Assert.Equal(1_293_600L, originalSession.Character.Uid);
        Assert.Equal(3, original.InventoryItems.Count);
        Assert.Equal(2, original.RuneItems.Count);
        Assert.Single(original.EquipmentItems);
        Assert.Single(original.WarehouseItems);
        Assert.Empty(original.RelicFuseItems);
        Assert.Empty(original.MythsoulBagItems);
        Assert.Empty(original.WardrobeItems);
        var originalTarget = Assert.Single(original.RuneItems, item => item.Uid == RuneComposePrefixTargetUid);
        Assert.Equal(RuneComposePrefixTargetId, originalTarget.ItemId);
        Assert.Equal((uint)0, originalTarget.RuneExperience);
        Assert.Equal((ushort)102, originalTarget.Position);
        Assert.Equal((ushort)100, originalTarget.Durability);
        Assert.Equal((ushort)100, originalTarget.MaximumDurability);
        foreach (var materialUid in new[] { RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialBUid })
        {
            var material = Assert.Single(original.InventoryItems, item => item.Uid == materialUid);
            Assert.Equal(RuneComposePrefixMaterialId, material.ItemId);
            Assert.Equal((ushort)0, material.Position);
            Assert.Equal((ushort)1, material.StackSize);
            Assert.Equal((uint)0, material.RuneExperience);
            Assert.Equal((byte)0, material.Plus);
            Assert.Equal((byte)0, material.Locked);
        }

        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original);
        var path = Path.Combine(
            _itemsRoot, originalSession.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        // Take the physical baseline after entry has completed its normal loading/normalization.
        var baselineBytes = File.ReadAllBytes(path);
        Assert.Equal(originalBytes, baselineBytes);
        var expectedTarget = originalTarget.WithItemId(expectedItemId).WithRuneExperience(expectedExperience);
        var expected = original with
        {
            InventoryItems = original.InventoryItems.Where(item => !selectedUids.Contains(item.Uid)).ToArray(),
            RuneItems = original.RuneItems
                .Select(item => item.Uid == RuneComposePrefixTargetUid ? expectedTarget : item)
                .ToArray(),
        };
        var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneComposePrefixUse(declaredCount, offeredUids)));

        var outgoing = sent.Skip(countBeforeRequest).ToArray();
        Assert.NotEmpty(outgoing);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(outgoing[^1]), out var targetPacket));
        Assert.Equal(RuneComposePrefixTargetUid, targetPacket.Uid);
        Assert.Equal(expectedItemId, targetPacket.ItemId);
        Assert.Equal(expectedExperience, targetPacket.RuneExperience);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, targetPacket.Mode);
        Assert.Equal((uint)0, targetPacket.SpellId);
        Assert.Equal(LegacyGameItemPacket.NoEffect, targetPacket.Effect);
        Assert.Equal(selectedUids.Length + 1, outgoing.Length);
        // Managed send order is each selected material removal, followed by the target UPDATE.
        for (var index = 0; index < selectedUids.Length; index++)
        {
            var expectedRemoval = new MsgItemUsagePacket(
                selectedUids[index], DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0);
            AssertItemUsage(outgoing[index], expectedRemoval);
            Assert.Equal(expectedRemoval.ToFrameBytes(), outgoing[index]);
        }
        var expectedUpdate = LegacyGameItemPacket.FromSnapshotItem(
            expectedTarget,
            new LegacyGameItemPacket.RuneSpellOverride(0, LegacyGameItemPacket.NoEffect),
            LegacyGameItemPacket.UpdateItemMode);
        Assert.Equal(expectedUpdate.ToFrameBytes(), outgoing[^1]);

        var updatedSession = GetSession(handler, connection);
        var updated = updatedSession.PlayerItems;
        Assert.NotSame(originalSession, updatedSession);
        Assert.NotSame(original, updated);
        Assert.Equal(originalSession with { PlayerItems = updated }, updatedSession);
        Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(updated));
        foreach (var item in expected.InventoryItems)
        {
            Assert.Same(item, Assert.Single(updated.InventoryItems, candidate => candidate.Uid == item.Uid));
        }
        foreach (var item in original.RuneItems.Where(item => item.Uid != RuneComposePrefixTargetUid))
        {
            Assert.Same(item, Assert.Single(updated.RuneItems, candidate => candidate.Uid == item.Uid));
        }
        Assert.Same(original.EquipmentItems, updated.EquipmentItems);
        Assert.Same(original.WarehouseItems, updated.WarehouseItems);
        Assert.Same(original.RelicFuseItems, updated.RelicFuseItems);
        Assert.Same(original.MythsoulBagItems, updated.MythsoulBagItems);
        Assert.Same(original.WardrobeItems, updated.WardrobeItems);
        foreach (var uid in selectedUids)
        {
            Assert.DoesNotContain(updated.InventoryItems, item => item.Uid == uid);
        }

        var finalBytes = File.ReadAllBytes(path);
        Assert.Equal(expectedBytes, finalBytes);
        Assert.Equal(expectSerializedChange, !baselineBytes.AsSpan().SequenceEqual(finalBytes));
        // This is a new physical loader; the test never saves or invokes a persistence callback.
        var reloaded = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(originalSession.Character.Uid);
        Assert.NotSame(updated, reloaded);
        Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloaded));
        Assert.Equal(expected.InventoryItems.Select(item => item.Uid), reloaded.InventoryItems.Select(item => item.Uid));
        Assert.Equal(expected.RuneItems.Select(item => item.Uid), reloaded.RuneItems.Select(item => item.Uid));
        Assert.Equal(expected.EquipmentItems.Select(item => item.Uid), reloaded.EquipmentItems.Select(item => item.Uid));
        Assert.Equal(expected.WarehouseItems.Select(item => item.Uid), reloaded.WarehouseItems.Select(item => item.Uid));
        Assert.Same(original, originalSession.PlayerItems);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
        Assert.Contains("handled MsgItemUsage compose rune", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage compose rune", log.ToString());
    }

    private WorldDataCatalog WriteRuneComposePrefixScenario()
    {
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 1),
            PlayerItemSnapshotLoaderTests.DbItem(
                RuneComposePrefixMaterialBUid, RuneComposePrefixMaterialId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 1),
            PlayerItemSnapshotLoaderTests.DbItem(
                5_348_703, 1_088_000, position: 0,
                durability: 73, maximumDurability: 100, stackSize: 7, color: 6, bound: 1),
            PlayerItemSnapshotLoaderTests.DbItem(
                5_348_705, 500_109, position: 4,
                durability: 82, maximumDurability: 100, stackSize: 1, color: 5),
            PlayerItemSnapshotLoaderTests.DbItem(
                RuneComposePrefixTargetUid, RuneComposePrefixTargetId, position: 102,
                durability: 100, maximumDurability: 100, stackSize: 1),
            PlayerItemSnapshotLoaderTests.DbItem(
                5_348_704, 4_030_101, position: 211,
                durability: 81, maximumDurability: 100, stackSize: 1, runeExperience: 17, bound: 1),
            PlayerItemSnapshotLoaderTests.DbItem(
                5_348_706, 1_088_001, position: 0, warehouseId: 6,
                durability: 83, maximumDurability: 100, stackSize: 3, color: 4, bound: 1));

        return CreateWorldDataCatalogWithRuneLevelExperience(
            [
                CreateRuneComposePrefixExperience(1, 402, 1, 100),
                CreateRuneComposePrefixExperience(2, 402, 2, 300),
                CreateRuneComposePrefixExperience(3, 402, 3, 600),
                CreateRuneComposePrefixExperience(4, 402, 4, 1_000),
                CreateRuneComposePrefixExperience(5, 404, 1, 250),
            ],
            CreateItemType(RuneComposePrefixTargetId, "PrefixBlueRune1", durability: 100),
            CreateItemType(RuneComposePrefixTargetId + 1, "PrefixBlueRune2", durability: 100),
            CreateItemType(RuneComposePrefixTargetId + 2, "PrefixBlueRune3", durability: 100),
            CreateItemType(RuneComposePrefixMaterialId, "PrefixRuneEssence", durability: 100),
            CreateItemType(1_088_000, "UnrelatedMeteor", durability: 100),
            CreateItemType(4_030_101, "UnrelatedStoredRune", durability: 100),
            CreateItemType(500_109, "UnrelatedBow", durability: 100),
            CreateItemType(1_088_001, "UnrelatedWarehouseItem", durability: 100));
    }

    private static RuneLevelExperience CreateRuneComposePrefixExperience(
        int number, int family, int level, long experience)
    {
        var fields = new[]
        {
            number.ToString(CultureInfo.InvariantCulture),
            family.ToString(CultureInfo.InvariantCulture),
            level.ToString(CultureInfo.InvariantCulture),
            experience.ToString(CultureInfo.InvariantCulture),
            "",
        };
        return new RuneLevelExperience(
            number, RuneType: family, Level: level, Experience: experience,
            SourceFile: "fixture://rune-compose-prefix/rune_levexp.txt", LineNumber: number,
            RawLine: string.Join("@@", fields), Fields: fields);
    }

    private static byte[] CreateRuneComposePrefixUse(uint declaredCount, params uint[] offeredUids)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + (offeredUids.Length * sizeof(uint)));
        writer.WriteUInt32(0, RuneComposePrefixTargetUid);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.ComposeRune);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, declaredCount);
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < offeredUids.Length; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), offeredUids[index]);
        }
        return writer.ToArray();
    }
}
