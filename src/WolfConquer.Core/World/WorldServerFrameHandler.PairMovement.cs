namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Closed failure-only engine control; no path, images, writer or accepted
    // result can be supplied by a packet/configuration/test callback.
    internal LegacyJournalInterruption? PairMovementJournalInterruption { get; set; }

    internal bool IsPairMovementContextCurrent(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldSession actor, WorldSession partner)
    {
        var own = actor.NativeGameClient?.OrdinarySpellBinding;
        var peer = partner.NativeGameClient?.OrdinarySpellBinding;
        return own is not null && peer is not null && IsActionLifecycleCurrent(store, context) &&
            ReferenceEquals(context.Connection, own.Connection) && ReferenceEquals(own.Store, store) && ReferenceEquals(peer.Store, store) &&
            ReferenceEquals(own.Recovery, _tradeRecovery) && ReferenceEquals(peer.Recovery, _tradeRecovery) &&
            !ReferenceEquals(own.Client, peer.Client) && !_tradeStates.HasConnectionEnded(peer.Connection) &&
            _creationLifetimes.TryGetValue(peer.Connection, out var lifetime) && lifetime.IsCurrent(peer.Connection) &&
            store.TryGet(own.Connection, out var actual) && ReferenceEquals(actual, actor) &&
            store.TryGet(peer.Connection, out var other) && ReferenceEquals(other, partner) &&
            HasCurrentInteractionPairProof(store, actor, partner);
    }
}

public sealed partial record WorldPacketHandlerContext
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyJournalInterruption? PairMovementJournalInterruption { get; init; }
}
