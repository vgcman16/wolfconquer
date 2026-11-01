using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalWrite
{
    internal sealed class JournalOwner
    {
        private readonly LegacyPreparedBasicPhysicalWrite writer;
        internal JournalOwner(LegacyPreparedBasicPhysicalWrite writer) => this.writer = writer;
        internal LegacyPhysicalDeathOperation Operation => writer.Operation;
        internal bool IsCurrent() => ReferenceEquals(writer.journalOwner, this) && writer.IsCurrent();
        internal void RequireJournalTargets(string users, string items, string spellRoot, IReadOnlyList<LegacyTradeJournalChange> changes)
        {
            if (!IsCurrent() || !SameRoot(users, writer.Roots.Users) || !SameRoot(items, writer.Roots.Items) ||
                !SameRoot(spellRoot, writer.Roots.Spells) || changes is null ||
                changes.GetType() != typeof(LegacyTradeJournalChange[]) && changes.GetType() != typeof(List<LegacyTradeJournalChange>) ||
                changes.Count != 2) throw Unavailable();
            var character = changes[0]; var spells = changes[1];
            if (character.Kind != LegacyTradeJournalFileKind.Character || character.FileId != writer.Binding.AccountId ||
                character.Before is null || !character.Before.AsSpan().SequenceEqual(writer.characterBefore) ||
                !character.After.AsSpan().SequenceEqual(writer.characterAfter) || spells.Kind != LegacyTradeJournalFileKind.Spells ||
                spells.FileId != writer.Binding.Uid || !Same(spells.Before, writer.spellsBefore) ||
                !spells.After.AsSpan().SequenceEqual(writer.spellsAfter)) throw Unavailable();
        }
        private static bool SameRoot(string supplied, string actual) =>
            string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(supplied)), actual, StringComparison.OrdinalIgnoreCase);
    }

    internal Outcome TryCommit(LegacyJournalInterruption? interruption, out string reason)
    {
        var failure = "basicPhysicalDeathOwnerChanged";
        try
        {
            var outcome = SnapshotFileLockCoordinator.ExecuteLocked(Paths, () =>
            {
                if (!IsCurrent() || !Binding.Recovery.TryRequireLiveJournalClear(out failure) || !ImagesMatch(after: false)) return Outcome.Rejected;
                // Real existing-file access probes detect a current read-only
                // lease before durable intent; they reserve no future access.
                using (var characterAccess = new FileStream(CharacterPath, FileMode.Open, FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    if (spellsBefore is not null)
                        using (var spellAccess = new FileStream(SpellPath, FileMode.Open, FileAccess.ReadWrite,
                            FileShare.ReadWrite | FileShare.Delete)) { }
                }
                LegacyTradeJournalChange[] changes = [
                    new(LegacyTradeJournalFileKind.Character, Binding.AccountId, characterBefore.ToArray(), characterAfter.ToArray()),
                    new(LegacyTradeJournalFileKind.Spells, Binding.Uid, spellsBefore?.ToArray(), spellsAfter.ToArray())];
                if (!LegacyTradeJournal.TryCommitPhysicalDeath(Roots.Users, Roots.Items, Roots.Spells,
                        changes, journalOwner, interruption, out failure))
                    return LegacyTradeJournal.HasPending(Roots.Users) || !ImagesMatch(after: false) ? Outcome.RecoveryRequired : Outcome.Rejected;
                if (LegacyTradeJournal.HasPending(Roots.Users) || !ImagesMatch(after: true)) return Outcome.RecoveryRequired;
                committed = Operation;
                spells.PublishCommittedPhysicalDeath(this);
                return Outcome.Committed;
            });
            reason = failure; return outcome;
        }
        catch (Exception error) when (IsFileFailure(error))
        {
            reason = "basicPhysicalDeathWrite:" + error.GetType().Name;
            try
            {
                return SnapshotFileLockCoordinator.ExecuteLocked(Paths, () =>
                    !LegacyTradeJournal.HasPending(Roots.Users) && ImagesMatch(after: false) ? Outcome.Rejected : Outcome.RecoveryRequired);
            }
            catch (Exception observation) when (IsFileFailure(observation))
            { reason += ";observation:" + observation.GetType().Name; return Outcome.RecoveryRequired; }
        }
    }

    private bool ImagesMatch(bool after) =>
        CharacterSnapshotFileBaseline.TryReadBytes(CharacterPath, "basicPhysicalDeath", out var character, out _) &&
        character.AsSpan().SequenceEqual(after ? characterAfter : characterBefore) &&
        CharacterSnapshotFileBaseline.TryReadBytes(ItemPath, "basicPhysicalDeathItems", out var items, out _) && items.AsSpan().SequenceEqual(itemsBefore) &&
        spells.MatchesPhysicalDeathImage(after ? spellsAfter : spellsBefore);
    private static bool Same(byte[]? first, byte[]? second) => first is null ? second is null :
        second is not null && first.AsSpan().SequenceEqual(second);
    private static bool IsFileFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        InvalidOperationException or ArgumentException or NotSupportedException or System.Security.SecurityException;
}
