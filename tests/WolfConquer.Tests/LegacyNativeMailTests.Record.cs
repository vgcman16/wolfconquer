using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Mail = WolfConquer.Core.World.LegacyNativeMailList;
using Record = WolfConquer.Core.World.LegacyNativeTMailList;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMailTests
{
    [Fact]
    public Task RecordUsesNativeDefaultPackingAndAllNumericWidths() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Equal(744, Marshal.SizeOf<Record>());
        (string Name, int Offset)[] offsets =
        {
            ("_title", 0), ("_Sender_name", 64), ("_content", 192), ("id", 704), ("Money", 712),
            ("EMoney", 720), ("itemid", 724), ("Emoney_Record_Type", 728), ("Flag", 732), ("Timer", 736),
        };
        foreach (var (name, offset) in offsets) Assert.Equal(offset, Marshal.OffsetOf<Record>(name).ToInt32());
        var row = Row(uint.MaxValue); row.Money = ulong.MaxValue; row.EMoney = 0x12345678;
        row.itemid = 0x90ABCDEF; row.Emoney_Record_Type = 0x98765432; row.Flag = 0x87654321;
        row.TimeStamp = new DateTime(638000000001234567, DateTimeKind.Utc);
        var record = Stored(row); byte[] bytes = RecordBytes(record);
        Assert.Equal((byte)'T', bytes[0]); Assert.Equal((byte)'S', bytes[64]); Assert.Equal((byte)'C', bytes[192]);
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(704)));
        Assert.Equal(ulong.MaxValue, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(712)));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(720)));
        Assert.Equal(0x90ABCDEFu, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(724)));
        Assert.Equal(0x98765432u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(728)));
        Assert.Equal(0x87654321u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(732)));
        Assert.Equal(row.TimeStamp.Ticks, BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(736)));
        Assert.NotEqual(row.TimeStamp.ToBinary(), record.Timer); // Native stores Ticks, not Kind bits.
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task ShortFixedAsciiAssignmentRetainsTailAndEmptyAssignmentDoesNotClear(int field) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); Record record = default;
        SetText(ref record, field, "ABCDEFG");
        SetText(ref record, field, "xy");
        Assert.Equal("xyCDEFG", GetText(record, field));
        SetText(ref record, field, ""); Assert.Equal("xyCDEFG", GetText(record, field));
        SetText(ref record, field, "Q\0R"); Assert.Equal("Q", GetText(record, field));
        Assert.Throws<NullReferenceException>(() => SetText(ref record, field, null!));
        Assert.Equal("Q", GetText(record, field));
    });

    [Fact]
    public Task WholeRecordConversionMutatesReceiverAndReturnedCopyAndRetainsShortStringTails() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); Record record = default;
        var first = Row(4); first.title = "TITLE"; first.Sender_name = "SENDER"; first.content = "CONTENT";
        record.GetMailList(first);
        var second = Row(5); second.title = "x"; second.Sender_name = "y"; second.content = "z";
        var copy = record.GetMailList(second);
        Assert.Equal("xITLE", record.title); Assert.Equal("yENDER", record.Sender_name); Assert.Equal("zONTENT", record.content);
        Assert.Equal(5u, record.id); Assert.Equal(RecordBytes(record), RecordBytes(copy));
        copy.id = 71; Assert.Equal(5u, record.id);
        Assert.Equal(second.TimeStamp.Ticks, record.Timer);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task NullStringConversionFaultKeepsOnlyEarlierStringAssignmentsAndNoNumericPublication(int broken) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var record = Stored(Row(99));
        var input = Row(17); input.title = broken == 0 ? null! : "A";
        input.Sender_name = broken == 1 ? null! : "B"; input.content = broken == 2 ? null! : "D";
        input.Money = 400; input.TimeStamp = DateTime.MaxValue;
        Assert.Throws<NullReferenceException>(() => record.GetMailList(input));
        Assert.Equal(broken == 0 ? "T" : "A", record.title);
        Assert.Equal(broken <= 1 ? "S" : "B", record.Sender_name); Assert.Equal("C", record.content);
        Assert.Equal(99u, record.id); Assert.Equal(2ul, record.Money); Assert.NotEqual(DateTime.MaxValue.Ticks, record.Timer);
    });

    [Fact]
    public Task FutureRecordCreatesIndependentMailAndUsesWholeMinuteExpirationNotStoredExpiration() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(17); row.TimeStamp = DateTime.Now.AddMinutes(10.75);
        row.Expiration_Date = uint.MaxValue;
        var record = Stored(row); var before = DateTime.Now;
        var loaded = record.GetMailList(); var after = DateTime.Now;
        Assert.NotNull(loaded); Assert.NotSame(row, loaded);
        Assert.Equal(row.id, loaded.id); Assert.Equal(row.Money, loaded.Money); Assert.Equal(row.EMoney, loaded.EMoney);
        Assert.Equal(row.itemid, loaded.itemid); Assert.Equal(row.Emoney_Record_Type, loaded.Emoney_Record_Type); Assert.Equal(row.Flag, loaded.Flag);
        Assert.Equal("T", loaded.title); Assert.Equal("S", loaded.Sender_name); Assert.Equal("C", loaded.content);
        Assert.Equal(DateTimeKind.Unspecified, loaded.TimeStamp.Kind); Assert.Equal(row.TimeStamp.Ticks, loaded.TimeStamp.Ticks);
        Assert.InRange(loaded.Expiration_Date, (uint)(loaded.TimeStamp - after).TotalMinutes * 60,
            (uint)(loaded.TimeStamp - before).TotalMinutes * 60);
        Assert.Equal(0u, loaded.Expiration_Date % 60); Assert.Empty(scope.Queue);
        loaded.title = "Changed"; Assert.Equal("T", record.title);
    });

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L)]
    public Task NonpositiveOrExpiredRecordDoesNotReadStringBuffersOrCreateMail(long timer) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); Record record = default; record.Timer = timer;
        Assert.Null(record.GetMailList()); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task SubminuteFutureIsStillLoadedButItsExpirationIsZeroAndBadPositiveDateFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(); row.TimeStamp = DateTime.Now.AddSeconds(40);
        Assert.Equal(0u, Stored(row).GetMailList()!.Expiration_Date);
        Record bad = default; bad.Timer = long.MaxValue;
        Assert.Throws<ArgumentException>(() => bad.GetMailList());
    });

    private static void SetText(ref Record record, int field, string value)
    {
        if (field == 0) record.title = value;
        else if (field == 1) record.Sender_name = value;
        else record.content = value;
    }
    private static string GetText(Record record, int field) => field == 0 ? record.title : field == 1 ? record.Sender_name : record.content;
}
