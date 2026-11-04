using System.Runtime.CompilerServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Only the actual successful redemption branch installs this private marker.
    // Its presence can only deny another enter; it grants no SQL/native/Store authority.
    private readonly ConditionalWeakTable<ConquerTcpConnection, SuccessfulInternalHandoff> _successfulInternalHandoffs = new();

    private sealed class SuccessfulInternalHandoff { }

    private async ValueTask HandleWorldEnterRequestAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (!TryAllowInternalWorldEnter(connection, out var eligibilityReason))
        {
            await _log.WriteLineAsync("WorldServer ignored internal world-enter request: " + eligibilityReason + ".")
                .ConfigureAwait(false);
            return;
        }

        if (!InternalWorldEnterRequestPacket.TryParse(frame, out var request))
        {
            await SendResponseAsync(
                    connection,
                    WorldSnapshotWireStatus.InvalidPacket,
                    accountId: 0,
                    snapshot: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync("WorldServer rejected malformed internal world-enter request.").ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var redeemed = _handoffService?.Redeem(request.Ticket) ??
            new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.UnknownTicket);
        if (!redeemed.IsSuccess)
        {
            var accountId = redeemed.Ticket?.AccountId ?? 0;
            await SendResponseAsync(
                    connection,
                    MapHandoffStatus(redeemed.Status),
                    accountId,
                    snapshot: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync(
                    $"WorldServer handoff {redeemed.Status} for account {accountId}.")
                .ConfigureAwait(false);
            return;
        }

        // The actual redemption committed; retain this deny-only marker even if
        // loading, cancellation or output later fails. It grants no other custody.
        _successfulInternalHandoffs.Add(connection, new SuccessfulInternalHandoff());
        if (IsClosedSuccessfulInternalHandoff(connection))
        {
            await _log.WriteLineAsync("WorldServer internal handoff committed before observed transport closure; startup skipped.")
                .ConfigureAwait(false);
            return;
        }
        cancellationToken.ThrowIfCancellationRequested();

        var ticket = redeemed.Ticket!;
        var reply = await SendSnapshotForAccountAsync(connection, ticket.AccountId, cancellationToken, captureManagedOrigin: true)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (IsClosedSuccessfulInternalHandoff(connection)) return;
        if (reply.Snapshot is { } snapshot)
        {
            await StartWorldSessionAsync(connection, ticket.AccountId, ticket.Username, snapshot, cancellationToken, managedOrigin: reply.ManagedOrigin)
                .ConfigureAwait(false);
        }
        else if (reply.Status == WorldSnapshotWireStatus.MissingSnapshot && HasOrdinaryUidNamespace() &&
            !TryRegisterAuthenticatedCreation(connection, ticket, out var creationReason))
            await _log.WriteLineAsync("WorldServer authenticated creation permit refused: " + creationReason + ".")
                .ConfigureAwait(false);
    }

    // InternalWorldEnterRequest already holds this Frame's exclusive world operation.
    // No authorization is consumed and no response is produced on these refusals.
    private bool TryAllowInternalWorldEnter(ConquerTcpConnection connection, out string reason)
    {
        if (connection.HasObservedTransportClose)
            reason = "observedTransportClosed";
        else if (HasSqlConnectionHandoffFence(connection))
            reason = "sqlConnectionHandoffFenced";
        else if (!_creationLifetimes.TryGetValue(connection, out var lifetime) || !lifetime.IsCurrent(connection))
            reason = "connectionLifetimeUnavailable";
        else if (_sessions.TryGet(connection, out _))
            reason = "connectionAlreadyHasSession";
        else if (_successfulInternalHandoffs.TryGetValue(connection, out _))
            reason = "connectionHandoffAlreadySucceeded";
        else if (connection.NativeGameClient is { } native &&
            (native.FullLoading || native.Fake || native.Player is not { UID: 0 } player ||
                !ReferenceEquals(player.Owner, native) || native.Inventory is not null ||
                native.Equipment is not null || native.Rune is not null))
            reason = "nativeConnectionAlreadyInitialized";
        else
        {
            reason = string.Empty;
            return true;
        }
        return false;
    }

    // Closed-only evidence is rechecked at handoff response/publication boundaries.
    // This does not claim atomicity over transport closure racing a later operation.
    private bool IsClosedSuccessfulInternalHandoff(ConquerTcpConnection connection) =>
        connection.HasObservedTransportClose && _successfulInternalHandoffs.TryGetValue(connection, out _);
}
