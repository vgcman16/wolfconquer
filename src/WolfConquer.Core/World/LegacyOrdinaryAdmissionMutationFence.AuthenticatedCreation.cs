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
            var directory = Path.GetDirectoryName(absolute);
            var leaf = Path.GetFileName(absolute);
            foreach (var participant in SnapshotParticipants(directory))
            {
                var owner = participant.Owner;
                var extension = ParticipantExtension(participant.Kind);
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
