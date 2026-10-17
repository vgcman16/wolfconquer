using System.Buffers;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class ConquerServerFirstPayloadTests
{
    [Fact]
    public void AccountServerPayloadUsesLegacyPasswordSeedPacket()
    {
        var payload = ConquerServerFirstPayloads.Create(ConquerRuntimeDefaults.AccountServer);

        Assert.Equal(LegacyAccountPasswordSeedPacket.FrameLength, payload.Length);
        Assert.True(LegacyAccountPasswordSeedPacket.TryParse(ReadFrame(payload.ToArray()), out _));
    }

    [Fact]
    public void WorldServerPayloadUsesCapturedLengthAndVariableBytes()
    {
        var first = ConquerServerFirstPayloads.Create(ConquerRuntimeDefaults.WorldServer);
        var second = ConquerServerFirstPayloads.Create(ConquerRuntimeDefaults.WorldServer);

        Assert.Equal(256, first.Length);
        Assert.Equal(256, second.Length);
        Assert.NotEqual(Convert.ToHexString(first.Span), Convert.ToHexString(second.Span));
    }

    [Fact]
    public void GatewayPayloadIsEmptyBecauseOriginalEndpointIsQuietOnConnect()
    {
        var payload = ConquerServerFirstPayloads.Create(ConquerRuntimeDefaults.Gateway);

        Assert.True(payload.IsEmpty);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
