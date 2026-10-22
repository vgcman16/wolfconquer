using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Owned component MODEL: no native socket Create/pump, DataCore birth, or host adoption.
internal sealed class NativeSpellTestFixture : IDisposable
{
    private static readonly FieldInfo SealField = typeof(LegacyNativePacket)
        .GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
    private readonly object? priorSeal;
    internal Queue<byte[]> Queue { get; } = new();
    internal LegacyNativeGameClientRuntime Owner { get; }
    internal LegacyNativeSpellStorage Storage { get; }
    internal LegacyNativePacket Packet { get; }

    internal NativeSpellTestFixture()
    {
        Packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
        priorSeal = SealField.GetValue(null);
        SealField.SetValue(null, null);
        Owner = CreateOwner(Queue);
        Owner.Player.UID = 17;
        Storage = new LegacyNativeSpellStorage(Owner);
        Owner.MySpells = Storage;
    }

    internal static LegacyNativeGameClientRuntime CreateOwner(Queue<byte[]> queue)
    {
        var socket = new LegacyNativeSecuritySocket(null!, null!) { Alive = true };
        typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(socket, queue);
        return new LegacyNativeGameClientRuntime { Socket = socket };
    }

    internal static unsafe byte[] Bytes(LegacyNativePacket packet)
    {
        var bytes = new byte[packet.Size];
        Marshal.Copy((IntPtr)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }

    internal static LegacyNativeMsgSpell ReadSpell(byte[] bytes)
    {
        Assert.Equal(25, bytes.Length);
        Assert.Equal((ushort)25, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal((ushort)2101, BitConverter.ToUInt16(bytes, 2));
        using var packet = new LegacyNativePacket(bytes); // Bounded25B in native1024B allocation.
        packet.Seek(4);
        packet.GetSpell(out var spell);
        Assert.Equal(25, packet.Position);
        return spell;
    }

    public void Dispose()
    {
        Packet.Dispose();
        SealField.SetValue(null, priorSeal);
    }
}
