using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask HandleVisibleDx8D13TrainingVitalityStudyAsync(
        ConquerTcpConnection connection,
        VisibleDx8D13TrainingVitalityPacket packet,
        CancellationToken cancellationToken)
    {
        if (!packet.HasZeroReserved ||
            packet.Power is < (byte)LegacyChiPowerType.Dragon or > (byte)LegacyChiPowerType.Turtle ||
            (packet.LockedFlags & 0xF0) != 0)
        {
            await LogVisibleDx8D13TrainingVitalityAsync(
                    session: null,
                    packet,
                    mutation: "none",
                    reason: !packet.HasZeroReserved
                        ? "reservedNonzero"
                        : packet.Power is < (byte)LegacyChiPowerType.Dragon or > (byte)LegacyChiPowerType.Turtle
                            ? "powerInvalid"
                            : "lockedFlagsInvalid")
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

        if (packet.Uid != session.Character.Uid)
        {
            await LogVisibleDx8D13TrainingVitalityAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason: "uidMismatch")
                .ConfigureAwait(false);
            return;
        }

        if (!LegacyChiStudyState.TryStudy(
                session.Character,
                (LegacyChiPowerType)packet.Power,
                packet.LockedFlags,
                _worldData?.FateExperienceByType,
                randomNextInclusive: null,
                out var mutation,
                out var reason))
        {
            await LogVisibleDx8D13TrainingVitalityAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason)
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { Character = mutation.Character };
        if (!_sessions.TryCompareAndSet(connection, session, updatedSession))
        {
            await LogVisibleDx8D13TrainingVitalityAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason: "sessionConflict")
                .ConfigureAwait(false);
            return;
        }

        foreach (var notification in mutation.ScoreNotifications)
        {
            var frame = new VisibleDx8D13TrainingVitalityScorePacket(
                    (byte)notification.PowerType,
                    notification.AttributeScore,
                    notification.CharacterName)
                .ToFrameBytes();
            foreach (var active in _sessions.Snapshot())
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }

        await connection.SendAsync(
                ChiInfoPacketProfileSelector.CreateFrame(
                    _protocolProfile,
                    mutation.Character,
                    LegacyChiInfoAction.Send),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                ChiInfoPacketProfileSelector.CreateFrame(
                    _protocolProfile,
                    mutation.Character,
                    LegacyChiInfoAction.Upgrade),
                cancellationToken)
            .ConfigureAwait(false);
        await LogVisibleDx8D13TrainingVitalityAsync(
                updatedSession,
                packet,
                mutation: "study",
                reason: reason + ":pointsSpent=" + mutation.PointsSpent)
            .ConfigureAwait(false);
    }
}
