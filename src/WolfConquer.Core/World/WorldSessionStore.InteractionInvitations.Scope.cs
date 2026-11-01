namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class InteractionPairScope : IDisposable
    {
        private readonly WorldSessionStore store;
        internal readonly WorldServerFrameHandler Frame;
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldActiveSession Partner;
        internal WorldSession Owner => Context.Session!;
        internal SessionHolder OwnHolder = null!, PeerHolder = null!;
        internal LegacyOrdinarySpellBinding Own = null!, Peer = null!;
        private object? lifecycle, firstGate, secondGate;
        private bool lifecycleHeld, firstHeld, secondHeld, nativeHeld;
        private NativeConnectionGateScope native;
        private IDisposable? package;
        private SpellActorScope? firstActors, secondActors;

        private InteractionPairScope(WorldSessionStore store, WorldServerFrameHandler frame,
            WorldPacketHandlerContext context, WorldActiveSession partner)
        { this.store = store; Frame = frame; Context = context; Partner = partner; }

        internal static InteractionPairScope? TryEnter(WorldSessionStore store, WorldPacketHandlerContext context, WorldActiveSession partner)
        {
            RefuseBoundSpellReentry(context.Connection); RefuseBoundSpellReentry(partner.Connection);
            if (store.framePersistenceOwner is not { } frame || context.Session is not { } owner ||
                ReferenceEquals(context.Connection, partner.Connection) ||
                !frame.TryPrepareActionContext(store, context, owner, out var actual)) return null;
            var scope = new InteractionPairScope(store, frame, actual, partner);
            try { if (scope.Enter()) return scope; }
            catch { scope.Dispose(); throw; }
            scope.Dispose(); return null;
        }

        private bool Enter()
        {
            lifecycle = Frame.RequireActionLifecycleGate(store); Monitor.Enter(lifecycle, ref lifecycleHeld);
            package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, Owner, Partner.Session);
            if (!store._connectionGates.TryGetValue(Context.Connection, out var ownGate) ||
                !store._connectionGates.TryGetValue(Partner.Connection, out var peerGate)) return false;
            bool ownFirst = GetConnectionGateOrder(Context.Connection) < GetConnectionGateOrder(Partner.Connection);
            firstGate = ownFirst ? ownGate : peerGate; secondGate = ownFirst ? peerGate : ownGate;
            Monitor.Enter(firstGate, ref firstHeld); Monitor.Enter(secondGate, ref secondHeld);
            native = EnterNativeConnectionGate(); nativeHeld = true;
            if (!store._sessions.TryGetValue(Context.Connection, out var ownHolder) || !ReferenceEquals(ownHolder.Read(), Owner) ||
                !store._sessions.TryGetValue(Partner.Connection, out var peerHolder) || !ReferenceEquals(peerHolder.Read(), Partner.Session) ||
                ownHolder.SpellBinding is not { } own || peerHolder.SpellBinding is not { } peer || ReferenceEquals(own.Client, peer.Client)) return false;
            OwnHolder = ownHolder; PeerHolder = peerHolder; Own = own; Peer = peer;
            firstActors = new(ownFirst ? own : peer); secondActors = new(ownFirst ? peer : own);
            return IsCurrentPlain();
        }

        internal bool IsCurrentPlain() => IsCurrentThreadInsideAnyConnectionGate &&
            Monitor.IsEntered(lifecycle!) && Monitor.IsEntered(firstGate!) && Monitor.IsEntered(secondGate!) &&
            Monitor.IsEntered(Own.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(Own.Player.SpellAdmissionSyncRoot) &&
            Monitor.IsEntered(Peer.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(Peer.Player.SpellAdmissionSyncRoot) &&
            store.BindingIsCurrentPlain(Own, OwnHolder, Owner) &&
            store.BindingIsCurrentPlain(Peer, PeerHolder, Partner.Session) &&
            Frame.IsInteractionInvitationParticipantsCurrent(store, Context, Owner, Partner.Session);

        public void Dispose()
        {
            secondActors?.Dispose(); secondActors = null; firstActors?.Dispose(); firstActors = null;
            if (nativeHeld) { nativeHeld = false; native.Dispose(); }
            if (secondHeld) { secondHeld = false; Monitor.Exit(secondGate!); }
            if (firstHeld) { firstHeld = false; Monitor.Exit(firstGate!); }
            package?.Dispose(); package = null;
            if (lifecycleHeld) { lifecycleHeld = false; Monitor.Exit(lifecycle!); }
        }
    }

    internal void RetireInteractionInvitations(LegacyOrdinarySpellBinding binding)
    {
        if (ReferenceEquals(binding.Store, this)) framePersistenceOwner?.RetireInteractionInvitation(binding);
    }

    internal bool HasCurrentInteractionPairProof(LegacyOrdinarySpellBinding own, WorldSession actor,
        LegacyOrdinarySpellBinding peer, WorldSession partner) => framePersistenceOwner is { } frame &&
        _connectionGates.TryGetValue(own.Connection, out var ownGate) && Monitor.IsEntered(ownGate) &&
        _connectionGates.TryGetValue(peer.Connection, out var peerGate) && Monitor.IsEntered(peerGate) &&
        _sessions.TryGetValue(own.Connection, out var ownHolder) && ReferenceEquals(ownHolder.SpellBinding, own) &&
        _sessions.TryGetValue(peer.Connection, out var peerHolder) && ReferenceEquals(peerHolder.SpellBinding, peer) &&
        Monitor.IsEntered(own.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(own.Player.SpellAdmissionSyncRoot) &&
        Monitor.IsEntered(peer.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(peer.Player.SpellAdmissionSyncRoot) &&
        BindingIsCurrentPlain(own, ownHolder, actor) && BindingIsCurrentPlain(peer, peerHolder, partner) &&
        frame.HasCurrentInteractionPairProof(this, actor, partner);
}
