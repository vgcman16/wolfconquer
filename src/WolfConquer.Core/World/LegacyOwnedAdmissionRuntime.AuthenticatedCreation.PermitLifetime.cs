namespace WolfConquer.Core.World;

public sealed partial class LegacyOwnedAdmissionRuntime
{
    // Diagnostics only; this cannot mint, resume or retire a permit.
    internal int RetainedCreationAccountCount { get { lock (gate) return creationAccounts.Count; } }

    internal void RetireEndedAuthenticatedCreation(WorldServerFrameHandler actualFrame,
        WorldServerFrameHandler.AuthenticatedCreationRedemption redemption)
    {
        lock (creationGate)
        lock (gate)
        {
            if (!ReferenceEquals(frame, actualFrame) || !redemption.IsClosed(this, actualFrame) ||
                !creationPermits.TryGetValue(redemption.Connection, out var permit) ||
                !ReferenceEquals(permit.Redemption, redemption)) return;
            if (activeCreationLease?.BorrowsRedemption(redemption) == true)
            {
                permit.EndObserved = true;
                return; // The real borrower retires only after its writer/fence unwinds.
            }
            RetireCreationReferences(permit);
        }
    }

    private void RetireCreationReferences(CreationPermit permit)
    {
        if (!Monitor.IsEntered(creationGate) || !Monitor.IsEntered(gate))
            throw new InvalidOperationException("Creation references require the actual serialization gates.");
        if (creationPermits.TryGetValue(permit.Redemption.Connection, out var byConnection) && ReferenceEquals(byConnection, permit))
            creationPermits.Remove(permit.Redemption.Connection);
        if (creationAccounts.TryGetValue(permit.Redemption.AccountId, out var byAccount) && ReferenceEquals(byAccount, permit))
            creationAccounts.Remove(permit.Redemption.AccountId);
    }
}
