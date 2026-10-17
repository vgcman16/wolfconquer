using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask HandleVisibleDx8D13TrainingVitalityAsync(
        ConquerTcpConnection connection,
        VisibleDx8D13TrainingVitalityPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.Action == (ushort)VisibleDx8D13TrainingVitalityAction.BuyPoints)
        {
            await HandleVisibleDx8D13TrainingVitalityBuyPointsAsync(connection, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == (ushort)VisibleDx8D13TrainingVitalityAction.Study)
        {
            await HandleVisibleDx8D13TrainingVitalityStudyAsync(connection, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == (ushort)VisibleDx8D13TrainingVitalityAction.RetreatChiPowers)
        {
            await HandleVisibleDx8D13TrainingVitalityRetreatInfoAsync(connection, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == (ushort)VisibleDx8D13TrainingVitalityAction.Open)
        {
            await HandleVisibleDx8D13TrainingVitalityOpenAsync(connection, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        _sessions.TryGet(connection, out var session);
        await LogVisibleDx8D13TrainingVitalityAsync(
                session,
                packet,
                mutation: "none",
                reason: packet.HasKnownAction ? "actionFenced" : "actionUnknown")
            .ConfigureAwait(false);
    }

    private async ValueTask HandleVisibleDx8D13TrainingVitalityOpenAsync(
        ConquerTcpConnection connection,
        VisibleDx8D13TrainingVitalityPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.Power != 0 || packet.LockedFlags != 0 || !packet.HasZeroReserved)
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

        var target = packet.Uid == session.Character.Uid
            ? session
            : FindVisibleDx8D13TrainingVitalityTarget(packet.Uid);
        if (target is null)
        {
            await LogVisibleDx8D13TrainingVitalityAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason: "targetMissing")
                .ConfigureAwait(false);
            return;
        }

        await connection.SendAsync(
                ChiInfoPacketProfileSelector.CreateFrame(
                    _protocolProfile,
                    target.Character,
                    LegacyChiInfoAction.Send),
                cancellationToken)
            .ConfigureAwait(false);
        await LogVisibleDx8D13TrainingVitalityAsync(
                session,
                packet,
                mutation: "none",
                reason: packet.Uid == session.Character.Uid ? "openedSelf" : "openedActiveTarget")
            .ConfigureAwait(false);
    }

    private WorldSession? FindVisibleDx8D13TrainingVitalityTarget(uint uid)
    {
        foreach (var active in _sessions.Snapshot())
        {
            if (active.Session.Character.Uid == uid)
            {
                return active.Session;
            }
        }

        return null;
    }

    private ValueTask LogVisibleDx8D13TrainingVitalityAsync(
        WorldSession? session,
        VisibleDx8D13TrainingVitalityPacket packet,
        string mutation,
        string reason)
    {
        return new ValueTask(_log.WriteLineAsync(
            "WIRE-EVIDENCE VisibleDx8D13 CMsgTrainingVitality " +
            $"{FormatSession(session)} opcode={VisibleDx8D13TrainingVitalityPacket.Opcode} " +
            $"frameLength={VisibleDx8D13TrainingVitalityPacket.FrameLength} uid={packet.Uid} " +
            $"action={packet.Action} power={packet.Power} lockedFlags={packet.LockedFlags} " +
            $"reserved={packet.Reserved0},{packet.Reserved1},{packet.Reserved2} " +
            $"knownAction={packet.HasKnownAction} mutation={mutation} reason={reason}"));
    }
}
