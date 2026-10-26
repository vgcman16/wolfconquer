using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    private static bool TryCopyWorkImages(IReadOnlyList<LegacyTradeJournalChange> source,
        out LegacyTradeJournalChange[] copies)
    {
        copies = [];
        if (source.Count != 4) return false;
        var captured = new LegacyTradeJournalChange[4];
        for (var index = 0; index < captured.Length; index++)
        {
            // Read each caller-owned row once. Validation and cloning use that
            // same captured row, never a later enumeration of the input list.
            var row = source[index];
            if (row is null) return false;
            var before = row.Before;
            var after = row.After;
            if (before is null || after is null) return false;
            captured[index] = row with { Before = before.ToArray(), After = after.ToArray() };
        }
        copies = captured;
        return true;
    }
}
