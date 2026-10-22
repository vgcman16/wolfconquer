using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeClientSpellRecordTests
{
    [Fact]
    public void DBSpellRetainsItsNativeTwelveByteSequentialLayoutAndAllFieldWidths()
    {
        var type = typeof(LegacyNativeClientSpells.DBSpell);
        Assert.True(type.IsLayoutSequential);
        Assert.Equal(12, Marshal.SizeOf<LegacyNativeClientSpells.DBSpell>());
        Assert.Equal(12, Unsafe.SizeOf<LegacyNativeClientSpells.DBSpell>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<LegacyNativeClientSpells.DBSpell>());
        var names = new[] { "ID", "Level", "Experience", "PreviousLevel", "SoulLevel", "UseJiangSpell" };
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(ushort), typeof(ushort), typeof(int), typeof(byte), typeof(byte), typeof(byte) }, fields.Select(field => field.FieldType));
        Assert.Equal(new[] { 0, 2, 4, 8, 9, 10 }, names.Select(name => Marshal.OffsetOf(type, name).ToInt32()));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
    }

    [Fact]
    public void GetDBSpellMutatesTheReceiverReturnsACopyAndPreservesNativeAlignmentPadding()
    {
        var row = MemoryMarshal.Read<LegacyNativeClientSpells.DBSpell>(Enumerable.Repeat((byte)0xA5, 12).ToArray());
        var spell = new LegacyNativeMsgSpell { ID = 0x1234, Level = 0x5678, Experience = int.MinValue,
            PreviousLevel = 0x9A, SoulLevel = 4, UseSpellSoul = 3 };
        var returned = row.GetDBSpell(spell);
        var expected = LegacyNativeClientSpellPersistenceTests.Row(0x1234, 0x5678, int.MinValue, 0x9A, 4, 3, 0xA5);
        Assert.Equal(expected, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
        Assert.Equal(expected, LegacyNativeClientSpellPersistenceTests.Bytes(ref returned));
        returned.ID = 99;
        Assert.Equal((ushort)0x1234, row.ID);
        Assert.Equal((ushort)0x1234, spell.ID);
    }

    [Fact]
    public void GetClientSpellBuildsFreshNativeObjectsAndAppliesTheRealSoulSetters()
    {
        var row = new LegacyNativeClientSpells.DBSpell { ID = ushort.MaxValue, Level = ushort.MaxValue,
            Experience = int.MaxValue, PreviousLevel = byte.MaxValue, SoulLevel = byte.MaxValue, UseJiangSpell = 4 };
        var first = row.GetClientSpell();
        var second = row.GetClientSpell();
        Assert.NotSame(first, second);
        Assert.Equal(ushort.MaxValue, first.ID); Assert.Equal(ushort.MaxValue, first.Level);
        Assert.Equal(int.MaxValue, first.Experience); Assert.Equal(byte.MaxValue, first.PreviousLevel);
        Assert.Equal(byte.MaxValue, first.SoulLevel); Assert.Equal((byte)4, first.UseSpellSoul);
        Assert.Equal(LegacyNativeMsgSpell.UseSpellSoulTyp.Four, first.UseSoul);
        Assert.Equal(65810, first.HashLevelSoul);
        Assert.Equal(default, first.ColdTime); Assert.Equal(default, first.StampSpell);
        Assert.False(first.IsSpellWithColdTime);
        Assert.Equal((ushort)0, first.Size); Assert.Equal((ushort)0, first.PacketType);
        Assert.Equal(0, first.PacketStamp); Assert.Equal((ushort)0, first.UnKnow);
        Assert.Equal((ushort)0, first.Type); Assert.Equal((byte)0, first.Soul);
    }

    [Fact]
    public void InvalidPersistedUseJiangSpellNormalizesThroughTheNativeClientSetterOnly()
    {
        foreach (var raw in new byte[] { 0, 1, 2, 3, 4, 5, 9, 255 })
        {
            var row = new LegacyNativeClientSpells.DBSpell { UseJiangSpell = raw };
            var client = row.GetClientSpell();
            var expected = raw <= 4 ? raw : (byte)0;
            Assert.Equal(expected, client.UseSpellSoul);
            Assert.Equal(raw, row.UseJiangSpell);
            var normalized = default(LegacyNativeClientSpells.DBSpell).GetDBSpell(client);
            Assert.Equal(expected, normalized.UseJiangSpell);
        }
    }

    [Fact]
    public void NullClientConversionFaultsBeforeMutatingAnyRecordByte()
    {
        var row = MemoryMarshal.Read<LegacyNativeClientSpells.DBSpell>(Enumerable.Range(0, 12).Select(value => (byte)(value + 71)).ToArray());
        var before = LegacyNativeClientSpellPersistenceTests.Bytes(ref row);
        Assert.Throws<NullReferenceException>(() => row.GetDBSpell(null!));
        Assert.Equal(before, LegacyNativeClientSpellPersistenceTests.Bytes(ref row));
    }
}
