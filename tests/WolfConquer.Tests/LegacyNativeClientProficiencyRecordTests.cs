using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeClientProficiencyRecordTests
{
    [Fact]
    public void DBProfUsesNativeTwentyByteSequentialPackingAndExactUnsignedFields()
    {
        var type = typeof(LegacyNativeClientProficiency.DBProf);
        Assert.True(type.IsLayoutSequential);
        Assert.Equal(20, Marshal.SizeOf<LegacyNativeClientProficiency.DBProf>());
        Assert.Equal(20, Unsafe.SizeOf<LegacyNativeClientProficiency.DBProf>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<LegacyNativeClientProficiency.DBProf>());
        var names = new[] { "ID", "Level", "PreviouseLevel", "Experience", "NeededExperience" };
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(uint), typeof(uint), typeof(byte), typeof(uint), typeof(uint) }, fields.Select(field => field.FieldType));
        Assert.Equal(new[] { 0, 4, 8, 12, 16 }, names.Select(name => Marshal.OffsetOf(type, name).ToInt32()));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
    }

    [Fact]
    public void GetDBSpellMutatesTheReceiverReturnsAnIndependentCopyAndRetainsThreePaddingBytes()
    {
        var row = MemoryMarshal.Read<LegacyNativeClientProficiency.DBProf>(Enumerable.Repeat((byte)0xA5, 20).ToArray());
        var prof = new LegacyNativeMsgProficiency { ID = uint.MaxValue, Level = 0x87654321,
            PreviouseLevel = byte.MaxValue, Experience = 0xFEDCBA98, NeededExperience = 0x80000000 };
        var copy = row.GetDBSpell(prof);
        var expected = LegacyNativeClientProficiencyPersistenceTests.Row(uint.MaxValue, 0x87654321,
            byte.MaxValue, 0xFEDCBA98, 0x80000000, 0xA5);
        Assert.Equal(expected, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
        Assert.Equal(expected, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref copy));
        copy.ID = 3;
        Assert.Equal(uint.MaxValue, row.ID);
        Assert.Equal(uint.MaxValue, prof.ID);
    }

    [Fact]
    public void GetClientProfCreatesFreshMutableObjectsWithoutLevelOrIdNormalization()
    {
        var row = new LegacyNativeClientProficiency.DBProf { ID = uint.MaxValue, Level = uint.MaxValue,
            PreviouseLevel = byte.MaxValue, Experience = 0x80000000, NeededExperience = uint.MaxValue };
        var first = row.GetClientProf();
        var second = row.GetClientProf();
        Assert.NotSame(first, second);
        Assert.Equal(uint.MaxValue, first.ID); Assert.Equal(uint.MaxValue, first.Level);
        Assert.Equal(byte.MaxValue, first.PreviouseLevel);
        Assert.Equal(0x80000000u, first.Experience); Assert.Equal(uint.MaxValue, first.NeededExperience);
        first.Level = 0; first.Experience = 0;
        Assert.Equal(uint.MaxValue, second.Level); Assert.Equal(uint.MaxValue, row.Level);
        var zero = default(LegacyNativeClientProficiency.DBProf).GetClientProf();
        Assert.Equal(0u, zero.ID); Assert.Equal(0u, zero.Level);
        Assert.Equal((byte)0, zero.PreviouseLevel); Assert.Equal(0u, zero.Experience); Assert.Equal(0u, zero.NeededExperience);
    }

    [Fact]
    public void NullProficiencyFaultsBeforeAnyRecordOrPaddingMutation()
    {
        var row = MemoryMarshal.Read<LegacyNativeClientProficiency.DBProf>(Enumerable.Range(1, 20).Select(value => (byte)value).ToArray());
        var before = LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row);
        Assert.Throws<NullReferenceException>(() => row.GetDBSpell(null!));
        Assert.Equal(before, LegacyNativeClientProficiencyPersistenceTests.Bytes(ref row));
    }

    [Fact]
    public void ProficiencyBirthRetainsItsExactOwnerAndPhysicalDictionaryWithoutClientAutoBirth()
    {
        var client = new LegacyNativeGameClientRuntime { Fake = true, FullLoading = false };
        var field = typeof(LegacyNativeGameClientRuntime).GetField("MyProfs", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(typeof(LegacyNativeProficiencyStorage), field.FieldType);
        Assert.False(field.IsInitOnly);
        Assert.Null(typeof(LegacyNativeGameClientRuntime).GetProperty("MyProfs", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(client.MyProfs);
        var first = new LegacyNativeProficiencyStorage(client);
        Assert.Throws<ArgumentNullException>(() => new LegacyNativeProficiencyStorage(null!));
        var owner = typeof(LegacyNativeProficiencyStorage).GetField("Owner", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Same(client, owner.GetValue(first));

        Assert.Empty(first.ClientProf);
        var replacement = new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>();
        first.ClientProf = replacement;
        client.MyProfs = first;
        Assert.Same(replacement, client.MyProfs.ClientProf);
        Assert.Same(first, field.GetValue(client));
    }
}
