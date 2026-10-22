using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Mail = WolfConquer.Core.World.LegacyNativeMailList;
using Record = WolfConquer.Core.World.LegacyNativeTMailList;
using ListMessage = WolfConquer.Core.World.LegacyNativeCMsgMailList;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMailTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture, oldUi = CultureInfo.CurrentUICulture;
        private readonly TextWriter oldOutput = Console.Out;
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ConcurrentQueue<LegacyNativePacket> bin = new();
        private readonly List<LegacyNativePacket> recycled = new();
        private readonly string artifacts = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "native-mail-tests"));
        private string? directory;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal string Log => log.ToString();

        internal Scope()
        {
            // Player's actual initializer consumes Pool.GetRandom; isolate it before any birth.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            Native = new NativeSpellTestFixture();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(log);
            Client.MailList = new LegacyNativeDbSafeDictionary<uint, Mail>();
            Fill(Packet);
            for (int i = 0; i < 12; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet); recycled.Add(packet); bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
        }

        internal int Available => LegacyNativePacketRecycle.Count;
        internal LegacyNativePacket Recycled(int index) => recycled[index];
        internal void NullBin() => Bin.SetValue(null, null);
        internal void RestoreBin() => Bin.SetValue(null, bin);
        internal string OwnFiles(bool mailDirectory = true)
        {
            directory ??= Path.GetFullPath(Path.Combine(artifacts, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            if (mailDirectory) Directory.CreateDirectory(Path.Combine(directory, "PlayersMailList"));
            LegacyNativeServerConfig.DbLocation = directory;
            return directory;
        }
        internal string FileName => Path.Combine(OwnFiles(), "PlayersMailList", Client.Player.UID + ".bin");
        internal LegacyNativeGameClientRuntime Other(Queue<byte[]> queue)
        {
            var client = NativeSpellTestFixture.CreateOwner(queue);
            client.Player.UID = Client.Player.UID;
            client.MailList = new LegacyNativeDbSafeDictionary<uint, Mail>();
            return client;
        }

        internal unsafe void Input(byte[] bytes, int position = 4)
        {
            Fill(Packet);
            Marshal.Copy(bytes, 0, (IntPtr)Packet.Memory, bytes.Length);
            Packet.Size = bytes.Length; Packet.Seek(position);
        }

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0,
                (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321; packet.Seek(237);
        }

        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            foreach (var packet in recycled) packet.Dispose();
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativeServerConfig.DbLocation = oldLocation;
            CultureInfo.CurrentCulture = oldCulture; CultureInfo.CurrentUICulture = oldUi;
            Console.SetOut(oldOutput); log.Dispose();
            if (directory != null && Directory.Exists(directory))
            {
                string resolved = Path.GetFullPath(directory);
                if (!resolved.StartsWith(artifacts + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !Guid.TryParseExact(Path.GetFileName(resolved), "N", out _))
                    throw new InvalidOperationException("Refusing cleanup outside the owned mail fixture leaf.");
                Directory.Delete(resolved, true);
            }
        }
    }

    private static Mail Row(uint id = 1) => new()
    {
        id = id, title = "T", Sender_name = "S", content = "C", Expiration_Date = 60,
        Money = 2, EMoney = 3, itemid = 4, Emoney_Record_Type = 5, Flag = 6,
        TimeStamp = DateTime.Now.AddDays(2),
    };

    private static void Process(LegacyNativeGameClientRuntime client, LegacyNativePacket packet) =>
        ((Action<LegacyNativeGameClientRuntime, LegacyNativePacket>)typeof(ListMessage)
            .GetMethod("Process", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate(typeof(Action<LegacyNativeGameClientRuntime, LegacyNativePacket>)))(client, packet);

    private static byte[] Frame(ushort opcode, byte[] payload)
    {
        var frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), opcode);
        payload.CopyTo(frame, 4); return frame;
    }

    // Independent required-field bytes, not the production serializer/producer under test.
    private static byte[] MailFrame(int type, int count, params Mail[] rows)
    {
        using var payload = new MemoryStream();
        payload.WriteByte(8); Varint(payload, unchecked((ulong)(long)type));
        payload.WriteByte(16); Varint(payload, unchecked((ulong)(long)count));
        foreach (var row in rows)
        {
            using var item = new MemoryStream();
            ulong[] fields = { row.id, row.Money, row.EMoney, row.Expiration_Date, row.itemid, row.Emoney_Record_Type, row.Flag };
            for (int i = 0; i < fields.Length; i++) { item.WriteByte((byte)((i + 1) * 8)); Varint(item, fields[i]); }
            Text(item, 66, row.title); Text(item, 74, row.Sender_name);
            payload.WriteByte(26); Varint(payload, (ulong)item.Length); payload.Write(item.ToArray());
        }
        return Frame(2183, payload.ToArray());
    }

    private static void Text(Stream stream, byte tag, string? value)
    {
        if (value == null) return;
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        stream.WriteByte(tag); Varint(stream, (ulong)bytes.Length); stream.Write(bytes);
    }
    private static void Varint(Stream stream, ulong value)
    {
        while (value > 127) { stream.WriteByte((byte)((value & 127) | 128)); value >>= 7; }
        stream.WriteByte((byte)value);
    }

    private static unsafe byte[] RecordBytes(Record row)
    {
        var bytes = new byte[sizeof(Record)];
        Marshal.Copy((IntPtr)(&row), bytes, 0, bytes.Length); return bytes;
    }
    private static Record Stored(Mail row) => default(Record).GetMailList(row);
    private static byte[] FileBytes(int count, params byte[][] records)
    {
        var bytes = new byte[4 + records.Sum(r => r.Length)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
        int offset = 4;
        foreach (var record in records) { record.CopyTo(bytes, offset); offset += record.Length; }
        return bytes;
    }
}
