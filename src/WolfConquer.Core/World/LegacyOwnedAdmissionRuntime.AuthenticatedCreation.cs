using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyOwnedAdmissionRuntime
{
    private readonly object creationGate = new();
    private readonly ConditionalWeakTable<ConquerTcpConnection, CreationPermit> creationPermits = new();
    private readonly Dictionary<uint, CreationPermit> creationAccounts = [];
    private CreationLease? activeCreationLease;

    private sealed class CreationPermit(WorldServerFrameHandler.AuthenticatedCreationRedemption redemption)
    {
        internal readonly WorldServerFrameHandler.AuthenticatedCreationRedemption Redemption = redemption;
        internal bool IntentIssued;
        internal Guid? IssuedOperation;
        internal bool EndObserved;
    }

    internal bool TryPrepareAuthenticatedCreation(WorldServerFrameHandler actualFrame,
        WorldServerFrameHandler.AuthenticatedCreationRedemption redemption, out string reason)
    {
        lock (creationGate)
        lock (gate)
        {
            reason = "authenticatedCreationOwnerUnavailable";
            if (!CreationOwnerIsCurrent(actualFrame, redemption)) return false;
            if (!registry.TryRequireFreshAuthenticatedCreationAccount(configured, redemption.AccountId, out reason)) return false;
            if (creationPermits.TryGetValue(redemption.Connection, out _) ||
                creationAccounts.TryGetValue(redemption.AccountId, out var previous) &&
                (previous.IntentIssued || previous.Redemption.IsCurrent(this, actualFrame)))
            { reason = "authenticatedCreationAccountAlreadyClaimed"; return false; }
            var permit = new CreationPermit(redemption);
            creationAccounts[redemption.AccountId] = permit;
            creationPermits.Add(redemption.Connection, permit);
            reason = string.Empty; return true;
        }
    }

    internal bool HasAuthenticatedCreationPermit(WorldServerFrameHandler actualFrame, ConquerTcpConnection connection)
    {
        lock (gate)
            return creationPermits.TryGetValue(connection, out var permit) && !permit.IntentIssued &&
                CreationOwnerIsCurrent(actualFrame, permit.Redemption);
    }

    internal bool TryEnterAuthenticatedCreation(WorldServerFrameHandler actualFrame, ConquerTcpConnection connection,
        string normalizedName, out CreationLease? lease, out string reason)
    {
        lease = null; reason = "authenticatedCreationPermitRequired";
        Monitor.Enter(creationGate);
        var transferred = false;
        try
        {
            lock (gate)
            {
                if (activeCreationLease is not null || !creationPermits.TryGetValue(connection, out var permit) ||
                    permit.IntentIssued || !CreationOwnerIsCurrent(actualFrame, permit.Redemption)) return false;
                lease = CreationLease.Capture(this, actualFrame, permit.Redemption);
                activeCreationLease = lease;
            }
            // Real filesystem evidence is captured under creation serialization,
            // outside the Runtime/Registry gates. No Store lock is taken here.
            LegacyTradeJournal.RequireClearRuneDecomposeNamespace(configured);
            lease.InitializeNameClaim(normalizedName);
            lease.RequireCurrentBefore();
            transferred = true; reason = string.Empty; return true;
        }
        catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
        { reason = "authenticatedCreationPreflightRefused:" + error.GetType().Name; return false; }
        finally
        {
            if (!transferred)
            {
                lock (gate) { if (ReferenceEquals(activeCreationLease, lease)) activeCreationLease = null; }
                lease = null; Monitor.Exit(creationGate);
            }
        }
    }

    private bool CreationOwnerIsCurrent(WorldServerFrameHandler actualFrame,
        WorldServerFrameHandler.AuthenticatedCreationRedemption redemption) =>
        Monitor.IsEntered(gate) && !closed && !stopping && runeDecomposeStartupComplete &&
        ReferenceEquals(frame, actualFrame) && redemption.IsCurrent(this, actualFrame) &&
        Ordinary.IsCurrent(configured.Roots) && Ordinary.IsReconciliationComplete &&
        Recovery.IsReadyForOrdinaryAdmission(configured.Roots) &&
        registry.TryValidateProducerBinding(configured.ExpectedDomain, configured.UsersRoot,
            configured.ItemsRoot, configured.MailboxRoot, out _);
}
