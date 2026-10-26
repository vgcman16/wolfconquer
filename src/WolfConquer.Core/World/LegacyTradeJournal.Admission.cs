using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyOrdinaryAdmissionInterruption { None, AfterPending, AfterCharacter, AfterItems, AfterSpells, AfterProficiencies,
    AfterUidIntent, AfterUidReservation, AfterJournalComplete }

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitOrdinaryAdmission(LegacyOrdinaryAdmissionRoots configured,
        IReadOnlyList<LegacyTradeJournalChange> changes, IReadOnlyList<LegacyOrdinaryAdmissionInput> inputs,
        out string reason, LegacyOrdinaryAdmissionInterruption interruption = LegacyOrdinaryAdmissionInterruption.None)
        => TryCommitOrdinaryAdmission(configured, changes, inputs, DateTime.Now, out reason, interruption);

    internal static bool TryCommitOrdinaryAdmission(LegacyOrdinaryAdmissionRoots configured,
        IReadOnlyList<LegacyTradeJournalChange> changes, IReadOnlyList<LegacyOrdinaryAdmissionInput> inputs,
        DateTime preparedAt, out string reason,
        LegacyOrdinaryAdmissionInterruption interruption = LegacyOrdinaryAdmissionInterruption.None)
    {
        reason = "ordinaryAdmissionJournalInvalid";
        if (!Enum.IsDefined(interruption)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(inputs);
            if (inputs.Count > 5 || inputs.Any(row => row is null || row.Image?.Length > MaxTargetBytes))
                throw new ArgumentException("Invalid admission input bounds.");
            var roots = GetAdmissionRoots(configured);
            var record = CreateRecord(changes, 7) with
            {
                AdmissionDomain = configured,
                AdmissionInputs = inputs.Select(row => new LegacyOrdinaryAdmissionInput(row.Kind, row.FileId, row.Image?.ToArray())).ToArray(),
                AdmissionPreparedAt = preparedAt.Ticks,
            };
            var targets = GetAdmissionTargets(roots, record);
            var failure = string.Empty;
            bool Stop(int count) => interruption != LegacyOrdinaryAdmissionInterruption.None &&
                (count == 0 ? interruption == LegacyOrdinaryAdmissionInterruption.AfterPending :
                    interruption == (targets[count - 1].Change.Kind switch
                    {
                        LegacyTradeJournalFileKind.Character => LegacyOrdinaryAdmissionInterruption.AfterCharacter,
                        LegacyTradeJournalFileKind.Items => LegacyOrdinaryAdmissionInterruption.AfterItems,
                        LegacyTradeJournalFileKind.Spells => LegacyOrdinaryAdmissionInterruption.AfterSpells,
                        LegacyTradeJournalFileKind.Proficiencies => LegacyOrdinaryAdmissionInterruption.AfterProficiencies,
                        _ => LegacyOrdinaryAdmissionInterruption.None,
                    }));
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(AdmissionLockPaths(roots, record, targets),
                () => CommitLocked(roots, record, targets, Stop, out failure));
            reason = failure;
            return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "ordinaryAdmissionJournalInvalid:" + error.GetType().Name; return false; }
    }

    private static void ValidateAdmissionChanges(LegacyTradeJournalChange[] changes)
    {
        if (changes.Length is < 1 or > 4 || changes.Count(row => row.Kind == LegacyTradeJournalFileKind.Character) != 1 ||
            changes.Select(row => row.Kind).Distinct().Count() != changes.Length)
            throw new ArgumentException("Invalid ordinary-admission target shape.");
        foreach (var row in changes)
        {
            if (row.Kind is not (LegacyTradeJournalFileKind.Character or LegacyTradeJournalFileKind.Items or
                    LegacyTradeJournalFileKind.Spells or LegacyTradeJournalFileKind.Proficiencies) || row.FileId <= 0 ||
                row.After is null || row.After.Length > MaxTargetBytes || row.Before?.Length > MaxTargetBytes ||
                row.Kind == LegacyTradeJournalFileKind.Character && row.Before is null)
                throw new ArgumentException("Invalid ordinary-admission image.");
            ValidateAdmissionSavedImage(row, row.Before);
            ValidateAdmissionSavedImage(row, row.After);
        }
    }
}
