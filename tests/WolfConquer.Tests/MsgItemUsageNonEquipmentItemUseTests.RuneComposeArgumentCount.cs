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
        uint declaredCount, uint[] offeredUids, uint[] selectedUids, uint expectedItemId,
        uint expectedExperience, bool expectSerializedChange = true)
    {
        var catalog = WriteRuneComposePrefixScenario();
        await WithOwnedRuneComposeAsync(catalog, async f =>
        {
            const uint actor = 1_293_600; var before = f.Current(actor); var original = before.PlayerItems;
            Assert.Equal(3, original.InventoryItems.Count); Assert.Equal(2, original.RuneItems.Count);
            Assert.Single(original.EquipmentItems); Assert.Single(original.WarehouseItems);
            Assert.Empty(original.RelicFuseItems); Assert.Empty(original.MythsoulBagItems); Assert.Empty(original.WardrobeItems);
            var target = Assert.Single(original.RuneItems, row => row.Uid == RuneComposePrefixTargetUid);
            Assert.Equal(RuneComposePrefixTargetId, target.ItemId); Assert.Equal(0U, target.RuneExperience);
            Assert.Equal((ushort)102, target.Position); Assert.Equal((ushort)100, target.Durability); Assert.Equal((ushort)100, target.MaximumDurability);
            foreach (var materialUid in new[] { RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialBUid })
            {
                var material = Assert.Single(original.InventoryItems, row => row.Uid == materialUid);
                Assert.Equal(RuneComposePrefixMaterialId, material.ItemId); Assert.Equal((ushort)0, material.Position);
                Assert.Equal(materialUid == RuneComposePrefixMaterialAUid ? (ushort)45 : (ushort)75, material.StackSize);
                Assert.Equal(0U, material.RuneExperience); Assert.Equal((byte)0, material.Plus); Assert.Equal((byte)0, material.Locked);
            }
            var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original); Assert.Equal(originalBytes, File.ReadAllBytes(f.ItemPath(actor)));
            var expectedTarget = target.WithItemId(expectedItemId).WithRuneExperience(expectedExperience);
            var expected = original with
            {
                InventoryItems = original.InventoryItems.Where(row => !selectedUids.Contains(row.Uid)).ToArray(),
                RuneItems = original.RuneItems.Select(row => row.Uid == RuneComposePrefixTargetUid ? expectedTarget : row).ToArray(),
            };
            var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
            var files = OwnedComposeExpectedFiles(f, actor, expectedBytes); var sent = f.Frames(actor); int first = sent.Count, observed = 0;
            f.BeforeSend = (uid, _) => { Assert.Equal(actor, uid); AssertOwnedComposeConverged(f, actor, files); observed++; };
            try { await f.SendAsync(actor, CreateRuneComposePrefixUse(declaredCount, offeredUids)); } finally { f.BeforeSend = null; }
            Assert.Equal(selectedUids.Length + 1, observed); var output = sent.Skip(first).ToArray(); Assert.Equal(observed, output.Length);
            for (int index = 0; index < selectedUids.Length; index++)
            {
                var removal = new MsgItemUsagePacket(selectedUids[index], 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0);
                AssertItemUsage(output[index], removal); Assert.Equal(OwnedComposeRemoveGolden(selectedUids[index]), output[index]);
            }
            Assert.Equal(OwnedComposeUpdateGolden(expectedTarget), output[^1]);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(output[^1]), out var update));
            Assert.Equal(RuneComposePrefixTargetUid, update.Uid); Assert.Equal(expectedItemId, update.ItemId);
            Assert.Equal(expectedExperience, update.RuneExperience); Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
            Assert.Equal(1_416_000U + expectedItemId % 100 - 1, update.SpellId); Assert.Equal(LegacyGameItemPacket.NoEffect, update.Effect);
            var current = f.Current(actor); AssertRuneUntouchedSessionFields(before, current); AssertOwnedComposeConverged(f, actor, files);
            Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Equal(expectSerializedChange, !originalBytes.AsSpan().SequenceEqual(expectedBytes));
            var reloaded = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(actor); Assert.NotSame(current.PlayerItems, reloaded);
            Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(reloaded));
            Assert.Equal(expected.InventoryItems.Select(row => row.Uid), reloaded.InventoryItems.Select(row => row.Uid));
            Assert.Equal(expected.RuneItems.Select(row => row.Uid), reloaded.RuneItems.Select(row => row.Uid));
            Assert.Equal(expected.EquipmentItems.Select(row => row.Uid), reloaded.EquipmentItems.Select(row => row.Uid));
            Assert.Equal(expected.WarehouseItems.Select(row => row.Uid), reloaded.WarehouseItems.Select(row => row.Uid));
            Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
            AssertOwnedComposeUidRetirement(originalBytes, expectedBytes, selectedUids);
        }, 1_293_600);
    }

    private WorldDataCatalog WriteRuneComposePrefixScenario()
    {
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                RuneComposePrefixMaterialAUid, RuneComposePrefixMaterialId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 45),
            PlayerItemSnapshotLoaderTests.DbItem(
                RuneComposePrefixMaterialBUid, RuneComposePrefixMaterialId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 75),
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
                CreateRuneComposePrefixExperience(2, 402, 2, 500),
                CreateRuneComposePrefixExperience(3, 402, 3, 1_300),
                CreateRuneComposePrefixExperience(4, 402, 4, 2_500),
                CreateRuneComposePrefixExperience(5, 404, 1, 10),
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
