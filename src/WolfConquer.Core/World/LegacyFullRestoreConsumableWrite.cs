using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyFullRestoreConsumableOutcome { Rejected, Committed, RecoveryRequired }
internal enum LegacyFullRestoreConsumableInterruption { None, AfterPending, AfterCharacter, AfterItems }

// Immutable images for the actual v2 Character(AccountId)+Items(UID) journal.
// No permission or current-owner acceptance is supplied by this serializer.
internal sealed class LegacyFullRestoreConsumableWrite
{
    private readonly string users, items, characterPath, itemPath;
    private readonly LegacyTradeJournalChange[] changes;
    private LegacyFullRestoreConsumableWrite(string users, string items, string characterPath, string itemPath,
        LegacyTradeJournalChange[] changes)
    { this.users = users; this.items = items; this.characterPath = characterPath; this.itemPath = itemPath; this.changes = changes; }

    internal static bool TryPrepare(WorldSession before, WorldSession after, string itemsRoot, uint itemUid,
        out LegacyFullRestoreConsumableWrite? write, out string reason)
    {
        write = null; reason = "fullRestoreConsumableImageBindingInvalid";
        if (before.AccountId is <= 0 or > uint.MaxValue || before.Character.Uid is <= 0 or > uint.MaxValue ||
            before.AccountId != after.AccountId || before.Character.Uid != after.Character.Uid || before.UsersRoot != after.UsersRoot ||
            string.IsNullOrWhiteSpace(before.UsersRoot) || !Path.IsPathFullyQualified(before.UsersRoot) ||
            string.IsNullOrWhiteSpace(itemsRoot) || !Path.IsPathFullyQualified(itemsRoot) ||
            after.Character.MinHitPoints < before.Character.MinHitPoints || after.Character.MinMana < before.Character.MinMana ||
            after.Character.MinHitPoints == before.Character.MinHitPoints && after.Character.MinMana == before.Character.MinMana ||
            !WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(before.Character, after.Character with
                { MinHitPoints = before.Character.MinHitPoints, MinMana = before.Character.MinMana }) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(before.Character, after.Character)) return false;
        try
        {
            var users = Path.TrimEndingDirectorySeparator(Path.GetFullPath(before.UsersRoot));
            var items = Path.TrimEndingDirectorySeparator(Path.GetFullPath(itemsRoot));
            var characterPath = Path.Combine(users, before.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            var itemPath = Path.Combine(items, before.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var failure = reason; LegacyFullRestoreConsumableWrite? prepared = null;
            var valid = SnapshotFileLockCoordinator.ExecuteLocked([characterPath, itemPath], () =>
            {
                if (LegacyTradeJournal.HasPending(users) || !Directory.Exists(items) ||
                    !CharacterSnapshotFileBaseline.TryRead(characterPath, "fullRestoreConsumable", out var file, out failure) || file is null ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(itemPath, "fullRestoreConsumable", out var rawItems, out failure)) return false;
                var text = CharacterSnapshotFileEncoding.Decode(file.Bytes, out var encoding);
                var disk = CharacterSnapshotParser.ParseText(text);
                if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(file.Bytes) ||
                    !WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(disk, before.Character) ||
                    CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(disk, before.Character) ||
                    !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(disk, before.Character) ||
                    disk.Attributes.Any(pair => !before.Character.Attributes.TryGetValue(pair.Key, out var value) || value != pair.Value))
                { failure = "fullRestoreConsumableCharacterBaselineStale"; return false; }
                var characterAfter = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(file.Bytes, after.Character);
                var itemAfter = WorldPacketHandlers.PrepareOwnedConsumableItemImage(rawItems, before.PlayerItems, after.PlayerItems, itemUid, LegacyConsumableItemKind.FullRestore);
                prepared = new(users, items, characterPath, itemPath,
                    [new(LegacyTradeJournalFileKind.Character, before.AccountId, file.Bytes, characterAfter),
                     new(LegacyTradeJournalFileKind.Items, before.Character.Uid, rawItems, itemAfter)]);
                return true;
            });
            write = prepared; reason = valid ? string.Empty : failure; return valid;
        }
        catch (Exception error) when (LegacyEquipmentMovePersistence.IsPreparationFailure(error))
        { reason = "fullRestoreConsumablePreparation:" + error.GetType().Name; return false; }
    }

    internal LegacyFullRestoreConsumableOutcome TryCommit(out string reason,
        LegacyFullRestoreConsumableInterruption interruption = LegacyFullRestoreConsumableInterruption.None)
    {
        reason = "fullRestoreConsumableBeforeImageChanged";
        if (!Enum.IsDefined(interruption)) return LegacyFullRestoreConsumableOutcome.Rejected;
        var enteredWriter = false; var failure = reason;
        try
        {
            var result = SnapshotFileLockCoordinator.ExecuteLocked([characterPath, itemPath], () =>
            {
                if (LegacyTradeJournal.HasPending(users))
                { failure = LegacyTradeJournal.RecoveryRequired + ":pending"; return LegacyFullRestoreConsumableOutcome.RecoveryRequired; }
                if (!ImagesMatch(after: false)) return LegacyFullRestoreConsumableOutcome.Rejected;
                Func<int, bool>? stop = interruption == LegacyFullRestoreConsumableInterruption.None ? null :
                    count => count == (int)interruption - 1;
                enteredWriter = true;
                var saved = LegacyTradeJournal.TryCommitSingleOwner(users, items, changes, stop, out failure);
                if (saved && !LegacyTradeJournal.HasPending(users) && ImagesMatch(after: true)) return LegacyFullRestoreConsumableOutcome.Committed;
                if (!saved && !failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal) &&
                    !LegacyTradeJournal.HasPending(users) && ImagesMatch(after: false)) return LegacyFullRestoreConsumableOutcome.Rejected;
                failure = "fullRestoreConsumableRecoveryRequired:" + failure; return LegacyFullRestoreConsumableOutcome.RecoveryRequired;
            });
            reason = result == LegacyFullRestoreConsumableOutcome.Committed ? string.Empty : failure; return result;
        }
        catch (Exception error) when (LegacyEquipmentMovePersistence.IsPreparationFailure(error))
        {
            reason = "fullRestoreConsumableWrite:" + error.GetType().Name;
            return enteredWriter ? LegacyFullRestoreConsumableOutcome.RecoveryRequired : LegacyFullRestoreConsumableOutcome.Rejected;
        }
    }

    private bool ImagesMatch(bool after) =>
        CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "fullRestoreConsumable", out var character, out _) &&
        character.AsSpan().SequenceEqual(after ? changes[0].After : changes[0].Before) &&
        CharacterSnapshotFileBaseline.TryReadBytes(itemPath, "fullRestoreConsumable", out var items, out _) &&
        items.AsSpan().SequenceEqual(after ? changes[1].After : changes[1].Before);
}
