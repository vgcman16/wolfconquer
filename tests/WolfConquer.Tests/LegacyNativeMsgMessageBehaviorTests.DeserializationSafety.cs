using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Msg = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMsgMessageBehaviorTests
{
    [Theory]
    [InlineData("0500830808", false)]
    [InlineData("07008308080110", false)]
    [InlineData("0800830808017203", false)]
    [InlineData("0900830808", true)]
    public Task MalformedMessageRetainsThePriorProtoArrayAndAllOuterState(string hex, bool invalidFrame) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); byte[] input = Convert.FromHexString(hex);
        CopyMessageDecodeInput(scope, input);
        var message = RetainedMessage(); var old = RetainedInnerMessage(); message.proto = old;
        string[] content = old.Content; byte[] before = Bytes(scope.Packet, 1024);
        if (invalidFrame) Assert.Throws<InvalidDataException>(() => message.Deserialize(scope.Packet));
        else Assert.Throws<EndOfStreamException>(() => message.Deserialize(scope.Packet));
        AssertRetainedMessageDecodeState(message, old, content);
        Assert.Equal(700, scope.Packet.Position); Assert.Equal(input.Length, scope.Packet.Size);
        Assert.Equal(before, Bytes(scope.Packet, 1024));
    });

    [Fact]
    public unsafe Task DisposedMessageInputRetainsTheCompleteOldPublishedState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); CopyMessageDecodeInput(scope, [4, 0, 0x83, 0x08]);
        var message = RetainedMessage(); var old = RetainedInnerMessage(); message.proto = old;
        string[] content = old.Content; nuint cursor = (nuint)scope.Packet.stream;
        scope.Packet.Dispose();
        Assert.Throws<ObjectDisposedException>(() => message.Deserialize(scope.Packet));
        AssertRetainedMessageDecodeState(message, old, content);
        Assert.Equal(cursor, (nuint)scope.Packet.stream); Assert.Equal((nuint)0, (nuint)scope.Packet.Memory);
        Assert.Equal(4, scope.Packet.Size);
    });

    [Fact]
    public Task HeaderOnlyMessageKeepsGenuineAbsentContentAndRetainsOuterStringsFromBeyondSizeCursor() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); byte[] input = [4, 0, 0x83, 0x08]; CopyMessageDecodeInput(scope, input);
        var message = RetainedMessage(); var old = RetainedInnerMessage(); message.proto = old;
        string[] content = old.Content;
        message.Deserialize(scope.Packet);
        Assert.NotSame(old, message.proto); Assert.Null(message.proto.Content); Assert.Equal(0u, message.proto.Count);
        Assert.Equal(new uint[7], new[] { message.Color, (uint)message.ChatType, message.MessageUID1,
            message.MessageUID2, message.Mesh, message.New1, message.New2 });
        Assert.Equal("old-from", message._From); Assert.Equal("old-to", message._To);
        Assert.Equal("old-message", message.__Message); Assert.Equal("old-server", message.ServerName);
        Assert.Same(content, old.Content); Assert.Equal(new[] { "retained0", "retained1" }, old.Content);
        Assert.Equal(777u, old.Count); Assert.Equal(201u, old.Color);
        Assert.Equal(4, scope.Packet.Position); Assert.Equal(4, scope.Packet.Size); Assert.Equal(input, Bytes(scope.Packet));
    });

    private static Msg.MsgTalkProto RetainedInnerMessage() => new()
    {
        Color = 201, ChatType = 202, MessageUID1 = 203, MessageUID2 = 204,
        Mesh = 205, New1 = 206, New2 = 207, ServerID = 208, UnionType = 209,
        Count = 777, Content = ["retained0", "retained1"]
    };

    private static void AssertRetainedMessageDecodeState(Msg message, Msg.MsgTalkProto old, string[] content)
    {
        Assert.Same(old, message.proto); Assert.Same(content, old.Content);
        Assert.Equal(new[] { "retained0", "retained1" }, old.Content);
        Assert.Equal(new uint[] { 201, 202, 203, 204, 205, 206, 207, 208, 209, 777 },
            new[] { old.Color, old.ChatType, old.MessageUID1, old.MessageUID2, old.Mesh,
                old.New1, old.New2, old.ServerID, old.UnionType, old.Count });
        Assert.Equal(new uint[] { 91, (uint)Msg.ChatMode.World, 92, 93, 900, 94, 95 },
            new[] { message.Color, (uint)message.ChatType, message.MessageUID1, message.MessageUID2,
                message.Mesh, message.New1, message.New2 });
        Assert.Equal("old-from", message._From); Assert.Equal("old-to", message._To);
        Assert.Equal("old-message", message.__Message); Assert.Equal("old-server", message.ServerName);
    }

    private static unsafe void CopyMessageDecodeInput(Scope scope, byte[] input)
    {
        Marshal.Copy(input, 0, (nint)scope.Packet.Memory, input.Length);
        scope.Packet.Size = input.Length; scope.Packet.Seek(700);
    }
}
