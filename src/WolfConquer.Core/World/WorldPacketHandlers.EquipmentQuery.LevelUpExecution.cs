namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class PreparedLevelUpRebuild
    {
        private LegacyNativeMagicQueryClient.CompletedMaximums? completion;
        private WorldSession? restoredSession;

        internal async ValueTask<CompletedLevelUpPublication?> RebuildAndRestoreAsync(
            WorldSessionStore.ActionPublication receipt, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref started, 1, 0) != 0 || committed is null ||
                !ReferenceEquals(receipt.Published, committed) || !receipt.TryCurrent(out var current) ||
                !ReferenceEquals(current, committed) || !IsQueryCurrent(context, current, current)) return null;
            var scope = new LegacyEquipmentQueryPublicationScope(binding.Uid, current.PlayerItems)
            { OwnerNativeClientIdentity = binding.NativeIdentity, PhysicalReceiveOwner = binding, LevelUpRebuild = this };
            var result = await ExecuteLegacyEquipmentQueryAsync(context with { Session = current }, current,
                new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.LevelUp, scope, CallItems: false),
                cancellationToken).ConfigureAwait(false);
            if (!result.Admitted || !result.Completed || result.Session is not { } rebuilt || completion is null ||
                !IsQueryCurrent(context, rebuilt, rebuilt)) return null;

            // UpdateLevel restores HP only after the real full query. The
            // ordinary bound HP writer commits before team/self setter sends.
            int restored = checked((int)rebuilt.EquipmentHitpointsStatus!.MaximumHitpoints);
            var final = await AssignLegacyPlayerHitpointsAsync(context with { Session = rebuilt }, rebuilt,
                scope, restored, cancellationToken).ConfigureAwait(false);
            if (!final.Completed || final.Session is not { } published || !IsQueryCurrent(context, published, published) ||
                published.Character.MinHitPoints != restored || binding.Player.HitPoints != restored) return null;
            restoredSession = published;
            return CompletedLevelUpPublication.Issue(this, published);
        }

        // Called only by the actual query completion phase, with its privately
        // registered native completion. This performs no substitute query.
        internal bool AcceptQueryCompletion(WorldPacketHandlerContext actual, WorldSession current,
            LegacyNativeMagicQueryClient.CompletedMaximums proof)
        {
            if (completion is not null || !IsQueryCurrent(actual, current, current) || !proof.IsCurrentPlain(binding, current)) return false;
            completion = proof;
            return true;
        }

        internal bool IsQueryCurrent(WorldPacketHandlerContext actual, WorldSession supplied, WorldSession current) =>
            Volatile.Read(ref started) == 1 && committed is not null && ReferenceEquals(supplied, current) &&
            ReferenceEquals(actual.Connection, context.Connection) && ReferenceEquals(actual.Sessions, binding.Store) &&
            ReferenceEquals(actual.WorldData, context.WorldData) &&
            ReferenceEquals(actual.FrameOperationLease, context.FrameOperationLease) &&
            ReferenceEquals(actual.TradeOperations, context.TradeOperations) &&
            actual.TradeOperations?.OwnsActiveExclusiveLease(actual.FrameOperationLease) == true &&
            SameProgression(current) && items.IsCurrent(current.PlayerItems) && metadata.IsCurrentPlain() &&
            speed.IsCurrentPlain(context.WorldData) &&
            binding.Store.IsLevelUpRebuildQueryOwnerCurrent(binding, current, actual, completion);

        internal bool IsRestoredCurrent(WorldSession current) => completion is not null &&
            ReferenceEquals(current, restoredSession) && IsQueryCurrent(context, current, current);
    }

    internal sealed class CompletedLevelUpPublication
    {
        private readonly PreparedLevelUpRebuild owner;
        internal WorldSession Session { get; }
        private CompletedLevelUpPublication(PreparedLevelUpRebuild owner, WorldSession session)
        { this.owner = owner; Session = session; }

        internal static CompletedLevelUpPublication? Issue(PreparedLevelUpRebuild owner, WorldSession session) =>
            owner.IsRestoredCurrent(session)
                ? new(owner, session) : null;

        internal bool TryCurrent(out WorldSession current)
        {
            current = null!;
            if (!owner.IsRestoredCurrent(Session)) return false;
            current = Session;
            return true;
        }
    }
}
