using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const string LegacyDuplicateLoginDialogText =
        "Your account is already logged in on another computer. Please try again.";

    private readonly SemaphoreSlim _legacyDuplicateLoginAdmissionGate = new(1, 1);

    private async ValueTask HandleLegacyWorldLoginAdmissionAsync(
        ConquerTcpConnection connection,
        long characterUid,
        CancellationToken cancellationToken)
    {
        await _legacyDuplicateLoginAdmissionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sessions.TryGet(connection, out _))
            {
                await _log.WriteLineAsync(
                        $"WorldServer ignored repeated legacy MsgLoginGame on accepted connection key={characterUid}.")
                    .ConfigureAwait(false);
                return;
            }

            var snapshotLoad = await LoadSnapshotForLegacyWorldLoginAsync(characterUid).ConfigureAwait(false);
            if (snapshotLoad.Snapshot is null)
            {
                if (snapshotLoad.Status == LegacyWorldLoginSnapshotLoadStatus.MissingSnapshot)
                {
                    _pendingCreations.Remove(connection);
                    _pendingCreations.Add(connection, new PendingLegacyCharacterCreation(characterUid));
                    await connection.SendAsync(LegacyMsgMessagePacket.NewRole().ToFrameBytes(), cancellationToken)
                        .ConfigureAwait(false);
                    await _log.WriteLineAsync(
                            $"WorldServer legacy MsgLoginGame pending NEW_ROLE key={characterUid}.")
                        .ConfigureAwait(false);
                }

                return;
            }

            if (await TryRejectLegacyDuplicateLoginAsync(connection, characterUid, cancellationToken)
                    .ConfigureAwait(false))
            {
                return;
            }

            var snapshot = snapshotLoad.Snapshot;
            if (await TryStartLegacyMailboxRecoveryBeforePrefixAsync(connection, characterUid, snapshot, cancellationToken)
                    .ConfigureAwait(false)) return;
            await StartLegacyOrdinaryExistingCharacterAsync(connection, characterUid, snapshot, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _legacyDuplicateLoginAdmissionGate.Release();
        }
    }

    private async ValueTask<bool> TryRejectLegacyDuplicateLoginAsync(
        ConquerTcpConnection connection,
        long characterUid,
        CancellationToken cancellationToken)
    {
        foreach (var active in _sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, connection) ||
                active.Session.AccountId != characterUid)
            {
                continue;
            }

            // Escrow must remain reachable until its refund is persisted; removing the
            // old session first would make disconnect cleanup unable to recover it.
            var tradeContext = new WorldPacketHandlerContext(active.Connection, _log,
                Sessions: _sessions, PlayerItemsRoot: _snapshotLoader?.PlayerItemsRoot)
            {
                Trades = _tradeStates,
                TradeRecovery = _tradeRecovery,
            };
            if (!await WorldPacketHandlers.CancelLegacyTradeForDisconnectAsync(tradeContext, cancellationToken)
                    .ConfigureAwait(false))
            {
                await connection.SendAsync(
                        LegacyMsgMessagePacket.BoxDialog(LegacyDuplicateLoginDialogText).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await _log.WriteLineAsync(
                        "WorldServer rejected duplicate legacy MsgLoginGame " +
                        $"key={characterUid}; old session and unresolved trade escrow retained.")
                    .ConfigureAwait(false);
                return true;
            }

            if (!_sessions.TryRemove(active.Connection, out var removedSession) || removedSession is null)
            {
                continue;
            }

            await active.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            await connection.SendAsync(
                    LegacyMsgMessagePacket.BoxDialog(LegacyDuplicateLoginDialogText).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync(
                    "WorldServer rejected duplicate legacy MsgLoginGame " +
                    $"key={characterUid} oldUid={removedSession.Character.Uid}; old session removed and disconnected.")
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }
}
