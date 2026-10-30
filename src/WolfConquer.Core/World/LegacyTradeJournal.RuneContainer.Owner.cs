using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitRuneContainer(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRuneContainerOperation operation,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "runeContainerJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(operation); operation.Validate();
            var frozen = operation with { Metadata = operation.Metadata.ToArray() };
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var record = CreateRecord(changes, 15) with
            { CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!), RuneContainer = frozen };
            var targets = GetTargets(roots, record); var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(), () =>
            {
                ValidateRecordedRuneContainerImages(record, uint.MaxValue);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "runeContainerJournalInvalid:" + error.GetType().Name; return false; }
    }
}
