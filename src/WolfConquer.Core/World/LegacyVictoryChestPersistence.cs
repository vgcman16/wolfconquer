using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyVictoryChestPersistence
{
    internal const string RecoveryRequired = "victoryChestRecoveryRequired";
    private const string Prefix = "victoryChest";
    internal sealed record Prepared(Binding Owner, Paths Paths, byte[] CharacterBefore, byte[] ItemsBefore,
        byte[] QuestsBefore, LegacyDragonSkinInventory Inventory, LegacyVictoryQuestCapture Quests);

    internal static bool TryPrepare(Binding owner, LegacyDragonSkinInventory inventory,
        LegacyVictoryQuestCapture quests, out Prepared prepared, out string reason)
    {
        prepared = null!;
        reason = Prefix + "RequiredOwnersUnavailable";
        try
        {
            if (!GrantOwnersCurrent(owner) || !TryPaths(owner, out var paths, out reason)) return false;
            Prepared? captured = null;
            var failure = Prefix + "PreparedOwnerChanged";
            var valid = false;
            // Read-only preparation follows the SAME Trade -> Store -> files
            // order as the later persisted CAS. Never acquire Store from a file
            // gate while another ordinary Store writer may be autosaving there.
            owner.Context.Sessions!.WithCurrentNativeClientIdentity(owner.Context.Connection,
                owner.Expected.Character.Uid, identity =>
            {
                if (!ReferenceEquals(identity, owner.Identity) || !GrantOwnersCurrent(owner)) return;
                valid = SnapshotFileLockCoordinator.ExecuteLocked([paths.Character, paths.Items, paths.Quests], () =>
                {
                    if (LegacyTradeJournal.HasPending(paths.Users))
                    { failure = RecoveryRequired + ":pending"; return false; }
                    if (!TryReadBaselines(paths, owner.Expected, inventory, quests,
                        out var character, out var items, out var questBytes, out failure)) return false;
                    if (!GrantOwnersCurrent(owner))
                    { failure = Prefix + "PreparedOwnerChanged"; return false; }
                    captured = new(owner, paths, character, items, questBytes, inventory, quests);
                    return true;
                });
            });
            prepared = captured!;
            reason = failure;
            return valid;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = Prefix + "Preparation:" + error.GetType().Name; return false; }
    }

    internal static bool TryPersist(Prepared before, WorldSession updated, LegacyDragonSkinInventory items,
        LegacyVictoryQuestCapture quests, out string reason) => TryPersist(before, updated, items, quests, null, out reason);

    // Only the real journal's failure-only interruption seam; never an accepting saver.
    internal static bool TryPersist(Prepared before, WorldSession updated, LegacyDragonSkinInventory items,
        LegacyVictoryQuestCapture quests, Func<int, bool>? interrupt, out string reason)
    {
        reason = string.Empty;
        try
        {
            if (!TryValidateChange(before, updated, items, quests, out reason)) return false;
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked(
                [before.Paths.Character, before.Paths.Items, before.Paths.Quests],
                () => PersistLocked(before, updated, items, quests, interrupt, out failure));
            reason = failure;
            return saved;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = Prefix + "Preparation:" + error.GetType().Name; return false; }
    }

    private static bool PersistLocked(Prepared before, WorldSession updated, LegacyDragonSkinInventory items,
        LegacyVictoryQuestCapture quests, Func<int, bool>? interrupt, out string reason)
    {
        reason = string.Empty;
        var paths = before.Paths;
        if (LegacyTradeJournal.HasPending(paths.Users))
        { reason = RecoveryRequired + ":pending"; return false; }
        if (!TryReadBaselines(paths, before.Owner.Expected, before.Inventory, before.Quests,
            out var character, out var oldItems, out var oldQuests, out reason)) return false;
        if (!character.AsSpan().SequenceEqual(before.CharacterBefore) || !oldItems.AsSpan().SequenceEqual(before.ItemsBefore) ||
            !oldQuests.AsSpan().SequenceEqual(before.QuestsBefore))
        { reason = Prefix + "PreparedFileChanged"; return false; }
        var afterItems = (byte[])items.Bytes.Clone();
        var afterQuests = quests.CopyImage();
        // All fallible collection revalidation happens BEFORE the final physical
        // Store/native check. No accepting ownership callback is involved.
        if (!TryValidateChange(before, updated, items, quests, out reason) || !GrantOwnersCurrent(before.Owner))
        { if (reason.Length == 0) reason = Prefix + "PreparedOwnerChanged"; return false; }
        bool committed;
        string failure;
        try
        {
            committed = LegacyTradeJournal.TryCommitQuestOwner(paths.Users, paths.ItemsRoot, paths.QuestsRoot,
            [
                new(LegacyTradeJournalFileKind.Character, before.Owner.Expected.AccountId, before.CharacterBefore, before.CharacterBefore),
                new(LegacyTradeJournalFileKind.Items, before.Owner.Expected.Character.Uid, before.ItemsBefore, afterItems),
                new(LegacyTradeJournalFileKind.Quests, before.Owner.Expected.Character.Uid, before.QuestsBefore, afterQuests),
            ], interrupt, out failure);
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = RecoveryRequired + ":writeFault:" + error.GetType().Name; return false; }
        try
        {
            if (committed)
            {
                if (TryProveFiles(paths, before.CharacterBefore, afterItems, afterQuests)) return true;
                reason = RecoveryRequired + ":postimagesUnproven";
                return false;
            }
            if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal) || LegacyTradeJournal.HasPending(paths.Users))
            { reason = RecoveryRequired + ":" + failure; return false; }
            if (TryProveFiles(paths, before.CharacterBefore, before.ItemsBefore, before.QuestsBefore))
            { reason = Prefix + "WriteRejected:" + failure; return false; }
            reason = RecoveryRequired + ":preimagesUnproven:" + failure;
            return false;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        { reason = RecoveryRequired + ":postWriteObservation:" + error.GetType().Name; return false; }
    }

    internal static bool IsPreparationFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or OverflowException or FormatException or InvalidOperationException or
        IndexOutOfRangeException or System.Security.SecurityException;
}
