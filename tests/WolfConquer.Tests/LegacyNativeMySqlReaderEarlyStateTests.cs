using MySql.Data.MySqlClient;
using System.Data;
using Command = WolfConquer.Core.Data.LegacyNativeMySqlCommand;
using Reader = WolfConquer.Core.Data.LegacyNativeMySqlReader;
using Kind = WolfConquer.Core.Data.LegacyNativeMySqlCommandType;

namespace WolfConquer.Tests;

public sealed class LegacyNativeMySqlReaderEarlyStateTests
{
    [Fact]
    public void SelectPreparationCarriesExactValuesIntoTheRealAdapterCommand()
    {
        const string username = "中文 O'Brien\\\0\n";
        using var builder = new Command(Kind.SELECT);
        builder.Select("accounts").Where("Username", username);
        using MySqlCommand select = Reader.PrepareSelect(builder);
        using var adapter = new MySqlDataAdapter(select);
        Assert.Same(select, adapter.SelectCommand);
        Assert.Equal("SELECT * FROM `accounts` WHERE `Username` = @p0", adapter.SelectCommand.CommandText);
        Assert.Equal(0, adapter.SelectCommand.CommandTimeout);
        Assert.Null(select.Connection);
        Assert.Single(adapter.SelectCommand.Parameters.Cast<MySqlParameter>());
        Assert.Equal(MySqlDbType.VarChar, select.Parameters[0].MySqlDbType);
        Assert.Equal(username, select.Parameters[0].Value);
        // The caller retains the builder and snapshots have separate bindings.
        using MySqlCommand second = builder.CreateProviderCommand();
        Assert.NotSame(select.Parameters[0], second.Parameters[0]);
    }

    [Fact]
    public void MissingWrongKindAndIncompleteCommandsRejectBeforeConnectionCreation()
    {
        Assert.Throws<ArgumentNullException>(() => new Reader((Command)null!));
        Assert.Throws<ArgumentNullException>(() => new Reader((DataSet)null!));
        foreach (Kind kind in new[] { Kind.UPDATE, Kind.INSERT, Kind.DELETE, Kind.COUNT })
        {
            using var builder = new Command(kind);
            Assert.Throws<ArgumentException>(() => new Reader(builder));
            Assert.Throws<ArgumentException>(() => builder.CreateReader());
        }
        using var select = new Command(Kind.SELECT);
        Assert.Throws<InvalidOperationException>(() => new Reader(select));
        select.Dispose();
        Assert.Throws<ObjectDisposedException>(() => Reader.PrepareSelect(select));
    }

