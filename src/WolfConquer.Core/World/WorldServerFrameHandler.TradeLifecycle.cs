using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly LegacyTradeStateStore _tradeStates = new();
    private readonly WorldOperationBarrier _worldOperations = new();
    private readonly LegacyTradeRecoveryState _tradeRecovery;
    private readonly ConditionalWeakTable<ConquerTcpConnection, TradeDisconnectRegistration> _tradeLifetimes = new();

    private void RegisterTradeDisconnect(ConquerTcpConnection connection, CancellationToken connectionLifetime)
    {
        connection.RegisterTransportCloseCleanup(() =>
            CancelTradeAfterDisconnectAsync(CreateTradeDisconnectContext(connection), transportClosed: true));
        if (!connectionLifetime.CanBeCanceled) return;
        _tradeLifetimes.GetValue(connection, currentConnection =>
        {
            var context = CreateTradeDisconnectContext(currentConnection);
            return new TradeDisconnectRegistration(connectionLifetime.Register(() =>
            {
                // Transport cancellation can occur inside a write gate. Queue cleanup rather
                // than waiting/reentering that gate from a synchronous cancellation callback.
                QueueOwnedAdmissionDisconnectCleanup(context);
            }));
        });
    }

    private WorldPacketHandlerContext CreateTradeDisconnectContext(ConquerTcpConnection connection) =>
        new(connection, _log, Session: null, Sessions: _sessions,
            PlayerItemsRoot: _snapshotLoader?.PlayerItemsRoot)
        {
            Trades = _tradeStates,
            TradeOperations = _worldOperations,
            TradeRecovery = _tradeRecovery,
        };

    private async Task CancelTradeAfterDisconnectAsync(WorldPacketHandlerContext context, bool transportClosed = false)
    {
        try
        {
            bool hadTrade;
            lock (_tradeStates.SyncRoot)
            {
                _tradeStates.MarkConnectionEnded(context.Connection);
                hadTrade = _tradeStates.Contains(context.Connection);
            }
            // A canceled update scope still only owns trade cleanup. Only the TCP
            // producer's awaited close notification retires a nontrading session.
            if (!hadTrade)
            {
                if (transportClosed) await RemoveClosedTransportSessionAsync(context.Connection).ConfigureAwait(false);
                return;
            }
            if (await WorldPacketHandlers.CancelLegacyTradeForDisconnectAsync(context, CancellationToken.None)
                    .ConfigureAwait(false))
            {
                if (transportClosed) await RemoveClosedTransportSessionAsync(context.Connection).ConfigureAwait(false);
                else _sessions.TryRemove(context.Connection, out _);
            }
            else
                await _log.WriteLineAsync("Trade disconnect cleanup retained unresolved escrow; persistence/recovery required.")
                    .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try { await _log.WriteLineAsync($"Trade disconnect cleanup failed; escrow retained: {ex.GetType().Name}.")
                    .ConfigureAwait(false); }
            catch { /* A failed logger must not become an unobserved cleanup-task exception. */ }
        }
    }

    private async Task RemoveClosedTransportSessionAsync(ConquerTcpConnection connection)
    {
        // Wait out outstanding world actions so an awaited handler/runtime update
        // cannot republish a removed session. Refund/partner sends run outside this lease.
        using var operation = await _worldOperations.EnterExclusiveAsync().ConfigureAwait(false);
        _sessions.TryRemove(connection, out _);
    }

    private sealed record TradeDisconnectRegistration(CancellationTokenRegistration Registration);
}
