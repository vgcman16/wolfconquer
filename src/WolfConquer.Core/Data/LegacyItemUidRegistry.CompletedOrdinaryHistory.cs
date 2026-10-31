using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // Issued only inside the actual registry/custody after the full anchored
    // archive and its known producer recipes are checked. This class cannot
    // materialize a reward, issue IDs or authorize a new creation.
    private sealed class CompletedOrdinaryHistory
    {
        internal Guid Domain { get; }
        internal string? Head { get; }
        internal string? HeadDigest { get; }
        internal ulong Count { get; }
        internal long Bytes { get; }
        private readonly HashSet<uint> accounts;
        internal bool ContainsAccount(uint account) => accounts.Contains(account);

        internal CompletedOrdinaryHistory(RegistryState current, ArchiveCatalog archives, HashSet<uint> accounts)
        {
            Domain = Guid.ParseExact(current.Domain, "N"); Head = current.ArchiveHead;
            HeadDigest = current.ArchiveHeadSha256; Count = current.ArchiveCount.GetValueOrDefault();
            Bytes = archives.Entries.Values.Sum(entry => entry.Bytes.LongLength);
            this.accounts = new(accounts);
        }
    }

    private static CompletedOrdinaryHistory RequireCompletedOrdinaryHistory(RegistryPaths paths,
        RegistryState current, LegacyOwnedAdmissionOptions configuration, ArchiveCatalog archives)
    {
        if (configuration.ExpectedDomain != Guid.ParseExact(current.Domain, "N") ||
            !MatchesConfiguration(current, paths, configuration.RegistryOptions))
            throw new InvalidDataException("Completed ordinary history differs from the actual configured domain.");
        if ((current.ProducerIntents?.Length ?? 0) != 0 ||
            current.Operations?.Any(row => row.Writer == LegacyOrdinaryAdmissionUidOwner.Writer) == true)
            throw new InvalidDataException("Incomplete ordinary or other producer work still owns this domain.");
        var accounts = new HashSet<uint>();
        foreach (var entry in archives.Entries.Values)
        {
            var archive = entry.Record;
            if (archive.Intent.Writer != LegacyOrdinaryAdmissionUidOwner.Writer)
                throw new InvalidDataException("The configured history contains an unsupported completed producer.");
            var intent = archive.Intent; var allocation = archive.Allocation;
            var domain = Guid.ParseExact(current.Domain, "N");
            var request = new LegacyItemUidReservationRequest(Guid.ParseExact(intent.OperationId, "N"),
                intent.Writer, intent.AccountId, intent.CharacterUid, intent.PayloadSha256, intent.Count);
            var receipt = new LegacyItemUidReservationReceipt(domain, Guid.ParseExact(allocation.OperationId, "N"),
                allocation.Writer, allocation.AccountId, allocation.CharacterUid, allocation.PayloadSha256,
                allocation.Generation, allocation.First, allocation.Count);
            var snapshot = new LegacyItemUidProducerIntentSnapshot(domain, request, intent.PreparedGeneration,
                Convert.FromBase64String(intent.PayloadBase64), receipt);
            var recipe = LegacyOrdinaryProducerRecipe.Read(snapshot, domain, configuration.Roots, configuration.MailboxRoot);
            if (!accounts.Add(recipe.Account))
                throw new InvalidDataException("More than one completed ordinary producer owns the same character.");
        }
        return new(current, archives, accounts);
    }

    private CompletedOrdinaryHistory RequireCompletedOrdinaryHistory(LegacyOwnedAdmissionOptions configuration)
    {
        if (!Monitor.IsEntered(gate)) throw new InvalidOperationException("Completed history requires the actual Registry gate.");
        return RequireCompletedOrdinaryHistory(paths, state, configuration,
            ReadArchiveCatalog(paths, state, null, allowNext: false));
    }
}
