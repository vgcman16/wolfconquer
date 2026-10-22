using System.Buffers.Binary;
using System.Text;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

// A component-only cache fixture; no native metadata loader, player, socket or host is installed.
internal sealed class NativeCombatGearMutationScope : IDisposable
{
    private readonly bool load = GearTable.m_Load;
    private readonly Dictionary<uint, Dictionary<uint, GearTable.CQ>>? rows = GearTable.Info;
    internal Gear User { get; } = new();

    internal NativeCombatGearMutationScope()
    {
        GearTable.m_Load = true;
        GearTable.Info = new();
    }

    internal static GearTable.CQ Row(uint id, uint level, uint exp)
    {
        var row = new GearTable.CQ { ID = id + 1000, Level = level, Exp = exp };
        if (!GearTable.Info.TryGetValue(id, out var levels)) GearTable.Info[id] = levels = new();
        levels[level] = row;
        return row;
    }

    internal Gear.Weapon Weapon(uint key, uint id, uint equip = 1, uint exp = 23, uint transform = uint.MaxValue)
    {
        var weapon = new Gear.Weapon { ItemID = (Gear.TypeID)id, Equip = equip, Exp = exp, Transform = transform };
        User.Items[(Gear.TypeID)key] = weapon;
        return weapon;
    }

    // Independent bounded field reader; no production serializer or builder supplies expectations.
    internal static Dictionary<int, ulong> ReadAdded(byte[] frame, uint user)
    {
        Assert.InRange(frame.Length, 12, 512);
        Assert.Equal(frame.Length - 8, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal((ushort)2420, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal("TQServer", Encoding.ASCII.GetString(frame[^8..]));
        int position = 4;
        Assert.Equal(8UL, Varint(frame, ref position));
        Assert.Equal(2UL, Varint(frame, ref position));
        Assert.Equal(16UL, Varint(frame, ref position));
        Assert.Equal((ulong)user, Varint(frame, ref position));
        Assert.Equal(26UL, Varint(frame, ref position));
        int length = checked((int)Varint(frame, ref position));
        int end = position + length;
        Assert.Equal(frame.Length - 8, end);
        var fields = new Dictionary<int, ulong>();
        while (position < end)
        {
            ulong tag = Varint(frame, ref position);
            Assert.Equal(0UL, tag & 7);
            Assert.True(fields.TryAdd((int)(tag >> 3), Varint(frame, ref position)));
        }
        Assert.Equal(end, position);
        Assert.Equal(Enumerable.Range(1, 12), fields.Keys);
        return fields;
    }

    private static ulong Varint(byte[] bytes, ref int position)
    {
        ulong value = 0;
        for (int shift = 0; shift < 70; shift += 7)
        {
            Assert.InRange(position, 4, bytes.Length - 9);
            byte current = bytes[position++];
            if (shift == 63) Assert.InRange(current, (byte)0, (byte)1);
            value |= (ulong)(current & 127) << shift;
            if ((current & 128) == 0) return value;
        }
        throw new InvalidOperationException("Malformed test observation.");
    }

    public void Dispose()
    {
        GearTable.m_Load = load;
        GearTable.Info = rows!;
    }
}
