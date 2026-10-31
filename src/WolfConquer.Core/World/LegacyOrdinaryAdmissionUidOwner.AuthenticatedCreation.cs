namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionUidOwner
{
    internal void RegisterAuthenticatedCreation(LegacyAuthenticatedOrdinaryCreationProducer producer)
    {
        lock (allocationGate)
        {
            producer.RequireIssuedOwner(this);
            if (!pending.TryAdd(producer.Account, producer.Request.OperationId))
                throw new InvalidDataException("Authenticated creation account already has retained work.");
        }
    }
}
