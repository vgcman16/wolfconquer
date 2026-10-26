using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionUidOwner
{
    internal const string Writer = "normal-admission-v1";
    private readonly LegacyItemUidRegistry registry;
    private readonly Guid domain;
    private readonly string users, items, mailbox;
    internal LegacyOrdinaryAdmissionRoots Roots { get; }
    internal LegacyItemUidRegistry Registry => registry;
    internal Guid Domain => domain;
    internal string MailboxRoot => mailbox;
    internal bool IsReconciliationComplete => !reconciliationFailed && pending.IsEmpty;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, Guid> pending = new();
    private readonly object allocationGate = new();
    private bool reconciliationFailed = true;

    private LegacyOrdinaryAdmissionUidOwner(LegacyItemUidRegistry registry, Guid domain, LegacyOrdinaryAdmissionRoots roots, string mailbox)
    { this.registry = registry; this.domain = domain; Roots = roots; users = roots.Users; items = roots.Items!; this.mailbox = mailbox; }

    internal static bool TryCreate(LegacyItemUidRegistry registry, Guid domain, LegacyOrdinaryAdmissionRoots roots, string mailbox,
        out LegacyOrdinaryAdmissionUidOwner? owner, out string reason)
    {
        owner = null;
        reason = "ordinaryAdmissionUidOwnerUnavailable";
        try
        {
            if (registry is null || roots?.Items is null ||
                !registry.TryValidateProducerBinding(domain, roots.Users, roots.Items, mailbox, out reason)) return false;
            LegacyTradeJournal.ValidateOrdinaryAdmissionRoots(roots);
            var candidate = new LegacyOrdinaryAdmissionUidOwner(registry, domain, roots, Path.TrimEndingDirectorySeparator(Path.GetFullPath(mailbox)));
            if (!registry.TryClaimOrdinaryAdmissionOwner(candidate, out reason)) return false;
            // A genuine claimed lease may deny writes even if its retained data
            // is corrupt. Registration never mints grant/admission authority.
            LegacyOrdinaryAdmissionMutationFence.Register(candidate);
            if (!candidate.TryReconcile(out reason)) return false;
            candidate.reconciliationFailed = false;
            owner = candidate;
            reason = candidate.IsReconciliationComplete ? string.Empty : "ordinaryAdmissionRetainedWorkFenced";
            return true;
        }
        catch (Exception error) when (OwnerFailure(error))
        { reason = "ordinaryAdmissionUidOwnerUnavailable:" + error.GetType().Name; return false; }
    }

    internal bool IsCurrent(LegacyOrdinaryAdmissionRoots roots) => roots == Roots &&
        registry.IsOrdinaryAdmissionOwner(this) &&
        registry.TryValidateProducerBinding(domain, users, items, mailbox, out _);

    internal bool TryAllocate(LegacyOrdinaryAdmissionRoots roots, uint account, byte[] recipe, int count,
        LegacyOrdinaryAdmissionInterruption interruption, out LegacyItemUidReservationReceipt? receipt, out string reason)
        => TryAllocate(roots, account, recipe, count, interruption, out receipt, out _, out reason);

    internal bool TryAllocate(LegacyOrdinaryAdmissionRoots roots, uint account, byte[] recipe, int count,
        LegacyOrdinaryAdmissionInterruption interruption, out LegacyItemUidReservationReceipt? receipt,
        out LegacyOrdinaryAdmissionAllocationFailure failure, out string reason)
    {
        // Kept as an explicit refusal ABI for old internal callers. Empty gear,
        // zero progress and durable history do not authorize a starter grant.
        failure = LegacyOrdinaryAdmissionAllocationFailure.Rejected;
        receipt = null; reason = "ordinaryAdmissionStarterAllocationRetired";
        return false;
    }

    // A retained fence entry cannot treat an ended/blocked registry lease as
    // proof of quiescence. Only the genuine current owner may report no debt.
    internal bool IsPending(uint account) => !IsCurrent(Roots) || reconciliationFailed || pending.ContainsKey(account);
    internal bool MatchesPending(uint account, byte[] recipe) => false; // No historical migration replay capability.
}
