namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class PairMovementPublication
    {
        private readonly WorldSessionStore store;
        private readonly WorldServerFrameHandler frame;
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyOrdinarySpellBinding actor, partner;
        private readonly LegacyNativeActorScalarState.NativeValueProof actorAfter, partnerAfter;
        private readonly LegacyActionState.NativeValueProof actorAction, partnerAction;
        private readonly ushort fromX, fromY;
        private bool committed;
        internal WorldSession Actor { get; }
        internal WorldSession Partner { get; }

        internal PairMovementPublication(WorldSessionStore store, WorldServerFrameHandler frame, WorldPacketHandlerContext context,
            LegacyOrdinarySpellBinding actor, LegacyOrdinarySpellBinding partner, WorldSession own, WorldSession peer,
            LegacyPairMovementOperation operation)
        {
            this.store = store; this.frame = frame; this.context = context; this.actor = actor; this.partner = partner;
            Actor = own; Partner = peer; actorAfter = new(own.Character); partnerAfter = new(peer.Character);
            actorAction = new(own.Character); partnerAction = new(peer.Character);
            fromX = operation.FromX; fromY = operation.FromY;
        }

        internal void ConfirmCommitted()
        {
            if (!IsCurrentPlain()) throw InvalidSpellPublication();
            committed = true;
        }

        private bool IsCurrentPlain() => frame.IsPairMovementContextCurrent(store, context, Actor, Partner) &&
            store._sessions.TryGetValue(actor.Connection, out var own) && ReferenceEquals(own.Read(), Actor) &&
            store._sessions.TryGetValue(partner.Connection, out var peer) && ReferenceEquals(peer.Read(), Partner) &&
            store.BindingIsCurrentPlain(actor, own, Actor) && store.BindingIsCurrentPlain(partner, peer, Partner) &&
            actorAfter.IsCurrentPlain() && partnerAfter.IsCurrentPlain() && actorAction.IsCurrentPlain() && partnerAction.IsCurrentPlain() &&
            (uint)actor.Player.Action == actorAction.Value && (uint)partner.Player.Action == partnerAction.Value &&
            actor.Player.Px == fromX && actor.Player.Py == fromY && partner.Player.Px == fromX && partner.Player.Py == fromY &&
            NativeMatches(actor, actorAfter.Value) && NativeMatches(partner, partnerAfter.Value);

        private static bool NativeMatches(LegacyOrdinarySpellBinding binding, LegacyNativeActorScalarState.Values value) =>
            binding.Player.Reborn == value.Reborn && binding.Player.Agility == value.Agility &&
            binding.Player.X == value.X && binding.Player.Y == value.Y &&
            binding.Player.Map == value.Map && binding.Player.DynamicID == value.DynamicID;

        internal bool IsCurrent()
        {
            if (!committed) return false;
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, Actor, Partner);
                if (!store._connectionGates.TryGetValue(actor.Connection, out var ownGate) ||
                    !store._connectionGates.TryGetValue(partner.Connection, out var peerGate)) return false;
                bool ownFirst = GetConnectionGateOrder(actor.Connection) < GetConnectionGateOrder(partner.Connection);
                lock (ownFirst ? ownGate : peerGate)
                lock (ownFirst ? peerGate : ownGate)
                {
                    using var nativeGate = EnterNativeConnectionGate();
                    using var first = new SpellActorScope(ownFirst ? actor : partner);
                    using var second = new SpellActorScope(ownFirst ? partner : actor);
                    return IsCurrentPlain();
                }
            }
        }

        internal void FenceBoth(WorldSessionPublicationException failure)
        {
            actor.Recovery.Block("pairMovementRecoveryRequired:committedPostludeFailure");
            store.FenceObserverFailure(failure);
        }
    }
}
