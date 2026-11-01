using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // These failure paths run under their existing real owner scopes. They
    // grant no publication, retry, rollback or replacement-owner authority.
    [DoesNotReturn]
    private void FailCommittedSpellPublication(SessionHolder holder, LegacyOrdinarySpellBinding? binding,
        LegacyCommittedSpellPublicationException? primary = null)
    {
        var failures = CommittedPublicationFailures(primary, capacity: 3);
        BlockCommittedRecovery(binding?.Recovery,
            "ordinarySpellRecoveryRequired:durableCommitPublicationUnproven", failures);
        ClearCommittedHolder(holder, failures);
        ThrowCommittedPublicationFailure(failures);
    }

    [DoesNotReturn]
    private static void FailCommittedPairPublication(SessionHolder firstHolder, LegacyOrdinarySpellBinding? first,
        SessionHolder secondHolder, LegacyOrdinarySpellBinding? second,
        LegacyCommittedSpellPublicationException? primary = null)
    {
        var failures = CommittedPublicationFailures(primary, capacity: 5);
        BlockCommittedRecovery(first?.Recovery,
            "ordinarySpellRecoveryRequired:durablePairPublicationUnproven", failures);
        BlockCommittedRecovery(second?.Recovery,
            "ordinarySpellRecoveryRequired:durablePairPublicationUnproven", failures);
        ClearCommittedHolder(firstHolder, failures);
        ClearCommittedHolder(secondHolder, failures);
        ThrowCommittedPublicationFailure(failures);
    }

    [DoesNotReturn]
    private static void FailCommittedMailboxPublication(SessionHolder holder, LegacyOrdinarySpellBinding? binding,
        LegacyMailboxPackageProducer producer, string packageReason,
        LegacyCommittedSpellPublicationException? primary = null)
    {
        var failures = CommittedPublicationFailures(primary, capacity: 4);
        BlockCommittedRecovery(producer.Recovery, packageReason, failures);
        BlockCommittedRecovery(binding?.Recovery,
            "ordinarySpellRecoveryRequired:durableCommitPublicationUnproven", failures);
        ClearCommittedHolder(holder, failures);
        ThrowCommittedPublicationFailure(failures);
    }

    private static List<Exception> CommittedPublicationFailures(
        LegacyCommittedSpellPublicationException? primary, int capacity) =>
        new(capacity) { primary ?? new LegacyCommittedSpellPublicationException() };

    private static void BlockCommittedRecovery(LegacyTradeRecoveryState? recovery,
        string reason, List<Exception> failures)
    {
        try { recovery?.Block(reason); }
        catch (Exception failure) { failures.Add(failure); }
    }

    private static void ClearCommittedHolder(SessionHolder holder, List<Exception> failures)
    {
        try { holder.Clear(); }
        catch (Exception failure) { failures.Add(failure); }
    }

    [DoesNotReturn]
    private static void ThrowCommittedPublicationFailure(List<Exception> failures)
    {
        // Preserve the actual primary object and throw site when cleanup worked.
        // Otherwise keep the same outer classification and every failure in the
        // exact order attempted, beginning with the primary committed failure.
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new LegacyCommittedSpellPublicationException(new AggregateException(
            "Committed publication and required recovery cleanup failed.", failures));
    }
}
