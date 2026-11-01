namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class PreparedAdmissionContinuationQuery
    {
        private LegacyEquipmentQueryPublicationScope? failureInvocation;

        internal void BindFailureInvocation(LegacyEquipmentQueryPublicationScope scope)
        {
            if (failureInvocation is not null || !ContinuationInvocations.TryGetValue(scope, out var source) ||
                !ReferenceEquals(source.Admission, this) || !ReferenceEquals(source.Expected, admitted) ||
                !ReferenceEquals(source.Scope, scope)) throw InvalidContinuationQueryMutation();
            failureInvocation = scope;
        }

        internal bool TryGetFailureOwner(out WorldPacketHandlerContext original, out LegacyOrdinarySpellBinding owner)
        {
            original = context; owner = binding;
            return Volatile.Read(ref started) == 1 && failureInvocation is { } scope &&
                ContinuationInvocations.TryGetValue(scope, out var source) && source.Execution is not null &&
                ReferenceEquals(source.Admission, this) && ReferenceEquals(source.Expected, admitted) &&
                ReferenceEquals(source.Scope, scope);
        }

        // Cleanup authority only. It cannot select a new holder for query or
        // continuation work, and does not depend on a completed Adopt().
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        internal void FenceFailure(Exception primary) => binding.Store.FenceAdmissionContinuationQueryFailure(this, primary);
    }

    internal sealed partial class CompletedAdmissionContinuationQuery
    {
        // Also covers failure while preparing the second source query: this
        // is the actual first invocation's lineage, never a refreshed origin.
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        internal void FenceFailure(Exception primary) => owner.FenceFailure(primary);
    }
}
