using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    private sealed class FlagMutationWitness
    {
        private static readonly FieldInfo State = typeof(LegacyNativeNinja)
            .GetField("persistenceState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly Scope scope;
        private readonly object state;
        private readonly PacketImage[] packets;

        internal FlagMutationWitness(Scope scope)
        {
            this.scope = scope;
            state = State.GetValue(scope.Ninja)!;
            packets = Enumerable.Range(0, 4).Select(index => new PacketImage(scope.Used(index)))
                .Append(new(scope.Packet)).ToArray();
        }

        internal void RequireUnchanged()
        {
            Assert.Same(state, State.GetValue(scope.Ninja));
            scope.NoSerialization();
            foreach (var packet in packets) packet.RequireUnchanged();
        }
    }

    private sealed class PacketImage
    {
        private readonly LegacyNativePacket packet;
        private readonly nint address;
        private readonly int size, position;
        private readonly byte[] bytes;

        internal unsafe PacketImage(LegacyNativePacket packet)
        {
            this.packet = packet; address = (nint)packet.Memory;
            size = packet.Size; position = packet.Position;
            bytes = new byte[packet.Capacity];
            Marshal.Copy(address, bytes, 0, bytes.Length);
        }

        internal unsafe void RequireUnchanged()
        {
            Assert.Equal(address, (nint)packet.Memory);
            Assert.Equal(size, packet.Size); Assert.Equal(position, packet.Position);
            Assert.Equal(bytes, new ReadOnlySpan<byte>(packet.Memory, packet.Capacity).ToArray());
        }
    }

    private static void InvokeFlagMutation(Scope scope, bool remove)
    {
        if (remove) scope.Ninja.RemoveFlag(1);
        else scope.Ninja.AddFlag(1);
    }
}
