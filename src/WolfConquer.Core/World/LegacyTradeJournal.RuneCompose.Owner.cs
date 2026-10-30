using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitRuneCompose(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRuneComposeOperation operation,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "runeComposeJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(operation); operation.Validate();
            var frozen = operation with
            {
                Materials = operation.Materials.ToArray(), Metadata = operation.Metadata.ToArray(), Progress = operation.Progress.ToArray(),
                MaterialMetadata = operation.MaterialMetadata.ToArray(),
            };
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var record = CreateRecord(changes, 16) with
            { CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!), RuneCompose = frozen };
            var targets = GetTargets(roots, record); var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(), () =>
            {
                ValidateRecordedRuneComposeImages(record, uint.MaxValue);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "runeComposeJournalInvalid:" + error.GetType().Name; return false; }
    }
}
