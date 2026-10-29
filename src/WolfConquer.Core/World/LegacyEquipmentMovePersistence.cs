using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyEquipmentMoveJournalInterruption { None, AfterPending, AfterCharacter, AfterItems }

// Real unchanged Character + changed Items participant, using the existing v2 WAL.
// This API supplies no identity/permission authority; the actual Store caller owns it.
internal static partial class LegacyEquipmentMovePersistence
{
    internal const string RecoveryRequired = "equipmentMoveRecoveryRequired";
    private const string Prefix = "equipmentMove";

    internal static bool TryPersist(WorldSession expected, WorldSession stable, WorldSession updated,
        string itemsRoot, LegacyItemSaveGraph before, LegacyItemSaveGraph after, out string reason,
        LegacyEquipmentMoveJournalInterruption interruption = LegacyEquipmentMoveJournalInterruption.None)
    {
        reason = "equipmentMoveStorageBindingInvalid";
        if (!Enum.IsDefined(interruption) || expected.AccountId <= 0 || expected.Character.Uid is <= 0 or > uint.MaxValue ||
            expected.AccountId != updated.AccountId || expected.Character.Uid != updated.Character.Uid ||
            !ReferenceEquals(expected.Character, updated.Character) ||
            string.IsNullOrWhiteSpace(expected.UsersRoot) || !Path.IsPathFullyQualified(expected.UsersRoot) ||
            string.IsNullOrWhiteSpace(itemsRoot) || !Path.IsPathFullyQualified(itemsRoot)) return false;
        try
        {
            var users = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected.UsersRoot));
            var items = Path.TrimEndingDirectorySeparator(Path.GetFullPath(itemsRoot));
            var accountName = expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
            var characterPath = Path.Combine(users, accountName);
            var itemPath = Path.Combine(items, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (expected.UsersRoot != updated.UsersRoot || !Directory.Exists(users) || !Directory.Exists(items) ||
                !(string.Equals(expected.Character.SourceFile, accountName, comparison) ||
                    Path.IsPathFullyQualified(expected.Character.SourceFile) &&
                    string.Equals(Path.GetFullPath(expected.Character.SourceFile), characterPath, comparison))) return false;
            var failure = string.Empty;
            var result = SnapshotFileLockCoordinator.ExecuteLocked([characterPath, itemPath], () =>
                PersistLocked(expected, stable, updated, users, items, characterPath, itemPath,
                    before, after, interruption, out failure));
            reason = failure; return result;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = Prefix + "Preparation:" + error.GetType().Name; return false; }
    }

    private static bool PersistLocked(WorldSession expected, WorldSession stable, WorldSession updated,
        string users, string items, string characterPath, string itemPath,
        LegacyItemSaveGraph before, LegacyItemSaveGraph after, LegacyEquipmentMoveJournalInterruption interruption,
        out string reason)
    {
        reason = string.Empty;
        if (LegacyTradeJournal.HasPending(users)) { reason = RecoveryRequired + ":pending"; return false; }
        if (!CharacterSnapshotFileBaseline.TryRead(characterPath, Prefix, out var character, out reason) || character is null ||
            !CharacterMatches(character, stable) ||
            !CharacterSnapshotFileBaseline.TryReadBytes(itemPath, Prefix, out var rawItems, out reason))
        { if (reason.Length == 0) reason = "equipmentMoveCharacterBaselineStale"; return false; }
        if (!TryPrepareItemImage(rawItems, stable.PlayerItems, updated.PlayerItems, out var itemAfter, out reason) ||
            !before.IsCurrent(expected.PlayerItems) || !after.IsCurrent(updated.PlayerItems)) return false;
        var changes = new LegacyTradeJournalChange[]
        {
            new(LegacyTradeJournalFileKind.Character, expected.AccountId, character.Bytes, character.Bytes),
            new(LegacyTradeJournalFileKind.Items, expected.Character.Uid, rawItems, itemAfter),
        };
        bool committed;
        try
        {
            // The only optional seam interrupts a real journal after an existing
            // durable boundary; it cannot grant a write or fabricate a saved image.
            LegacyJournalInterruption? stop = interruption == LegacyEquipmentMoveJournalInterruption.None ? null :
                LegacyJournalInterruption.StopAfter((int)interruption - 1);
            committed = LegacyTradeJournal.TryCommitSingleOwner(users, items, changes, stop, out reason);
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = RecoveryRequired + ":writerFault:" + error.GetType().Name; return false; }
        if (committed)
        {
            try
            {
                if (ImagesMatch(characterPath, character.Bytes, itemPath, itemAfter)) return true;
            }
            catch (Exception error) when (IsPreparationFailure(error)) { }
            reason = RecoveryRequired + ":postimagesUnproven"; return false;
        }
        if (reason.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal))
        { reason = RecoveryRequired + ":" + reason; return false; }
        try
        {
            if (ImagesMatch(characterPath, character.Bytes, itemPath, rawItems)) return false;
        }
        catch (Exception error) when (IsPreparationFailure(error)) { }
        reason = RecoveryRequired + ":preimagesUnproven:" + reason; return false;
    }

    private static bool ImagesMatch(string character, byte[] characterImage, string items, byte[] itemImage) =>
        CharacterSnapshotFileBaseline.TryReadBytes(character, Prefix, out var first, out _) && first.AsSpan().SequenceEqual(characterImage) &&
        CharacterSnapshotFileBaseline.TryReadBytes(items, Prefix, out var second, out _) && second.AsSpan().SequenceEqual(itemImage);

    private static bool CharacterMatches(CharacterSnapshotFileBaseline.Snapshot file, WorldSession stable)
    {
        var text = CharacterSnapshotFileEncoding.Decode(file.Bytes, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(file.Bytes)) return false;
        var disk = CharacterSnapshotParser.ParseText(text);
        var current = stable.Character;
        // Only unchanged ownership/admission facts are consumed by this move;
        // unrelated runtime HP/quest fields are preserved byte-for-byte, not normalized.
        return disk.Uid == current.Uid && disk.Body == current.Body && disk.Class == current.Class &&
            disk.Reborn == current.Reborn && disk.Level == current.Level && disk.Strength == current.Strength &&
            disk.Agility == current.Agility && disk.Money == current.Money && disk.ConquerPoints == current.ConquerPoints &&
            LegacyTradeEscrowClaims.TryRead(disk.Attributes, out var storedClaim, out _) && storedClaim is null &&
            LegacyTradeEscrowClaims.TryRead(current.Attributes, out var claim, out _) && claim is null &&
            LegacySecondaryPasswordState.TryRead(disk.Attributes, out var storedPin, out _) &&
            LegacySecondaryPasswordState.TryRead(current.Attributes, out var pin, out _) && storedPin == pin &&
            disk.Attributes.TryGetValue("SecurityPass", out var storedRaw) == current.Attributes.TryGetValue("SecurityPass", out var currentRaw) &&
            string.Equals(storedRaw, currentRaw, StringComparison.Ordinal);
    }

    internal static bool IsPreparationFailure(Exception error) => error is not WorldSessionPublicationException &&
        error is not LegacyCommittedSpellPublicationException &&
        error is (IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or
            FormatException or NotSupportedException or OverflowException or System.Security.SecurityException);
}
