using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.World;
using Records = WolfConquer.Core.World.LegacyNativeMsgItemRefineRecord;

namespace WolfConquer.Tests;

public sealed unsafe partial class LegacyNativeItemTransportTests
{
    [Fact]
    public void WholeRefinementContractsRetainEveryTagRequiredFlagAndImplicitNullField()
    {
        var outer = new Records.ItemRefineRecord(); var row = new Records.RefineRecord();
        Assert.Equal(0u, outer.Type); Assert.Equal(0u, outer.Points); Assert.Equal(0u, outer.UnKnow);
        Assert.Equal(0u, outer.Unlocked); Assert.Null(outer.msgs); Assert.Null(row.str);
        Assert.NotNull(typeof(Records.ItemRefineRecord).GetCustomAttribute<ProtoContractAttribute>());
        Assert.NotNull(typeof(Records.RefineRecord).GetCustomAttribute<ProtoContractAttribute>());
        (string Name, int Tag, bool Required)[] fields = { ("Type",1,true),("Points",2,false),("UnKnow",3,true),
            ("Unlocked",4,false),("msgs",5,false) };
        foreach (var entry in fields)
        {
            var attribute = typeof(Records.ItemRefineRecord).GetField(entry.Name)!.GetCustomAttribute<ProtoMemberAttribute>()!;
            Assert.Equal(entry.Tag, attribute.Tag); Assert.Equal(entry.Required, attribute.IsRequired);
        }
        string[] rowNames = { "Index", "ItemUID", "ItemID", "PurificationID", "OpenStars", "RequestStars", "str" };
        for (int index = 0; index < rowNames.Length; index++)
        {
            var attribute = typeof(Records.RefineRecord).GetField(rowNames[index])!.GetCustomAttribute<ProtoMemberAttribute>()!;
            Assert.Equal(index + 1, attribute.Tag); Assert.True(attribute.IsRequired);
        }
    }

