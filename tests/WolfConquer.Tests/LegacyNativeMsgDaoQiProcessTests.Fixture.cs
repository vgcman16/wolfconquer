using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using Dao = WolfConquer.Core.World.LegacyNativeMsgDaoQi;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMsgDaoQiProcessTests
{
    private sealed class Scope : IDisposable
    {
        private readonly bool load = GearTable.m_Load;
        private readonly Dictionary<uint, JadeTable.CQ>? rows = JadeTable.Info;
        private readonly Random? random = Gear.GetRandom;
        internal Gear User { get; } = new();

        internal Scope()
        {
            GearTable.m_Load = true;
            JadeTable.Info = new();
            Gear.GetRandom = new DrawRandom([], []);
        }

        internal static JadeTable.CQ Row(uint id, uint rune = 1, uint points = 0, uint rate = 10000,
            JadeTable.Type quality = JadeTable.Type.Refined, uint exp = 0)
        {
            var row = new JadeTable.CQ { ID = id, TypeRune = rune, points = points, rate = rate, Type = quality, Exp = exp };
            JadeTable.Info[id] = row;
            return row;
        }

        public void Dispose()
        {
            GearTable.m_Load = load;
            JadeTable.Info = rows!;
            Gear.GetRandom = random!;
        }
    }

    // Replacement of the genuine mutable Random field, never a production hook.
    // Finite queues fail immediately on an unexpected retry rather than allowing a hanging fixture.
    private sealed class DrawRandom : Random
    {
        private readonly Queue<int> selections;
        private readonly Queue<int> rates;
        internal List<(int Minimum, int Maximum)> Bounds { get; } = [];
        internal List<int> RateValues { get; } = [];
        internal DrawRandom(int[] selections, int[] rates)
        {
            this.selections = new(selections);
            this.rates = new(rates);
        }
        public override int Next(int minValue, int maxValue)
        {
            Bounds.Add((minValue, maxValue));
            if (selections.Count == 0) throw new InvalidOperationException("Unexpected native draw retry.");
            int value = selections.Dequeue();
            Assert.InRange(value, minValue, maxValue == minValue ? minValue : maxValue - 1);
            return value;
        }
        public override int Next()
        {
            if (rates.Count == 0) throw new InvalidOperationException("Unexpected native rate retry.");
            int value = rates.Dequeue();
            Assert.InRange(value, 0, int.MaxValue);
            RateValues.Add(value);
            return value;
        }
        internal void Drained() { Assert.Empty(selections); Assert.Empty(rates); }
    }

    private sealed record Message(Dictionary<int, ulong> Fields, uint[] Items, uint[] Positions);

    // Independent bounded reader for the exact registered DaoQi scalar/list contract.
    // Root's pinned-DLL tests, not this reader, establish serializer compatibility.
    private static Message Read(byte[] frame, int type)
    {
        Assert.InRange(frame.Length, 12, 4096);
        Assert.Equal(frame.Length - 8, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal((ushort)2451, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), frame[^8..]);
        var payload = frame[4..^8];
        int offset = 0;
        var fields = new Dictionary<int, ulong>();
        var items = new List<uint>();
        var positions = new List<uint>();
        while (offset < payload.Length)
        {
            ulong tag = Varint(payload, ref offset);
            int field = (int)(tag >> 3);
            if (field == 13)
            {
                Assert.Equal(2UL, tag & 7);
                int length = checked((int)Varint(payload, ref offset));
                int end = offset + length;
                Assert.InRange(end, offset, payload.Length);
                if (length == 0) items.Add(0); // Native null LIST entry is an empty submessage.
                else { Assert.Equal(8UL, Varint(payload, ref offset)); items.Add((uint)Varint(payload, ref offset)); }
                Assert.Equal(end, offset);
            }
            else
            {
                Assert.Equal(0UL, tag & 7);
                ulong value = Varint(payload, ref offset);
                if (field == 14) positions.Add((uint)value);
                else Assert.True(fields.TryAdd(field, value));
            }
        }
        Assert.Equal(Enumerable.Range(1, 12), fields.Keys.Order());
        Assert.Equal(unchecked((ulong)(long)type), fields[1]);
        return new(fields, items.ToArray(), positions.ToArray());
    }

    private static ulong Varint(byte[] bytes, ref int offset)
    {
        ulong value = 0;
        for (int shift = 0; shift < 70; shift += 7)
        {
            Assert.InRange(offset, 0, bytes.Length - 1);
            byte current = bytes[offset++];
            if (shift == 63) Assert.InRange(current, (byte)0, (byte)1);
            value |= (ulong)(current & 127) << shift;
            if ((current & 128) == 0) return value;
        }
        throw new InvalidOperationException("Malformed test frame.");
    }

    private static void SameMessage(Dao info, byte[] frame)
    {
        var actual = Read(frame, (int)info.Type);
        Assert.Equal(new ulong[] { unchecked((ulong)(long)(int)info.Type), info.ItemID, info.TaoistReiki, info.LOTTERY,
            info.Data5, info.Data6, info.MasteryPoints, info.Data8, info.PirateExp, info.Data10, info.Position, info.Done },
            Enumerable.Range(1, 12).Select(key => actual.Fields[key]));
        Assert.Equal(info.Items?.Select(item => item?.Id ?? 0) ?? [], actual.Items);
        Assert.Equal(info.Positions ?? [], actual.Positions);
    }

    private static Gear.Weapon Weapon(Gear user, uint key = 201)
    {
        var item = new Gear.Weapon { ItemID = Gear.TypeID.Bloodlust };
        Assert.True(user.Items.TryAdd((Gear.TypeID)key, item)); // Lookup uses the key, not mutable ItemID.
        return item;
    }

    private static Gear.BlackJade Jade(Gear user, uint key, uint type)
    {
        var item = new Gear.BlackJade(type, 77);
        Assert.True(user.BlackJadeBag.TryAdd(key, item));
        return item;
    }
}
