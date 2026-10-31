using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneDecomposeTests
{
    private static async Task AssertDecomposeRefused(DecomposeFixture f, Func<Task> action, bool blocked = false)
    {
        var current = f.Current; var native = f.Native;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
        var image = binding.Items.Image; var spellMap = native.MySpells!.ClientSpells;
        var inventoryMap = native.Inventory!.ClientItems;
        var files = f.Data.Files(); var highWater = f.HighWater;
        var directories = Directory.GetDirectories(f.Data.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var itemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems);
        var attributes = current.Character.Attributes.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase).ToArray();
        var spells = spellMap.OrderBy(row => row.Key).Select(row =>
            (row.Key, row.Value.Level, row.Value.Experience, row.Value.PreviousLevel, row.Value.SoulLevel, row.Value.UseSpellSoul)).ToArray();
        var runes = binding.Items.Rune.Objects.OrderBy(row => row.Uid).Select(row =>
            (row.Uid, row.ITEM_ID, row.Position, row.StackSize, row.RuneEXP, row.Bound, row.Locked, row.Color)).ToArray();
        var inventory = inventoryMap.OrderBy(row => row.Key).Select(row =>
            (row.Key, row.Value.ITEM_ID, row.Value.Position, row.Value.StackSize, row.Value.Bound, row.Value.Locked, row.Value.Color)).ToArray();
        var vitals = (native.Player!.Money, native.Player.ConquerPoints, native.Player.HitPoints);
        var ranks = f.Rankings.Snapshot(); int first = f.Sent.Count;

        await action();

        Assert.Same(current, f.Current); Assert.Equal(first, f.Sent.Count);
        f.Data.AssertFiles(files); Assert.Equal(highWater, f.HighWater);
        Assert.Equal(directories, Directory.GetDirectories(f.Data.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.Equal(attributes, f.Current.Character.Attributes.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase));
        Assert.Same(native, f.Connection.NativeGameClient); Assert.Same(binding, native.OrdinarySpellBinding);
        Assert.Same(image, binding.Items.Image); Assert.Same(spellMap, native.MySpells.ClientSpells);
        Assert.Same(inventoryMap, native.Inventory.ClientItems);
        Assert.Equal(spells, native.MySpells.ClientSpells.OrderBy(row => row.Key).Select(row =>
            (row.Key, row.Value.Level, row.Value.Experience, row.Value.PreviousLevel, row.Value.SoulLevel, row.Value.UseSpellSoul)));
        Assert.Equal(runes, binding.Items.Rune.Objects.OrderBy(row => row.Uid).Select(row =>
            (row.Uid, row.ITEM_ID, row.Position, row.StackSize, row.RuneEXP, row.Bound, row.Locked, row.Color)));
        Assert.Equal(inventory, native.Inventory.ClientItems.OrderBy(row => row.Key).Select(row =>
            (row.Key, row.Value.ITEM_ID, row.Value.Position, row.Value.StackSize, row.Value.Bound, row.Value.Locked, row.Value.Color)));
        Assert.Equal(vitals, (native.Player.Money, native.Player.ConquerPoints, native.Player.HitPoints));
        Assert.Equal(ranks, f.Rankings.Snapshot()); Assert.Equal(blocked, f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
}
