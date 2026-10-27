using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Captures the actual mutable owners, not computed cooldown/score getters.
internal sealed class LegacyNativeSpellMutationWitness
{
    private readonly LegacyNativeSpellStorage storage;
    private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell>? map;
    private readonly KeyValuePair<ushort, LegacyNativeMsgSpell>[] entries;
    private readonly Dictionary<LegacyNativeMsgSpell, Dictionary<FieldInfo, object?>> rows = new(ReferenceEqualityComparer.Instance);

    internal LegacyNativeSpellMutationWitness(LegacyNativeSpellStorage storage)
    {
        this.storage = storage;
        map = storage.ClientSpells;
        entries = map?.ToArray() ?? [];
        foreach (var row in entries.Select(pair => pair.Value).Where(row => row is not null))
            rows.TryAdd(row, row.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .ToDictionary(field => field, field => field.GetValue(row)));
    }

    internal void RequireUnchanged()
    {
        Assert.Same(map, storage.ClientSpells);
        RequireEscapedMapUnchanged();
    }

    internal void RequireEscapedMapUnchanged()
    {
        if (map is null) return;
        var actual = map.ToArray(); // No comparer-dependent key lookups in the witness.
        Assert.Equal(entries.Select(pair => pair.Key), actual.Select(pair => pair.Key));
        for (var index = 0; index < entries.Length; index++) Assert.Same(entries[index].Value, actual[index].Value);
        foreach (var row in rows)
        foreach (var field in row.Value) Assert.Equal(field.Value, field.Key.GetValue(row.Key));
    }
}

internal sealed class LegacyNativePacketMutationWitness
{
    private readonly LegacyNativePacket packet;
    private readonly nint address;
    private readonly int size;
    private readonly int position;
    private readonly byte[] bytes;

    internal unsafe LegacyNativePacketMutationWitness(LegacyNativePacket packet)
    {
        this.packet = packet;
        address = (nint)packet.Memory;
        Assert.NotEqual(nint.Zero, address);
        size = packet.Size;
        position = packet.Position;
        bytes = new byte[packet.Capacity];
        Marshal.Copy(address, bytes, 0, bytes.Length);
    }

    internal unsafe void RequireUnchanged()
    {
        Assert.NotEqual(nint.Zero, (nint)packet.Memory);
        Assert.Equal(address, (nint)packet.Memory);
        Assert.Equal(size, packet.Size);
        Assert.Equal(position, packet.Position);
        var actual = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, actual, 0, actual.Length);
        Assert.Equal(bytes, actual);
    }
}
