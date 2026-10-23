using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearMessageTests
{
    private static Gear.Weapon Weapon(Gear.TypeID id = Gear.TypeID.Dragonhowl)
    {
        var weapon = new Gear.Weapon
        {
            ItemID = id, Level = 2, Exp = 3, Equip = 4, Transform = 5, MasteryScore = 12,
        };
        for (var i = 0; i < 6; i++) weapon.BlackJades[i].Type = (uint)(6 + i);
        for (var i = 0; i < 7; i++)
        {
            weapon.TypeAnima[0][i] = (uint)(1 + i);
            weapon.TypeAnima[1][i] = (uint)(11 + i);
        }
        return weapon;
    }

    // Independent bounded reader for assertions only; never invokes the production writer.
    private static List<Field> ReadFrame(byte[] frame, ushort opcode)
    {
        Assert.True(frame.Length >= 12);
        Assert.Equal(frame.Length - 8, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal(opcode, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal("TQServer", Encoding.ASCII.GetString(frame, frame.Length - 8, 8));
        return ReadFields(frame.AsSpan(4, frame.Length - 12).ToArray());
    }

    private static List<Field> ReadFields(byte[] bytes)
    {
        var fields = new List<Field>();
        var position = 0;
        while (position < bytes.Length)
        {
            var tag = ReadVarint(bytes, ref position);
            var number = checked((int)(tag >> 3));
            Assert.True(number > 0);
            if ((tag & 7) == 0)
                fields.Add(new Field(number, ReadVarint(bytes, ref position), null));
            else
            {
                Assert.Equal(2UL, tag & 7);
                var length = checked((int)ReadVarint(bytes, ref position));
                Assert.InRange(length, 0, bytes.Length - position);
                fields.Add(new Field(number, 0, bytes.AsSpan(position, length).ToArray()));
                position += length;
            }
        }
        return fields;
    }

    private static ulong ReadVarint(byte[] bytes, ref int position)
    {
        ulong value = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            Assert.InRange(position, 0, bytes.Length - 1);
            var next = bytes[position++];
            value |= (ulong)(next & 0x7F) << shift;
            if ((next & 0x80) == 0) return value;
        }
        throw new InvalidOperationException("Malformed test observation: oversized varint.");
    }

    private static List<Field> Entry(Field field) => ReadFields(Assert.IsType<byte[]>(field.Bytes));
    private static ulong Scalar(List<Field> fields, int number) =>
        Assert.Single(fields, field => field.Number == number && field.Bytes is null).Value;
    private sealed record Field(int Number, ulong Value, byte[]? Bytes);

    private static void AssertItem(List<Field> fields, Gear.Weapon weapon)
    {
        Assert.Equal(Enumerable.Range(1, 12), fields.Select(field => field.Number));
        Assert.Equal((ulong)weapon.ItemID, Scalar(fields, 1));
        Assert.Equal((ulong)weapon.Level, Scalar(fields, 2));
        Assert.Equal((ulong)weapon.Exp, Scalar(fields, 3));
        Assert.Equal((ulong)weapon.Equip, Scalar(fields, 4));
        Assert.Equal((ulong)weapon.Transform, Scalar(fields, 5));
        for (var i = 0; i < 6; i++) Assert.Equal((ulong)weapon.BlackJades[i].Type, Scalar(fields, i + 6));
        Assert.Equal((ulong)weapon.MasteryScore, Scalar(fields, 12));
    }

    private static void AssertSkin(List<Field> fields, Gear.Weapon weapon, int row)
    {
        Assert.Equal(Enumerable.Range(1, 9), fields.Select(field => field.Number));
        Assert.Equal((ulong)weapon.ItemID, Scalar(fields, 1));
        Assert.Equal((ulong)(row + 1), Scalar(fields, 2));
        for (var i = 0; i < 7; i++) Assert.Equal((ulong)weapon.TypeAnima[row][i], Scalar(fields, i + 3));
    }

    private sealed class LoadScope : IDisposable
    {
        private readonly bool _saved = LegacyNativeCombatGearTable.m_Load;
        public LoadScope(bool loaded = true) => LegacyNativeCombatGearTable.m_Load = loaded;
        public void Dispose() => LegacyNativeCombatGearTable.m_Load = _saved;
    }
}
