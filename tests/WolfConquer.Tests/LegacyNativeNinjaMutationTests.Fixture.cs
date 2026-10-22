using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Genuine Ninja/message/packet/queued socket owners only; no login, rank, socket
// pump, RNG override, row provider, packet dispatcher or host caller is installed.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNinjaMutationTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previousBin = Bin.GetValue(null);
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? previousTypes = LegacyNativeNinjaFile.gouyu_type;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.Item>? previousImmortals = LegacyNativeNinjaFile.gouyu_immortals;
        private readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        private readonly TextWriter previousConsole = Console.Out;
        private readonly StringWriter console = new(CultureInfo.InvariantCulture);
        private readonly ConcurrentQueue<LegacyNativePacket> ownedBin = new();
        private readonly List<LegacyNativePacket> recycled = new();
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeNinja Ninja { get; }
        internal Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem> Metadata { get; } = new();
        internal int Available => LegacyNativePacketRecycle.Count;
        internal string Log => console.ToString();
        internal LegacyNativePacket Used(int index = 0) => recycled[index];

        internal Scope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(console);
            LegacyNativeNinjaFile.gouyu_type = Metadata;
            LegacyNativeNinjaFile.gouyu_immortals = new();
            Ninja = new LegacyNativeNinja(Client);
            Client.MyNinja = Ninja;
            for (int index = 0; index < 4; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet);
                recycled.Add(packet);
                ownedBin.Enqueue(packet);
            }
            Fill(Packet);
            RestoreOwnedBin();
        }

        internal void UseNullBin() => Bin.SetValue(null, null);
        internal void RestoreOwnedBin() => Bin.SetValue(null, ownedBin);

        internal LegacyNativeNinjaFile.NinjaSprintItem Meta(uint key, uint id, uint level, uint score)
        {
            var row = new LegacyNativeNinjaFile.NinjaSprintItem
            {
                ID = key, ItemID = id, Level = level, Score = score,
            };
            Metadata.Add(key, row);
            return row;
        }

        internal LegacyNativeNinja.Item Item(uint key, uint id, uint level = 1)
        {
            var row = new LegacyNativeNinja.Item { ItemID = id, Level = level };
            Assert.True(Ninja.Items.TryAdd(key, row));
            return row;
        }

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
        }

        internal unsafe void Serialized(byte[] expected, int index = 0, bool disposed = true)
        {
            Assert.InRange(expected.Length, 4, LegacyNativePacket.MAX_SIZE - 1);
            LegacyNativePacket packet = Used(index);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(packet));
            Assert.Equal(expected.Length, packet.Size);
            Assert.Equal(disposed ? 0 : 4, packet.Position);
            Assert.Equal((byte)0xA5, packet.Memory[expected.Length]);
        }

        internal void Sent(byte[] expected, int index = 0)
        {
            Assert.Equal(expected, Queue.ElementAt(index));
            Serialized(expected, index);
        }

        internal unsafe void NoSerialization()
        {
            Assert.Empty(Queue);
            Assert.Equal(4, Available);
            foreach (LegacyNativePacket packet in recycled)
            {
                Assert.Equal(321, packet.Size);
                Assert.Equal(237, packet.Position);
                Assert.Equal((byte)0xA5, packet.Memory[0]);
            }
        }

        internal unsafe void FaultDisposed(int index = 0)
        {
            Assert.Empty(Queue);
            Assert.Equal(3 - index, Available);
            LegacyNativePacket packet = Used(index);
            Assert.Equal(321, packet.Size);
            Assert.Equal(0, packet.Position);
            Assert.Equal((byte)0xA5, packet.Memory[0]);
        }

        public void Dispose()
        {
            Bin.SetValue(null, previousBin);
            foreach (LegacyNativePacket packet in recycled) packet.Dispose();
            Native.Dispose(); // Restores the exact prior static packet seal.
            LegacyNativeNinjaFile.gouyu_type = previousTypes!;
            LegacyNativeNinjaFile.gouyu_immortals = previousImmortals!;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            Console.SetOut(previousConsole);
            console.Dispose();
        }
    }

    // Independent positive scalar protobuf writer for all21 required native fields.
    // This is expected behavior, not a call through the production codec/serializer.
    private static byte[] Frame(params ulong[] values)
    {
        Assert.InRange(values.Length, 0, 21);
        var payload = new List<byte>();
        for (int field = 1; field <= 21; field++)
        {
            Varint(payload, (ulong)field << 3);
            Varint(payload, field <= values.Length ? values[field - 1] : 0);
        }
        byte[] frame = new byte[4 + payload.Count];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2395);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value > 127)
        {
            bytes.Add((byte)((value & 127) | 128));
            value >>= 7;
        }
        bytes.Add((byte)value);
    }
}
