using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Message = WolfConquer.Core.World.LegacyNativeMsgQuestList;
using Mode = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestMode;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeQuestPacketTests
{
    [Fact]
    public Task CompletePhysicalContractKeepsNativeEnumWidthsAndIndependentDefaultIntentions() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.True(typeof(Message).IsValueType); Assert.Equal(LayoutKind.Sequential, typeof(Message).StructLayoutAttribute!.Value);
        Assert.Equal(1, typeof(Message).StructLayoutAttribute!.Size); Assert.Equal(1, Marshal.SizeOf<Message>());
        Assert.Empty(typeof(Message).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Null(typeof(Message).GetMethod("Process", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(Mode))); Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Status)));
        Assert.Equal(new[] { "AcceptQuest", "QuitQuest", "Review", "FinishQuest" }, Enum.GetNames<Mode>());
        Assert.Equal(new ushort[] { 1, 2, 3, 4 }, Enum.GetValues<Mode>().Select(v => (ushort)v));
        Assert.Equal(new[] { "Accepted", "Finished", "Available" }, Enum.GetNames<Status>());
        Assert.Equal(new uint[] { 0, 1, 2 }, Enum.GetValues<Status>().Select(v => (uint)v));
        var fields = typeof(Message.QuestListItem).GetFields(BindingFlags.Instance | BindingFlags.Public);
        Assert.Equal(new[] { "UID", "Status", "Time", "Intentions" }, fields.OrderBy(f => f.MetadataToken).Select(f => f.Name));
        Assert.Equal(new[] { typeof(uint), typeof(Status), typeof(uint), typeof(uint[]) }, fields.OrderBy(f => f.MetadataToken).Select(f => f.FieldType));
        var one = new Message.QuestListItem(); var two = new Message.QuestListItem();
        Assert.Equal(0u, one.UID); Assert.Equal(Status.Accepted, one.Status); Assert.Equal(0u, one.Time);
        Assert.Equal(new uint[] { 0 }, one.Intentions); Assert.NotSame(one.Intentions, two.Intentions);
        one.Intentions[0] = 8; Assert.Equal(0u, two.Intentions[0]);
    });

    [Fact]
    public Task ListCreateAddsReservedWordsThenAppendsExactlyThreePhysicalFieldsWithoutFixingCount() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = new Message.QuestListItem
        { UID = 0x12345678, Status = (Status)uint.MaxValue, Time = 0xABCDEF12, Intentions = null! };
        Assert.Same(scope.Packet, scope.Packet.QuestListCreate(Mode.Review, 513));
        Assert.Equal(24, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(Convert.FromHexString("A5A5A5A50300010200000000000000000000000000000000"), scope.Raw(0, 24));
        Assert.Same(scope.Packet, scope.Packet.AddItemQuestList(item));
        Assert.Equal(36, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Same(scope.Packet, scope.Packet.QuestListFinalize());
        Assert.Equal(Frame(2120, 0x02010003, 0, 0, 0, 0, 0x12345678, uint.MaxValue, 0xABCDEF12), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position); Assert.Null(item.Intentions);
        Assert.Equal(0x12345678u, item.UID); Assert.Equal((Status)uint.MaxValue, item.Status); Assert.Equal(0xABCDEF12u, item.Time);
    });

    [Fact]
    public Task ListReaderKeepsUnknownEnumBitsWithinCapturedInputAndLeavesTheActualTail() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] input = Frame(9999, 0xFFFEFFFF, uint.MaxValue, 0xDEADBEEF, 0x12345678, 71); scope.Input(input);
        scope.Packet.GetQuestList(out var mode, out var count, out var id, out var status, out var timer);
        Assert.Equal((Mode)ushort.MaxValue, mode); Assert.Equal((ushort)65534, count); Assert.Equal(uint.MaxValue, id);
        Assert.Equal((Status)0xDEADBEEF, status); Assert.Equal(0x12345678u, timer); Assert.Equal(20, scope.Packet.Position);
        Assert.Equal(input.Length, scope.Packet.Size);
        Assert.Equal(71u, scope.Packet.ReadUInt32()); Assert.Equal(input.Length, scope.Packet.Position);
        Assert.Throws<EndOfStreamException>(() => scope.Packet.ReadUInt32()); Assert.Equal(input.Length, scope.Packet.Position);
    });

    [Fact]
    public Task ReaderOfOwnCreatedListConsumesReservedWordsNotTheFirstAppendedItem() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Packet.QuestListCreate(Mode.FinishQuest, 1)
            .AddItemQuestList(new Message.QuestListItem { UID = 17, Status = Status.Finished, Time = 71 }).QuestListFinalize();
        scope.Packet.GetQuestList(out var mode, out var count, out var id, out var status, out var timer);
        Assert.Equal(Mode.FinishQuest, mode); Assert.Equal((ushort)1, count);
        Assert.Equal(0u, id); Assert.Equal(Status.Accepted, status); Assert.Equal(0u, timer); Assert.Equal(20, scope.Packet.Position);
        scope.Packet.Seek(24); scope.Packet.GetQuestData(out var realId, out var realStatus, out var realTimer);
        Assert.Equal(17u, realId); Assert.Equal(1u, realStatus); Assert.Equal(new uint[] { 71 }, realTimer);
    });

    [Fact]
    public Task AppendUsesCurrentCursorAndMissingItemRefusesWithoutChangingAnything() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Throws<ArgumentNullException>(() => scope.Packet.AddItemQuestList(null!));
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size); Assert.Equal(Enumerable.Repeat((byte)0xA5, 12), scope.Raw(237, 12));
        scope.Packet.AddItemQuestList(new Message.QuestListItem { UID = 1, Status = Status.Available, Time = 3, Intentions = new uint[] { 999 } });
        Assert.Equal(249, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(Convert.FromHexString("010000000200000003000000"), scope.Raw(237, 12));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 4), scope.Raw(0, 4));
    });

    [Fact]
    public Task IncompleteAppendOrReaderCannotPublishPartialWordsOrDefaultFields() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Packet.Seek(16375);
        byte[] before = scope.Raw(0, scope.Packet.Capacity);
        Assert.Throws<InvalidOperationException>(() => scope.Packet.AddItemQuestList(
            new Message.QuestListItem { UID = 1, Status = (Status)uint.MaxValue, Time = 3 }));
        Assert.Equal(16375, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        var mode = (Mode)91; ushort count = 92; uint id = 93, timer = 94; var status = (Status)95;
        Assert.Throws<EndOfStreamException>(() => scope.Packet.GetQuestList(out mode, out count, out id, out status, out timer));
        Assert.Equal((Mode)91, mode); Assert.Equal((ushort)92, count); Assert.Equal(93u, id);
        Assert.Equal((Status)95, status); Assert.Equal(94u, timer); Assert.Equal(16375, scope.Packet.Position);
        Assert.Equal(321, scope.Packet.Size); Assert.Equal(before, scope.Raw(0, scope.Packet.Capacity));
    });

    [Fact]
    public Task ListFinalizerDoesNotInitializeWriterOrNormalizeAnyPayload() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Packet.QuestListFinalize();
        var expected = Enumerable.Repeat((byte)0xA5, 237).ToArray(); expected[0] = 237; expected[1] = 0; expected[2] = 0x48; expected[3] = 8;
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(237, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
    });

    [Fact]
    public Task EveryPrimitiveOwnerDereferencesRealPacketRatherThanUsingFallbackOrDefaultBytes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Throws<ArgumentNullException>(() => LegacyNativeQuestPacket.GetQuestData(null!, out _, out _, out _));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeQuestPacket.MsgQuestDataCreate(null!, 0, 0, Array.Empty<uint>()));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeQuestPacket.MsgQuestDataCreate(null!, 0, 0, 0, Array.Empty<uint>()));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeQuestPacket.GetQuestList(null!, out _, out _, out _, out _, out _));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeQuestPacket.QuestListCreate(null!, Mode.Review, 0));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeQuestPacket.AddItemQuestList(null!, new Message.QuestListItem()));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeQuestPacket.QuestListFinalize(null!));
    });
}
