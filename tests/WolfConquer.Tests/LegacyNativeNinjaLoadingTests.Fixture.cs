using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNinjaLoadingTests
{
    // Owned real component state only: no worker, socket pump, host/startup,
    // fake rank/spell provider, clock override or gameplay callback.
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previousBin = Bin.GetValue(null);
        private readonly LegacyNativeDbSafeDictionary<RankType, LegacyNativeNinjaRank.Rank> previousRanks = LegacyNativeNinjaRank.Ranks;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? previousTypes = LegacyNativeNinjaFile.gouyu_type;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.Item>? previousImmortals = LegacyNativeNinjaFile.gouyu_immortals;
        private readonly CultureInfo previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        private readonly TextWriter previousConsole = Console.Out;
        private readonly StringWriter console = new(CultureInfo.InvariantCulture);
        private readonly ConcurrentQueue<LegacyNativePacket> ownedBin = new();
        private readonly List<LegacyNativePacket> recycled = new();
        private readonly Dictionary<FieldInfo, object?> previousClientFields;
        private readonly Dictionary<FieldInfo, object?> previousPlayerFields;
        private readonly LegacyNativePlayerRuntime originalPlayer;
        private readonly ConcurrentDictionary<ushort, LegacyNativeMsgSpell> previousSpellRows;
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativeNinja Ninja { get; }
        internal LegacyNativeSpellStorage Spells => Native.Storage;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem> Metadata { get; } = new();
        internal int Available => LegacyNativePacketRecycle.Count;
        internal LegacyNativePacket Used(int index) => recycled[index];
        internal string Log => console.ToString();

        internal Scope()
        {
            previousClientFields = Capture(Client);
            originalPlayer = Client.Player;
            previousPlayerFields = Capture(originalPlayer);
            previousSpellRows = Spells.ClientSpells;
            Spells.ClientSpells = new();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(console);
            LegacyNativeNinjaFile.gouyu_type = Metadata;
            LegacyNativeNinjaFile.gouyu_immortals = new();
            LegacyNativeNinjaRank.Ranks = new();
            LegacyNativeNinjaRank.Create();
            Client.Equipment = new LegacyNativeEquipItemStorage(Client);
            Ninja = new LegacyNativeNinja(Client);
            Client.MyNinja = Ninja;
            SetClass(5000);
            for (int i = 0; i < 12; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet);
                recycled.Add(packet);
                ownedBin.Enqueue(packet);
            }
            Fill(Packet);
            RestoreOwnedBin();
        }

        private static Dictionary<FieldInfo, object?> Capture(object value) => value.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => !field.IsInitOnly).ToDictionary(field => field, field => field.GetValue(value));

        private static void Restore(object value, Dictionary<FieldInfo, object?> fields)
        {
            foreach (var pair in fields) pair.Key.SetValue(value, pair.Value);
        }

        internal void SetClass(uint value) => SetPlayerField("_class", value);
        internal void SetLevel(ushort value) => SetPlayerField("_level", value);
        internal void SetNinia(byte value) => SetPlayerField("_NiniaP0", value);
        internal void SetPlayerField(string name, object value) => typeof(LegacyNativePlayerRuntime)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Client.Player, value);
        internal void UseNullBin() => Bin.SetValue(null, null);
        internal void RestoreOwnedBin() => Bin.SetValue(null, ownedBin);

        internal LegacyNativeNinja.Item Item(uint key, uint id, uint level = 1, uint position = 0, uint exp = 0, uint uk = 0)
        {
            var row = new LegacyNativeNinja.Item { ItemID = id, Level = level, Position = position, Exp = exp, UK = uk };
            Assert.True(Ninja.Items.TryAdd(key, row));
            return row;
        }

        internal LegacyNativeNinjaFile.NinjaSprintItem Meta(uint key, uint id, uint level,
            uint score = 0, uint magic1 = 0, uint magic2 = 0)
        {
            var row = new LegacyNativeNinjaFile.NinjaSprintItem
            {
                ID = key, ItemID = id, Level = level, Score = score, magic1 = magic1, magic2 = magic2,
            };
            Metadata.Add(key, row);
            return row;
        }

        internal LegacyNativeMsgSpell Spell(ushort id, ushort level = 1)
        {
            var row = new LegacyNativeMsgSpell { ID = id, Level = level, Experience = 123 };
            Assert.True(Spells.ClientSpells.TryAdd(id, row));
            return row;
        }

        internal LegacyNativeNinjaRank.Rank Rank(RankType type) => LegacyNativeNinjaRank.Ranks[type];

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
        }

        internal unsafe void Serialized(int index, byte[] expected, bool untouchedTail = true)
        {
            LegacyNativePacket packet = Used(index);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(packet));
            Assert.Equal(expected.Length, packet.Size);
            Assert.Equal(0, packet.Position);
            if (untouchedTail) Assert.Equal((byte)0xA5, packet.Memory[expected.Length]);
        }

        internal unsafe void UnserializedDisposed(int index)
        {
            Assert.Equal(321, Used(index).Size);
            Assert.Equal(0, Used(index).Position);
            Assert.Equal((byte)0xA5, Used(index).Memory[0]);
        }

        internal unsafe void Untaken(int index)
        {
            Assert.Equal(321, Used(index).Size);
            Assert.Equal(237, Used(index).Position);
            Assert.Equal((byte)0xA5, Used(index).Memory[0]);
        }

        internal void Sent(params byte[][] expected)
        {
            Assert.Equal(expected.Length, Queue.Count);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], Queue.ElementAt(i));
        }

        public void Dispose()
        {
            // Exact former references/backing fields, not property setters that emit packets.
            Restore(originalPlayer, previousPlayerFields);
            Restore(Client, previousClientFields);
            Spells.ClientSpells = previousSpellRows;
            Bin.SetValue(null, previousBin);
            foreach (LegacyNativePacket packet in recycled) packet.Dispose();
            Native.Dispose(); // Restores the actual prior static packet seal.
            LegacyNativeNinjaRank.Ranks = previousRanks;
            LegacyNativeNinjaFile.gouyu_type = previousTypes!;
            LegacyNativeNinjaFile.gouyu_immortals = previousImmortals!;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            Console.SetOut(previousConsole);
            console.Dispose();
        }
    }

    private static readonly ushort[] NinjaSkills =
    [16310, 16320, 16330, 16340, 16350, 16360, 16370, 16380, 16390, 16400,
     16410, 16420, 16430, 16440, 16450, 16460, 16470, 16480, 16490, 16500,
     16510, 16520, 16530, 16540, 16550];

    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value > 127) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }

    private static void Field(List<byte> bytes, uint number, ulong value)
    {
        Varint(bytes, (ulong)number << 3);
        Varint(bytes, value);
    }

    private static byte[] Frame(ushort opcode, List<byte> payload)
    {
        byte[] bytes = new byte[payload.Count + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode);
        payload.CopyTo(bytes, 4);
        return bytes;
    }

    private static byte[] ItemFrame(LegacyNativeMsgNinja.MsgNinjaItem.TypeID type, uint uid,
        params (uint Id, uint Level, uint Position, uint Points, uint Uk)[] rows)
    {
        var payload = new List<byte>();
        Field(payload, 1, (uint)type);
        Field(payload, 2, uid);
        foreach (var row in rows)
        {
            var nested = new List<byte>();
            Field(nested, 1, row.Id); Field(nested, 2, row.Level); Field(nested, 3, row.Position);
            Field(nested, 4, row.Points); Field(nested, 5, row.Uk);
            Varint(payload, 26); Varint(payload, (ulong)nested.Count); payload.AddRange(nested);
        }
        return Frame(2393, payload);
    }

    private static byte[] UserFrame(params ulong[] values)
    {
        var payload = new List<byte>();
        for (uint number = 1; number <= 21; number++)
            Field(payload, number, number <= values.Length ? values[number - 1] : 0);
        return Frame(2395, payload);
    }

    private static byte[] LevelFrame(uint uid, byte value)
    {
        var payload = new List<byte>(); Field(payload, 1, uid);
        var nested = new List<byte>(); Field(nested, 1, 123);
        if (value != 0) Field(nested, 2, value);
        Varint(payload, 18); Varint(payload, (ulong)nested.Count); payload.AddRange(nested);
        return Frame(2176, payload);
    }

    private static byte[] RemoveFrame(uint uid, ushort id)
    {
        var payload = new List<byte>();
        Field(payload, 1, uid); Field(payload, 3, id); Field(payload, 9, 0);
        Field(payload, 12, 109); Field(payload, 13, 0); Field(payload, 14, 0); Field(payload, 15, 0);
        return Frame(2046, payload);
    }

    private static byte[] SpellFrame(ushort id, ushort level)
    {
        var frame = new byte[25];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 25);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2101);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8), id);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(12), level);
        return frame;
    }
}
