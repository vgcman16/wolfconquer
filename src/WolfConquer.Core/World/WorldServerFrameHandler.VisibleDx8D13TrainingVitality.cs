using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask<bool> TryHandleVisibleDx8D13PacketAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (_protocolProfile != ConquerProtocolProfile.VisibleDx8D13)
        {
            return false;
        }

        if (frame.Header.Opcode == VisibleDx8D13TrainingVitalityPacket.Opcode)
        {
            if (!VisibleDx8D13TrainingVitalityPacket.TryParse(frame, out var vitalityPacket))
            {
                _sessions.TryGet(connection, out var malformedVitalitySession);
                await _log.WriteLineAsync(
                        "PROTOCOL-MALFORMED consumed world packet " +
                        $"profile={_protocolProfile} {FormatSession(malformedVitalitySession)} " +
                        $"{FormatFrameDiagnostic(frame)} " +
                        $"expectedFrameLength={VisibleDx8D13TrainingVitalityPacket.FrameLength} " +
                        "packet=CMsgTrainingVitality action=ignore")
                    .ConfigureAwait(false);
                return true;
            }

            await HandleVisibleDx8D13TrainingVitalityAsync(
                    connection,
                    vitalityPacket,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (frame.Header.Opcode != VisibleDx8D13TrainingVitalityProtectPacket.Opcode)
        {
            return false;
        }

        if (!VisibleDx8D13TrainingVitalityProtectPacket.TryParse(frame, out var packet))
        {
            _sessions.TryGet(connection, out var malformedSession);
            await _log.WriteLineAsync(
                    "PROTOCOL-MALFORMED consumed world packet " +
                    $"profile={_protocolProfile} {FormatSession(malformedSession)} " +
                    $"{FormatFrameDiagnostic(frame)} " +
                    $"expectedFrameLength={VisibleDx8D13TrainingVitalityProtectPacket.FrameLength} " +
                    "packet=CMsgTrainingVitalityProtect action=ignore")
                .ConfigureAwait(false);
            return true;
        }

        if (packet.Action == (ushort)VisibleDx8D13TrainingVitalityProtectAction.Seal)
        {
            await HandleVisibleDx8D13TrainingVitalitySealAsync(
                    connection,
                    packet,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (packet.Action == (ushort)VisibleDx8D13TrainingVitalityProtectAction.Restore)
        {
            await HandleVisibleDx8D13TrainingVitalityRestoreAsync(
                    connection,
                    packet,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (packet.Action == (ushort)VisibleDx8D13TrainingVitalityProtectAction.CancelRetreat)
        {
            await HandleVisibleDx8D13TrainingVitalityCancelAsync(
                    connection,
                    packet,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        _sessions.TryGet(connection, out var session);
        await _log.WriteLineAsync(
                "WIRE-EVIDENCE VisibleDx8D13 CMsgTrainingVitalityProtect " +
                $"{FormatSession(session)} opcode={frame.Header.Opcode} " +
                $"frameLength={frame.Header.Length} action={packet.Action} mode={packet.Mode} " +
                $"reserved={packet.Reserved} knownAction={packet.HasKnownAction} mutation=none")
            .ConfigureAwait(false);
        return true;
    }

    private async ValueTask HandleVisibleDx8D13TrainingVitalityRestoreAsync(
        ConquerTcpConnection connection,
        VisibleDx8D13TrainingVitalityProtectPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.Reserved != 0 ||
            packet.Mode is < (byte)LegacyChiPowerType.Dragon or > (byte)LegacyChiPowerType.Turtle)
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session: null,
                    packet,
                    mutation: "none",
                    reason: packet.Reserved == 0 ? "modeInvalid" : "reservedNonzero")
                .ConfigureAwait(false);
            return;
        }

        if (!_sessions.TryGet(connection, out var session) || session is null)
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session: null,
                    packet,
                    mutation: "none",
                    reason: "sessionMissing")
                .ConfigureAwait(false);
            return;
        }

        var now = (_runtimeUpdateLoop.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime;
        if (!LegacyChiRetreatState.TryRestoreRetreatedPower(
                session.Character,
                (LegacyChiPowerType)packet.Mode,
                now,
                out var updatedCharacter,
                out var reason))
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason)
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { Character = updatedCharacter };
        if (!_sessions.TryCompareAndSet(connection, session, updatedSession))
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
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
                    updatedCharacter,
                    LegacyChiInfoAction.Send),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                ChiInfoPacketProfileSelector.CreateFrame(
                    _protocolProfile,
                    updatedCharacter,
                    LegacyChiInfoAction.Upgrade),
                cancellationToken)
            .ConfigureAwait(false);

        var acknowledgement = new VisibleDx8D13TrainingVitalityProtectPacket(
            (ushort)VisibleDx8D13TrainingVitalityProtectAction.RestoreAcknowledged,
            packet.Mode,
            Reserved: 0);
        await connection.SendAsync(acknowledgement.ToResponseFrameBytes(), cancellationToken).ConfigureAwait(false);
        await LogVisibleDx8D13TrainingVitalityProtectAsync(
                updatedSession,
                packet,
                mutation: "restore",
                reason)
            .ConfigureAwait(false);
    }

    private async ValueTask HandleVisibleDx8D13TrainingVitalityCancelAsync(
        ConquerTcpConnection connection,
        VisibleDx8D13TrainingVitalityProtectPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.Reserved != 0 ||
            packet.Mode is < (byte)LegacyChiPowerType.Dragon or > (byte)LegacyChiPowerType.Turtle)
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session: null,
                    packet,
                    mutation: "none",
                    reason: packet.Reserved == 0 ? "modeInvalid" : "reservedNonzero")
                .ConfigureAwait(false);
            return;
        }

        if (!_sessions.TryGet(connection, out var session) || session is null)
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session: null,
                    packet,
                    mutation: "none",
                    reason: "sessionMissing")
                .ConfigureAwait(false);
            return;
        }

        if (!LegacyChiRetreatState.TryCancelRetreat(
                session.Character,
                (LegacyChiPowerType)packet.Mode,
                out var updatedCharacter,
                out var reason))
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason)
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { Character = updatedCharacter };
        if (!_sessions.TryCompareAndSet(connection, session, updatedSession))
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason: "sessionConflict")
                .ConfigureAwait(false);
            return;
        }

        var acknowledgement = new VisibleDx8D13TrainingVitalityProtectPacket(
            (ushort)VisibleDx8D13TrainingVitalityProtectAction.CancelRetreat,
            packet.Mode,
            Reserved: 0);
        await connection.SendAsync(acknowledgement.ToResponseFrameBytes(), cancellationToken).ConfigureAwait(false);
        await LogVisibleDx8D13TrainingVitalityProtectAsync(
                updatedSession,
                packet,
                mutation: "cancel",
                reason)
            .ConfigureAwait(false);
    }

    private async ValueTask HandleVisibleDx8D13TrainingVitalitySealAsync(
        ConquerTcpConnection connection,
        VisibleDx8D13TrainingVitalityProtectPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.Reserved != 0 ||
            packet.Mode is < (byte)LegacyChiPowerType.Dragon or > (byte)LegacyChiPowerType.Turtle)
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session: null,
                    packet,
                    mutation: "none",
                    reason: packet.Reserved == 0 ? "modeInvalid" : "reservedNonzero")
                .ConfigureAwait(false);
            return;
        }

        if (!_sessions.TryGet(connection, out var session) || session is null)
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session: null,
                    packet,
                    mutation: "none",
                    reason: "sessionMissing")
                .ConfigureAwait(false);
            return;
        }

        var now = (_runtimeUpdateLoop.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime;
        if (!LegacyChiRetreatState.TrySealCurrentPower(
                session.Character,
                (LegacyChiPowerType)packet.Mode,
                now,
                out var updatedCharacter,
                out var reason))
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason)
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { Character = updatedCharacter };
        if (!_sessions.TryCompareAndSet(connection, session, updatedSession))
        {
            await LogVisibleDx8D13TrainingVitalityProtectAsync(
                    session,
                    packet,
                    mutation: "none",
                    reason: "sessionConflict")
                .ConfigureAwait(false);
            return;
        }

        var acknowledgement = new VisibleDx8D13TrainingVitalityProtectPacket(
            (ushort)VisibleDx8D13TrainingVitalityProtectAction.SealAcknowledged,
            packet.Mode,
            Reserved: 0);
        await connection.SendAsync(acknowledgement.ToResponseFrameBytes(), cancellationToken).ConfigureAwait(false);
        await LogVisibleDx8D13TrainingVitalityProtectAsync(
                updatedSession,
                packet,
                mutation: "seal",
                reason)
            .ConfigureAwait(false);
    }

    private ValueTask LogVisibleDx8D13TrainingVitalityProtectAsync(
        WorldSession? session,
        VisibleDx8D13TrainingVitalityProtectPacket packet,
        string mutation,
        string reason)
    {
        return new ValueTask(_log.WriteLineAsync(
            "WIRE-EVIDENCE VisibleDx8D13 CMsgTrainingVitalityProtect " +
            $"{FormatSession(session)} opcode={VisibleDx8D13TrainingVitalityProtectPacket.Opcode} " +
            $"frameLength={VisibleDx8D13TrainingVitalityProtectPacket.FrameLength} " +
            $"action={packet.Action} mode={packet.Mode} reserved={packet.Reserved} " +
            $"knownAction={packet.HasKnownAction} mutation={mutation} reason={reason}"));
    }
}
