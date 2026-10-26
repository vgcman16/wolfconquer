using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class Chinese7935XuanbaoEchoRequestTests
{
    [Fact]
    public void FreshNativeEchoActivationUsesFieldFourAndOmitsResponseFields()
    {
        // Official7931 engine carried by7935, SHA256 a7e1aac8...bb413422.
        // Callback009D72DF: UI selection+1, operation6; builder011AF9D2
        // writes fields1/4 only after fresh PB initialization011D2B10.
        // This is selection0's independent wire image, not a gameplay grant.
        var golden = Convert.FromHexString("0800540808062001");
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(golden),
            out var frame, out _, out _));
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownFrame(frame, out var request));
        Assert.NotNull(request);
        Assert.Equal(6U, request.Operation);
        Assert.Equal(1U, request.Fields.Field4);
        Assert.Null(request.Fields.Field2);
        Assert.Null(request.Fields.Field10);
        Assert.False(request.HasData8);
        Assert.Empty(request.AccInfo.ToArray());

        var payload = new Chinese7935XuanbaoOperationPayload(6, new(Field4: 1)).ToPayloadBytes();
        var writer = new PacketWriter(2132, payload.Length);
        payload.CopyTo(writer.Payload);
        Assert.Equal(golden, writer.ToArray());
    }
}
