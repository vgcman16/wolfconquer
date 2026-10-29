using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitCharacterSpells(string usersRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, out string reason) =>
        TryCommitCharacterSpells(usersRoot, spellsRoot, changes,
            LegacyCharacterSpellsJournalInterruption.None, out reason);

    internal static bool TryCommitCharacterSpells(string usersRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes,
        LegacyCharacterSpellsJournalInterruption interruption, out string reason)
    {
        reason = "characterSpellsJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        if (!Enum.IsDefined(interruption)) return false;
        try
        {
            var roots = GetCharacterSpellsRoots(usersRoot, spellsRoot);
            var record = CreateRecord(changes, 8) with
            { CharacterSpellsDomain = new(roots.Users, roots.Spells!) };
            var targets = GetTargets(roots, record);
            var failure = string.Empty;
            var stop = interruption == LegacyCharacterSpellsJournalInterruption.None ? null :
                LegacyJournalInterruption.StopAfter((int)interruption - 1);
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(),
                () => CommitLocked(roots, record, targets, stop, out failure));
            reason = failure;
            return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        {
            reason = "characterSpellsJournalInvalid:" + error.GetType().Name;
            return false;
        }
    }
}
