using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyDeityChestPersistence
{
    internal const string RecoveryRequired = "deityChestRecoveryRequired";
    private const string Prefix = "deityChest";

    internal sealed record Paths(string Users, string ItemsRoot, string Character, string Items);
    internal sealed record Prepared(WorldSession Expected, Paths Paths, byte[] CharacterBefore,
        byte[] ItemsBefore, LegacyDragonSkinInventory Inventory, uint Key, uint Reward, uint ConquerPoints);

    internal static bool TryPrepare(WorldSession expected, string? itemsRoot, LegacyDragonSkinInventory inventory,
        uint key, uint reward, uint cpReward, out Prepared prepared, out string reason)
    {
        prepared = null!;
        reason = string.Empty;
        try
        {
            if (!TryPaths(expected, itemsRoot, out var paths, out reason)) return false;
            Prepared? captured = null;
            var failure = string.Empty;
            var valid = SnapshotFileLockCoordinator.ExecuteLocked([paths.Character, paths.Items], () =>
            {
                if (LegacyTradeJournal.HasPending(paths.Users))
                { failure = RecoveryRequired + ":pending"; return false; }
                if (!TryReadCharacter(paths.Character, expected, out var character, out failure) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(paths.Items, Prefix, out var items, out failure)) return false;
                if (!items.AsSpan().SequenceEqual(inventory.Bytes) || !inventory.IsCurrent(expected.PlayerItems))
                { failure = "deityChestItemBaselineStale"; return false; }
                captured = new(expected, paths, character.Bytes, items, inventory, key, reward, cpReward);
                return true;
            });
            reason = failure;
            prepared = captured!;
            return valid;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = Prefix + "Preparation:" + error.GetType().Name; return false; }
    }

    internal static bool TryPersist(Prepared prepared, WorldSession updated, LegacyDragonSkinInventory after,
        out string reason) => TryPersist(prepared, updated, after, null, out reason);

    // Only the real journal's failure-only interruption boundary, never a saver
    // that can manufacture acceptance. Live callers use the overload above.
    internal static bool TryPersist(Prepared prepared, WorldSession updated, LegacyDragonSkinInventory after,
        Func<int, bool>? interrupt, out string reason)
    {
        reason = string.Empty;
        try
        {
            if (!TryValidateChange(prepared, updated, after, out reason)) return false;
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked([prepared.Paths.Character, prepared.Paths.Items], () =>
                PersistLocked(prepared, updated, after, interrupt, out failure));
            reason = failure;
            return saved;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = Prefix + "Preparation:" + error.GetType().Name; return false; }
    }

    private static bool PersistLocked(Prepared prepared, WorldSession updated, LegacyDragonSkinInventory after,
        Func<int, bool>? interrupt, out string reason)
    {
        var paths = prepared.Paths;
        reason = string.Empty;
        if (LegacyTradeJournal.HasPending(paths.Users))
        { reason = RecoveryRequired + ":pending"; return false; }
        if (!TryReadCharacter(paths.Character, prepared.Expected, out var current, out reason) ||
            !CharacterSnapshotFileBaseline.TryReadBytes(paths.Items, Prefix, out var items, out reason)) return false;
        if (!current.Bytes.AsSpan().SequenceEqual(prepared.CharacterBefore) || !items.AsSpan().SequenceEqual(prepared.ItemsBefore))
        { reason = "deityChestPreparedFileChanged"; return false; }
        if (!TryProjectCharacter(current, updated, prepared.ConquerPoints != 0, out var characterAfter, out reason) ||
            !prepared.Inventory.IsCurrent(prepared.Expected.PlayerItems) || !after.IsCurrent(updated.PlayerItems))
        { if (reason.Length == 0) reason = "deityChestPreparedItemsChanged"; return false; }
        bool committed;
        string failure;
        try
        {
            // Item-only still names its real Character owner; an identical
            // Character before/after is a supported genuine v2 no-op target.
            committed = LegacyTradeJournal.TryCommitSingleOwner(paths.Users, paths.ItemsRoot,
            [
                new(LegacyTradeJournalFileKind.Character, prepared.Expected.AccountId, prepared.CharacterBefore, characterAfter),
                new(LegacyTradeJournalFileKind.Items, prepared.Expected.Character.Uid, prepared.ItemsBefore, after.Bytes),
            ], interrupt, out failure);
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = RecoveryRequired + ":writeFault:" + error.GetType().Name; return false; }
        try
        {
            if (committed)
            {
                if (TryProveFiles(paths, characterAfter, after.Bytes)) return true;
                reason = RecoveryRequired + ":postimagesUnproven";
                return false;
            }
            if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal) || LegacyTradeJournal.HasPending(paths.Users))
            { reason = RecoveryRequired + ":" + failure; return false; }
            if (TryProveFiles(paths, prepared.CharacterBefore, prepared.ItemsBefore))
            { reason = Prefix + "WriteRejected:" + failure; return false; }
            reason = RecoveryRequired + ":preimagesUnproven:" + failure;
            return false;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = RecoveryRequired + ":postWriteObservation:" + error.GetType().Name; return false; }
    }

    private static bool TryProveFiles(Paths paths, byte[] character, byte[] items)
    {
        try
        {
            return CharacterSnapshotFileBaseline.TryReadBytes(paths.Character, Prefix, out var actualCharacter, out _) &&
                actualCharacter.AsSpan().SequenceEqual(character) &&
                CharacterSnapshotFileBaseline.TryReadBytes(paths.Items, Prefix, out var actualItems, out _) &&
                actualItems.AsSpan().SequenceEqual(items);
        }
        catch (Exception error) when (IsPreparationFailure(error)) { return false; }
    }

    internal static bool IsPreparationFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or OverflowException or FormatException or InvalidOperationException or
        System.Security.SecurityException;
}
