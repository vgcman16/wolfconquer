using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    private LegacyRelicFuseReturnOwnedEvidence? committedRelicFuseReturn;

    internal bool TryCommitRelicFuseReturn(string itemsRoot, IReadOnlyList<LegacyTradeJournalChange> changes,
        LegacyRelicFuseReturnOwnedEvidence producer, LegacyJournalInterruption? interruption, out string reason)
    {
        ArgumentNullException.ThrowIfNull(producer);
        producer.RequireJournalWriter(this);
        var owned = ValidateChanges(changes);
        var items = owned.Where(change => change.Kind == LegacyTradeJournalFileKind.Items).ToArray();
        if (binding is null || owned.Length != 3 || items.Length != 1 ||
            items[0].FileId != expected.Character.Uid || items[0].Before is null ||
            !Path.IsPathFullyQualified(itemsRoot) || !Recovery.IsReadyForCharacterItemsSpells(UsersRoot, itemsRoot, SpellsRoot))
            throw Unavailable();
        var failure = "relicFuseReturnSavedImageChanged";
        var success = SnapshotFileLockCoordinator.ExecuteLocked([CharacterPath,
            Path.Combine(itemsRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin"), SpellsPath], () =>
        {
            if (!RequireReady(out failure)) return false;
            producer.RequireJournalTargets(UsersRoot, itemsRoot, SpellsRoot, owned);
            if (!LegacyTradeJournal.TryCommitRelicFuseReturn(UsersRoot, itemsRoot, SpellsRoot, owned, producer, interruption, out failure))
                return false;
            PublishCommittedEvidencePlain(); committedRelicFuseReturn = producer;
            failure = string.Empty; return true;
        });
        if (!success && failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal)) Recovery.Block(failure);
        reason = failure; return success;
    }

    internal bool HasCommittedRelicFuseReturn(LegacyRelicFuseReturnOwnedEvidence producer) =>
        committed && ReferenceEquals(committedRelicFuseReturn, producer);
}
