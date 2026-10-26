using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    internal const string Writer = "mailbox-package-v1";
    internal LegacyItemUidRegistry Registry { get; }
    internal Guid Domain { get; }
    internal LegacyTradeRecoveryState Recovery { get; }
    internal string UsersRoot { get; }
    internal string ItemsRoot { get; }
    internal string MailboxRoot { get; }
    internal string CombatGearRoot { get; }
    private readonly LegacyMailboxPackageWorkStore workStore;
    private int storeBound;
    internal void NotifyStoreBinding() => Interlocked.Exchange(ref storeBound, 1);

    private LegacyMailboxPackageProducer(LegacyItemUidRegistry registry, Guid domain,
        LegacyTradeRecoveryState recovery, string users, string items, string mailbox, string gear)
    {
        Registry = registry; Domain = domain; Recovery = recovery;
        UsersRoot = users; ItemsRoot = items; MailboxRoot = mailbox; CombatGearRoot = gear;
        workStore = new(this);
    }

    internal static bool TryCreate(LegacyItemUidRegistry registry, Guid expectedDomain,
        LegacyTradeRecoveryState recovery, string usersRoot, string itemsRoot, string mailboxRoot,
        string combatGearRoot, out LegacyMailboxPackageProducer? producer, out string reason)
        => TryCreate(registry, expectedDomain, recovery, usersRoot, itemsRoot, mailboxRoot, combatGearRoot,
            LegacyMailboxHistoryInterruption.None, out producer, out reason);

    // Failure-only migration boundary; it supplies no writer, owner, or acceptance callback.
    internal static bool TryCreate(LegacyItemUidRegistry registry, Guid expectedDomain,
        LegacyTradeRecoveryState recovery, string usersRoot, string itemsRoot, string mailboxRoot,
        string combatGearRoot, LegacyMailboxHistoryInterruption interruption,
        out LegacyMailboxPackageProducer? producer, out string reason)
    {
        producer = null;
        reason = "mailboxPackageProducerUnavailable";
        if (registry is null || recovery is null || expectedDomain == Guid.Empty || !Enum.IsDefined(interruption) ||
            !recovery.IsReadyForMailboxGear(usersRoot, itemsRoot, mailboxRoot, combatGearRoot) ||
            !registry.TryValidateProducerBinding(expectedDomain, usersRoot, itemsRoot, mailboxRoot, out reason)) return false;
        var candidate = new LegacyMailboxPackageProducer(registry, expectedDomain, recovery, Normalize(usersRoot), Normalize(itemsRoot),
            Normalize(mailboxRoot), Normalize(combatGearRoot));
        if (!registry.TryClaimMailboxPackageProducer(candidate, out reason)) return false;
        LegacyMailboxPackageMutationFence.Register(candidate);
        try { candidate.workStore.EnsureHistoryLayout(interruption); }
        catch (Exception error) when (IsWorkFailure(error))
        { reason = "mailboxClaimRecoveryRequired:historyLayout:" + error.GetType().Name; return false; }
        if (!candidate.TryReconcileRetainedWork(out reason)) return false;
        producer = candidate;
        reason = string.Empty;
        return true;
    }

    internal bool IsCurrent(string? users, string? items, string? mailbox, LegacyTradeRecoveryState? recovery) =>
        ReferenceEquals(recovery, Recovery) && Same(users, UsersRoot) && Same(items, ItemsRoot) && Same(mailbox, MailboxRoot) &&
        Recovery.IsReadyForMailboxGear(users, items, mailbox, CombatGearRoot) &&
        Registry.TryValidateProducerBinding(Domain, UsersRoot, ItemsRoot, MailboxRoot, out _) && Registry.IsMailboxPackageProducer(this);

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool Same(string? expected, string actual) => !string.IsNullOrWhiteSpace(expected) &&
        Path.IsPathFullyQualified(expected) && string.Equals(Normalize(expected), actual,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
