using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private const uint RepairDistributionMeteorId = 1_088_001;
    private const uint RepairDistributionMeteorTearId = 1_088_002;

    [Fact]
    public async Task HandlerZeroDurabilityRepairDistributesCostAcrossFiveMeteorStacks()
    {
        var itemsRoot = Path.Combine(Path.GetTempPath(), "wolf-repair-material-distribution-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(_usersRoot);
            Directory.CreateDirectory(itemsRoot);
            WriteSnapshot(1293600, uid: 1293600, money: 25);
            var target = new PlayerItemSnapshotItem
            {
                Uid = 2001,
                ItemId = 101_000,
                Position = 0,
                Durability = 0,
                MaximumDurability = 10,
            };
            var materials = Enumerable.Range(2002, 5)
                .Select(uid => RepairDistributionMaterial(checked((uint)uid), RepairDistributionMeteorId, 10))
                .ToArray();
            var initialItems = new PlayerItemSnapshot(
                new[] { target }.Concat(materials).ToArray(), [], [], [], [], []);
            new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(1293600, initialItems);
            using var log = new StringWriter();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ticket = handoff.Issue(1293600, "repair-distribution", ConquerRuntimeDefaults.WorldServer);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot, itemsRoot), handoff, log);
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
            var enteredItems = GetSession(handler, connection).PlayerItems;
            var enteredBytes = PlayerItemSnapshotLoader.ToLegacyBytes(enteredItems);
            var countBeforeRepair = sent.Count;

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgItemUsagePacket(
                    Id: target.Uid,
                    DwParam: 0,
                    Action: MsgItemUsageAction.RepairItem,
                    Timestamp: 1245,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes()));

            Assert.Equal(countBeforeRepair + 6, sent.Count);
            var debitUpdates = sent.Skip(countBeforeRepair).Take(5).Select(ParseLegacyGameItem).ToArray();
            Assert.Equal(
                materials.Select(item => item.Uid).OrderBy(uid => uid),
                debitUpdates.Select(item => item.Uid).OrderBy(uid => uid));
            Assert.All(debitUpdates, item =>
            {
                Assert.Equal(RepairDistributionMeteorId, item.ItemId);
                Assert.Equal((ushort)9, item.StackSize);
                Assert.Equal(LegacyGameItemPacket.UpdateItemMode, item.Mode);
            });
            var repairUpdate = ParseLegacyGameItem(sent[countBeforeRepair + 5]);
            Assert.Equal(target.Uid, repairUpdate.Uid);
            Assert.Equal(target.ItemId, repairUpdate.ItemId);
            Assert.Equal((ushort)10, repairUpdate.Durability);
            Assert.Equal((ushort)10, repairUpdate.MaximumDurability);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, repairUpdate.Mode);

            var session = GetSession(handler, connection);
            Assert.Equal(25, session.Character.Money);
            var repaired = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == target.Uid);
            Assert.Equal((ushort)10, repaired.Durability);
            Assert.Equal((ushort)10, repaired.MaximumDurability);
            Assert.Equal(6, session.PlayerItems.InventoryItems.Count);
            foreach (var material in materials)
            {
                var live = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == material.Uid);
                Assert.Equal(material.ItemId, live.ItemId);
                Assert.Equal((ushort)9, live.StackSize);
            }

            var persisted = new CharacterSnapshotLoader(_usersRoot, itemsRoot)
                .LoadPlayerItemsByCharacterUid(1293600);
            Assert.Equal(
                PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems),
                PlayerItemSnapshotLoader.ToLegacyBytes(persisted));
            Assert.Equal(enteredBytes, PlayerItemSnapshotLoader.ToLegacyBytes(enteredItems));
            Assert.Contains("handled MsgItemUsage zero-durability repair", log.ToString());
        }
        finally
        {
            if (Directory.Exists(itemsRoot))
            {
                Directory.Delete(itemsRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void RepairHelperDistributesOneDebitAcrossMixedMeteorAndTearEntries()
    {
        var materials = Enumerable.Range(1, 5)
            .Select(index => RepairDistributionMaterial(
                checked((uint)index),
                index % 2 == 0 ? RepairDistributionMeteorTearId : RepairDistributionMeteorId,
                10))
            .ToArray();
        var items = new PlayerItemSnapshot(materials, [], [], [], [], []);

        var (success, updated, frames) = ConsumeRepairDistributionCost(items, 5);

        Assert.True(success);
        Assert.Equal(5, updated.InventoryItems.Count);
        Assert.Equal(5, frames.Count);
        var updates = frames.Select(ParseLegacyGameItem).ToArray();
        Assert.Equal(
            materials.Select(item => item.Uid).OrderBy(uid => uid),
            updates.Select(item => item.Uid).OrderBy(uid => uid));
        foreach (var material in materials)
        {
            var actual = Assert.Single(updated.InventoryItems, item => item.Uid == material.Uid);
            var packet = Assert.Single(updates, item => item.Uid == material.Uid);
            Assert.Equal(material.ItemId, actual.ItemId);
            Assert.Equal(material.ItemId, packet.ItemId);
            Assert.Equal((ushort)9, actual.StackSize);
            Assert.Equal((ushort)9, packet.StackSize);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
            Assert.Equal((ushort)10, material.StackSize);
        }
    }

    [Fact]
    public void RepairHelperSingleLargeTearStackAllowsRepeatedPassesAndEveryIntermediateFrame()
    {
        var material = RepairDistributionMaterial(11, RepairDistributionMeteorTearId, 10);
        var items = new PlayerItemSnapshot([material], [], [], [], [], []);

        var (success, updated, frames) = ConsumeRepairDistributionCost(items, 5);

        Assert.True(success);
        var actual = Assert.Single(updated.InventoryItems);
        Assert.Equal(material.Uid, actual.Uid);
        Assert.Equal(material.ItemId, actual.ItemId);
        Assert.Equal((ushort)5, actual.StackSize);
        var updates = frames.Select(ParseLegacyGameItem).ToArray();
        Assert.Equal(new ushort[] { 9, 8, 7, 6, 5 }, updates.Select(item => item.StackSize));
        Assert.All(updates, item =>
        {
            Assert.Equal(material.Uid, item.Uid);
            Assert.Equal(RepairDistributionMeteorTearId, item.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, item.Mode);
        });
        Assert.Equal((ushort)10, material.StackSize);
    }

    [Fact]
    public void RepairHelperInsufficientCombinedAliasesRejectWithoutPublishingMutation()
    {
        var meteor = RepairDistributionMaterial(21, RepairDistributionMeteorId, 2);
        var tear = RepairDistributionMaterial(22, RepairDistributionMeteorTearId, 2);
        var unrelated = RepairDistributionMaterial(23, 1_000_000, 100);
        var items = new PlayerItemSnapshot([meteor, tear, unrelated], [], [], [], [], []);
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(items);

        var (success, updated, frames) = ConsumeRepairDistributionCost(items, 5);

        Assert.False(success);
        Assert.Same(items, updated);
        Assert.Empty(frames);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(items));
    }

    [Fact]
    public void RepairHelperTwoCompletePassesDebitEveryEligibleUidOncePerPass()
    {
        var materials = Enumerable.Range(31, 3)
            .Select(uid => RepairDistributionMaterial(checked((uint)uid), RepairDistributionMeteorId, 3))
            .ToArray();
        var items = new PlayerItemSnapshot(materials, [], [], [], [], []);

        var (success, updated, frames) = ConsumeRepairDistributionCost(items, 6);

        Assert.True(success);
        Assert.Equal(3, updated.InventoryItems.Count);
        Assert.Equal(6, frames.Count);
        var updates = frames.Select(ParseLegacyGameItem).ToArray();
        foreach (var material in materials)
        {
            var actual = Assert.Single(updated.InventoryItems, item => item.Uid == material.Uid);
            Assert.Equal((ushort)1, actual.StackSize);
            var perUid = updates.Where(packet => packet.Uid == material.Uid).ToArray();
            Assert.Equal(new ushort[] { 2, 1 }, perUid.Select(packet => packet.StackSize));
            Assert.All(perUid, packet =>
            {
                Assert.Equal(RepairDistributionMeteorId, packet.ItemId);
                Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
            });
            Assert.Equal((ushort)3, material.StackSize);
        }
    }

    [Fact]
    public void RepairHelperPreservesInputUnrelatedItemsContainersAndMaterialMetadata()
    {
        var material = new PlayerItemSnapshotItem
        {
            Uid = 41,
            ItemId = RepairDistributionMeteorId,
            StackSize = 10,
            Position = 0,
            Durability = 12,
            MaximumDurability = 34,
            Bound = 1,
            Plus = 5,
            OwnerUid = 56,
            OwnerName = "repair owner",
            Signature = "unchanged metadata",
        };
        // A consumable material cannot own child items. Keep container-preservation
        // coverage on an unrelated root; actual material-child refusals are separate.
        var unrelated = new PlayerItemSnapshotItem
        {
            Uid = 43, ItemId = 100_001, Position = 0, StackSize = 7,
            DepositedItems = [RepairDistributionMaterial(42, 100_000, 1)],
        };
        PlayerItemSnapshotItem[] relics = [RepairDistributionMaterial(51, RepairDistributionMeteorId, 10)];
        PlayerItemSnapshotItem[] mythsouls = [RepairDistributionMaterial(52, RepairDistributionMeteorTearId, 10)];
        PlayerItemSnapshotItem[] equipment = [RepairDistributionMaterial(53, RepairDistributionMeteorId, 10)];
        PlayerItemSnapshotItem[] runes = [RepairDistributionMaterial(54, RepairDistributionMeteorTearId, 10)];
        PlayerItemSnapshotItem[] wardrobe = [RepairDistributionMaterial(55, RepairDistributionMeteorId, 10)];
        PlayerItemSnapshotItem[] warehouse = [RepairDistributionMaterial(56, RepairDistributionMeteorTearId, 10)];
        var items = new PlayerItemSnapshot([material, unrelated], relics, mythsouls, equipment, runes, wardrobe)
        {
            WarehouseItems = warehouse,
        };
        var originalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(items);

        var (success, updated, frames) = ConsumeRepairDistributionCost(items, 5);

        Assert.True(success);
        Assert.Same(unrelated, updated.InventoryItems[1]);
        Assert.Same(relics, updated.RelicFuseItems);
        Assert.Same(mythsouls, updated.MythsoulBagItems);
        Assert.Same(equipment, updated.EquipmentItems);
        Assert.Same(runes, updated.RuneItems);
        Assert.Same(wardrobe, updated.WardrobeItems);
        Assert.Same(warehouse, updated.WarehouseItems);
        Assert.Equal(originalBytes, PlayerItemSnapshotLoader.ToLegacyBytes(items));
        var expected = items with { InventoryItems = new[] { material.WithStackSize(5), unrelated } };
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(updated));
        Assert.Same(material.DepositedItems, updated.InventoryItems[0].DepositedItems);
        Assert.Equal(5, frames.Count);
    }

    private static PlayerItemSnapshotItem RepairDistributionMaterial(uint uid, uint itemId, ushort stackSize) =>
        new() { Uid = uid, ItemId = itemId, Position = 0, StackSize = stackSize };

    private static (bool Success, PlayerItemSnapshot Updated, IReadOnlyList<byte[]> Frames)
        ConsumeRepairDistributionCost(PlayerItemSnapshot items, ushort count)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryConsumeLegacyMeteorRepairCost",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
            binder: null,
            types:
            [
                typeof(PlayerItemSnapshot),
                typeof(ushort),
                typeof(PlayerItemSnapshot).MakeByRefType(),
                typeof(IReadOnlyList<byte[]>).MakeByRefType(),
            ],
            modifiers: null);
        Assert.NotNull(method);
        object?[] arguments = [items, count, null, null];
        var success = Assert.IsType<bool>(method.Invoke(null, arguments));
        return (
            success,
            Assert.IsType<PlayerItemSnapshot>(arguments[2]),
            Assert.IsAssignableFrom<IReadOnlyList<byte[]>>(arguments[3]));
    }
}
