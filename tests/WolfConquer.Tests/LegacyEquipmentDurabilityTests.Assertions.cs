using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyEquipmentDurabilityTests
{
    private sealed record NativeRow(uint Key, object Row, uint Uid, uint Id, ushort Position,
        ushort Durability, ushort Maximum, byte Bound, byte Locked);
    private sealed record NativeImage(object Player, object? Inventory, object? Equipment, object? InventoryMap,
        object? EquipmentMap, NativeRow[] InventoryRows, NativeRow[] EquipmentRows, bool Checked, int Hitpoints, ushort Mana);

    private static NativeImage CaptureNative(EquipmentFixture f) => new(f.Native.Player, f.Native.Inventory, f.Native.Equipment,
        f.Native.Inventory?.ClientItems, f.Native.Equipment?.ClientItems,
        Rows(f.Native.Inventory?.ClientItems), Rows(f.Native.Equipment?.ClientItems),
        f.Native.Player.IsCheckedPass, f.Native.Player.HitPoints, f.Native.Player.Mana);
    private static NativeRow[] Rows(ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>? map) =>
        map is null ? [] : map.ToArray().OrderBy(pair => pair.Key).Select(pair => new NativeRow(pair.Key, pair.Value,
            pair.Value.Uid, pair.Value.ITEM_ID, pair.Value.Position, pair.Value.Durability,
            pair.Value.MaximDurability, pair.Value.Bound, pair.Value.Locked)).ToArray();

    private static void AssertNativeUnchanged(EquipmentFixture f, NativeImage before)
    {
        var after = CaptureNative(f);
        Assert.Same(before.Player, after.Player);
        Assert.Same(before.Inventory, after.Inventory); Assert.Same(before.Equipment, after.Equipment);
        Assert.Same(before.InventoryMap, after.InventoryMap); Assert.Same(before.EquipmentMap, after.EquipmentMap);
        Assert.Equal(before.InventoryRows, after.InventoryRows); Assert.Equal(before.EquipmentRows, after.EquipmentRows);
        Assert.Equal(before.Checked, after.Checked); Assert.Equal(before.Hitpoints, after.Hitpoints); Assert.Equal(before.Mana, after.Mana);
    }

    private static void AssertCommittedBeforeOutput(EquipmentFixture f, byte[] expectedItems, byte[] characterBefore)
    {
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
        Assert.False(Monitor.IsEntered(LegacySecondaryPasswordRuntime.SyncRoot));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.Equal(expectedItems, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(characterBefore, File.ReadAllBytes(f.CharacterPath));
        var disk = f.ReadItems();
        Assert.Equal(disk.InventoryItems.Select(row => (row.Uid, row.ItemId, row.Position, row.Bound, row.Locked)).OrderBy(row => row.Uid),
            f.Current.PlayerItems.InventoryItems.Select(row => (row.Uid, row.ItemId, row.Position, row.Bound, row.Locked)).OrderBy(row => row.Uid));
        Assert.Equal(disk.EquipmentItems.Select(row => (row.Uid, row.ItemId, row.Position, row.Bound, row.Locked)).OrderBy(row => row.Uid),
            f.Current.PlayerItems.EquipmentItems.Select(row => (row.Uid, row.ItemId, row.Position, row.Bound, row.Locked)).OrderBy(row => row.Uid));
        Assert.Same(f.Native, f.Current.NativeGameClient);
    }
}
