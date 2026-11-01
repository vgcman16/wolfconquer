using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed class ContinuationQueryInvocation(PreparedLevelUpRebuild? levelUp, PreparedAdmissionContinuationQuery? admission,
        WorldPacketHandlerContext context, WorldSession expected, LegacyEquipmentQueryPublicationScope scope)
    {
        internal readonly PreparedLevelUpRebuild? LevelUp = levelUp;
        internal readonly PreparedAdmissionContinuationQuery? Admission = admission;
        internal readonly WorldPacketHandlerContext Context = context;
        internal readonly WorldSession Expected = expected;
        internal readonly LegacyEquipmentQueryPublicationScope Scope = scope;
        internal LegacyEquipmentQueryExecution? Execution;
    }
    private static readonly ConditionalWeakTable<LegacyEquipmentQueryPublicationScope, ContinuationQueryInvocation> ContinuationInvocations = new();

    // Only PreparedLevelUpRebuild's consumed continuation purpose calls this
    // private factory. An externally supplied/copied scope has no registration.
    private static void RegisterContinuationInvocation(PreparedLevelUpRebuild owner, WorldPacketHandlerContext context,
        WorldSession expected, LegacyEquipmentQueryPublicationScope scope)
    {
        if (!owner.HasContinuationPurpose || !ReferenceEquals(scope.LevelUpRebuild, owner) ||
            !owner.IsQueryCurrent(context, expected, expected)) throw InvalidContinuationQueryMutation();
        if (scope.AdmissionContinuation is not null) throw InvalidContinuationQueryMutation();
        ContinuationInvocations.Add(scope, new(owner, null, context, expected, scope));
    }

    private static void RegisterContinuationInvocation(PreparedAdmissionContinuationQuery owner, WorldPacketHandlerContext context,
        WorldSession expected, LegacyEquipmentQueryPublicationScope scope)
    {
        if (scope.LevelUpRebuild is not null || scope.AttributeRebuild is not null ||
            !ReferenceEquals(scope.AdmissionContinuation, owner) || !owner.IsQueryCurrent(context, expected, expected))
            throw InvalidContinuationQueryMutation();
        ContinuationInvocations.Add(scope, new(null, owner, context, expected, scope));
        owner.BindFailureInvocation(scope);
    }

    private static bool TryBindContinuationInvocation(LegacyEquipmentQueryExecution execution,
        WorldPacketHandlerContext context, WorldSession expected, LegacyEquipmentQueryPublicationScope scope)
    {
        if (scope.LevelUpRebuild?.HasContinuationPurpose != true && scope.AdmissionContinuation is null) return true;
        if (!ContinuationInvocations.TryGetValue(scope, out var issued) ||
            !ReferenceEquals(issued.Context, context) || !ReferenceEquals(issued.Expected, expected) ||
            !ReferenceEquals(issued.Scope, scope) || !ReferenceEquals(issued.LevelUp, scope.LevelUpRebuild) ||
            !ReferenceEquals(issued.Admission, scope.AdmissionContinuation) ||
            Interlocked.CompareExchange(ref issued.Execution, execution, null) is not null) return false;
        execution.BindContinuationInvocation(issued);
        return true;
    }

    private sealed partial class LegacyEquipmentQueryExecution
    {
        private ContinuationQueryInvocation? continuationInvocation;
        internal void BindContinuationInvocation(ContinuationQueryInvocation invocation)
        {
            if (continuationInvocation is not null || !ReferenceEquals(invocation.Execution, this) ||
                !ReferenceEquals(Request.Scope, invocation.Scope)) throw InvalidContinuationQueryMutation();
            continuationInvocation = invocation;
        }

        private bool HasCurrentContinuationInvocation() => continuationInvocation is { } invocation &&
            ReferenceEquals(invocation.Execution, this) && ReferenceEquals(Request.Scope, invocation.Scope) &&
            ContinuationInvocations.TryGetValue(Request.Scope, out var current) && ReferenceEquals(current, invocation);
    }
}
