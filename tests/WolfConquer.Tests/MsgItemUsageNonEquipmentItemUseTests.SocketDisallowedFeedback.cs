using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task HandlerSocketSupportedOneDisallowedTargetSendsTopLeftBeforeFailureAck()
    {
        await AssertSocketCountRoutingAsync(
            declaredCount: 1, targetItemId: 201_003, expectedResponseUid: SocketUnsupportedTargetUid);
    }

    [Fact]
    public async Task HandlerSocketSupportedSevenDisallowedTargetSendsTopLeftBeforeFailureAck()
    {
        await AssertSocketCountRoutingAsync(
            declaredCount: 7, targetItemId: 201_003, expectedResponseUid: SocketUnsupportedTargetUid);
    }

    [Fact]
    public async Task HandlerSocketSupportedTwelveDisallowedTargetSendsTopLeftBeforeFailureAck()
    {
        await AssertSocketCountRoutingAsync(
            declaredCount: 12, targetItemId: 201_003, expectedResponseUid: SocketUnsupportedTargetUid);
    }

    private static void AssertSocketDisallowedPositionNotice(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal((ushort)2_179, frame.Header.Opcode);
        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var message));
        Assert.Equal(16_711_680U, message.Color);
        Assert.Equal(2_005U, message.ChatType);
        Assert.Equal(0U, message.MessageUid1);
        Assert.Equal(0U, message.MessageUid2);
        Assert.Equal(0U, message.ServerId);
        Assert.Equal(0U, message.Mesh);
        Assert.Equal(0U, message.UnionType);
        Assert.Equal(1U, message.Count);
        Assert.Equal(0U, message.New1);
        Assert.Equal(0U, message.New2);
        Assert.Equal(
            new[]
            {
                "SYSTEM", "ALL", string.Empty, "Sorry, this item cannot be socketed!",
                string.Empty, string.Empty, string.Empty,
            },
            message.Content);
    }
}
