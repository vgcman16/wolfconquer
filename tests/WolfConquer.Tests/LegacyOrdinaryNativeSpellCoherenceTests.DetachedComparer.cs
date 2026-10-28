using System.Collections.Concurrent;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// These exercise actual unbound native map preparation. Ordinary admission constructs
// its own default comparer, so these are not claims about a custom joined owner.
public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public Task DetachedNativePreparationRetainsTheActualOriginalComparer() => NativePacketTestThread.Run(() =>
    {
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 77;
        var storage = new LegacyNativeSpellStorage(client); client.MySpells = storage;
        var comparer = new ProbeComparer();
        storage.ClientSpells = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(comparer);
        Assert.True(storage.ClientSpells.TryAdd(6001, new LegacyNativeMsgSpell { ID = 6001, Level = 4 }));
        using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
        storage.Add(packet, 6002, 2, 1, 0, 8);
        Assert.Same(comparer, storage.ClientSpells.Comparer);
        Assert.Equal(new ushort[] { 6001, 6002 }, storage.ClientSpells.ToArray().Select(p => p.Key).Order());
        Assert.True(comparer.Calls > 0);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DetachedNativePreparationRejectsThrowingOrReentrantComparer(bool reenter) => NativePacketTestThread.Run(() =>
    {
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 77;
        var storage = new LegacyNativeSpellStorage(client); client.MySpells = storage;
        var comparer = new ProbeComparer(); var map = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(comparer);
        storage.ClientSpells = map;
        var row = new LegacyNativeMsgSpell { ID = 6001, Level = 4 }; Assert.True(map.TryAdd(6001, row));
        using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
        comparer.Action = reenter ? () => storage.Add(packet, 6003) : () => throw new InvalidOperationException("owned comparer fault");
        comparer.Armed = true;
        Assert.ThrowsAny<Exception>(() => storage.Add(packet, 6002));
        Assert.True(comparer.Triggered); Assert.Same(map, storage.ClientSpells);
        var raw = Assert.Single(map.ToArray()); Assert.Equal((ushort)6001, raw.Key); Assert.Same(row, raw.Value);
    });

    private sealed class ProbeComparer : IEqualityComparer<ushort>
    {
        internal int Calls;
        internal bool Armed, Triggered;
        internal Action? Action;
        public bool Equals(ushort left, ushort right) => left == right;
        public int GetHashCode(ushort value)
        {
            Calls++;
            if (Armed) { Armed = false; Triggered = true; Action!(); }
            return value.GetHashCode();
        }
    }
}