    [Fact]
    public Task FullRecordCreateWritesRequiredZeroFieldsWithoutOptionalZeroOrNullArrayFields() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var record = new Records.ItemRefineRecord();
        Assert.Same(scope.Packet, scope.Packet.ItemRefineRecordCreate(record));
        Assert.Equal(ProtoFrame(0x08,0x00,0x18,0x00), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(8, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
        Assert.Null(record.msgs); Assert.Equal(0u, record.Type); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task FullRecordCreateSerializesTheActualNestedOwnerWithAllSevenNativeFields() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = new Records.RefineRecord { Index=1,ItemUID=2,ItemID=3,PurificationID=4,OpenStars=5,RequestStars=6,str="A" };
        var rows = new[] { row }; var record = new Records.ItemRefineRecord { Type=7,Points=8,UnKnow=9,Unlocked=10,msgs=rows };
        scope.Packet.ItemRefineRecordCreate(record);
        byte[] payload = { 0x08,7,0x10,8,0x18,9,0x20,10,0x2A,15,
            0x08,1,0x10,2,0x18,3,0x20,4,0x28,5,0x30,6,0x3A,1,0x41 };
        Assert.Equal(ProtoFrame(payload), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Same(rows, record.msgs); Assert.Same(row, record.msgs![0]); Assert.Equal("A", row.str);
        Assert.Equal(29, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
    });

    [Fact]
    public Task RepeatedNestedAliasesStayRepeatedInTheRealProtobufPayload() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = new Records.RefineRecord { Index=1,ItemUID=2,ItemID=3,PurificationID=4,OpenStars=5,RequestStars=6,str="A" };
        var record = new Records.ItemRefineRecord { msgs = new[] { row, row } };
        scope.Packet.ItemRefineRecordCreate(record);
        byte[] nested = { 0x2A,15,0x08,1,0x10,2,0x18,3,0x20,4,0x28,5,0x30,6,0x3A,1,0x41 };
        byte[] payload = new byte[] { 0x08,0,0x18,0 }.Concat(nested).Concat(nested).ToArray();
        Assert.Equal(ProtoFrame(payload), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(2, record.msgs!.Length); Assert.Same(record.msgs[0], record.msgs[1]);
    });

    [Fact]
    public Task FullRecordGetReadsTheHeaderFrameAndReturnsARealNewNestedGraphNotTheSuppliedOutValue() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] frame = ProtoFrame(0x08,7,0x10,8,0x18,9,0x20,10,0x2A,15,
            0x08,1,0x10,2,0x18,3,0x20,4,0x28,5,0x30,6,0x3A,1,0x41);
        Marshal.Copy(frame, 0, (IntPtr)scope.Packet.Memory, frame.Length);
        scope.Packet.Size = frame.Length;
        var previous = new Records.ItemRefineRecord { Type=99 }; var record = previous;
        scope.Packet.GetItemRefineRecord(out record);
        Assert.NotSame(previous, record); Assert.Equal(99u, previous.Type);
        Assert.Equal(7u, record.Type); Assert.Equal(8u, record.Points); Assert.Equal(9u, record.UnKnow); Assert.Equal(10u, record.Unlocked);
        var row = Assert.Single(record.msgs!); Assert.Equal(1u, row.Index); Assert.Equal(2u, row.ItemUID);
        Assert.Equal(3u, row.ItemID); Assert.Equal(4u, row.PurificationID); Assert.Equal(5u, row.OpenStars);
        Assert.Equal(6u, row.RequestStars); Assert.Equal("A", row.str);
        Assert.Equal(frame.Length, scope.Packet.Position); Assert.Equal(frame.Length, scope.Packet.Size);
        Assert.Equal(frame, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task FullRecordGetDoesNotValidateOpcodeAndIgnoresUnknownProtobufFields() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); byte[] frame = ProtoFrame(0x08,1,0x18,2,0x98,0x06,123);
        U16(frame, 2, 7); Marshal.Copy(frame, 0, (IntPtr)scope.Packet.Memory, frame.Length);
        scope.Packet.Size = frame.Length;
        scope.Packet.GetItemRefineRecord(out var record);
        Assert.Equal(1u, record.Type); Assert.Equal(2u, record.UnKnow); Assert.Equal(0u, record.Points);
        Assert.Null(record.msgs); Assert.Equal(frame.Length, scope.Packet.Position); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task WholeRecordGetRetainsThePriorObjectAndCursorOnTruncatedPayload() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); byte[] frame = ProtoFrame(0x08);
        Marshal.Copy(frame, 0, (IntPtr)scope.Packet.Memory, frame.Length);
        scope.Packet.Size = frame.Length;
        var previous = new Records.ItemRefineRecord { Type=99 }; var record = previous;
        Assert.Throws<EndOfStreamException>(() => scope.Packet.GetItemRefineRecord(out record));
        Assert.Same(previous, record); Assert.Equal(99u, record.Type); Assert.Null(record.msgs);
        Assert.Equal(211, scope.Packet.Position); Assert.Equal(frame.Length, scope.Packet.Size);
        Assert.Equal(frame, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullRecordCreateRetainsTheActualGenericSerializerZeroPayloadRatherThanAddingAGuard() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Packet.ItemRefineRecordCreate(null!);
        Assert.Equal(ProtoFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
    });

    [Fact]
    public Task WholePrivateProcessIgnoresTheIncomingQueryAndSendsTheNativeConstantFiftyFourRecord() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        InvokePrivate(typeof(Records), "Process", null, scope.Client, scope.Packet);
        Assert.Equal(ProtoFrame(0x08,0,0x10,54,0x18,0), Assert.Single(scope.Queue));
        Assert.Equal(10, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
        Assert.Empty(scope.Equipment.ClientItems);
    });

    [Fact]
    public Task WholePrivateProcessBuildsBeforeNativeSendFilteringOrNullReceiverFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Fake = true;
        InvokePrivate(typeof(Records), "Process", null, scope.Client, scope.Packet);
        Assert.Empty(scope.Queue); Assert.Equal(10, scope.Packet.Size);
        Assert.Throws<NullReferenceException>(() => InvokePrivate(typeof(Records), "Process", null, null!, scope.Packet));
        Assert.Equal(ProtoFrame(0x08,0,0x10,54,0x18,0), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
    });
}
