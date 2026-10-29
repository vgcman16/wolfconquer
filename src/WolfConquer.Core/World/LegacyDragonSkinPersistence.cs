using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyDragonSkinPersistence
{
    internal const string RecoveryRequired = "dragonSkinRecoveryRequired";
    private const string Prefix = "dragonSkin";

    internal static bool TryPersist(WorldSession expected, WorldSession updated, string? itemsRoot, out string reason) =>
        TryPersist(expected, updated, itemsRoot, null, out reason);

    // Only the existing journal's failure-only interruption seam. It never
    // supplies an accepted save; live callers always use the no-hook overload.
    internal static bool TryPersist(WorldSession expected, WorldSession updated, string? itemsRoot,
        LegacyJournalInterruption? interrupt, out string reason)
    {
        reason = string.Empty;
        try
        {
            if (!TryValidate(expected, updated, out var change, out reason) ||
                !TryPaths(expected, updated, itemsRoot, out var paths, out reason)) return false;
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked([paths.Character, paths.Items], () =>
                PersistLocked(paths, expected, updated, change, interrupt, out failure));
            reason = failure;
            return saved;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = Prefix + "Preparation:" + error.GetType().Name; return false; }
    }

    private static bool PersistLocked(Paths paths, WorldSession expected, WorldSession updated, Change change,
        LegacyJournalInterruption? interrupt, out string reason)
    {
        reason = string.Empty;
        if (LegacyTradeJournal.HasPending(paths.UsersRoot))
        { reason = RecoveryRequired + ":pending"; return false; }
        if (!TryPrepareCharacter(paths.Character, expected, updated, change,
            out var beforeCharacter, out var afterCharacter, out reason) ||
            !TryReadItems(paths.Items, out var beforeItems, out reason)) return false;
        if (beforeItems is null ? change.BeforeItems.Bytes.Length != 8 :
            !beforeItems.AsSpan().SequenceEqual(change.BeforeItems.Bytes))
        { reason = "dragonSkinItemBaselineStale"; return false; }
        if (!change.BeforeBook.IsCurrent(expected.Character.Attributes) || !change.AfterBook.IsCurrent(updated.Character.Attributes) ||
            !change.BeforeItems.IsCurrent(expected.PlayerItems) || !change.AfterItems.IsCurrent(updated.PlayerItems))
        { reason = "dragonSkinPreparedStateChanged"; return false; }
        bool committed;
        string failure;
        try
        {
            committed = LegacyTradeJournal.TryCommitSingleOwner(paths.UsersRoot, paths.ItemsRoot,
            [
                new(LegacyTradeJournalFileKind.Character, expected.AccountId, beforeCharacter, afterCharacter),
                new(LegacyTradeJournalFileKind.Items, expected.Character.Uid, beforeItems, change.AfterItems.Bytes),
            ], interrupt, out failure);
        }
        catch (Exception error) when (IsPreparationFailure(error))
        {
            // Journal publication may already have happened. A thrown writer or
            // failure-only interrupt is not an ordinary preparation rejection.
            reason = RecoveryRequired + ":writeFault:" + error.GetType().Name;
            return false;
        }
        if (committed)
        {
            try
            {
                if (CharacterSnapshotFileBaseline.TryReadBytes(paths.Character, Prefix, out var character, out _) &&
                    character.AsSpan().SequenceEqual(afterCharacter) && TryReadItems(paths.Items, out var items, out _) &&
                    items is not null && items.AsSpan().SequenceEqual(change.AfterItems.Bytes)) return true;
            }
            catch (Exception error) when (IsPreparationFailure(error)) { }
            reason = RecoveryRequired + ":postimagesUnproven";
            return false;
        }
        if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal))
        { reason = RecoveryRequired + ":" + failure; return false; }
        try
        {
            if (CharacterSnapshotFileBaseline.TryReadBytes(paths.Character, Prefix, out var character, out _) &&
                character.AsSpan().SequenceEqual(beforeCharacter) && TryReadItems(paths.Items, out var items, out _) &&
                SameBytes(items, beforeItems))
            { reason = Prefix + "WriteRejected:" + failure; return false; }
        }
        catch (Exception error) when (IsPreparationFailure(error)) { }
        reason = RecoveryRequired + ":preimagesUnproven:" + failure;
        return false;
    }

    private static bool SameBytes(byte[]? first, byte[]? second) => first is null
        ? second is null : second is not null && first.AsSpan().SequenceEqual(second);

    internal static bool IsPreparationFailure(Exception error) => error is not WorldSessionPublicationException &&
        error is (IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException or
            FormatException or InvalidOperationException or System.Security.SecurityException);
}
