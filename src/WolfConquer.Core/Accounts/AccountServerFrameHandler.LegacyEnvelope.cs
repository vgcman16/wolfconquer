using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Accounts;

public sealed partial class AccountServerFrameHandler
{
    private async ValueTask<bool> RejectIncoherentLegacyAuthenticationEnvelopeAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (LegacyAccountPacketEnvelope.TryRead(
                frame,
                LegacyAccountAuthenticationPacket.Opcode,
                LegacyAccountAuthenticationPacket.FrameLength,
                out _))
        {
            return false;
        }

        await SendLegacyForwardAsync(
                connection,
                LegacyAccountForwardType.InvalidAuthenticationProtocol,
                identifier: 0,
                worldEndpoint: null,
                cancellationToken)
            .ConfigureAwait(false);
        await _log.WriteLineAsync("AccountServer rejected incoherent legacy authentication envelope.")
            .ConfigureAwait(false);
        return true;
    }
}
