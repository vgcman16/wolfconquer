namespace WolfConquer.Core.World;

// One live world's startup/recovery fence. This is deliberately not an automatic
// replay service for an already-running world: a pending write needs a fresh owner.
internal sealed class LegacyTradeRecoveryState(string? usersRoot, string? itemsRoot)
{
    private readonly string? _questsRoot;

    internal LegacyTradeRecoveryState(string? usersRoot, string? itemsRoot, string? questsRoot)
        : this(usersRoot, itemsRoot)
    {
        _questsRoot = questsRoot;
    }

    private readonly object _gate = new();
    private bool _initialized;
    private string? _failureReason;

    internal bool IsBlocked => Volatile.Read(ref _failureReason) is not null;

    // Evidence of this configured owner's completed startup recovery, not an
    // initializer or a live-world replay operation.
    internal bool IsReadyFor(string? expectedUsersRoot, string? expectedItemsRoot)
    {
        lock (_gate)
        {
            if (!_initialized || _failureReason is not null ||
                string.IsNullOrWhiteSpace(usersRoot) || string.IsNullOrWhiteSpace(itemsRoot) ||
                string.IsNullOrWhiteSpace(expectedUsersRoot) || string.IsNullOrWhiteSpace(expectedItemsRoot) ||
                !Path.IsPathFullyQualified(usersRoot) || !Path.IsPathFullyQualified(itemsRoot) ||
                !Path.IsPathFullyQualified(expectedUsersRoot) || !Path.IsPathFullyQualified(expectedItemsRoot)) return false;
            try
            {
                return SameReadyRoot(usersRoot, expectedUsersRoot) && SameReadyRoot(itemsRoot, expectedItemsRoot);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }
    }

    // Triple readiness is explicit; old pair-only owners never acquire quest
    // authority merely because no recovery record happens to be pending.
    internal bool IsReadyFor(string? expectedUsersRoot, string? expectedItemsRoot, string? expectedQuestsRoot)
    {
        lock (_gate)
        {
            if (!IsReadyFor(expectedUsersRoot, expectedItemsRoot) ||
                string.IsNullOrWhiteSpace(_questsRoot) || string.IsNullOrWhiteSpace(expectedQuestsRoot) ||
                !Path.IsPathFullyQualified(_questsRoot) || !Path.IsPathFullyQualified(expectedQuestsRoot)) return false;
            try
            {
                return SameReadyRoot(_questsRoot, expectedQuestsRoot) &&
                    LegacyTradeJournal.IsQuestDomainConfigured(usersRoot!, itemsRoot!, _questsRoot);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }
    }

    private static bool SameReadyRoot(string first, string second) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

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
                else if (!LegacyTradeJournal.TryRecover(usersRoot, itemsRoot, _questsRoot, out _, out reason))
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
