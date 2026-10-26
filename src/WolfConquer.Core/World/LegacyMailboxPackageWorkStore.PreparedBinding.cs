using System.Security.Cryptography;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    internal bool IsCurrentPrepared(Record row, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        RequireCurrentIntent(row);
        var current = Read(Guid.ParseExact(row.Operation, "N"));
        if (current is null || current.Completed || !Same(current, row) || changes.Count != 4) return false;
        LegacyTradeJournalFileKind[] kinds = [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items,
            LegacyTradeJournalFileKind.Mailbox, LegacyTradeJournalFileKind.CombatGear];
        for (var index = 0; index < 4; index++)
            if (changes[index].Kind != kinds[index] || changes[index].FileId != (index == 0 ? row.AccountId : row.CharacterUid) ||
                changes[index].Before is null ||
                Convert.ToHexString(SHA256.HashData(changes[index].Before!)).ToLowerInvariant() != row.Before[index] ||
                Convert.ToHexString(SHA256.HashData(changes[index].After)).ToLowerInvariant() != row.After[index]) return false;
        return ImagesMatch(row, after: false);
    }
}
