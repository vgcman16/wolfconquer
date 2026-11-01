namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class PreparedLevelUpRebuild
    {
        private LegacyExperienceContinuation? continuationMarker;
        private CompletedContinuationQuery? continuationQuery;
        private WorldSession? continuationSession;
        internal bool HasContinuationPurpose => continuation is not null;

        internal static bool TryPrepare(WorldPacketHandlerContext context, WorldSessionStore.OwnedContinuationStep purpose,
            LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed, out PreparedLevelUpRebuild? plan) =>
            TryPrepareCore(context, null, purpose, binding, expected, proposed, out plan);

        internal static bool TryPrepareResume(WorldPacketHandlerContext context, WorldSessionStore.OwnedContinuationStep purpose,
            LegacyOrdinarySpellBinding binding, WorldSession expected, out PreparedLevelUpRebuild? plan) =>
            TryPrepareCore(context, null, purpose, binding, expected, expected, out plan);

        private static bool ContinuationCovers(WorldSessionStore.OwnedContinuationStep purpose,
            WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed)
        {
            var before = LegacyExperienceContinuationCodec.ReadCharacter(expected.Character, expected.AccountId);
            var after = LegacyExperienceContinuationCodec.ReadCharacter(proposed.Character, proposed.AccountId);
            if (before is null || after is null || after.Phase is not
                (LegacyExperienceContinuationPhase.QueryPending or LegacyExperienceContinuationPhase.RestorePending)) return false;
            if (ReferenceEquals(expected, proposed))
                return purpose.CoversResume(context, binding, expected);
            LegacyExperienceContinuationCodec.ValidateTransition(before, after);
            return after.Phase == LegacyExperienceContinuationPhase.QueryPending && after.CurrentLevel == before.CurrentLevel + 1 &&
                purpose.CoversRebuild(context, binding, expected, proposed);
        }

        private bool ContinuationMarkerMatches(WorldSession current) => continuation is null || continuationMarker is not null &&
            Equals(continuationMarker, LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId));

        // The continuation has its own actual step/admission origin. A receipt
        // from a former process or an ordinary combat award cannot start this.
        internal async ValueTask<CompletedContinuationQuery?> RunContinuationQueryAsync(CancellationToken cancellationToken)
        {
            if (purpose is not null || continuation is null || Interlocked.CompareExchange(ref started, 1, 0) != 0 ||
                committed is null || !continuation.TryConsumeQueryStart(context, binding, committed, out var current) ||
                !ReferenceEquals(current, committed) || !IsQueryCurrent(context, current, current)) return null;
            var rebuilt = await ExecuteQueryAsync(current, cancellationToken).ConfigureAwait(false);
            if (rebuilt is null || continuationMarker is null || completion is null) return null;
            // Pending level-up's later source HP setter is signed, too.
            _ = checked((int)rebuilt.EquipmentHitpointsStatus!.MaximumHitpoints);
            continuationSession = rebuilt;
            continuationQuery = CompletedContinuationQuery.Issue(this, rebuilt, rebuilt.EquipmentHitpointsStatus.MaximumHitpoints);
            return continuationQuery;
        }

        internal bool IsContinuationCurrent(WorldSession current) => continuation is not null && completion is not null &&
            ReferenceEquals(current, continuationSession) &&
            IsQueryCurrent(context, current, current);

        internal bool CoversContinuation(WorldSession current, LegacyExperienceContinuation pending) =>
            Equals(continuationMarker, pending) && IsContinuationCurrent(current);

        internal bool CoversPendingQueryProjection(WorldPacketHandlerContext actual, WorldSession expected,
            WorldSession proposed, LegacyExperienceContinuation pending) => continuation is not null && completion is null &&
            pending.Phase is LegacyExperienceContinuationPhase.QueryPending or LegacyExperienceContinuationPhase.RestorePending &&
            Equals(continuationMarker, pending) && IsQueryCurrent(actual, expected, expected) && SameProgression(proposed) &&
            ReferenceEquals(expected.PlayerItems, proposed.PlayerItems) &&
            Equals(pending, LegacyExperienceContinuationCodec.ReadCharacter(proposed.Character, proposed.AccountId));

        internal bool TryAdvanceContinuation(CompletedContinuationQuery query, WorldSession before,
            WorldSessionStore.OwnedContinuationCheckpoint checkpoint, out WorldSession after)
        {
            after = null!;
            if (!ReferenceEquals(query, continuationQuery) || continuationMarker is null ||
                !checkpoint.TryResolve(query, binding, before, out var published) ||
                !IsQueryOwnerCurrent(context, published, published)) return false;
            var pending = LegacyExperienceContinuationCodec.ReadCharacter(published.Character, published.AccountId);
            if (pending is null || pending.CurrentLevel != continuationMarker.CurrentLevel ||
                pending.StepIndex != continuationMarker.StepIndex) return false;
            LegacyExperienceContinuationCodec.ValidateTransition(continuationMarker, pending);
            continuationMarker = pending;
            continuationSession = published;
            if (!IsQueryCurrent(context, published, published)) return false;
            after = published;
            return true;
        }

        internal async ValueTask<bool> PublishContinuationHitpointsAsync(WorldSession current, uint maximum,
            CancellationToken cancellationToken)
        {
            if (continuationMarker?.Phase != LegacyExperienceContinuationPhase.Ready || !IsContinuationCurrent(current) ||
                current.EquipmentHitpointsStatus?.MaximumHitpoints != maximum || current.Character.MinHitPoints != maximum) return false;
            var scope = CreateScope(current);
            // The real continuation writer already committed HP plus Ready.
            // This is the existing source setter's notification sequence only.
            var team = await SendLegacyAssignedHitpointsTeamInfoAsync(context with { Session = current }, current,
                scope, checked((int)maximum), cancellationToken).ConfigureAwait(false);
            if (!team.Completed || !ReferenceEquals(team.Session, current)) return false;
            var self = await SendLegacyCurrentStatusHitpointPairAsync(context with { Session = current }, current,
                scope, cancellationToken).ConfigureAwait(false);
            return self.Completed && ReferenceEquals(self.Session, current) && IsContinuationCurrent(current);
        }
    }

    internal sealed partial class CompletedContinuationQuery
    {
        private readonly PreparedLevelUpRebuild owner;
        private int restoredOutputStarted;
        internal WorldSession Session { get; private set; }
        internal uint MaximumHitpoints { get; }
        private CompletedContinuationQuery(PreparedLevelUpRebuild owner, WorldSession session, uint maximum)
        { this.owner = owner; Session = session; MaximumHitpoints = maximum; }

        internal static CompletedContinuationQuery? Issue(PreparedLevelUpRebuild owner, WorldSession current, uint maximum) =>
            owner.IsContinuationCurrent(current) && current.EquipmentHitpointsStatus?.MaximumHitpoints == maximum
                ? new(owner, current, maximum) : null;

        internal bool TryCurrent(out WorldSession current)
        {
            current = null!;
            if (!owner.IsContinuationCurrent(Session)) return false;
            current = Session; return true;
        }

        internal bool CoversPending(LegacyExperienceContinuation pending) => owner.CoversContinuation(Session, pending);

        internal bool TryAdvance(WorldSessionStore.OwnedContinuationCheckpoint checkpoint)
        {
            if (!owner.TryAdvanceContinuation(this, Session, checkpoint, out var after)) return false;
            Session = after; return true;
        }

        internal ValueTask<bool> PublishRestoredHitpointsAsync(CancellationToken cancellationToken) =>
            Interlocked.CompareExchange(ref restoredOutputStarted, 1, 0) == 0
                ? owner.PublishContinuationHitpointsAsync(Session, MaximumHitpoints, cancellationToken)
                : ValueTask.FromResult(false);
    }
}
