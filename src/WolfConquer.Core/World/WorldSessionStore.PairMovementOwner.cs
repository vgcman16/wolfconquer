namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Called below the existing complete file/journal lock boundary too. Every
    // higher-order owner gate is already held; this method acquires none.
    internal bool IsPairMovementWriteOwnerCurrent(LegacyPreparedPairMovementWrite write)
    {
        var own = write.ActorBinding; var peer = write.PartnerBinding;
        var actor = write.ActorBefore; var partner = write.PartnerBefore; var op = write.Operation;
        if (!ReferenceEquals(framePersistenceOwner, write.Frame) || !ReferenceEquals(write.Store, this) ||
            !IsCurrentThreadInsideAnyConnectionGate || !write.Frame.IsPairMovementContextCurrent(this, write.Context, actor, partner) ||
            !_connectionGates.TryGetValue(own.Connection, out var ownGate) || !Monitor.IsEntered(ownGate) ||
            !_connectionGates.TryGetValue(peer.Connection, out var peerGate) || !Monitor.IsEntered(peerGate) ||
            !_sessions.TryGetValue(own.Connection, out var ownHolder) || !ReferenceEquals(ownHolder.Read(), actor) ||
            !ReferenceEquals(ownHolder.SpellBinding, own) || !BindingIsCurrentPlain(own, ownHolder, actor) ||
            !_sessions.TryGetValue(peer.Connection, out var peerHolder) || !ReferenceEquals(peerHolder.Read(), partner) ||
            !ReferenceEquals(peerHolder.SpellBinding, peer) || !BindingIsCurrentPlain(peer, peerHolder, partner) ||
            !HasCurrentInteractionPairProof(own, actor, peer, partner) ||
            !Monitor.IsEntered(own.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(own.Player.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(peer.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(peer.Player.SpellAdmissionSyncRoot) ||
            !write.ContextCoordinatesMatch() || actor.InteractionPartnerUid != op.PartnerUid || partner.InteractionPartnerUid != op.ActorUid ||
            !ReferenceEquals(own.Recovery, peer.Recovery) || !ReferenceEquals(own.Store, this) || !ReferenceEquals(peer.Store, this) ||
            !string.Equals(own.UsersRoot, peer.UsersRoot, StringComparison.OrdinalIgnoreCase)) return false;
        // Native kinematics is owned by the real joint admission/Store scalar
        // participant, not by a cached item ID or raw saved DynamicID alias.
        return own.Player.Map == op.MapId && peer.Player.Map == op.MapId &&
            own.Player.DynamicID == op.DynamicId && peer.Player.DynamicID == op.DynamicId &&
            own.Player.X == op.FromX && own.Player.Y == op.FromY && peer.Player.X == op.FromX && peer.Player.Y == op.FromY;
    }
}
