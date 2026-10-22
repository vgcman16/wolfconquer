namespace WolfConquer.Core.World;

// One live world's startup/recovery fence. This is deliberately not an automatic
// replay service for an already-running world: a pending write needs a fresh owner.
internal sealed class LegacyTradeRecoveryState(string? usersRoot, string? itemsRoot)
{
    private readonly object _gate = new();
    private bool _initialized;
    private string? _failureReason;

    internal bool IsBlocked => Volatile.Read(ref _failureReason) is not null;

    internal void Block(string reason) => Interlocked.CompareExchange(ref _failureReason, reason, null);

    internal bool TryEnsureInitialized(out string reason)
    {
        lock (_gate)
        {
            reason = _failureReason ?? string.Empty;
            if (_failureReason is not null) return false;
            if (_initialized) return true;
            if (string.IsNullOrWhiteSpace(usersRoot))
            {
                _initialized = true; // Read-only/no-snapshot handler fixtures remain usable.
                return true;
            }
            try
            {
                // Finish an interrupted exchange/refund before considering orphan
                // debits. Otherwise a completed counterparty credit could be refunded.
                if (string.IsNullOrWhiteSpace(itemsRoot))
                {
                    if (LegacyTradeJournal.HasPending(usersRoot))
                    {
                        reason = "tradeJournalRecoveryRequired:itemsRootMissing";
                        Block(reason);
                        return false;
                    }
                }
                else if (!LegacyTradeJournal.TryRecover(usersRoot, itemsRoot, out _, out reason))
                {
                    Block(reason);
                    return false;
                }
                if (!LegacyTradeEscrowClaims.TryRecoverAll(usersRoot, out _, out reason))
                {
                    Block(reason);
                    return false;
                }
                _initialized = true;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or OverflowException)
            {
                reason = "tradeRecoveryStartup:" + exception.GetType().Name;
                Block(reason);
                return false;
            }
        }
    }
}
