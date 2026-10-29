using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask<bool> AdmitMailboxDebtFrameAsync(ConquerTcpConnection connection,
        PacketFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_mailboxPackages is null || !_sessions.TryGet(connection, out var current) || current is null) return true;
        if (LegacyMailboxPacketAdmission.TryRequireCurrent(_mailboxPackages, _sessions, connection, frame, _protocolProfile, out var reason))
            return true;
        await _log.WriteLineAsync($"WorldServer mailbox recovery denied world action: {reason}.").ConfigureAwait(false);
        return false;
    }
}
