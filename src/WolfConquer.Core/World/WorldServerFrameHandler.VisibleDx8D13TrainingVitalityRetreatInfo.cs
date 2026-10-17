using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask HandleVisibleDx8D13TrainingVitalityRetreatInfoAsync(
        ConquerTcpConnection connection,
        VisibleDx8D13TrainingVitalityPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.Uid != 0 || packet.Power != 0 || packet.LockedFlags != 0 || !packet.HasZeroReserved)
        {
            await LogVisibleDx8D13TrainingVitalityAsync(
                    session: null,
                    packet,
                    mutation: "none",
                    reason: "auxiliaryFieldsNonzero")
                .ConfigureAwait(false);
            return;
        }

        if (!_sessions.TryGet(connection, out var session) || session is null)
        {
            await LogVisibleDx8D13TrainingVitalityAsync(
                    session: null,
                    packet,
                    mutation: "none",
                    reason: "sessionMissing")
                .ConfigureAwait(false);
            return;
        }

        if (!VisibleDx8D13TrainingVitalityProtectInfoPacket.TryFromSnapshot(
                session.Character,
                out var response,
                out var stateReason))
        {
            await LogVisibleDx8D13TrainingVitalityAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason: "retreatInfoStateInvalid:" + stateReason)
                .ConfigureAwait(false);
            return;
        }

        await connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await LogVisibleDx8D13TrainingVitalityAsync(
                session,
                packet,
                mutation: "none",
                reason: "retreatInfoSent:" + response.Records.Count)
            .ConfigureAwait(false);
    }
}
