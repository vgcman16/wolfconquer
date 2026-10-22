using System.Reflection;
using WolfConquer.Core.Data;
using Command = WolfConquer.Core.Data.LegacyNativeMySqlCommand;
using Reader = WolfConquer.Core.Data.LegacyNativeMySqlReader;
using Kind = WolfConquer.Core.Data.LegacyNativeMySqlCommandType;

namespace WolfConquer.Tests;

public sealed class LegacyNativeMySqlReaderEarlyStateTests
{
    [Fact]
    public void NonSelectKindsReturnBeforeDataSourceSetupAndCommandDisposal()
    {
        var kinds = new[] { Kind.UPDATE, Kind.INSERT, Kind.DELETE, Kind.COUNT, (Kind)int.MaxValue };
        foreach (var kind in kinds)
        {
            using IDisposable owner = new Command(kind);
            var command = (Command)owner;
            command.Command = "safe-managed-marker";
            var reader = new Reader(command);
            Assert.Equal(0, reader.NumberOfRows);
            Assert.False(reader.Read());
            Assert.Equal("safe-managed-marker", command.Command);
            ((IDisposable)reader).Dispose();
        }
    }

    [Fact]
    public void CreateReaderUsesTheGenuineNonSelectConstructorNotAStub()
    {
        using IDisposable owner = new Command(Kind.UPDATE);
        var command = (Command)owner;
        command.Update("t").Set("c", 1L);
        var reader = command.CreateReader();
        Assert.IsType<Reader>(reader);
        Assert.Equal(0, reader.NumberOfRows);
        Assert.False(reader.Read());
        Assert.Equal("UPDATE `t` SET `c` = 1 ", command.Command);
        ((IDisposable)reader).Dispose();
    }

    [Fact]
    public void NullCommandKeepsTheNativeImmediateTypeDereferenceFault()
    {
        Assert.Throws<NullReferenceException>(() => new Reader(null!));
    }

    [Fact]
    public void PublicNoOpCleanupAndExplicitNullDatasetDisposeKeepEarlyState()
    {
        using IDisposable owner = new Command(Kind.COUNT);
        var command = (Command)owner;
        var reader = new Reader(command);
        reader.Dispose();
        reader.Close();
        ((IDisposable)reader).Dispose();
        ((IDisposable)reader).Dispose();
        Assert.Equal(0, reader.NumberOfRows);
        Assert.False(reader.Read());
        Assert.Equal("SELECT count(<V>) FROM <R>", command.Command);
    }

    [Fact]
    public void AllTypedAccessorsKeepMissingCurrentRowFaultWithoutFakeRows()
    {
        using IDisposable owner = new Command(Kind.UPDATE);
        var reader = new Reader((Command)owner);
        var reads = new Action[]
        {
            () => reader.ReadSByte("c"),
            () => reader.ReadByte("c"),
            () => reader.ReadInt16("c"),
            () => reader.ReadUInt16("c"),
            () => reader.ReadInt32("c"),
            () => reader.ReadUInt32("c"),
            () => reader.ReadInt64("c"),
            () => reader.ReadUInt64("c"),
            () => reader.ReadDouble("c"),
            () => reader.ReadUInt128("c"),
            () => reader.ReadString("c"),
            () => reader.method_6("c"),
            () => reader.method_7("c"),
            () => reader.ReadBoolean("c"),
            () => reader.ReadBlob("c"),
            () => reader.ReadUIntArray("c", new[] { "/" })
        };
        // One Fact; no SELECT constructor, private DataSet injection or extra case rows.
        foreach (var read in reads)
            Assert.Throws<NullReferenceException>(read);
    }

    [Fact]
    public void ReaderKeepsNativeWidthsAndDistinctPublicExplicitCleanupMethods()
    {
        var type = typeof(Reader);
        Assert.False(type.IsPublic);
        Assert.False(type.IsSealed);
        Assert.Equal(typeof(ulong), type.GetMethod("ReadUInt128")!.ReturnType);
        Assert.Equal(typeof(uint), type.GetMethod("method_6")!.ReturnType);
        Assert.Equal(typeof(long), type.GetMethod("method_7")!.ReturnType);
        Assert.NotNull(type.GetMethod("Close", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(type.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance));
        var map = type.GetInterfaceMap(typeof(IDisposable));
        Assert.Single(map.TargetMethods);
        Assert.Equal("System.IDisposable.Dispose", map.TargetMethods[0].Name);
        Assert.True(map.TargetMethods[0].IsPrivate);
        Assert.Null(type.TypeInitializer);
    }
}
