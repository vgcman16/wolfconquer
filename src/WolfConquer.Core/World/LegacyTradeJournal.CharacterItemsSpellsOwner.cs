using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitCharacterItemsSpells(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, out string reason) =>
        TryCommitCharacterItemsSpells(usersRoot, itemsRoot, spellsRoot, changes,
            (LegacyJournalInterruption?)null, out reason);

    internal static bool TryCommitCharacterItemsSpells(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyCharacterItemsSpellsJournalInterruption interruption,
        out string reason)
    {
        reason = "characterItemsSpellsJournalInvalid";
        if (!Enum.IsDefined(interruption)) return false;
        using var stop = interruption == LegacyCharacterItemsSpellsJournalInterruption.None ? null :
            LegacyJournalInterruption.StopAfter((int)interruption - 1);
        return TryCommitCharacterItemsSpells(usersRoot, itemsRoot, spellsRoot, changes, stop, out reason);
    }

    internal static bool TryCommitCharacterItemsSpells(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "characterItemsSpellsJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var record = CreateRecord(changes, 9) with
            { CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!) };
            var targets = GetTargets(roots, record);
            var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(),
                () => CommitLocked(roots, record, targets, interruption, out failure));
            reason = failure;
            return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        {
            reason = "characterItemsSpellsJournalInvalid:" + error.GetType().Name;
            return false;
        }
    }
}
