namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private bool TryCommitOwnedBasicPhysicalHeld(WorldServerFrameHandler frame, WorldPacketHandlerContext context,
        MsgAttackPacket packet, WorldActiveSession selected, LegacyOrdinarySpellBinding actor, LegacyOrdinarySpellBinding target,
        SessionHolder own, SessionHolder peer, out BasicPhysicalPublication? publication, out string reason)
    {
        publication = null; reason = "ordinaryBasicPhysicalTeamPermissionUnavailable";
        TeamSessionStateStore.OwnedBasicPhysicalPermission permission;
        try { permission = TeamSessionStateStore.OwnedBasicPhysicalPermission.Prepare(
            context.TeamStates ?? throw InvalidSpellPublication(), context, packet, selected); }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException) { return false; }
        using var permissionScope = permission;
        if (!WorldPacketHandlers.TryPrepareOwnedBasicPhysicalReceive(context, packet, selected, permission, out var plan, out reason) || plan is null)
            return false;
        LegacyPreparedBasicPhysicalProficiencyWrite progression;
        LegacyPreparedBasicPhysicalWrite? death;
        try
        {
            progression = LegacyPreparedBasicPhysicalProficiencyWrite.Prepare(frame, this, plan);
            death = plan.Death is null ? null : LegacyPreparedBasicPhysicalWrite.Prepare(frame, this, plan);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or
            InvalidOperationException or ArgumentException or OverflowException or FormatException)
        {
            reason = "basicPhysicalFilePreflight:" + error.GetType().Name;
            return false; // No actual writer or native/holder publication has run.
        }
        using var actorPublication = new PreparedSessionSpellPublication(this, actor.Connection, own, plan.ExpectedActor, plan.Actor);
        using var targetPublication = new PreparedSessionSpellPublication(this, target.Connection, peer, plan.ExpectedTarget, plan.Target,
            physicalDeath: plan.Death, basicPhysical: plan, physicalDeathPublication: death?.PreparePublication());
        var canonicalActor = actorPublication.FinalSession; var canonicalTarget = targetPublication.FinalSession;
        if (!plan.CanonicalOutcomesMatch(canonicalActor, canonicalTarget) ||
            !frame.TryReadBasicPhysicalHitProposal(this, context, plan.Decision, out var before, out var generation, out var stamp) || before is null)
            throw InvalidSpellPublication();
        var change = IssueBasicPhysicalChange(frame, actor, plan.ExpectedActor, before, generation,
            LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CommittedHit, null, stamp, context);
        var native = LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication.Prepare(actor, change);
        var runtime = frame.PrepareBasicPhysicalNativePublication(this, actor, change, native, context.BasicPhysicalAttempt);
        var deathNative = plan.Death is null ? null : LegacyNativeMagicQueryClient.OwnedPhysicalDeathPublication.Prepare(target, plan.Death, canonicalTarget);
        var deathWind = plan.Death is null ? null : LegacyNativeStatusFlagsBigVector32.OwnedPhysicalDeathWindWalkerPublication.Prepare(target, plan.Death, canonicalTarget);
        var targetCancellation = plan.Death is null ? null : PrepareBasicPhysicalCancellation(frame, context, target, plan.ExpectedTarget);
        var token = new BasicPhysicalPublication(this);
        var data = new BasicPhysicalPublicationData(frame, plan, canonicalActor, canonicalTarget, native.After, deathNative,
            deathWind, progression, actorPublication.PreparedSpellGraph, actorPublication.PreparedProficiencyGraph,
            targetPublication.PreparedSpellGraph, targetPublication.PreparedProficiencyGraph, targetCancellation?.After, death);
        basicPhysicalPublications.Add(token, data); // Prepared/inert until its real writer pointer is assigned.
        var hitReceipt = PrepareOwnedBasicPhysicalHitReceipt(plan, token);
        using var notifications = plan.Death?.PrepareNotifications(token, canonicalActor, canonicalTarget);
        data.DeathNotifications = notifications;
        actorPublication.ArmFinalProof(); targetPublication.ArmFinalProof();
        if (!permission.IsCurrentPlain() || !plan.IsCurrentPlain() || !native.IsCurrentPlain() || !runtime.IsCurrentPlain() ||
            deathNative is not null && !deathNative.IsCurrentPlain() || deathWind is not null && !deathWind.IsCurrentPlain() ||
            targetCancellation is not null && !targetCancellation.IsCurrentPlain() ||
            notifications is not null && !notifications.IsCurrentPlain() ||
            !actorPublication.ConsumeBeforeWriterPlain() || !targetPublication.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
        var hp = targetPublication.HitpointsWrite ?? throw InvalidSpellPublication();
        bool committed;
        if (death is null)
        {
            var outcome = hp.TryCommit(out reason);
            committed = outcome == LegacyBoundHitpointsScalarWrite.Outcome.Committed;
            if (outcome == LegacyBoundHitpointsScalarWrite.Outcome.RecoveryRequired)
                throw FenceBasicPhysicalPersistence(data, "basicPhysicalHitOutcomeUnproven:" + reason);
        }
        else
        {
            var outcome = death.TryCommit(context.PhysicalDeathJournalInterruption, out reason);
            committed = outcome == LegacyPreparedBasicPhysicalWrite.Outcome.Committed;
            if (outcome == LegacyPreparedBasicPhysicalWrite.Outcome.RecoveryRequired)
                throw FenceBasicPhysicalPersistence(data, "basicPhysicalDeathOutcomeUnproven:" + reason);
        }
        if (!committed) return false;
        if (!permission.IsCurrentPlain() || !targetPublication.AcceptExternallyPersistedHitpoints() || !actorPublication.IsCurrentPlain() ||
            !targetPublication.IsCurrentPlain() || !native.IsCurrentPlain() || !runtime.IsCurrentPlain() ||
            deathNative is not null && !deathNative.IsCurrentPlain() || deathWind is not null && !deathWind.IsCurrentPlain() ||
            targetCancellation is not null && !targetCancellation.IsCurrentPlain() ||
            notifications is not null && !notifications.IsCurrentPlain())
            throw FenceBasicPhysicalPersistence(data, "basicPhysicalCommittedPublicationUnproven");
        actorPublication.PublishNative(); targetPublication.PublishNative();
        deathNative?.PublishPrepared(); deathWind?.PublishPrepared(); notifications?.PublishPrepared();
        native.PublishPrepared(); runtime.PublishPrepared();
        targetCancellation?.PublishPrepared();
        actorPublication.PublishHolder(); targetPublication.PublishHolder();
        data.CommittedWriter = hp; // Actual same writer/publication, never a supplied success boolean.
        CommitOwnedBasicPhysicalHitReceipt(hitReceipt, token, data);
        notifications?.Dispose(); // Same synchronous thread; no await holds team preparation.
        actorPublication.Dispose(); targetPublication.Dispose();
        bool actorFirst = GetConnectionGateOrder(actor.Connection) < GetConnectionGateOrder(target.Connection);
        using var firstItems = (actorFirst ? actor : target).Items.EnterCurrentReadScope();
        using var secondItems = (actorFirst ? target : actor).Items.EnterCurrentReadScope();
        try
        {
            PersistFrameOwnedSessionChange(actor.Connection, plan.ExpectedActor, canonicalActor, actor.NativeIdentity, actor, actorPublication.HitpointsWrite);
            PersistFrameOwnedSessionChange(target.Connection, plan.ExpectedTarget, canonicalTarget, target.NativeIdentity, target, hp,
                completedPhysicalDeath: death is null ? null : token);
        }
        catch (Exception error)
        { throw FenceBasicPhysicalPersistence(data, "basicPhysicalCommittedPostludeFailed", error); }
        publication = token; reason = string.Empty; return true;
    }
}
