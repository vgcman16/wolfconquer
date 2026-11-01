namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal WorldPacketHandlerContext RequireOwnedDragonhowlDeathContext(OwnedDragonhowlDeath purpose)
    {
        if (framePersistenceOwner is not { } frame || !dragonhowlDeaths.TryGetValue(purpose, out var data) ||
            !ReferenceEquals(data.Owner.Frame, frame) || !purpose.IsCurrentPlain())
            throw new InvalidOperationException("ownedDragonhowlDeathActualOwnerChanged");
        return frame.CreateOwnedDragonhowlDeathContext(this, purpose);
    }
    internal WorldPacketHandlerContext RequireOwnedPhysicalDeathContext(WorldPacketHandlerContext context,
        WorldPacketHandlers.BasicPhysicalReceiveDecision decision, WorldServerFrameHandler.BasicPhysicalAttempt attempt,
        LegacyOrdinarySpellBinding actor, LegacyOrdinarySpellBinding target, WorldSession expectedActor, WorldSession expectedTarget)
    {
        if (framePersistenceOwner is not { } frame || !ReferenceEquals(context.Sessions, this) ||
            !ReferenceEquals(actor.Store, this) || !ReferenceEquals(target.Store, this) ||
            !frame.TryGetBasicPhysicalAttemptRoles(context, attempt, out var own, out var peer) ||
            !ReferenceEquals(own, actor) || !ReferenceEquals(peer, target) ||
            !_connectionGates.TryGetValue(actor.Connection, out var ownGate) || !Monitor.IsEntered(ownGate) ||
            !_connectionGates.TryGetValue(target.Connection, out var peerGate) || !Monitor.IsEntered(peerGate) ||
            !Monitor.IsEntered(actor.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(actor.Player.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(target.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(target.Player.SpellAdmissionSyncRoot) ||
            !_sessions.TryGetValue(actor.Connection, out var ownHolder) || !ReferenceEquals(ownHolder.Read(), expectedActor) ||
            !ReferenceEquals(ownHolder.SpellBinding, actor) || !BindingIsCurrentPlain(actor, ownHolder, expectedActor) ||
            !_sessions.TryGetValue(target.Connection, out var peerHolder) || !ReferenceEquals(peerHolder.Read(), expectedTarget) ||
            !ReferenceEquals(peerHolder.SpellBinding, target) || !BindingIsCurrentPlain(target, peerHolder, expectedTarget))
            throw new InvalidOperationException("ordinaryPhysicalDeathActualOwnersChanged");
        return frame.CreateOwnedPhysicalDeathContext(this, context, decision, attempt, expectedActor);
    }

    internal bool IsOwnedPhysicalDeathPlanCurrent(WorldPacketHandlers.OwnedPhysicalDeathPreparation plan)
    {
        if (plan.Dragonhowl is { } proc)
            return plan.Decision is null && plan.Attempt is null && dragonhowlDeaths.TryGetValue(proc, out var data) &&
                (data.Death is null || ReferenceEquals(data.Death, plan)) && proc.IsCurrentPlain() &&
                ReferenceEquals(plan.ActorBinding, proc.ActorBinding) && ReferenceEquals(plan.TargetBinding, proc.TargetBinding);
        var actor = plan.ActorBinding; var target = plan.TargetBinding;
        if (plan.Attempt is null || plan.Decision is null || framePersistenceOwner is not { } frame || !ReferenceEquals(plan.Context.Sessions, this) ||
            !ReferenceEquals(actor.Store, this) || !ReferenceEquals(target.Store, this) ||
            !frame.TryGetBasicPhysicalAttemptRoles(plan.Context, plan.Attempt, out var own, out var peer) ||
            !ReferenceEquals(own, actor) || !ReferenceEquals(peer, target) ||
            !_connectionGates.TryGetValue(actor.Connection, out var ownGate) || !Monitor.IsEntered(ownGate) ||
            !_connectionGates.TryGetValue(target.Connection, out var peerGate) || !Monitor.IsEntered(peerGate) ||
            !Monitor.IsEntered(actor.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(actor.Player.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(target.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(target.Player.SpellAdmissionSyncRoot) ||
            !_sessions.TryGetValue(actor.Connection, out var ownHolder) || !ReferenceEquals(ownHolder.Read(), plan.ExpectedActor) ||
            !ReferenceEquals(ownHolder.SpellBinding, actor) || !BindingIsCurrentPlain(actor, ownHolder, plan.ExpectedActor) ||
            !_sessions.TryGetValue(target.Connection, out var peerHolder) || !ReferenceEquals(peerHolder.Read(), plan.ExpectedTarget) ||
            !ReferenceEquals(peerHolder.SpellBinding, target) || !BindingIsCurrentPlain(target, peerHolder, plan.ExpectedTarget)) return false;
        return WorldPacketHandlers.TryReadBasicPhysicalReceive(plan.Decision, out var attempt, out var kind, out _, out _, out _) &&
            ReferenceEquals(attempt, plan.Attempt) && kind == WorldPacketHandlers.BasicPhysicalReceiveKind.NormalDeath;
    }
}
