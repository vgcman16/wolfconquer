namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class PreparedAdmissionContinuationQuery
    {
        private LegacyNativeMagicQueryClient.CompletedMaximums? completion;
        private WorldSession? completedSession;

        internal async ValueTask<CompletedAdmissionContinuationQuery?> RunAsync(CancellationToken cancellationToken)
        {
            // Starting outside the held writer gates is intentional; the
            // actual origin/current helpers reacquire them in established order.
            if (Volatile.Read(ref started) != 0 || !AdmissionSourceIsCurrent() || !AdmissionImageIsCurrent() ||
                !binding.Store.IsAdmissionContinuationQueryOwnerCurrent(binding, admitted, context, null) ||
                !attributes.IsCurrentPlain() || !paid.IsCurrentPlain() || !items.IsCurrent(admitted.PlayerItems) ||
                !metadata.IsCurrentPlain() || !speed.IsCurrentPlain(context.WorldData) ||
                Interlocked.CompareExchange(ref started, 1, 0) != 0) return null;
            var actual = context with { Session = admitted };
            var scope = new LegacyEquipmentQueryPublicationScope(binding.Uid, admitted.PlayerItems)
            {
                OwnerNativeClientIdentity = binding.NativeIdentity,
                PhysicalReceiveOwner = binding,
                AdmissionContinuation = this,
            };
            RegisterContinuationInvocation(this, actual, admitted, scope);
            var result = await ExecuteLegacyEquipmentQueryAsync(actual, admitted,
                new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.InitialWorldEntry, scope, CallItems: true),
                cancellationToken).ConfigureAwait(false);
            if (!result.Admitted || !result.Completed || result.Session is not { } current || completion is null ||
                !IsQueryCurrent(context, current, current)) return null;
            completedSession = current;
            return CompletedAdmissionContinuationQuery.Issue(this, current, pending);
        }

        internal bool IsQueryCurrent(WorldPacketHandlerContext actual, WorldSession supplied, WorldSession current) =>
            Volatile.Read(ref started) == 1 && ReferenceEquals(supplied, current) &&
            ReferenceEquals(actual.Connection, context.Connection) && ReferenceEquals(actual.Sessions, binding.Store) &&
            ReferenceEquals(actual.WorldData, context.WorldData) &&
            ReferenceEquals(actual.FrameOperationLease, context.FrameOperationLease) &&
            ReferenceEquals(actual.TradeOperations, context.TradeOperations) &&
            SameProgression(current) && items.IsCurrent(current.PlayerItems) && metadata.IsCurrentPlain() &&
            speed.IsCurrentPlain(context.WorldData) &&
            binding.Store.IsAdmissionContinuationQueryOwnerCurrent(binding, current, actual, completion);

        internal bool CoversPendingQueryProjection(WorldPacketHandlerContext actual, WorldSession expected,
            WorldSession proposed, LegacyExperienceContinuation marker) => completion is null && Equals(marker, pending) &&
            IsQueryCurrent(actual, expected, expected) && SameProgression(proposed) &&
            ReferenceEquals(expected.PlayerItems, proposed.PlayerItems);

        internal bool AcceptQueryCompletion(WorldPacketHandlerContext actual, WorldSession current,
            LegacyNativeMagicQueryClient.CompletedMaximums proof)
        {
            if (completion is not null || !IsQueryCurrent(actual, current, current) ||
                !proof.IsCurrentPlain(binding, current)) return false;
            completion = proof; return true;
        }

        internal bool IsCompletedCurrent(WorldSession current, LegacyExperienceContinuation marker) =>
            completion is not null && ReferenceEquals(current, completedSession) && Equals(marker, pending) &&
            IsQueryCurrent(context, current, current);
    }

    internal sealed partial class CompletedAdmissionContinuationQuery
    {
        private readonly PreparedAdmissionContinuationQuery owner;
        internal WorldSession Session { get; }
        internal LegacyExperienceContinuation Pending { get; }
        private CompletedAdmissionContinuationQuery(PreparedAdmissionContinuationQuery owner, WorldSession session,
            LegacyExperienceContinuation pending)
        { this.owner = owner; Session = session; Pending = pending; }

        internal static CompletedAdmissionContinuationQuery? Issue(PreparedAdmissionContinuationQuery owner,
            WorldSession session, LegacyExperienceContinuation pending) => owner.IsCompletedCurrent(session, pending)
                ? new(owner, session, pending) : null;

        internal bool TryCurrent(out WorldSession current)
        {
            current = null!;
            if (!owner.IsCompletedCurrent(Session, Pending)) return false;
            current = Session; return true;
        }
    }
}
