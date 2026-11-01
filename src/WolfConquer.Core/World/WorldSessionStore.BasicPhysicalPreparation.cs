namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryReadOwnedBasicPhysicalPreparationOwners(WorldPacketHandlerContext context, MsgAttackPacket packet,
        WorldActiveSession selected, out LegacyOrdinarySpellBinding actor, out LegacyOrdinarySpellBinding target,
        out WorldSession expectedActor, out WorldSession expectedTarget)
    {
        actor = target = null!; expectedActor = expectedTarget = null!;
        if (framePersistenceOwner is not { } frame || context.BasicPhysicalAttempt is not { } attempt ||
            !frame.TryGetBasicPhysicalAttemptRoles(context, attempt, out var own, out var peer) ||
            !ReferenceEquals(context.Connection, own.Connection) || !ReferenceEquals(selected.Connection, peer.Connection) ||
            !TryGet(own.Connection, out var currentActor) || currentActor is null ||
            !TryGet(peer.Connection, out var currentTarget) || currentTarget is null ||
            !ReferenceEquals(context.Session, currentActor) || !ReferenceEquals(selected.Session, currentTarget) ||
            !IsOwnedBasicPhysicalPreparationCurrent(context, packet, own, peer, currentActor, currentTarget)) return false;
        actor = own; target = peer; expectedActor = currentActor; expectedTarget = currentTarget;
        return true;
    }

    // Before-owner proof, deliberately valid through the real file writer and
    // invalid after native/holder publication. No file read or lock acquisition.
    internal bool IsOwnedBasicPhysicalPreparationCurrent(WorldPacketHandlerContext context, MsgAttackPacket packet,
        LegacyOrdinarySpellBinding actor, LegacyOrdinarySpellBinding target, WorldSession expectedActor, WorldSession expectedTarget) =>
        framePersistenceOwner is { } frame && IsCurrentThreadInsideAnyConnectionGate &&
        context.BasicPhysicalAttempt is { } attempt && frame.IsBasicPhysicalContextCurrentPlain(this, context) &&
        frame.IsBasicPhysicalAttemptPacketCurrent(context, attempt, packet) &&
        frame.TryGetBasicPhysicalAttemptRoles(context, attempt, out var own, out var peer) &&
        ReferenceEquals(own, actor) && ReferenceEquals(peer, target) && ReferenceEquals(actor.Store, this) && ReferenceEquals(target.Store, this) &&
        _connectionGates.TryGetValue(actor.Connection, out var actorGate) && Monitor.IsEntered(actorGate) &&
        _connectionGates.TryGetValue(target.Connection, out var targetGate) && Monitor.IsEntered(targetGate) &&
        Monitor.IsEntered(actor.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(actor.Player.SpellAdmissionSyncRoot) &&
        Monitor.IsEntered(target.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(target.Player.SpellAdmissionSyncRoot) &&
        _sessions.TryGetValue(actor.Connection, out var actorHolder) && ReferenceEquals(actorHolder.Read(), expectedActor) &&
        ReferenceEquals(actorHolder.SpellBinding, actor) && BindingIsCurrentPlain(actor, actorHolder, expectedActor) &&
        _sessions.TryGetValue(target.Connection, out var targetHolder) && ReferenceEquals(targetHolder.Read(), expectedTarget) &&
        ReferenceEquals(targetHolder.SpellBinding, target) && BindingIsCurrentPlain(target, targetHolder, expectedTarget);
}
