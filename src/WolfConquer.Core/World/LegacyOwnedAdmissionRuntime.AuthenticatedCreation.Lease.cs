using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyOwnedAdmissionRuntime
{
    // A synchronous borrow of the actual Runtime creation serialization. It may
    // not cross an await. The permit itself has no thread-affinity requirement.
    internal sealed class CreationLease : IDisposable
    {
        private readonly LegacyOwnedAdmissionRuntime runtime;
        private readonly WorldServerFrameHandler actualFrame;
        private readonly CreationPermit permit;
        private readonly int thread = Environment.CurrentManagedThreadId;
        private bool disposed;
        private LegacyCharacterCreationNameClaim? claim;
        private CreationLease(LegacyOwnedAdmissionRuntime runtime, WorldServerFrameHandler actualFrame,
            CreationPermit permit) { this.runtime = runtime; this.actualFrame = actualFrame; this.permit = permit; }

        internal static CreationLease Capture(LegacyOwnedAdmissionRuntime runtime,
            WorldServerFrameHandler actualFrame, WorldServerFrameHandler.AuthenticatedCreationRedemption redemption)
        {
            if (!Monitor.IsEntered(runtime.creationGate) || !Monitor.IsEntered(runtime.gate) ||
                runtime.activeCreationLease is not null ||
                !runtime.creationPermits.TryGetValue(redemption.Connection, out var permit) ||
                !ReferenceEquals(permit.Redemption, redemption) || !runtime.CreationOwnerIsCurrent(actualFrame, redemption))
                throw new InvalidOperationException("Creation lease requires the actual registered Runtime permit/gates.");
            return new(runtime, actualFrame, permit);
        }
        internal LegacyOwnedAdmissionOptions Configuration => runtime.configured;
        internal LegacyItemUidRegistry Registry => runtime.registry;
        internal LegacyOrdinaryAdmissionUidOwner Ordinary => runtime.Ordinary;
        internal uint AccountId => permit.Redemption.AccountId;
        internal string Username => permit.Redemption.Username;
        internal string TicketDigest => permit.Redemption.TicketDigest;
        internal LegacyCharacterCreationNameClaim NameClaim => claim ??
            throw new InvalidOperationException("Creation name/absence claim has not been captured.");

        internal void InitializeNameClaim(string normalizedName)
        {
            if (claim is not null || permit.IntentIssued)
                throw new InvalidOperationException("Creation name claim is immutable once captured.");
            claim = LegacyCharacterCreationNameClaim.Capture(this, normalizedName);
        }

        internal void RequireOwnerCurrent()
        {
            lock (runtime.gate)
            {
                RequirePlainOwnerCurrent();
                if (!runtime.CreationOwnerIsCurrent(actualFrame, permit.Redemption))
                    throw new InvalidDataException("Authenticated creation Runtime/domain owner changed.");
            }
        }

        internal void RequirePlainOwnerCurrent()
        {
            if (disposed || thread != Environment.CurrentManagedThreadId || !Monitor.IsEntered(runtime.creationGate) ||
                !ReferenceEquals(runtime.activeCreationLease, this) || Volatile.Read(ref runtime.closed) || Volatile.Read(ref runtime.stopping) ||
                !ReferenceEquals(runtime.frame, actualFrame) ||
                !runtime.creationPermits.TryGetValue(permit.Redemption.Connection, out var current) ||
                !ReferenceEquals(current, permit) || !permit.Redemption.IsCurrent(runtime, actualFrame))
                throw new InvalidDataException("Authenticated creation lease/connection is no longer current.");
        }

        internal void RequireCurrentBefore()
        {
            RequireOwnerCurrent();
            NameClaim.RequireCurrentBefore();
            RequireOwnerCurrent();
        }

        internal void RequireActualFrame(WorldServerFrameHandler expected)
        {
            if (!ReferenceEquals(actualFrame, expected))
                throw new InvalidDataException("Creation producer belongs to a different actual Frame.");
            RequireOwnerCurrent();
        }

        internal void FenceUncertainCreation()
        {
            lock (runtime.gate)
            {
                if (disposed || thread != Environment.CurrentManagedThreadId || !Monitor.IsEntered(runtime.creationGate) ||
                    !ReferenceEquals(runtime.activeCreationLease, this))
                    throw new InvalidOperationException("Only the actual borrowed creation owner may fence uncertain work.");
                permit.IntentIssued = true;
                runtime.Recovery.Block("authenticatedCreationRecoveryRequired:issuedTransitionUncertain");
                runtime.registry.BlockAuthenticatedCreation();
            }
        }

        internal void MarkIntentIssued(LegacyItemUidReservationRequest request, byte[] payload)
        {
            RequireCurrentBefore();
            if (permit.IntentIssued || request.Writer != LegacyOrdinaryAdmissionUidOwner.Writer ||
                request.AccountId != AccountId || request.CharacterUid != AccountId ||
                !runtime.registry.TryReadActiveProducerIntents(runtime.configured.ExpectedDomain, out var active, out _) ||
                active.SingleOrDefault(row => row.Request.OperationId == request.OperationId) is not { } retained ||
                retained.Request != request ||
                !retained.CopyPayload().AsSpan().SequenceEqual(payload))
                throw new InvalidDataException("Creation permit can be consumed only by its exact durable producer intent.");
            permit.IntentIssued = true;
            permit.IssuedOperation = request.OperationId;
        }

        internal void RetireCompletedPermit(LegacyAuthenticatedOrdinaryCreationProducer producer)
        {
            var operation = producer.RequireCompletedPermitRetirement(this);
            lock (runtime.gate)
            {
                if (disposed || thread != Environment.CurrentManagedThreadId || !Monitor.IsEntered(runtime.creationGate) ||
                    !ReferenceEquals(runtime.activeCreationLease, this) || !permit.IntentIssued || permit.IssuedOperation != operation)
                    throw new InvalidOperationException("Completed permit retirement lacks its actual lease/operation.");
                runtime.RetireCreationReferences(permit);
            }
        }

        internal bool BorrowsRedemption(WorldServerFrameHandler.AuthenticatedCreationRedemption expected) =>
            ReferenceEquals(permit.Redemption, expected);

        public void Dispose()
        {
            if (disposed) return;
            if (thread != Environment.CurrentManagedThreadId || !Monitor.IsEntered(runtime.creationGate))
                throw new InvalidOperationException("Creation serialization must be released by its synchronous borrower.");
            lock (runtime.gate)
            {
                if (!ReferenceEquals(runtime.activeCreationLease, this))
                    throw new InvalidOperationException("Creation serialization owner changed.");
                if (permit.EndObserved || permit.Redemption.IsClosed(runtime, actualFrame))
                    runtime.RetireCreationReferences(permit);
                runtime.activeCreationLease = null; disposed = true;
            }
            Monitor.Exit(runtime.creationGate);
        }
    }
}
