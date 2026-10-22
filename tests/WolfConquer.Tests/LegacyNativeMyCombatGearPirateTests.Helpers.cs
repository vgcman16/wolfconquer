using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMyCombatGearPirateTests
{
    private static (LegacyNativeMyCombatGear Caller, LegacyNativeMyCombatGear Current) GearOwners(NativeSpellTestFixture fixture)
    {
        var current = new LegacyNativeMyCombatGear(fixture.Owner);
        fixture.Owner.CombatGear = current; // Explicit component prestate, not constructor/host birth.
        var caller = new LegacyNativeMyCombatGear(fixture.Owner) { Items = null! };
        return (caller, current); // IsPirate must enumerate user.CombatGear.Items, NOT caller.Items.
    }

    private static Gear.Weapon PirateWeapon() => new()
    {
        ItemID = Gear.TypeID.Pirate2,
        Level = 3,
        Equip = 0,
        BlackJades = [new Gear.BlackJade(71, 0)]
    };

    private static LegacyNativeDaoqiDictTypeTable.CQ Jade(uint type1, uint value1, uint type2 = 0, uint value2 = 0)
    {
        var row = new LegacyNativeDaoqiDictTypeTable.CQ
        {
            attr_type1 = type1, attr_value1 = value1, attr_type2 = type2, attr_value2 = value2
        };
        LegacyNativeDaoqiDictTypeTable.Info[71] = row;
        return row;
    }

    private static LegacyNativeCombatGearTable.CQ Skills(Gear.Weapon weapon, uint[]? values)
    {
        var row = new LegacyNativeCombatGearTable.CQ { Skills = values! };
        LegacyNativeCombatGearTable.Info[(uint)weapon.ItemID] = new() { [weapon.Level] = row };
        return row;
    }

    private static LegacyNativeMsgSpell Seed(LegacyNativeSpellStorage storage, ushort id)
    {
        var spell = new LegacyNativeMsgSpell { ID = id, Level = 5, Experience = 88, PreviousLevel = 9, SoulLevel = 2 };
        storage.ClientSpells[id] = spell; // Explicit actual native storage prestate; no Add/Send substitute.
        return spell;
    }

    private static (ushort Id, ushort Level) SpellFrame(byte[] frame)
    {
        Assert.Equal(25, frame.Length);
        Assert.Equal((ushort)25, BitConverter.ToUInt16(frame, 0));
        Assert.Equal((ushort)2101, BitConverter.ToUInt16(frame, 2));
        Assert.Equal(0, BitConverter.ToInt32(frame, 4));
        Assert.Equal((ushort)0, BitConverter.ToUInt16(frame, 10));
        Assert.Equal((ushort)0, BitConverter.ToUInt16(frame, 14));
        Assert.Equal(0u, BitConverter.ToUInt32(frame, 16));
        Assert.Equal((byte)0, frame[20]);
        Assert.Equal(0, BitConverter.ToInt32(frame, 21));
        return (BitConverter.ToUInt16(frame, 8), BitConverter.ToUInt16(frame, 12));
    }

    private static void Added(Queue<byte[]> queue, params (ushort Id, ushort Level)[] expected)
    {
        Assert.Equal(expected, queue.Select(SpellFrame).ToArray());
    }

    private static ushort Removed(byte[] frame, uint ownerUid)
    {
        Assert.Equal((ushort)frame.Length, BitConverter.ToUInt16(frame, 0));
        Assert.Equal((ushort)2046, BitConverter.ToUInt16(frame, 2));
        int offset = 4;
        var fields = new Dictionary<uint, ulong>();
        while (offset < frame.Length)
        {
            ulong tag = Varint(frame, ref offset);
            Assert.Equal(0ul, tag & 7u);
            Assert.True(fields.TryAdd((uint)(tag >> 3), Varint(frame, ref offset)));
        }
        Assert.Equal((ulong)ownerUid, fields[1]);
        Assert.Equal(109ul, fields[12]);
        foreach (uint field in new uint[] { 9, 13, 14, 15 }) Assert.Equal(0ul, fields[field]);
        Assert.All(fields.Keys, field => Assert.Contains(field, new uint[] { 1, 3, 9, 12, 13, 14, 15 }));
        return fields.TryGetValue(3, out var id) ? checked((ushort)id) : (ushort)0;
    }

    private static ulong Varint(byte[] bytes, ref int offset)
    {
        ulong value = 0;
        for (int shift = 0; shift < 64; shift += 7)
        {
            Assert.InRange(offset, 0, bytes.Length - 1);
            byte next = bytes[offset++];
            value |= (ulong)(next & 0x7F) << shift;
            if ((next & 0x80) == 0) return value;
        }
        throw new InvalidDataException("Unterminated owned removal-frame varint.");
    }
}
