using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyItemRepairPayment { Money, Meteors }
internal enum LegacyItemRepairOutcome { Rejected, Committed, RecoveryRequired }
internal enum LegacyItemRepairJournalInterruption { None, AfterPending, AfterCharacter, AfterItems }

// Closed real journal images; no player/price/permission authority is issued here.
internal sealed partial class LegacyItemRepairPersistence
{
    private const string Prefix = "itemRepair";
    private readonly string users, items, characterPath, itemPath;
    private readonly LegacyTradeJournalChange[] changes;
    private LegacyItemRepairPersistence(string users, string items, string characterPath, string itemPath,
        LegacyTradeJournalChange[] changes)
    { this.users = users; this.items = items; this.characterPath = characterPath; this.itemPath = itemPath; this.changes = changes; }

    internal static bool TryPrepare(WorldSession before, WorldSession after, string itemsRoot, uint targetUid,
        LegacyItemRepairPayment payment, out LegacyItemRepairPersistence? write, out string reason)
    {
        write = null; reason = Prefix + "BindingInvalid";
        if (before is null || after is null || before.Character is null || after.Character is null ||
            before.Character.Attributes is null || after.Character.Attributes is null ||
            before.Character.PaidNobility is null || after.Character.PaidNobility is null ||
            !Enum.IsDefined(payment) || targetUid == 0 ||
            before.AccountId is <= 0 or > uint.MaxValue || before.Character.Uid is <= 0 or > uint.MaxValue ||
            before.AccountId != after.AccountId || before.Character.Uid != after.Character.Uid || before.UsersRoot != after.UsersRoot ||
            string.IsNullOrWhiteSpace(before.UsersRoot) || !Path.IsPathFullyQualified(before.UsersRoot) ||
            string.IsNullOrWhiteSpace(itemsRoot) || !Path.IsPathFullyQualified(itemsRoot) ||
            !CharactersHaveOnlyPermittedDelta(before.Character, after.Character, payment)) return false;
        try
        {
            var users = Path.TrimEndingDirectorySeparator(Path.GetFullPath(before.UsersRoot));
            var items = Path.TrimEndingDirectorySeparator(Path.GetFullPath(itemsRoot));
            var fileName = before.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
            var character = Path.Combine(users, fileName);
            var item = Path.Combine(items, before.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!(string.Equals(before.Character.SourceFile, fileName, comparison) ||
                Path.IsPathFullyQualified(before.Character.SourceFile) &&
                string.Equals(Path.GetFullPath(before.Character.SourceFile), character, comparison))) return false;
            var failure = reason; LegacyItemRepairPersistence? prepared = null;
            bool valid = SnapshotFileLockCoordinator.ExecuteLocked([character, item], () =>
            {
                if (LegacyTradeJournal.HasPending(users)) { failure = Prefix + "PendingJournal"; return false; }
                if (!Directory.Exists(items) ||
                    !CharacterSnapshotFileBaseline.TryRead(character, Prefix, out var file, out failure) || file is null ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(item, Prefix, out var raw, out failure)) return false;
                var characterAfter = PrepareCharacterImage(file.Bytes, before.Character, after.Character, payment);
                var itemAfter = PrepareItemImage(raw, before.PlayerItems, after.PlayerItems, targetUid, payment);
                prepared = new(users, items, character, item,
                    [new(LegacyTradeJournalFileKind.Character, before.AccountId, file.Bytes, characterAfter),
                     new(LegacyTradeJournalFileKind.Items, before.Character.Uid, raw, itemAfter)]);
                return true;
            });
            write = prepared; reason = valid ? string.Empty : failure; return valid;
        }
        catch (Exception error) when (LegacyEquipmentMovePersistence.IsPreparationFailure(error))
        { reason = Prefix + "Preparation:" + error.GetType().Name; return false; }
    }

    internal LegacyItemRepairOutcome TryCommit(out string reason,
        LegacyItemRepairJournalInterruption interruption = LegacyItemRepairJournalInterruption.None)
    {
        reason = Prefix + "InvalidInterruption";
        bool enteredWriter = false; var failure = Prefix + "PrewriteBaselineUnproven";
        try
        {
            var outcome = SnapshotFileLockCoordinator.ExecuteLocked([characterPath, itemPath], () =>
            {
                if (LegacyTradeJournal.HasPending(users))
                { failure = Prefix + "RecoveryRequired:pending"; return LegacyItemRepairOutcome.RecoveryRequired; }
                if (!ImagesMatch(after: false)) return LegacyItemRepairOutcome.RecoveryRequired;
                if (!Enum.IsDefined(interruption))
                { failure = Prefix + "InvalidInterruption"; return LegacyItemRepairOutcome.Rejected; }
                LegacyJournalInterruption? stop = interruption == LegacyItemRepairJournalInterruption.None ? null :
                    LegacyJournalInterruption.StopAfter((int)interruption - 1);
                enteredWriter = true;
                bool saved = LegacyTradeJournal.TryCommitSingleOwner(users, items, changes, stop, out failure);
                if (saved && !LegacyTradeJournal.HasPending(users) && ImagesMatch(after: true)) return LegacyItemRepairOutcome.Committed;
                if (!saved && !failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal) &&
                    !LegacyTradeJournal.HasPending(users) && ImagesMatch(after: false)) return LegacyItemRepairOutcome.Rejected;
                failure = Prefix + "RecoveryRequired:writerOutcomeUnproven:" + failure;
                return LegacyItemRepairOutcome.RecoveryRequired;
            });
            reason = outcome == LegacyItemRepairOutcome.Committed ? string.Empty : failure; return outcome;
        }
        catch (Exception error) when (LegacyEquipmentMovePersistence.IsPreparationFailure(error))
        {
            reason = Prefix + (enteredWriter ? "RecoveryRequired:writerFault:" : "PrewriteBaselineUnproven:") + error.GetType().Name;
            return LegacyItemRepairOutcome.RecoveryRequired;
        }
    }

    private bool ImagesMatch(bool after) =>
        CharacterSnapshotFileBaseline.TryReadBytes(characterPath, Prefix, out var character, out _) &&
        character.AsSpan().SequenceEqual(after ? changes[0].After : changes[0].Before) &&
        CharacterSnapshotFileBaseline.TryReadBytes(itemPath, Prefix, out var item, out _) &&
        item.AsSpan().SequenceEqual(after ? changes[1].After : changes[1].Before);
    private static InvalidDataException Invalid(string reason) => new(Prefix + ":" + reason);
}
