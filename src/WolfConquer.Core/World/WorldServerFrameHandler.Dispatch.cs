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
            if (!_tradeRecovery.TryEnsureInitialized(out var recoveryReason))
            {
                await _log.WriteLineAsync($"WorldServer trade recovery blocked world admission/actions: {recoveryReason}.")
                    .ConfigureAwait(false);
                return;
            }
            // Normal frames can run concurrently, but a trade mutation must not
            // cross an awaited handler that will later publish an older session.
            using var operation = frame.Header.Opcode is WorldPacketOpcodes.LegacyTrade or WorldPacketOpcodes.LegacySecondaryPassword ||
                IsLegacyExclusiveItemMutationFrame(frame)
                ? null
                : frame.Header.Opcode is WorldPacketOpcodes.LegacyMsgLoginGame or
                    WorldPacketOpcodes.InternalWorldEnterRequest or WorldPacketOpcodes.LegacyNewRole
                    ? await _worldOperations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false)
                    : await _worldOperations.EnterSharedAsync(cancellationToken).ConfigureAwait(false);
            // A preceding exclusive trade may have fenced recovery while this
            // operation was queued; do not proceed on the earlier readiness read.
            if (_tradeRecovery.IsBlocked || operation is not null &&
                !_tradeRecovery.TryRequireLiveJournalClear(out _))
            {
                await _log.WriteLineAsync("WorldServer trade recovery blocked a queued world action.").ConfigureAwait(false);
                return;
            }
            // Register from login onward, including a pending trade's recipient who
            // may disconnect before ever sending a trade packet of their own.
            RegisterTradeDisconnect(connection, cancellationToken);
            if (!await AdmitMailboxDebtFrameAsync(connection, frame, cancellationToken).ConfigureAwait(false)) return;
            await HandleFrameCoreAsync(connection, frame, cancellationToken, operation).ConfigureAwait(false);
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
        CancellationToken cancellationToken,
        IDisposable? frameOperation)
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
                LegacyClock: _runtimeUpdateLoop.LegacyClock ?? LegacySignedMillisecondClock.System,
                TournamentWatcherHolders: _tournamentWatcherHolders)
            {
                RuneRankings = _runeRankings,
                Trades = _tradeStates,
                TradeOperations = _worldOperations,
                TradeRecovery = _tradeRecovery,
                MailboxPackages = _mailboxPackages,
                FrameOperationLease = frameOperation,
                LocalServerIdentity = _runtimeUpdateLoop.LocalServerIdentity,
            };
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
