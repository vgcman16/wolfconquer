using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedPairMovementWrite
{
    internal sealed class JournalOwner
    {
        private readonly LegacyPreparedPairMovementWrite write;
        internal JournalOwner(LegacyPreparedPairMovementWrite write) => this.write = write;
        internal LegacyPairMovementOperation Operation => write.Operation;
        internal bool IsCurrent() => ReferenceEquals(write.journalOwner, this) && write.IsCurrent();
        internal void RequireJournalTargets(string usersRoot, IReadOnlyList<LegacyTradeJournalChange> changes)
        {
            if (!IsCurrent() || !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(usersRoot)),
                    write.UsersRoot, StringComparison.OrdinalIgnoreCase) || changes is null ||
                changes.GetType() != typeof(LegacyTradeJournalChange[]) && changes.GetType() != typeof(List<LegacyTradeJournalChange>) ||
                changes.Count != 2)
                throw new InvalidOperationException("pairMovementJournalOwnerInvalid");
            var members = write.actor.Binding.AccountId < write.partner.Binding.AccountId
                ? new[] { write.actor, write.partner } : new[] { write.partner, write.actor };
            for (int index = 0; index < members.Length; index++)
            {
                var change = changes[index]; var member = members[index];
                if (change.Kind != LegacyTradeJournalFileKind.Character || change.FileId != member.Binding.AccountId ||
                    change.Before is null || !change.Before.AsSpan().SequenceEqual(member.BeforeImage) ||
                    !change.After.AsSpan().SequenceEqual(member.AfterImage))
                    throw new InvalidOperationException("pairMovementJournalTargetsChanged");
            }
        }
    }

    internal Outcome TryCommit(LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "pairMovementOwnerChanged";
        if (!IsCurrent()) return Outcome.Rejected;
        string failure = reason;
        try
        {
            var result = SnapshotFileLockCoordinator.ExecuteLocked(new[] { actor.Path, partner.Path }, () =>
            {
                if (!IsCurrent()) return Outcome.Rejected;
                if (LegacyTradeJournal.HasPending(UsersRoot))
                { failure = "pairMovementPendingJournal"; return Outcome.RecoveryRequired; }
                bool success = LegacyTradeJournal.TryCommitPairMovement(UsersRoot, CopyChanges(), journalOwner,
                    interruption, out failure);
                if (success)
                {
                    if (LegacyTradeJournal.HasPending(UsersRoot) || !ImagesMatch(after: true))
                    { failure = "pairMovementCommittedImagesUnproven"; return Outcome.RecoveryRequired; }
                    committed = Operation; // Only the actual successful typed core can mark this pointer.
                    failure = string.Empty; return Outcome.Committed;
                }
                return LegacyTradeJournal.HasPending(UsersRoot) || !ImagesMatch(after: false)
                    ? Outcome.RecoveryRequired : Outcome.Rejected;
            });
            reason = failure;
            return result;
        }
        catch (Exception error) when (IsFileFailure(error))
        {
            reason = "pairMovementWrite:" + error.GetType().Name;
            try
            {
                return SnapshotFileLockCoordinator.ExecuteLocked(new[] { actor.Path, partner.Path }, () =>
                    !LegacyTradeJournal.HasPending(UsersRoot) && ImagesMatch(after: false) ? Outcome.Rejected : Outcome.RecoveryRequired);
            }
            catch (Exception observation) when (IsFileFailure(observation))
            { reason += ";observation:" + observation.GetType().Name; return Outcome.RecoveryRequired; }
        }
    }

    private bool RequireCommittedImages() => ReferenceEquals(committed, Operation) &&
        !LegacyTradeJournal.HasPending(UsersRoot) && ImagesMatch(after: true);
    private static bool IsFileFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        InvalidOperationException or InvalidDataException or ArgumentException or NotSupportedException or System.Security.SecurityException;
}
