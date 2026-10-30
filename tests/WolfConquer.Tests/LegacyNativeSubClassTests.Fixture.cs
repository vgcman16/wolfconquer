using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Metadata = WolfConquer.Core.Data.LegacyNativeSubProfessionInfo;
using Sort = WolfConquer.Core.Data.LegacyNativeDbLevExp.Sort;
using SubClass = WolfConquer.Core.World.LegacyNativeSubClassRuntime;
using Row = WolfConquer.Core.World.LegacyNativeMsgSubClass.SubClases;

namespace WolfConquer.Tests;

// Actual subclass/profile/status/study leaves. Equipment-dependent SetPhrase,
// SetLevelss, receiver dispatch and full character birth remain untested/absent.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeSubClassTests
{
    private sealed class State : IDisposable
    {
        private readonly Metadata previous = LegacyNativePoolSubClassInfo.SubClassInfo;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture, ui = CultureInfo.CurrentUICulture;
        private readonly TextWriter console = Console.Out;
        private readonly StringWriter output = new();
        internal Metadata Metadata { get; } = new();
        internal SubClass Runtime { get; } = new();

        internal State()
        {
            LegacyNativePoolSubClassInfo.SubClassInfo = Metadata;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(output);
        }

        internal Row Row(Sort key, Sort id, byte level = 3, byte phrase = 2)
        {
            var value = new Row { ID = id, Level = level, Phrase = phrase };
            Assert.True(Runtime.src.TryAdd(key, value));
            return value;
        }

        internal uint[] Damage(Sort type, byte phrase, params uint[] damage)
        {
            if (!Metadata.ContainsKey(type)) Metadata.Add(type, new());
            Metadata[type].Add(phrase, new Metadata.ProfessionInfo { Damage = damage });
            return damage;
        }

        public void Dispose()
        {
            LegacyNativePoolSubClassInfo.SubClassInfo = previous;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = ui;
            Console.SetOut(console);
            output.Dispose();
        }
    }

    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object previousBin = Bin.GetValue(null)!;
        private readonly List<LegacyNativePacket> chatPackets = new();
        internal State State { get; } = new();
        internal NativeSpellTestFixture Native { get; } = new();
        internal SubClass Runtime => State.Runtime;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;

        internal Scope(bool bindStudy = false, int chatCapacity = LegacyNativePacket.MAX_SIZE)
        {
            Client.Status = new LegacyNativeMsgStatus();
            if (bindStudy) Client.Player.SubClass = Runtime;
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 2; index++)
            {
                var packet = new LegacyNativePacket(chatCapacity);
                Fill(packet);
                chatPackets.Add(packet);
                bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
            Fill(Packet);
        }

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray(),
                0, (IntPtr)packet.Memory, packet.Capacity);
            packet.Size = Math.Min(321, packet.Capacity);
            packet.Seek(Math.Min(237, packet.Capacity));
        }

        internal void PoolContains(params LegacyNativePacket[] packets) =>
            Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>(packets));

        internal void MissingPool() => Bin.SetValue(null, null);

        internal void Frames(params byte[][] expected)
        {
            byte[][] actual = Queue.ToArray();
            Assert.Equal(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
        }

        internal unsafe void CallerIs(byte[] expected)
        {
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(Packet));
            Assert.Equal(4, Packet.Position);
            Assert.Equal(expected.Length, Packet.Size);
            Assert.Equal((byte)0xA5, Packet.Memory[expected.Length]);
        }

        internal unsafe void Unwritten()
        {
            Assert.Empty(Queue);
            Assert.Equal(321, Packet.Size);
            Assert.Equal(237, Packet.Position);
            Assert.Equal((byte)0xA5, Packet.Memory[0]);
        }

        internal void ChatWasDisposed(byte[] expected)
        {
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(chatPackets[0]));
            Assert.Equal(0, chatPackets[0].Position);
            Assert.Equal(1, LegacyNativePacketRecycle.Count);
        }

        public void Dispose()
        {
            Bin.SetValue(null, previousBin);
            foreach (var packet in chatPackets) packet.Dispose();
            Native.Dispose();
            State.Dispose();
        }
    }

    private static void RowIs(Row row, Sort id, byte level, byte phrase)
    {
        Assert.Equal(id, row.ID); Assert.Equal(level, row.Level); Assert.Equal(phrase, row.Phrase);
    }

    private static byte[] Single(ushort action, byte type, byte level)
    {
        var bytes = new byte[26];
        U16(bytes, 0, 26); U16(bytes, 2, 2033); U16(bytes, 4, action);
        bytes[6] = type; bytes[7] = level;
        return bytes;
    }

    private static byte[] ListFrame(ushort action, ushort study,
        params (byte ID, byte Phrase, byte Level)[] rows)
    {
        var bytes = new byte[26 + 3 * rows.Length];
        U16(bytes, 0, (ushort)bytes.Length); U16(bytes, 2, 2033); U16(bytes, 4, action); U16(bytes, 6, study);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), rows.Length);
        int cursor = 26;
        foreach (var row in rows)
        {
            bytes[cursor++] = row.ID; bytes[cursor++] = row.Phrase; bytes[cursor++] = row.Level;
        }
        return bytes;
    }

    private static byte[] Effect(uint uid = 17)
    {
        var bytes = new byte[20];
        U16(bytes, 0, 20); U16(bytes, 2, 2036);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
        bytes[8] = 10; bytes[9] = 1; U16(bytes, 10, 8);
        "zf2-e300"u8.CopyTo(bytes.AsSpan(12));
        return bytes;
    }

    // Independent required-field protobuf expectation, anchored by Root's pinned
    // MsgTalkProto controls. No native encoder/serializer builds expected bytes.
    private static byte[] StudyMessage(string amount)
    {
        var payload = new List<byte>();
        for (int tag = 1; tag <= 10; tag++)
        {
            payload.Add((byte)(tag << 3));
            uint value = tag switch { 1 => 0x00FF0000, 2 => 2000, 8 => 1, _ => 0 };
            Varint(payload, value);
        }
        foreach (string content in new[] { "SYSTEM", "ALL", "", "You received " + amount + " StudyPoints.", "", "", "" })
        {
            byte[] text = Encoding.UTF8.GetBytes(content);
            payload.Add(0x72); Varint(payload, (uint)text.Length); payload.AddRange(text);
        }
        var frame = new byte[payload.Count + 4];
        U16(frame, 0, (ushort)frame.Length); U16(frame, 2, 2179);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static void Varint(List<byte> bytes, uint value)
    {
        while (value > 127) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }

    private static void U16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
