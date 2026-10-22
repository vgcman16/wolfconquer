using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Storage = WolfConquer.Core.World.LegacyNativeCollectionStorageRuntime;

namespace WolfConquer.Tests;

// Complete collection record/storage/message leaves only. No Process handler,
// login birth, host persistence caller, socket pump or live client is exercised.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCollectionStorageTests
{
    private sealed class RecordScope : IDisposable
    {
        private readonly CultureInfo previous = CultureInfo.CurrentCulture;
        private readonly CultureInfo previousUi = CultureInfo.CurrentUICulture;
        internal Storage Storage { get; } = new(null!);

        internal RecordScope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object previousBin = Bin.GetValue(null)!;
        private readonly TextWriter previousConsole = Console.Out;
        private readonly StringWriter console = new();
        private readonly RecordScope record = new();
        private readonly List<LegacyNativePacket> recycled = new();
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal Storage Storage => record.Storage;
        internal int Available => LegacyNativePacketRecycle.Count;
        internal LegacyNativePacket Used(int index = 0) => recycled[index];

        internal Scope()
        {
            Storage.user = Client;
            Console.SetOut(console);
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 3; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet);
                recycled.Add(packet);
                bin.Enqueue(packet);
            }
            Fill(Native.Packet);
            Bin.SetValue(null, bin);
        }

        internal Storage.Scrap Scrap(uint key, uint type, uint amount)
        {
            var value = new Storage.Scrap { Type = type, Amount = amount };
            Assert.True(Storage.Scraps.TryAdd(key, value));
            return value;
        }

        internal Storage.Item Item(uint key, uint type, uint activate = 5,
            uint amount = 6, uint time = 0, uint f5 = 8)
        {
            var value = new Storage.Item
            {
                Type = type, Activate = activate, Amount = amount, RemainingTime = time, F5 = f5,
            };
            Assert.True(Storage.Items.TryAdd(key, value));
            return value;
        }

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
        }

        internal unsafe void Sent(byte[] expected, int index = 0)
        {
            Assert.Equal(expected, Queue.ElementAt(index));
            LegacyNativePacket packet = Used(index);
            Assert.Equal(expected.Length, packet.Size);
            Assert.Equal(0, packet.Position); // Native using-disposal runs before ToArray returns.
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(packet));
            Assert.Equal((byte)0xA5, packet.Memory[expected.Length]);
        }

        internal unsafe void NoSerialization()
        {
            Assert.Empty(Queue);
            Assert.Equal(3, Available);
            foreach (LegacyNativePacket packet in recycled)
            {
                Assert.Equal(321, packet.Size);
                Assert.Equal(237, packet.Position);
                Assert.Equal((byte)0xA5, packet.Memory[0]);
            }
        }

        public void Dispose()
        {
            Bin.SetValue(null, previousBin);
            foreach (LegacyNativePacket packet in recycled) packet.Dispose();
            Native.Dispose();
            record.Dispose();
            Console.SetOut(previousConsole);
            console.Dispose();
        }
    }

    private static void ItemIs(Storage.Item actual, uint type, uint activate,
        uint amount, uint time, uint f5)
    {
        Assert.Equal(type, actual.Type);
        Assert.Equal(activate, actual.Activate);
        Assert.Equal(amount, actual.Amount);
        Assert.Equal(time, actual.RemainingTime);
        Assert.Equal(f5, actual.F5);
    }

    // Independent uint/positive-enum protobuf writer. Root separately observed
    // the preserved serializer's required zero fields, nested rows and UserID
    // omission; production serializers are not used to generate expectations.
    private static byte[] Frame(uint identity,
        (uint Type, uint Amount)[]? scraps = null,
        (uint Type, uint Activate, uint Amount, uint Time, uint F5)[]? items = null)
    {
        var payload = new List<byte>();
        UInt(payload, 1, 0);
        UInt(payload, 2, identity);
        foreach (var scrap in scraps ?? [])
        {
            var nested = new List<byte>();
            UInt(nested, 1, scrap.Type);
            UInt(nested, 2, scrap.Amount);
            Nested(payload, 3, nested);
        }
        foreach (var item in items ?? [])
        {
            var nested = new List<byte>();
            UInt(nested, 1, item.Type);
            UInt(nested, 2, item.Activate);
            UInt(nested, 3, item.Amount);
            UInt(nested, 4, item.Time);
            UInt(nested, 5, item.F5);
            Nested(payload, 4, nested);
        }
        var frame = new byte[payload.Count + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2450);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static void Nested(List<byte> output, int tag, List<byte> nested)
    {
        output.Add((byte)((tag << 3) | 2));
        Varint(output, (uint)nested.Count);
        output.AddRange(nested);
    }

    private static void UInt(List<byte> output, int tag, uint value)
    {
        output.Add((byte)(tag << 3));
        Varint(output, value);
    }

    private static void Varint(List<byte> output, uint value)
    {
        while (value > 127)
        {
            output.Add((byte)((value & 127) | 128));
            value >>= 7;
        }
        output.Add((byte)value);
    }

    private static uint ReadVarint(byte[] bytes, ref int offset)
    {
        uint value = 0;
        int shift = 0;
        while (true)
        {
            byte next = bytes[offset++];
            value |= (uint)(next & 127) << shift;
            if ((next & 128) == 0) return value;
            shift += 7;
            Assert.True(shift < 35, "Owned collection uint exceeds five bytes.");
        }
    }

    private static int LocalSeconds() =>
        unchecked((int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds);

    private static void RemainingIsBetween(uint actual, uint expiration, int before, int after)
    {
        uint first = before < expiration ? unchecked((uint)(expiration - (long)before)) : 0;
        uint last = after < expiration ? unchecked((uint)(expiration - (long)after)) : 0;
        Assert.InRange(actual, Math.Min(first, last), Math.Max(first, last));
    }
}
