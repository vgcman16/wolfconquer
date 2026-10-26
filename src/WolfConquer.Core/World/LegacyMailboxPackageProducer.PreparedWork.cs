using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyMailboxPackageInterruption
{ None, AfterPendingJournal, AfterCharacter, AfterItems, AfterMailbox, AfterCombatGear, AfterJointCommit }

internal sealed partial class LegacyMailboxPackageProducer
{
    internal bool TryPrepareJointWork(LegacyMailboxPackageRecipe recipe, LegacyItemUidReservationReceipt receipt,
        IReadOnlyList<LegacyTradeJournalChange> changes, out PreparedJointWork? work, out string reason)
    {
        work = null;
        reason = "mailboxClaimRecoveryRequired:packageWorkPreparation";
        try
        {
            if (recipe is null || receipt is null || changes is null ||
                !Registry.TryReadProducerIntent(Domain, recipe.OperationId, out var intent, out reason) || intent is null || intent.Allocation != receipt ||
                !intent.CopyPayload().AsSpan().SequenceEqual(recipe.CopyPayload())) return false;
            var copies = changes.Select(change => change with { Before = change.Before?.ToArray(), After = change.After.ToArray() }).ToArray();
            PreparedJointWork? prepared = null;
            var paths = WorkPaths(intent.Request.AccountId, intent.Request.CharacterUid);
            SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                var row = workStore.PrepareWork(intent, copies);
                prepared = PreparedJointWork.FromPersistedWork(this, row, copies, paths);
                RetainSessionPublication(recipe.AccountId, recipe.OperationId);
                return true;
            });
            work = prepared;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsWorkFailure(error))
        { reason = "mailboxClaimRecoveryRequired:packageWorkPreparation:" + error.GetType().Name; return false; }
    }

    internal sealed class PreparedJointWork
    {
        private readonly LegacyMailboxPackageProducer owner;
        private readonly LegacyMailboxPackageWorkStore.Record row;
        private readonly LegacyTradeJournalChange[] changes;
        private readonly string[] paths;

        private PreparedJointWork(LegacyMailboxPackageProducer owner, LegacyMailboxPackageWorkStore.Record row,
            LegacyTradeJournalChange[] changes, string[] paths)
        { this.owner = owner; this.row = row; this.changes = changes; this.paths = paths; }

        internal static PreparedJointWork FromPersistedWork(LegacyMailboxPackageProducer owner,
            LegacyMailboxPackageWorkStore.Record row, LegacyTradeJournalChange[] changes, string[] paths)
        {
            if (!owner.workStore.IsCurrentPrepared(row, changes) ||
                !owner.workStore.TargetPaths(row).SequenceEqual(paths, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
                throw new InvalidOperationException("Package work is not the actual persisted preparation.");
            return new(owner, row with { Before = row.Before.ToArray(), After = row.After.ToArray() },
                changes.Select(change => change with { Before = change.Before!.ToArray(), After = change.After.ToArray() }).ToArray(), paths.ToArray());
        }

        internal bool IsCurrentPrepared() => owner.workStore.IsCurrentPrepared(row, changes);

        internal bool Permits(IReadOnlyList<string> targetPaths, IReadOnlyList<LegacyTradeJournalChange> proposed)
        {
            if (targetPaths.Count != 4 || proposed.Count != 4) return false;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            for (var index = 0; index < 4; index++)
                if (!string.Equals(Path.GetFullPath(targetPaths[index]), paths[index], comparison) ||
                    proposed[index].Kind != changes[index].Kind || proposed[index].FileId != changes[index].FileId ||
                    proposed[index].Before is null || !proposed[index].Before.AsSpan().SequenceEqual(changes[index].Before) ||
                    !proposed[index].After.AsSpan().SequenceEqual(changes[index].After)) return false;
            return true;
        }

        internal bool TryCommit(out LegacyMailboxPackageCommitProof? proof, out bool durable, out string reason)
            => TryCommit(LegacyMailboxPackageInterruption.None, out proof, out durable, out reason);

        internal bool TryCommit(LegacyMailboxPackageInterruption interruption,
            out LegacyMailboxPackageCommitProof? proof, out bool durable, out string reason)
        {
            proof = null;
            durable = false;
            reason = "mailboxClaimRecoveryRequired:packageWorkCommit";
            LegacyMailboxPackageCommitProof? completedProof = null;
            var committed = false;
            var failure = reason;
            if (!Enum.IsDefined(interruption)) return false;
            try
            {
                var success = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                {
                    var current = owner.workStore.Read(Guid.ParseExact(row.Operation, "N"));
                    if (current is null || current.Completed || !LegacyMailboxPackageWorkStore.Same(current, row) || !IsCurrentPrepared() ||
                        !owner.workStore.ImagesMatch(row, after: false)) return false;
                    using var permission = LegacyMailboxPackageMutationFence.EnterPreparedCommit(this);
                    Func<int, bool>? boundary = (int)interruption is >= 1 and <= 5
                        ? count => count == (int)interruption - 1 : null;
                    if (!LegacyMailboxPackageCommitProof.TryCommit(owner.UsersRoot, owner.ItemsRoot, owner.MailboxRoot,
                        owner.CombatGearRoot, changes, boundary, out completedProof, out failure)) return false;
                    committed = true;
                    if (interruption == LegacyMailboxPackageInterruption.AfterJointCommit)
                    { failure = "mailboxClaimRecoveryRequired:packageCompletionInterrupted"; return false; }
                    owner.workStore.CompleteWork(row, completedProof!);
                    failure = string.Empty;
                    return true;
                });
                proof = success ? completedProof : null;
                durable = committed;
                reason = failure;
                return success;
            }
            catch (Exception error) when (IsWorkFailure(error))
            { durable = committed; reason = "mailboxClaimRecoveryRequired:packageWorkCommit:" + error.GetType().Name; return false; }
        }
    }
}
