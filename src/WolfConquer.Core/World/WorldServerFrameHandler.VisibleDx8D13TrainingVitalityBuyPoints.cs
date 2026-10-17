using System.Globalization;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask HandleVisibleDx8D13TrainingVitalityBuyPointsAsync(
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

        if (!LegacyChiBuyPointsState.TryBuyToMaximum(
                session.Character,
                _worldData?.FateExperienceByType,
                session.LegacyTradeActive,
                out var mutation,
                out var requiredConquerPoints,
                out var reason))
        {
            if (reason == "conquerPointsInsufficient")
            {
                await connection.SendAsync(
                        LegacyMsgMessagePacket.SystemTalk(
                                "Sorry, you need " +
                                requiredConquerPoints.ToString(CultureInfo.InvariantCulture) +
                                " ConquerPoints")
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await LogVisibleDx8D13TrainingVitalityAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason: reason + ":requiredConquerPoints=" + requiredConquerPoints)
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
                mutation: "buyPoints",
                reason: reason +
                    ":pointsPurchased=" + mutation.PointsPurchased +
                    ":conquerPointsSpent=" + mutation.ConquerPointsSpent +
                    ":maximumChiPoints=" + mutation.MaximumChiPoints)
            .ConfigureAwait(false);
    }
}
