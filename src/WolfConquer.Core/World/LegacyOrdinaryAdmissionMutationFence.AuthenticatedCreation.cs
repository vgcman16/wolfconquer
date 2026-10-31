using System.Globalization;

namespace WolfConquer.Core.World;

internal static partial class LegacyOrdinaryAdmissionMutationFence
{
    internal static void RequireAuthenticatedCreationJournalMutation(LegacyAuthenticatedOrdinaryCreationProducer producer,
        IReadOnlyList<string> paths, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        producer.RequireJournalPreparedTargets(paths, changes);
        foreach (var path in paths)
        {
            var absolute = Path.GetFullPath(path);
            RequireConfiguredJournalWritable(absolute, null);
            foreach (var owner in Snapshot())
                foreach (var kind in owner.Roots.Kinds)
                {
                    if (!string.Equals(Path.GetDirectoryName(absolute), owner.Roots.Root(kind), StringComparison.OrdinalIgnoreCase)) continue;
                    var extension = kind is LegacyTradeJournalFileKind.Character or LegacyTradeJournalFileKind.CombatGear ? ".ini" : ".bin";
                    var leaf = Path.GetFileName(absolute);
                    if (!leaf.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
                        !uint.TryParse(leaf[..^extension.Length], NumberStyles.None, CultureInfo.InvariantCulture, out var account))
                        throw new InvalidDataException("Creation mutation has an invalid participant leaf.");
                    if (ReferenceEquals(owner, producer.Ordinary) && account == producer.Account) continue;
                    if (owner.IsPending(account)) throw new IOException("Creation cannot bypass another ordinary producer owner.");
                }
        }
        producer.RequireJournalPreparedTargets(paths, changes);
    }
}
