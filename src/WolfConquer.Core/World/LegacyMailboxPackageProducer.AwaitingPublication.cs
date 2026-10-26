namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    private uint[] ReadAwaitingPublicationAccounts(uint uid)
    {
        var accounts = new HashSet<uint>();
        foreach (var publication in awaitingSessionPublication.ToArray())
        {
            var row = workStore.Read(publication.Value);
            if (row is null || row.AccountId != publication.Key || row.CharacterUid == 0)
                throw new InvalidDataException("Awaiting publication lacks its real retained work identity.");
            if (row.Version == 1)
            {
                if (!Registry.TryReadProducerIntent(Domain, publication.Value, out var intent, out _) ||
                    intent is null || !workStore.MatchesIntent(row, intent))
                    throw new InvalidDataException("Awaiting publication allocation identity is unproven.");
            }
            else if (!TryValidateNoAllocationRecord(row, out _) ||
                !Registry.TryFindProducerIntent(Domain, publication.Value, out _, out var found, out _) || found)
                throw new InvalidDataException("Awaiting publication no-allocation identity is unproven.");
            if (row.CharacterUid == uid) accounts.Add(row.AccountId);
        }
        return accounts.ToArray();
    }
}
