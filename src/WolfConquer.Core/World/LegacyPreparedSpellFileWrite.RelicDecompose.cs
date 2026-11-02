using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    private LegacyRelicDecomposeOwnedEvidence? committedRelicDecompose;
    private LegacyTradeJournalChange[]? committedRelicDecomposeChanges;

    // Uses the actual prepared spell/file owner and fixed relic journal. No
    // external saver or caller-accepted publication result is accepted.
    internal bool TryCommitRelicDecompose(string itemsRoot, IReadOnlyList<LegacyTradeJournalChange> changes,
        LegacyRelicDecomposeOwnedEvidence producer, LegacyJournalInterruption? interruption, out string reason)
    {
        ArgumentNullException.ThrowIfNull(producer);
        producer.RequireJournalWriter(this);
        var owned = ValidateChanges(changes);
        var items = owned.Where(change => change.Kind == LegacyTradeJournalFileKind.Items).ToArray();
        if (binding is null || owned.Length != 3 || items.Length != 1 ||
            items[0].FileId != expected.Character.Uid || items[0].Before is null ||
            !Path.IsPathFullyQualified(itemsRoot) || !Recovery.IsReadyForCharacterItemsSpells(UsersRoot, itemsRoot, SpellsRoot))
            throw Unavailable();
        var failure = "relicDecomposeSavedImageChanged";
        var success = SnapshotFileLockCoordinator.ExecuteLocked([CharacterPath,
            Path.Combine(itemsRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin"), SpellsPath], () =>
        {
            if (!RequireReady(out failure)) return false;
            producer.RequireJournalTargets(UsersRoot, itemsRoot, SpellsRoot, owned);
            if (!LegacyTradeJournal.TryCommitRelicDecompose(UsersRoot, itemsRoot, SpellsRoot, owned, producer, interruption, out failure))
                return false;
            try
            {
                PublishCommittedEvidencePlain(); committedRelicDecompose = producer; committedRelicDecomposeChanges = owned;
                producer.RetireCommittedAllocation(this);
                failure = string.Empty; return true;
            }
            catch (LegacyCommittedSpellPublicationException) { throw; }
            catch (Exception primary)
            {
                Exception retained = primary;
                try { Recovery.Block("ordinarySpellRecoveryRequired:relicDecomposeCommittedWriterFault"); }
                catch (Exception fencing) { retained = new AggregateException(primary, fencing); }
                throw new LegacyCommittedSpellPublicationException(retained);
            }
        });
        if (!success && failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal)) Recovery.Block(failure);
        reason = failure; return success;
    }

    internal void RequireRelicDecomposeNamespaceRetirement(LegacyItemUidRegistry registry,
        LegacyRelicDecomposeOwnedEvidence producer, LegacyItemUidReservationReceipt receipt)
    {
        if (!HasCommittedRelicDecompose(producer) || committedRelicDecomposeChanges is not { Length: 3 } changes ||
            !RelicDecomposeCommittedOwnerIsCurrentPlain() || !Recovery.TryRequireLiveJournalClear(out _)) throw Unavailable();
        producer.RequireRegistryAndReceipt(registry, receipt);
        var character = changes.Single(row => row.Kind == LegacyTradeJournalFileKind.Character);
        var items = changes.Single(row => row.Kind == LegacyTradeJournalFileKind.Items);
        var spells = changes.Single(row => row.Kind == LegacyTradeJournalFileKind.Spells);
        if (!CharacterSnapshotFileBaseline.TryReadBytes(CharacterPath, "relicDecomposeRetirement", out var actualCharacter, out _) ||
            !CharacterSnapshotFileBaseline.TryReadBytes(producer.ItemPath, "relicDecomposeRetirement", out var actualItems, out _) ||
            !CharacterSnapshotFileBaseline.TryReadBytes(SpellsPath, "relicDecomposeRetirement", out var actualSpells, out _) ||
            !actualCharacter.AsSpan().SequenceEqual(character.After) || !actualItems.AsSpan().SequenceEqual(items.After) ||
            !actualSpells.AsSpan().SequenceEqual(spells.After) || !prepared!.Matches(actualSpells) ||
            !RelicDecomposeCommittedOwnerIsCurrentPlain()) throw Unavailable();
    }
    // This is a distinct post-journal proof. The original owner predicate is
    // deliberately precommit and must continue to require the baseline image.
    private bool RelicDecomposeCommittedOwnerIsCurrentPlain() => committed && prepared is not null &&
        binding is not null && !Recovery.IsBlocked &&
        store.IsSpellFileWriteOwnerCurrent(connection, expected, identity, binding) &&
        ReferenceEquals(binding.SavedSpells, prepared);

    internal bool HasCommittedRelicDecompose(LegacyRelicDecomposeOwnedEvidence producer) =>
        committed && ReferenceEquals(committedRelicDecompose, producer);
}
