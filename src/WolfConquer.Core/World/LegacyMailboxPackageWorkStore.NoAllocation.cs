using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    internal Record PrepareNoAllocationWork(LegacyMailboxPackageRecipe recipe, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        LegacyTradeJournalFileKind[] kinds = [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items,
            LegacyTradeJournalFileKind.Mailbox, LegacyTradeJournalFileKind.CombatGear];
        if (recipe.Count != 0 || recipe.AccountId == 0 || recipe.CharacterUid == 0 || changes.Count != 4 ||
            !changes.Select(change => change.Kind).SequenceEqual(kinds) || changes[0].FileId != recipe.AccountId ||
            changes.Skip(1).Any(change => change.FileId != recipe.CharacterUid) || changes.Any(change => change.Before is null))
            throw new InvalidOperationException("No-allocation package targets are invalid.");
        var payload = recipe.CopyPayload();
        var row = new Record(2, domain.ToString("N"), recipe.OperationId.ToString("N"),
            Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), recipe.AccountId, recipe.CharacterUid, 0, 0,
            users, items, mailbox, gear, changes.Select(change => Digest(change.Before!)).ToArray(),
            changes.Select(change => Digest(change.After)).ToArray(), false, "NoAllocation", Convert.ToBase64String(payload));
        RequireCurrentIntent(row);
        if (!ImagesMatch(row, after: false)) throw new InvalidOperationException("No-allocation preimages changed.");
        var existing = Read(recipe.OperationId);
        if (existing is { Completed: true }) throw new InvalidOperationException("No-allocation work cannot reopen.");
        _ = ReadPendingNoAllocationRecords();
        Write(row);
        return row;
    }

    internal bool ReconcileNoAllocation(Record row, out bool pending)
    {
        pending = true;
        if (!owner.TryValidateNoAllocationRecord(row, out _)) return false;
        if (row.Completed) { pending = false; return true; }
        var remainsPending = true;
        var valid = SnapshotFileLockCoordinator.ExecuteLocked(TargetPaths(row), () =>
        {
            RequireCurrentIntent(row);
            var current = Read(Guid.ParseExact(row.Operation, "N"));
            if (current is null || !Same(current, row)) return false;
            if (current.Completed) { remainsPending = false; return true; }
            if (ImagesMatch(current, after: true))
            { Write(current with { Completed = true }); remainsPending = false; return true; }
            return ImagesMatch(current, after: false);
        });
        pending = remainsPending;
        return valid;
    }
}
