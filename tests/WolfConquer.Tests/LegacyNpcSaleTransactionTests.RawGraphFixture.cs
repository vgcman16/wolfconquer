using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    private NativeGraphFault RefuseMalformedProposalThenCorruptNativeGraph(
        Owner owner, Player player, PlayerItemSnapshot malformed, string scenario)
    {
        var current = Current(owner, player);
        var client = player.Connection.NativeGameClient!;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
        var image = binding.Items.Image;
        var inventory = client.Inventory!; var sold = inventory.ClientItems[SoldUid];
        var character = File.ReadAllBytes(UserPath(player.Uid)); var items = File.ReadAllBytes(ItemPath(player.Uid));
        int first = player.Sent.Count;

        Assert.Throws<InvalidDataException>(() =>
            owner.Sessions.Set(player.Connection, current with { PlayerItems = malformed }));

        Assert.Same(current, Current(owner, player)); Assert.Same(image, binding.Items.Image);
        Assert.Same(sold, inventory.ClientItems[SoldUid]);
        Assert.Equal(character, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(items, File.ReadAllBytes(ItemPath(player.Uid))); Assert.Equal(first, player.Sent.Count);
        // Inject only an alias of the genuine admitted item into an actual raw
        // map. The Store never accepts a malformed canonical snapshot as setup.
        var bank = client.Warehouse!.ClientItems[1000];
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> map = scenario switch
        {
            "inventory-duplicate" => inventory.ClientItems,
            "warehouse-duplicate" => bank,
            "nested-duplicate" => Assert.Single(bank.Values).Deposite,
            _ => throw new InvalidOperationException("Unsupported owned native graph fault."),
        };
        uint key = scenario == "inventory-duplicate" ? SoldUid + 1000 : SoldUid;
        var fault = new NativeGraphFault(map, key, sold);
        Assert.False(map.ContainsKey(key)); Assert.True(map.TryAdd(key, sold));
        return fault;
    }

    private sealed class NativeGraphFault(
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> map,
        uint key, LegacyNativeGameItemRuntime item) : IDisposable
    {
        internal void AssertRetained()
        {
            Assert.True(map.TryGetValue(key, out var current)); Assert.Same(item, current);
            Assert.Equal(SoldUid, item.Uid);
        }
        internal void AssertRemoved() => Assert.False(map.ContainsKey(key));
        public void Dispose()
        {
            // Exact key/value removal leaves any foreign replacement alone and
            // cannot mask the primary test failure with a cleanup assertion.
            ((ICollection<KeyValuePair<uint, LegacyNativeGameItemRuntime>>)map).Remove(new(key, item));
        }
    }
}
