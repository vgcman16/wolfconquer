namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void RequireHitProgressionStageDomain(JournalRoots roots, JournalRecord record, JournalTarget[] targets)
    {
        foreach (var root in new[] { roots.Users, roots.Items, roots.Spells!, roots.Proficiencies! })
        {
            EnsureSafePath(root);
            if (!Directory.Exists(root)) continue;
            foreach (var path in Directory.EnumerateFileSystemEntries(root))
            {
                if (JournalDomainComparer.Equals(path, roots.Journal) ||
                    !Path.GetFileName(path).Contains(".managed-trade-", StringComparison.OrdinalIgnoreCase)) continue;
                EnsureSafePath(path);
                var target = targets.SingleOrDefault(candidate => JournalDomainComparer.Equals(path, StagePath(candidate, record.TransactionId)));
                if (target is null || Directory.Exists(path)) throw new InvalidDataException("Hit progression found a foreign staging entry.");
                var bytes = ReadTarget(path);
                if (bytes is null || bytes.Length > target.Change.After.Length ||
                    !bytes.AsSpan().SequenceEqual(target.Change.After.AsSpan(0, bytes.Length)))
                    throw new InvalidDataException("Hit progression stage differs from its bounded captured afterimage.");
            }
        }
    }
}
