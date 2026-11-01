using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalHitProgressionWrite
{
    internal sealed class JournalOwner
    {
        private readonly LegacyPreparedBasicPhysicalHitProgressionWrite writer;
        internal JournalOwner(LegacyPreparedBasicPhysicalHitProgressionWrite writer) => this.writer = writer;
        internal LegacyPhysicalHitProgressionOperation Operation => writer.Operation;
        internal bool IsCurrent() => ReferenceEquals(writer.owner, this) && writer.IsCurrent();
        internal void RequireJournalTargets(string users, string items, string spells, string proficiencies,
            IReadOnlyList<LegacyTradeJournalChange> changes)
        {
            if (!IsCurrent() || users != writer.roots.Users || items != writer.roots.Items || spells != writer.roots.Spells ||
                proficiencies != writer.proficiencies || changes.Count != writer.changes.Length) throw Invalid();
            for (var index = 0; index < changes.Count; index++)
            {
                var expected = writer.changes[index]; var actual = changes[index];
                if (actual.Kind != expected.Kind || actual.FileId != expected.FileId || !Same(actual.Before, expected.Before) ||
                    !Same(actual.After, expected.After)) throw Invalid();
            }
        }
    }

    internal Outcome TryCommit(LegacyJournalInterruption? interruption, out string reason)
    {
        var failure = "physicalHitProgressionOwnerChanged";
        try
        {
            var outcome = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                if (!IsCurrent() || !hit.ActorBinding.Recovery.TryRequireLiveJournalClear(out failure) ||
                    !hit.TargetBinding.Recovery.TryRequireLiveJournalClear(out failure) || !ImagesMatch(after: false)) return Outcome.Rejected;
                for (var index = 0; index < changes.Length; index++)
                    if (changes[index].Before is not null)
                        using (var access = new FileStream(paths[index], FileMode.Open, FileAccess.ReadWrite,
                            FileShare.ReadWrite | FileShare.Delete)) { }
                if (!LegacyTradeJournal.TryCommitPhysicalHitProgression(roots.Users, roots.Items, roots.Spells, proficiencies,
                        changes, owner, interruption, out failure))
                    return LegacyTradeJournal.HasPending(roots.Users) || !ImagesMatch(after: false) ? Outcome.RecoveryRequired : Outcome.Rejected;
                if (LegacyTradeJournal.HasPending(roots.Users) || !ImagesMatch(after: true)) return Outcome.RecoveryRequired;
                committed = true;
                death?.AcceptCommittedHitProgression(this);
                progression.AcceptCommittedHitProgression(this, publication);
                return Outcome.Committed;
            });
            reason = failure; return outcome;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            reason = "physicalHitProgressionWrite:" + error.GetType().Name;
            try
            {
                return SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                    !LegacyTradeJournal.HasPending(roots.Users) && ImagesMatch(after: false) ? Outcome.Rejected : Outcome.RecoveryRequired);
            }
            catch (Exception observation) when (observation is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or
                ArgumentException or NotSupportedException or System.Security.SecurityException)
            { reason += ";observation:" + observation.GetType().Name; return Outcome.RecoveryRequired; }
        }
    }

    private bool ImagesMatch(bool after)
    {
        for (var index = 0; index < changes.Length; index++)
            if (!Same(ReadImage(paths[index]), after ? changes[index].After : changes[index].Before)) return false;
        return Same(ReadImage(actorCharacterPath), actorCharacter) && Same(ReadImage(actorItemsPath), actorItems) &&
            (death is null || Same(ReadImage(death.ItemPath), death.Operation.ItemsBefore));
    }
    private static byte[]? ReadImage(string path)
    {
        if (!File.Exists(path)) return null;
        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "physicalHitProgression", out var bytes, out var reason))
            throw new IOException(reason);
        return bytes;
    }
    internal bool IsCommittedDeathWriter(LegacyPreparedBasicPhysicalWrite actual) =>
        committed && ReferenceEquals(death, actual) && IsCurrent() && !LegacyTradeJournal.HasPending(roots.Users) && ImagesMatch(after: true);
    internal bool IsCommittedProficiencyWriter(LegacyPreparedBasicPhysicalProficiencyWrite actual,
        WorldSessionStore.BasicPhysicalPublication token) => committed && ReferenceEquals(progression, actual) &&
        ReferenceEquals(publication, token) && !LegacyTradeJournal.HasPending(roots.Users) && ImagesMatch(after: true);
}