    [Fact]
    public void OwnedBufferedRowsRetainAllSuccessfulTypedConversions()
    {
        var dataset = new DataSet();
        DataTable table = dataset.Tables.Add("table");
        var values = new Dictionary<string, object>
        {
            ["SByte"] = sbyte.MinValue, ["Byte"] = byte.MaxValue,
            ["Int16"] = short.MinValue, ["UInt16"] = ushort.MaxValue,
            ["Int32"] = int.MinValue, ["UInt32"] = uint.MaxValue,
            ["Int64"] = long.MinValue, ["UInt64"] = ulong.MaxValue,
            ["Double"] = 1.5, ["Text"] = "中文'\\\0",
            ["True"] = "1", ["False"] = "0", ["Boolean"] = "True",
            ["Blob"] = new byte[] { 0, 128, 255 }, ["Array"] = "1/2//invalid/4294967295",
            ["Null"] = DBNull.Value
        };
        foreach (string column in values.Keys) table.Columns.Add(column, typeof(object));
        table.Rows.Add(values.Values.ToArray());
        using var reader = new Reader(dataset);
        Assert.Equal(1, reader.NumberOfRows);
        Assert.True(reader.Read());
        Assert.Equal(sbyte.MinValue, reader.ReadSByte("SByte"));
        Assert.Equal(byte.MaxValue, reader.ReadByte("Byte"));
        Assert.Equal(short.MinValue, reader.ReadInt16("Int16"));
        Assert.Equal(ushort.MaxValue, reader.ReadUInt16("UInt16"));
        Assert.Equal(int.MinValue, reader.ReadInt32("Int32"));
        Assert.Equal(uint.MaxValue, reader.ReadUInt32("UInt32"));
        Assert.Equal(long.MinValue, reader.ReadInt64("Int64"));
        Assert.Equal(ulong.MaxValue, reader.ReadUInt64("UInt64"));
        Assert.Equal(ulong.MaxValue, reader.ReadUInt128("UInt64"));
        Assert.Equal(1.5, reader.ReadDouble("Double"));
        Assert.Equal(values["Text"], reader.ReadString("Text"));
        Assert.Equal(uint.MaxValue, reader.method_6("UInt32"));
        Assert.Equal(long.MinValue, reader.method_7("Int64"));
        Assert.True(reader.ReadBoolean("True"));
        Assert.False(reader.ReadBoolean("False"));
        Assert.True(reader.ReadBoolean("Boolean"));
        Assert.Equal(new byte[] { 0, 128, 255 }, reader.ReadBlob("Blob"));
        Assert.Equal(new uint[] { 1, 2, 0, uint.MaxValue }, reader.ReadUIntArray("Array", new[] { "/" }));
        Assert.Equal(0, reader.ReadInt32("Null"));
        Assert.Equal(0UL, reader.ReadUInt64("Null"));
        Assert.Equal("", reader.ReadString("Null"));
        Assert.False(reader.ReadBoolean("Null"));
        Assert.Empty(reader.ReadBlob("Null"));
        Assert.Empty(reader.ReadUIntArray("Null", new[] { "/" }));
        Assert.False(reader.Read());
        Assert.False(reader.Read());
        Assert.Throws<InvalidOperationException>(() => reader.ReadString("Text"));
    }

    [Fact]
    public void EveryCleanupSurfaceDisposesTheSoleOwnedDatasetExactlyOnce()
    {
        Action<Reader>[] cleanup = { reader => reader.Dispose(), reader => reader.Close(), reader => ((IDisposable)reader).Dispose() };
        foreach (Action<Reader> close in cleanup)
        {
            var dataset = new DataSet();
            DataTable table = dataset.Tables.Add("table");
            table.Columns.Add("Value", typeof(string));
            table.Rows.Add("owned");
            int disposals = 0;
            dataset.Disposed += (_, _) => disposals++;
            using var reader = new Reader(dataset);
            Assert.True(reader.Read());
            close(reader);
            reader.Dispose();
            reader.Close();
            ((IDisposable)reader).Dispose();
            Assert.Equal(1, disposals);
            Assert.Throws<ObjectDisposedException>(() => reader.Read());
            Assert.Throws<ObjectDisposedException>(() => _ = reader.NumberOfRows);
            Assert.Throws<ObjectDisposedException>(() => reader.ReadString("Value"));
        }
    }

    [Fact]
    public void AccessWithoutCurrentRowIsExplicitAndEmptyResultsStayEmpty()
    {
        using var reader = new Reader(new DataSet());
        Assert.Equal(0, reader.NumberOfRows);
        foreach (Action read in Accessors(reader)) Assert.Throws<InvalidOperationException>(read);
        Assert.False(reader.Read());
        Assert.False(reader.Read());
    }

    private static Action[] Accessors(Reader reader) =>
    [
        () => reader.ReadSByte("c"), () => reader.ReadByte("c"),
        () => reader.ReadInt16("c"), () => reader.ReadUInt16("c"),
        () => reader.ReadInt32("c"), () => reader.ReadUInt32("c"),
        () => reader.ReadInt64("c"), () => reader.ReadUInt64("c"),
        () => reader.ReadDouble("c"), () => reader.ReadUInt128("c"),
        () => reader.ReadString("c"), () => reader.method_6("c"),
        () => reader.method_7("c"), () => reader.ReadBoolean("c"),
        () => reader.ReadBlob("c"), () => reader.ReadUIntArray("c", new[] { "/" })
    ];
}
