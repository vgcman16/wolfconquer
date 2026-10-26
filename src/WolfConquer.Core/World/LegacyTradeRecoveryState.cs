namespace WolfConquer.Core.World;

// One live world's startup/recovery fence. This is deliberately not an automatic
// replay service for an already-running world: a pending write needs a fresh owner.
internal sealed partial class LegacyTradeRecoveryState(string? usersRoot, string? itemsRoot)
{
    private readonly string? _questsRoot;
    private readonly string? _mailboxRoot;
    private readonly string? _combatGearRoot;
    private readonly LegacyOrdinaryAdmissionRoots? _admissionRoots;

    internal LegacyTradeRecoveryState(string? usersRoot, string? itemsRoot, string? questsRoot, string? mailboxRoot,
        string? combatGearRoot, string? spellsRoot, string? proficienciesRoot)
        : this(usersRoot, itemsRoot, questsRoot, mailboxRoot, combatGearRoot)
    {
        static string? Absolute(string? path) => string.IsNullOrWhiteSpace(path) ? null :
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(usersRoot))
        {
            _admissionRoots = new(Absolute(usersRoot)!, Absolute(itemsRoot), Absolute(spellsRoot),
                Absolute(proficienciesRoot), Absolute(questsRoot), Absolute(combatGearRoot));
            // All six ordinary routes are real configuration, including for a
            // world with no UID producer. Bind denial knowledge before recovery.
            LegacyOrdinaryAdmissionMutationFence.BindConfiguredDomain(_admissionRoots);
        }
    }

    internal LegacyTradeRecoveryState(string? usersRoot, string? itemsRoot, string? questsRoot, string? mailboxRoot,
        string? combatGearRoot) : this(usersRoot, itemsRoot, questsRoot, mailboxRoot)
    {
        _combatGearRoot = combatGearRoot;
    }

    internal LegacyTradeRecoveryState(string? usersRoot, string? itemsRoot, string? questsRoot, string? mailboxRoot)
        : this(usersRoot, itemsRoot, questsRoot)
    {
        _mailboxRoot = mailboxRoot;
    }

    internal LegacyTradeRecoveryState(string? usersRoot, string? itemsRoot, string? questsRoot)
        : this(usersRoot, itemsRoot)
    {
        _questsRoot = questsRoot;
    }

    private readonly object _gate = new();
    private bool _initialized;
    private string? _failureReason;

    internal bool IsBlocked => Volatile.Read(ref _failureReason) is not null;

    // Additive peer readiness predicate; binding above grants no readiness.
    internal bool IsReadyForOrdinaryAdmission(LegacyOrdinaryAdmissionRoots expected)
    {
        lock (_gate)
            return _admissionRoots is not null && _admissionRoots == expected &&
                IsReadyForUsers(expected.Users) && !LegacyTradeJournal.HasPending(expected.Users);
    }

    // One-file actions still require this world's ACTUAL completed startup recovery.
    // They do not gain an unrelated item-file prerequisite or initialize recovery themselves.
    internal bool IsReadyForUsers(string? expectedUsersRoot)
    {
        lock (_gate)
        {
            if (!_initialized || _failureReason is not null ||
                string.IsNullOrWhiteSpace(usersRoot) || string.IsNullOrWhiteSpace(expectedUsersRoot) ||
                !Path.IsPathFullyQualified(usersRoot) || !Path.IsPathFullyQualified(expectedUsersRoot)) return false;
            try { return SameReadyRoot(usersRoot, expectedUsersRoot); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }
    }

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

    internal bool IsReadyForMailbox(string? expectedUsersRoot, string? expectedItemsRoot, string? expectedMailboxRoot)
    {
        lock (_gate)
        {
            if (!IsReadyFor(expectedUsersRoot, expectedItemsRoot) || string.IsNullOrWhiteSpace(_mailboxRoot) ||
                string.IsNullOrWhiteSpace(expectedMailboxRoot) || !Path.IsPathFullyQualified(_mailboxRoot) ||
                !Path.IsPathFullyQualified(expectedMailboxRoot)) return false;
            try
            {
                return SameReadyRoot(_mailboxRoot, expectedMailboxRoot) &&
                    LegacyTradeJournal.IsMailboxDomainConfigured(usersRoot!, itemsRoot!, _mailboxRoot);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }
    }

    internal bool IsReadyForMailboxGear(string? expectedUsersRoot, string? expectedItemsRoot,
        string? expectedMailboxRoot, string? expectedCombatGearRoot)
    {
        lock (_gate)
        {
            if (!IsReadyForMailbox(expectedUsersRoot, expectedItemsRoot, expectedMailboxRoot) ||
                string.IsNullOrWhiteSpace(_combatGearRoot) || string.IsNullOrWhiteSpace(expectedCombatGearRoot) ||
                !Path.IsPathFullyQualified(_combatGearRoot) || !Path.IsPathFullyQualified(expectedCombatGearRoot)) return false;
            try
            {
                return SameReadyRoot(_combatGearRoot, expectedCombatGearRoot) &&
                    LegacyTradeJournal.IsMailboxGearDomainConfigured(usersRoot!, itemsRoot!, _mailboxRoot!, _combatGearRoot);
            }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or System.Security.SecurityException)
            { return false; }
        }
    }

    private static bool SameReadyRoot(string first, string second) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    // Denial only, under the caller's real world-operation lease. Do not replay
    // a live world's journal or let a later fresh owner revive its stale sessions.
    internal bool TryRequireLiveJournalClear(out string reason)
    {
        lock (_gate)
        {
            reason = _failureReason ?? string.Empty;
            if (_failureReason is not null) return false;
            if (!string.IsNullOrWhiteSpace(usersRoot) && LegacyTradeJournal.HasPending(usersRoot))
            {
                reason = LegacyTradeJournal.RecoveryRequired + ":pendingLiveOwner";
                Block(reason);
                return false;
            }
            return true;
        }
    }

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
                if (string.IsNullOrWhiteSpace(itemsRoot) && _admissionRoots is null)
                {
                    if (LegacyTradeJournal.HasPending(usersRoot))
                    {
                        reason = "tradeJournalRecoveryRequired:itemsRootMissing";
                        Block(reason);
                        return false;
                    }
                }
                else if (!LegacyTradeJournal.TryRecover(usersRoot, itemsRoot ?? "", _questsRoot, _mailboxRoot,
                    _combatGearRoot, _admissionRoots, out _, out reason))
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
