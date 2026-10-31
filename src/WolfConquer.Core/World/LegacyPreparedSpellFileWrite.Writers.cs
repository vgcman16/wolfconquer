using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    internal bool TryCommitCharacterSpells(IReadOnlyList<LegacyTradeJournalChange> changes, out string reason)
    {
        var owned = ValidateChanges(changes);
        if (owned.Length != 2) throw Unavailable();
        var failure = "ordinarySpellSavedImageChanged";
        var success = SnapshotFileLockCoordinator.ExecuteLocked([CharacterPath, SpellsPath], () =>
        {
            if (!RequireReady(out failure)) return false;
            if (!LegacyTradeJournal.TryCommitCharacterSpells(UsersRoot, SpellsRoot, owned, out failure)) return false;
            PublishCommittedEvidencePlain(); failure = string.Empty; return true;
        });
        if (!success && failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal)) Recovery.Block(failure);
        reason = failure; return success;
    }

    internal bool TryCommitCharacterItemsSpells(string itemsRoot, IReadOnlyList<LegacyTradeJournalChange> changes, out string reason,
        LegacyJournalInterruption? interruption = null, LegacyRuneOperation? runeOperation = null,
        LegacyRuneContainerOperation? runeContainer = null, RuneCharacterPublication? runePublication = null,
        LegacyRuneComposeOperation? runeCompose = null, LegacyRuneChanceOperation? runeChance = null,
        LegacyRuneDecomposeEvidence? runeDecompose = null)
    {
        var owned = ValidateChanges(changes);
        var items = owned.Where(change => change.Kind == LegacyTradeJournalFileKind.Items).ToArray();
        if (owned.Length != 3 || items.Length != 1 || items[0].FileId != expected.Character.Uid || items[0].Before is null)
            throw Unavailable();
        if (!Path.IsPathFullyQualified(itemsRoot) || !Recovery.IsReadyForCharacterItemsSpells(UsersRoot, itemsRoot, SpellsRoot))
            throw Unavailable();
        var failure = "ordinarySpellSavedImageChanged";
        var success = SnapshotFileLockCoordinator.ExecuteLocked([CharacterPath,
            Path.Combine(itemsRoot, expected.Character.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin"), SpellsPath], () =>
        {
            if (!RequireReady(out failure)) return false;
            if ((runeOperation is not null ? 1 : 0) + (runeContainer is not null ? 1 : 0) + (runeCompose is not null ? 1 : 0) +
                (runeChance is not null ? 1 : 0) + (runeDecompose is not null ? 1 : 0) > 1) throw Unavailable();
            if ((runeOperation is not null || runeContainer is not null || runeCompose is not null || runeChance is not null ||
                runeDecompose is not null) != (runePublication is not null)) throw Unavailable();
            runePublication?.RequireBeforeCommit(this, owned, runeOperation, runeContainer, runeCompose, runeChance, runeDecompose?.Operation);
            var committed = runeDecompose is not null
                ? LegacyTradeJournal.TryCommitRuneDecompose(UsersRoot, itemsRoot, SpellsRoot, owned, runeDecompose, interruption, out failure)
                : runeChance is not null
                ? LegacyTradeJournal.TryCommitRuneChance(UsersRoot, itemsRoot, SpellsRoot, owned, runeChance, interruption, out failure)
                : runeCompose is not null
                ? LegacyTradeJournal.TryCommitRuneCompose(UsersRoot, itemsRoot, SpellsRoot, owned, runeCompose, interruption, out failure)
                : runeContainer is not null
                ? LegacyTradeJournal.TryCommitRuneContainer(UsersRoot, itemsRoot, SpellsRoot, owned, runeContainer, interruption, out failure)
                : runeOperation is null
                ? LegacyTradeJournal.TryCommitCharacterItemsSpells(UsersRoot, itemsRoot, SpellsRoot, owned, interruption, out failure)
                : LegacyTradeJournal.TryCommitRuneOperation(UsersRoot, itemsRoot, SpellsRoot, owned, runeOperation, interruption, out failure);
            if (!committed) return false;
            PublishCommittedEvidencePlain(); committedRuneCharacter = runePublication; failure = string.Empty; return true;
        });
        if (!success && failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal)) Recovery.Block(failure);
        reason = failure; return success;
    }

    internal bool TryReplaceSpellsAtomically(out string reason)
    {
        var failure = "ordinarySpellSavedImageChanged";
        var success = SnapshotFileLockCoordinator.ExecuteLocked(SpellsPath, () =>
        {
            if (!RequireReady(out failure)) return false;
            try
            {
                SnapshotFilePersistence.WriteAllBytesAtomically(SpellsPath, after!, ".player-spell-file-");
                if (!prepared!.Matches(ReadImage(SpellsPath))) throw new IOException("Spell replacement could not be verified.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
                NotSupportedException or InvalidOperationException or System.Security.SecurityException)
            {
                bool unchanged;
                try { unchanged = baseline.Matches(ReadImage(SpellsPath)); }
                catch (Exception) { unchanged = false; }
                if (unchanged) { failure = "ordinarySpellWriteFailed:" + error.GetType().Name; return false; }
                Recovery.Block("ordinarySpellRecoveryRequired:spellFileOutcomeUnproven");
                throw new LegacyCommittedSpellPublicationException();
            }
            PublishCommittedEvidencePlain(); failure = string.Empty; return true;
        });
        reason = failure; return success;
    }

    private LegacyTradeJournalChange[] ValidateChanges(IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        if (prepared is null || after is null || committed) throw Unavailable();
        var owned = changes.Select(change => new LegacyTradeJournalChange(change.Kind, change.FileId,
            change.Before?.ToArray(), change.After.ToArray())).ToArray();
        var characters = owned.Where(change => change.Kind == LegacyTradeJournalFileKind.Character).ToArray();
        if (characters.Length != 1 || characters[0].FileId != expected.AccountId || characters[0].Before is null ||
            owned.Any(change => change.Kind is not (LegacyTradeJournalFileKind.Character or LegacyTradeJournalFileKind.Items or LegacyTradeJournalFileKind.Spells)))
            throw Unavailable();
        var spells = owned.Where(change => change.Kind == LegacyTradeJournalFileKind.Spells).ToArray();
        if (spells.Length != 1 || spells[0].FileId != expected.Character.Uid ||
            !baseline.Matches(spells[0].Before) || !prepared.Matches(spells[0].After)) throw Unavailable();
        return owned;
    }

    private bool RequireReady(out string reason)
    {
        reason = "ordinarySpellSavedImageChanged";
        return prepared is not null && after is not null && OwnerIsCurrentPlain() &&
            Recovery.IsReadyForCharacterSpells(UsersRoot, SpellsRoot) && Recovery.TryRequireLiveJournalClear(out reason) &&
            baseline.Matches(ReadImage(SpellsPath));
    }
}
