using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    public async ValueTask HandleFrameAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            await HandleFrameCoreAsync(connection, frame, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (NetworkExceptionClassifier.IsPeerDisconnect(ex))
        {
        }
        catch (Exception ex)
        {
            await LogFrameExceptionAsync(
                    connection,
                    frame,
                    ex,
                    sourceSeam: "WorldServerFrameHandler.Dispatch",
                    surface: "world-frame-dispatch")
                .ConfigureAwait(false);
        }
    }

    private async ValueTask HandleFrameCoreAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (frame.Header.Opcode == WorldPacketOpcodes.InternalSnapshotRequest)
        {
            await HandleSnapshotRequestAsync(connection, frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (frame.Header.Opcode == WorldPacketOpcodes.InternalWorldEnterRequest)
        {
            await HandleWorldEnterRequestAsync(connection, frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (frame.Header.Opcode == LegacyMsgLoginGamePacket.Opcode)
        {
            await HandleLegacyWorldLoginAsync(connection, frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (frame.Header.Opcode == LegacyNewRolePacket.Opcode)
        {
            await HandleLegacyNewRoleAsync(connection, frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await TryHandleVisibleDx8D13PacketAsync(connection, frame, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        if (_protocolProfile == ConquerProtocolProfile.VisibleDx8D13 &&
            frame.Header.Opcode == WorldPacketOpcodes.MsgItemOperation)
        {
            await _log.WriteLineAsync(
                    "PROTOCOL-COLLISION rejected world packet " +
                    $"profile={_protocolProfile} opcode={frame.Header.Opcode} " +
                    "rootHandler=RootD11_7899.MsgItemOperation action=disconnect " +
                    "reason=VisibleDx8D13 assigns opcode 2427 to a different protobuf schema")
                .ConfigureAwait(false);
            await connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_packetHandlers.TryGetHandler(frame.Header.Opcode, out var handler))
        {
            _sessions.TryGet(connection, out var session);
            var context = new WorldPacketHandlerContext(
                connection,
                _log,
                session,
                _arenaStatistics,
                _arenaStates,
                _worldData,
                _sessions,
                _teamArenaStatistics,
                _teamArenaStates,
                _teamStates,
                _monsterStates,
                _sobNpcStates,
                _floorItems,
                _terrain,
                _guildWarStates,
                _snapshotLoader?.PlayerSpellsRoot,
                _snapshotLoader?.PlayerItemsRoot,
                _mailboxes,
                _snapshotLoader?.PlayerProficienciesRoot,
                _elitePkStates,
                _teamPkStates,
                _dragonWarStates,
                PlayerQuestsRoot: _snapshotLoader?.PlayerQuestsRoot,
                FreezeWarStates: _freezeWarStates,
                ClientCompatibility: _clientCompatibility,
                VoteStates: _voteStates,
                TimeProvider: _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System,
                AccountEmailVerifier: _accountEmailVerifier,
                ProtocolProfile: _protocolProfile,
                ChatItems: _chatItems,
                TeleportTerrain: _teleportTerrain,
                LegacyClock: _runtimeUpdateLoop.LegacyClock ?? LegacySignedMillisecondClock.System);
            try
            {
                await handler(context, frame, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await LogHandlerExceptionAsync(session, frame, ex)
                    .ConfigureAwait(false);
            }

            return;
        }

        await _log.WriteLineAsync(
                LegacyParityGapDiagnostics.UnsupportedPacket(
                    frame.Header.Opcode,
                    packet: "World",
                    reason: "unknown world opcode is not modeled")
                .FormatLogMessage())
            .ConfigureAwait(false);
        await _log.WriteLineAsync(
                "PARITY-GAP unknown world opcode " +
                $"{FormatSession(_sessions.TryGet(connection, out var unknownSession) ? unknownSession : null)} " +
                $"{FormatFrameDiagnostic(frame)} from {connection.RemoteEndPoint}")
            .ConfigureAwait(false);
    }
}
