using System.Buffers.Binary;
using WolfConquer.Core.World;
using Msg = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMsgMessageBehaviorTests
{
    [Fact]
    public Task GetArrayReplacesProtoCopiesAllScalarsAndRetainsOldAliasesWhileResettingTheWriter() => NativePacketTestThread.Run(() =>
    {
        foreach (bool seal in new[] { false, true })
        {
            using var scope = new Scope(seal);
            LegacyNativeGroupServerList.GroupServers = null!; // Empty ServerName skips the catalog entirely.
            var message = RetainedMessage();
            message.ServerName = "";
            var old = message.proto;
            scope.Packet.Seek(700); scope.Packet.Size = 731;
            Assert.Same(scope.Packet, message.GetArray(scope.Packet, uint.MaxValue));
            var first = message.proto;
            Assert.NotSame(old, first); Assert.Equal(777u, old.Count); Assert.Null(old.Content);
            Assert.Equal(91u, first.Color); Assert.Equal((uint)Msg.ChatMode.World, first.ChatType);
            Assert.Equal(92u, first.MessageUID1); Assert.Equal(93u, first.MessageUID2);
            Assert.Equal(900u, first.Mesh); Assert.Equal(94u, first.New1); Assert.Equal(95u, first.New2);
            Assert.Equal(uint.MaxValue, first.UnionType); Assert.Equal(1u, first.Count); Assert.Equal(0u, first.ServerID);
            Assert.Equal(new[] { "old-from", "old-to", "", "old-message", "", "", "" }, first.Content);
            Assert.Equal(4, scope.Packet.Position);
            byte[] bytes = Bytes(scope.Packet);
            Assert.Equal(bytes.Length - (seal ? 8 : 0), BinaryPrimitives.ReadUInt16LittleEndian(bytes));
            var decoded = Decode(bytes);
            Assert.Equal(first.Content, decoded.Content); Assert.Equal(uint.MaxValue, decoded.UnionType);
            if (seal) Assert.Equal("TQServer", System.Text.Encoding.ASCII.GetString(bytes[^8..]));
            message.__Message = "second";
            message.GetArray(scope.Packet, 0);
            Assert.NotSame(first, message.proto); Assert.NotSame(first.Content, message.proto.Content);
            Assert.Equal("old-message", first.Content[3]); Assert.Equal("second", message.proto.Content[3]);
            Assert.Equal(uint.MaxValue, first.UnionType); Assert.Equal(0u, message.proto.UnionType);
            Assert.Equal("old-from", message._From); Assert.Equal("old-to", message._To); Assert.Equal(900u, message.Mesh);
        }
    });

    [Fact]
    public Task NamedServerIgnoresEscapedSnapshotReplacementsAndMatchingRowBreaksBeforeLaterNull() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var server = new LegacyNativeGroupServerList.Server { ID = 777, Name = "Wolf" };
        scope.Servers.Add(1, server);
        var escaped = scope.Servers.GetValues();
        var cached = scope.Servers.MyArray;
        long revision = scope.Servers.Revision;
        escaped[0] = null!; cached[0] = null!;
        Assert.Same(server, Assert.Single(scope.Servers.GetValues()));
        Assert.Equal(revision, scope.Servers.Revision);
        var message = new Msg("body", Msg.MsgColor.white, Msg.ChatMode.Talk) { ServerName = "unlisted" };
        message.GetArray(scope.Packet);
        Assert.Equal(0u, message.proto.ServerID); Assert.Equal("unlisted", message.proto.Content[6]);
        scope.Servers.Add(2, null!);
        _ = scope.Servers.GetValues();
        message.ServerName = "Wolf";
        message.GetArray(scope.Packet);
        Assert.Equal(0u, message.proto.ServerID); // Native local serverID never takes row.ID.
        Assert.Equal(777u, server.ID); Assert.Equal("Wolf", message.proto.Content[6]);
        var current = scope.Servers.MyArray;
        Assert.Same(server, current[0]); Assert.Null(current[1]); Assert.False(scope.Servers.Update);
        Assert.Equal(revision + 1, scope.Servers.Revision);
        Assert.Null(Assert.Single(escaped)); Assert.Null(Assert.Single(cached));
        Assert.Equal("Wolf", Decode(Bytes(scope.Packet)).Content[6]);
    });

    [Fact]
    public Task NamedAndNullServerCatalogFaultsPublishOnlyNewScalarProtoAfterInitWriter() => NativePacketTestThread.Run(() =>
    {
        foreach (int mode in new[] { 0, 1, 2 })
        {
            using var scope = new Scope();
            var message = RetainedMessage();
            message.ServerName = mode == 1 ? null! : "missing";
            if (mode < 2) LegacyNativeGroupServerList.GroupServers = null!;
            else
            {
                scope.Servers.Add(1, new() { ID = 1, Name = "MISSING" }); // Native comparison is case-sensitive.
                scope.Servers.Add(2, null!);
                var escaped = scope.Servers.GetValues();
                Array.Clear(escaped); // Only the escaped snapshot changes; the real null row still faults.
            }
            var old = message.proto;
            scope.Packet.Size = 731; scope.Packet.Seek(700);
            var before = Bytes(scope.Packet, 1024);
            Assert.Throws<NullReferenceException>(() => message.GetArray(scope.Packet, 73));
            Assert.NotSame(old, message.proto); Assert.Equal(777u, old.Count);
            Assert.Equal(91u, message.proto.Color); Assert.Equal(900u, message.proto.Mesh);
            Assert.Equal(73u, message.proto.UnionType); Assert.Equal(1u, message.proto.Count);
            Assert.Equal(0u, message.proto.ServerID); Assert.Null(message.proto.Content);
            Assert.Equal(4, scope.Packet.Position); Assert.Equal(731, scope.Packet.Size);
            Assert.Equal(before, Bytes(scope.Packet, 1024));
            Assert.Equal("old-message", message.__Message); Assert.Equal("old-from", message._From);
        }
    });

    [Fact]
    public Task NullWriterFaultsBeforeReplacingProtoOrReadingTheServerCatalog() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        LegacyNativeGroupServerList.GroupServers = null!;
        var message = RetainedMessage();
        var old = message.proto;
        Assert.Throws<NullReferenceException>(() => message.GetArray(null!, 99));
        Assert.Same(old, message.proto); Assert.Equal(777u, old.Count);
        Assert.Equal("old-server", message.ServerName); Assert.Equal("old-message", message.__Message);
    });

    [Fact]
    public Task DeserializeCopiesScalarsAndOnlyPresentStringSlotsUsingActualInputWithoutCountOpcodeOrCallerCursorAdmission() => NativePacketTestThread.Run(() =>
    {
        foreach (int count in Enumerable.Range(1, 8))
        {
            using var scope = new Scope();
            var content = Enumerable.Range(0, count).Select(index => index == 3 ? "body\0狼" : "slot" + index).ToArray();
            scope.Frame(Source(content));
            int length = scope.Packet.Size;
            scope.Packet.Seek(2); scope.Packet.Write((ushort)0xBEEF); // MsgMessage has no opcode check.
            var before = Bytes(scope.Packet, 1024);
            scope.Packet.Seek(700); // Generic framed decoding begins at its own header.
            var message = RetainedMessage();
            var old = message.proto;
            message.Deserialize(scope.Packet);
            Assert.NotSame(old, message.proto); Assert.Equal(777u, old.Count);
            AssertScalarsCopied(message);
            Assert.Equal(content, message.proto.Content);
            Assert.Equal(content[0], message._From);
            Assert.Equal(count > 1 ? content[1] : "old-to", message._To);
            Assert.Equal(count > 3 ? content[3] : "old-message", message.__Message);
            Assert.Equal(count > 6 ? content[6] : "old-server", message.ServerName);
            Assert.Equal(length, scope.Packet.Position); Assert.Equal(length, scope.Packet.Size);
            Assert.Equal(before, Bytes(scope.Packet, 1024));
        }
    });

    [Fact]
    public Task AbsentSerializedContentCopiesScalarsAndRetainsAllPreviousStringSlots() => NativePacketTestThread.Run(() =>
    {
        foreach (string[]? content in new string[]?[] { null, [] })
        {
            using var scope = new Scope();
            scope.Frame(Source(content));
            int length = scope.Packet.Size;
            var message = RetainedMessage();
            var old = message.proto;
            message.Deserialize(scope.Packet);
            Assert.NotSame(old, message.proto); Assert.Null(message.proto.Content);
            AssertScalarsCopied(message);
            Assert.Equal("old-from", message._From); Assert.Equal("old-to", message._To);
            Assert.Equal("old-message", message.__Message); Assert.Equal("old-server", message.ServerName);
            Assert.Equal(length, scope.Packet.Position);
        }
    });

    [Fact]
    public Task FailedDeserializePreservesThePriorProtoAndEveryOuterScalarAndString() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        foreach (bool nullStream in new[] { true, false })
        {
            var message = RetainedMessage();
            var old = message.proto;
            if (nullStream) Assert.Throws<ArgumentNullException>(() => message.Deserialize(null!));
            else
            {
                scope.Packet.Seek(0); scope.Packet.Write((ushort)3); scope.Packet.Write((ushort)2179);
                scope.Packet.Size = scope.Packet.Position; // Actual completed header, not spare allocation.
                scope.Packet.Seek(700);
                Assert.Throws<InvalidDataException>(() => message.Deserialize(scope.Packet));
                Assert.Equal(700, scope.Packet.Position); Assert.Equal(4, scope.Packet.Size);
            }
            Assert.Same(old, message.proto); Assert.Equal(777u, old.Count);
            Assert.Null(message.proto.Content); Assert.Equal(0u, message.proto.Color); Assert.Equal(0u, message.proto.Mesh);
            Assert.Equal(91u, message.Color); Assert.Equal(Msg.ChatMode.World, message.ChatType);
            Assert.Equal(92u, message.MessageUID1); Assert.Equal(93u, message.MessageUID2);
            Assert.Equal(900u, message.Mesh); Assert.Equal(94u, message.New1); Assert.Equal(95u, message.New2);
            Assert.Equal("old-from", message._From); Assert.Equal("old-to", message._To);
            Assert.Equal("old-message", message.__Message); Assert.Equal("old-server", message.ServerName);
        }
    });

    [Fact]
    public Task NullSourceSlotsRemainInPhysicalProtoButPinnedWireSkippingShiftsDeserializeAssignments() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var source = new Msg("body", "to", null!, Msg.MsgColor.white, Msg.ChatMode.Talk);
        source.GetArray(scope.Packet);
        var stored = source.proto.Content;
        Assert.Equal(7, stored.Length); Assert.Null(stored[0]); Assert.Equal("body", stored[3]);
        var target = RetainedMessage();
        target.Deserialize(scope.Packet);
        Assert.Equal(new[] { "to", "", "body", "", "", "" }, target.proto.Content);
        Assert.Equal("to", target._From); Assert.Equal("", target._To); Assert.Equal("", target.__Message);
        Assert.Equal("old-server", target.ServerName);
        Assert.Same(stored, source.proto.Content); Assert.Null(stored[0]); Assert.Equal("body", stored[3]);
    });
}
