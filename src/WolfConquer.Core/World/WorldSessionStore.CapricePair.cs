using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitCapricePair(ProcParticipant attacker, WorldSession proposedAttacker,
        ProcParticipant target, WorldSession proposedTarget, WorldPacketHandlers.LegacyCapricePairIntent intent,
        out CapricePairReceipt? receipt, out string reason)
    {
        receipt = null; reason = "capricePairOwnerUnavailable";
        RefuseBoundSpellReentry(attacker.Connection); RefuseBoundSpellReentry(target.Connection);
        if (ReferenceEquals(attacker.Connection, target.Connection) ||
            !intent.Matches(attacker.Expected, target.Expected, proposedAttacker, proposedTarget)) return false;
        WorldSession finalAttacker, finalTarget; LegacyOrdinarySpellBinding firstBinding, secondBinding;
        CapricePairReceipt prepared;
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages,
                attacker.Expected, target.Expected, proposedAttacker, proposedTarget);
            var firstGate = _connectionGates.GetValue(attacker.Connection, static _ => new object());
            var secondGate = _connectionGates.GetValue(target.Connection, static _ => new object());
            var attackerFirst = GetConnectionGateOrder(attacker.Connection) < GetConnectionGateOrder(target.Connection);
            lock (attackerFirst ? firstGate : secondGate)
            lock (attackerFirst ? secondGate : firstGate)
            {
                using var scope = EnterNativeConnectionGate();
                if (!TryCurrentProcParticipant(attacker, out var firstHolder) ||
                    !TryCurrentProcParticipant(target, out var secondHolder) ||
                    firstHolder.SpellBinding is not { } ownedFirst || secondHolder.SpellBinding is not { } ownedSecond ||
                    !ReferenceEquals(ownedFirst.Recovery, ownedSecond.Recovery) ||
                    !string.Equals(ownedFirst.UsersRoot, ownedSecond.UsersRoot, StringComparison.Ordinal) ||
                    !ownedFirst.Recovery.IsReadyForUsers(ownedFirst.UsersRoot) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, attacker.Expected, proposedAttacker) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, target.Expected, proposedTarget)) return false;
                using var first = new PreparedSessionSpellPublication(this, attacker.Connection, firstHolder,
                    attacker.Expected, proposedAttacker, intent.Attacker);
                using var second = new PreparedSessionSpellPublication(this, target.Connection, secondHolder,
                    target.Expected, proposedTarget, intent.Target);
                if (ReferenceEquals(ownedFirst.Client, ownedSecond.Client) || first.HitpointsWrite is null || second.HitpointsWrite is null)
                    throw InvalidSpellPublication();
                firstBinding = ownedFirst; secondBinding = ownedSecond;
                finalAttacker = first.FinalSession; finalTarget = second.FinalSession;
                prepared = new(this, attacker with { Expected = finalAttacker }, target with { Expected = finalTarget },
                    firstBinding, secondBinding, intent.Attacker.AfterStamina, intent.Target.AfterStamina);
                LegacyTradeJournalChange[] changes = [
                    new(LegacyTradeJournalFileKind.Character, attacker.Expected.AccountId,
                        first.HitpointsWrite.CopyBeforeImage(), first.HitpointsWrite.CopyAfterImage()),
                    new(LegacyTradeJournalFileKind.Character, target.Expected.AccountId,
                        second.HitpointsWrite.CopyBeforeImage(), second.HitpointsWrite.CopyAfterImage())];
                first.ArmFinalProof(); second.ArmFinalProof();
                if (!first.ConsumeBeforeWriterPlain() || !second.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
                var persisted = TryPersistCapricePair(firstBinding, secondBinding, changes, out reason, out var writerCommitted);
                if (!persisted)
                {
                    if (writerCommitted) FailCommittedPairPublication(firstHolder, firstBinding, secondHolder, secondBinding);
                    if (!reason.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal)) return false;
                    var uncertain = new LegacyCapricePairRecoveryRequiredException(reason);
                    try { firstBinding.Recovery.Block("capricePairRecoveryRequired:" + reason); }
                    catch (Exception fencing) { throw new AggregateException("Caprice persistence and recovery fencing failed.", uncertain, fencing); }
                    finally { firstHolder.Clear(); secondHolder.Clear(); }
                    throw uncertain;
                }
                if (!first.AcceptExternallyPersistedHitpoints() || !second.AcceptExternallyPersistedHitpoints() ||
                    !first.IsCurrentPlain() || !second.IsCurrentPlain())
                    FailCommittedPairPublication(firstHolder, firstBinding, secondHolder, secondBinding);
                first.PublishNative(); second.PublishNative();
                first.PublishHolder(); second.PublishHolder();
                first.Dispose(); second.Dispose();
                // The exact prepared full images prove scalar persistence; the
                // real Frame postlude consumes that proof instead of rewriting.
                try
                {
                    PersistFrameOwnedSessionChange(attacker.Connection, attacker.Expected, finalAttacker,
                        attacker.Identity, firstBinding, first.HitpointsWrite);
                    PersistFrameOwnedSessionChange(target.Connection, target.Expected, finalTarget,
                        target.Identity, secondBinding, second.HitpointsWrite);
                }
                catch (WorldSessionPublicationException)
                { firstHolder.Clear(); secondHolder.Clear(); throw; }
            }
        }
        receipt = prepared;
        try
        {
            NotifyPublishedSessionChange(attacker.Connection, attacker.Expected, finalAttacker, attacker.Identity, firstBinding);
            NotifyPublishedSessionChange(target.Connection, target.Expected, finalTarget, target.Identity, secondBinding);
        }
        catch (WorldSessionPublicationException failure) { prepared.FenceBoth(failure); throw; }
        reason = string.Empty; return true;
    }

    private static bool TryPersistCapricePair(LegacyOrdinarySpellBinding first, LegacyOrdinarySpellBinding second,
        LegacyTradeJournalChange[] changes, out string reason, out bool writerCommitted)
    {
        reason = "capricePairCharacterAccessUnavailable";
        writerCommitted = false;
        var paths = changes.Select(change => Path.Combine(first.UsersRoot,
            change.FileId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini")).ToArray();
        var failure = reason;
        bool journalAttempted = false, committedByJournal = false;
        try
        {
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                if (!first.PhysicalOwnerIsCurrentPlain() || !second.PhysicalOwnerIsCurrentPlain())
                { failure = "capricePairPhysicalOwnerChanged"; return false; }
                if (!first.Recovery.TryRequireLiveJournalClear(out failure))
                { failure = LegacyTradeJournal.RecoveryRequired + ":capricePendingOrBlocked"; return false; }
                // These probes write no bytes and close before the journal's
                // read-share handles open. They detect current access refusal,
                // not a reservation against future external handle changes.
                using (var accessFirst = new FileStream(paths[0], FileMode.Open, FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var accessSecond = new FileStream(paths[1], FileMode.Open, FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete)) { }
                journalAttempted = true;
                committedByJournal = LegacyTradeJournal.TryCommitCharacterResources(first.UsersRoot, changes, out failure);
                return committedByJournal;
            });
            writerCommitted = committedByJournal; reason = failure; return committed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        {
            writerCommitted = committedByJournal;
            reason = journalAttempted && LegacyTradeJournal.HasPending(first.UsersRoot)
                ? LegacyTradeJournal.RecoveryRequired + ":capriceAccessCleanup:" + error.GetType().Name
                : "capricePairCharacterAccess:" + error.GetType().Name;
            return false;
        }
    }
}

internal sealed class LegacyCapricePairRefusalException(string reason) : InvalidOperationException(reason) { }
internal sealed class LegacyCapricePairRecoveryRequiredException(string reason) : Exception(reason) { }
