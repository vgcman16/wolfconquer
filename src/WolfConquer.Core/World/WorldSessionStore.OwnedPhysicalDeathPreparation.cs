namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
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
        var actor = plan.ActorBinding; var target = plan.TargetBinding;
        if (framePersistenceOwner is not { } frame || !ReferenceEquals(plan.Context.Sessions, this) ||
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
