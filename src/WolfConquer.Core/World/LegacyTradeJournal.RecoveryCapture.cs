using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private sealed record PendingImage(string Path, byte[] Bytes);
    private sealed record RecoveryCapture(JournalRoots Roots, JournalRecord Record, JournalTarget[] Targets, PendingImage Pending);

    private static bool TryCapturePendingImage(string usersRoot, out PendingImage? image, out string reason)
    {
        image = null; reason = string.Empty;
        using var serialized = new JournalDomainScope(usersRoot, mutation: false);
        if (!HasPending(usersRoot)) return true;
        var journal = Path.Combine(Path.GetFullPath(usersRoot), JournalDirectoryName);
        EnsureSafePath(journal);
        var pending = Path.Combine(journal, PendingName);
        var children = Directory.GetFileSystemEntries(journal);
        if (children.Length != 1 || !StringComparer.Ordinal.Equals(children[0], pending))
        { reason = RecoveryRequired + ":unexpectedJournalEntry"; return false; }
        image = new(pending, ReadRecordImage(pending));
        return true;
    }

    private static bool TryCaptureRecovery(string usersRoot, string itemsRoot, string? questsRoot, string? mailboxRoot,
        string? combatGearRoot, LegacyOrdinaryAdmissionRoots? admissionRoots, out RecoveryCapture? capture, out string reason)
    {
        capture = null; reason = string.Empty;
        if (!TryCapturePendingImage(usersRoot, out var pending, out reason)) return false;
        if (pending is null) return true;
        var record = ParseRecord(pending.Bytes);
        if (!TryResolveRecoveryRoots(usersRoot, itemsRoot, questsRoot, mailboxRoot, combatGearRoot,
            admissionRoots, record, out var roots, out reason)) return false;
        capture = new(roots, record, GetTargets(roots, record), pending);
        return true;
    }

    private static bool RecoverCaptured(RecoveryCapture capture, out int recovered, out string reason)
    {
        recovered = 0; reason = string.Empty;
        var count = 0; var failure = string.Empty;
        var result = SnapshotFileLockCoordinator.ExecuteLocked(
            AdmissionLockPaths(capture.Roots, capture.Record, capture.Targets), () =>
            {
                using var serialized = new JournalDomainScope(capture.Roots.Users, mutation: true);
                if (!TryCapturePendingImage(capture.Roots.Users, out var current, out failure)) return false;
                if (current is null)
                {
                    // Absence alone is not completion authority. A concurrent
                    // recovery must have left every exact afterimage/input and
                    // no stage evidence before this becomes a zero-count no-op.
                    try
                    {
                        EnsureAllAfter(capture.Targets);
                        EnsureAdmissionInputs(capture.Roots, capture.Record);
                        foreach (var target in capture.Targets)
                        {
                            var stage = StagePath(target, capture.Record.TransactionId);
                            EnsureSafePath(stage);
                            if (File.Exists(stage) || Directory.Exists(stage))
                                throw new IOException("Captured recovery still has stage evidence.");
                        }
                        return true;
                    }
                    catch (Exception error) when (IsJournalException(error))
                    { failure = RecoveryRequired + ":capturedRecordMissingOrIncomplete:" + error.GetType().Name; return false; }
                }
                if (!JournalDomainComparer.Equals(current.Path, capture.Pending.Path) ||
                    !current.Bytes.AsSpan().SequenceEqual(capture.Pending.Bytes))
                { failure = RecoveryRequired + ":pendingRecordChanged"; return false; }
                var completed = RecoverLocked(capture.Roots, capture.Record, capture.Targets, current.Path, out failure);
                if (completed) count = 1;
                return completed;
            });
        recovered = count; reason = failure; return result;
    }
}
