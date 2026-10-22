using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Storage = WolfConquer.Core.World.LegacyNativeAnimaSkinRuntime;

namespace WolfConquer.Tests;

// Complete AnimaSkin storage/record/send leaves plus explicit birth slot only.
// The DragonSkin.Process handler and ServerDatabase callers are not exercised.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeAnimaSkinTests
{
    private sealed class Records : IDisposable
    {
        private readonly CultureInfo culture = CultureInfo.CurrentCulture, ui = CultureInfo.CurrentUICulture;
        internal Storage Storage { get; } = new(null!);

        internal Records()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        internal Storage.Item Row(uint key, uint id, bool equipped)
        {
            var row = new Storage.Item { ID = id, isEquip = equipped };
            Assert.True(Storage.Items.TryAdd(key, row));
            return row;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = ui;
        }
    }

    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object oldBin = Bin.GetValue(null)!;
        private readonly TextWriter oldConsole = Console.Out;
        private readonly StringWriter console = new();
        private readonly List<LegacyNativePacket> packets = new();
        internal Records Records { get; } = new();
        internal NativeSpellTestFixture Native { get; } = new();
        internal Storage Storage => Records.Storage;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal int Available => LegacyNativePacketRecycle.Count;

        internal Scope()
        {
            Storage.user = Client;
            Console.SetOut(console);
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 3; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet);
                packets.Add(packet);
                bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
            Fill(Native.Packet);
        }

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
        }

        internal unsafe void Serialized(byte[] expected, int index = 0)
        {
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(packets[index]));
            Assert.Equal(expected.Length, packets[index].Size);
            Assert.Equal(0, packets[index].Position);
            Assert.Equal((byte)0xA5, packets[index].Memory[expected.Length]);
        }

        internal void Sent(byte[] expected, int index = 0)
        {
            Assert.Equal(expected, Queue.ElementAt(index));
            Serialized(expected, index);
        }

        internal unsafe void FaultDisposed()
        {
            Assert.Empty(Queue);
            Assert.Equal(2, Available);
            Assert.Equal(321, packets[0].Size);
            Assert.Equal(0, packets[0].Position); // Take happened, then the genuine using disposed it.
            Assert.Equal((byte)0xA5, packets[0].Memory[0]);
        }

        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            foreach (LegacyNativePacket packet in packets) packet.Dispose();
            Native.Dispose();
            Records.Dispose();
            Console.SetOut(oldConsole);
            console.Dispose();
        }
    }

    private static void RowIs(Storage.Item row, uint id, bool equipped)
    {
        Assert.Equal(id, row.ID);
        Assert.Equal(equipped, row.isEquip);
    }

    // Independent protobuf uint/positive enum writer: all four native fields
    // are optional. Default MyShow/zero IDs/false OK must not be made required.
    private static byte[] Frame(uint uid, uint skin = 0, bool restore = false)
    {
        var payload = new List<byte>();
        if (restore) Field(payload, 1, 5);
        if (uid != 0) Field(payload, 2, uid);
        if (skin != 0) Field(payload, 3, skin);
        if (restore) Field(payload, 4, 1);
        var bytes = new byte[payload.Count + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2415);
        payload.CopyTo(bytes, 4);
        return bytes;
    }

    private static void Field(List<byte> bytes, int tag, uint value)
    {
        bytes.Add((byte)(tag << 3));
        while (value > 127) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }
}
