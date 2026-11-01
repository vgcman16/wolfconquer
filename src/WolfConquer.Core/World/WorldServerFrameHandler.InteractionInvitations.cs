using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int InvitationPending = 1, InvitationPaired = 2, InvitationClosed = 3;
    private sealed class InvitationSlot
    {
        internal InvitationState? State;
        internal InvitationState? RetiredPair;
        internal long Generation;
    }
    private sealed class InvitationState(LegacyOrdinarySpellBinding requester, InvitationSlot requesterSlot,
        LegacyOrdinarySpellBinding recipient, InvitationSlot recipientSlot, uint dance)
    {
        internal readonly LegacyOrdinarySpellBinding Requester = requester, Recipient = recipient;
        internal readonly InvitationSlot RequesterSlot = requesterSlot, RecipientSlot = recipientSlot;
        internal readonly uint Dance = dance;
        internal int Phase = InvitationPending, Retired;
        internal LegacyOrdinarySpellBinding? RetiredEndpoint;
        internal long RequesterGeneration, RecipientGeneration;
        internal bool Contains(LegacyOrdinarySpellBinding binding) => ReferenceEquals(Requester, binding) || ReferenceEquals(Recipient, binding);
        internal bool IsLivePlain(int phase) => Volatile.Read(ref Retired) == 0 && Phase == phase &&
            ReferenceEquals(Volatile.Read(ref RequesterSlot.State), this) && ReferenceEquals(Volatile.Read(ref RecipientSlot.State), this) &&
            RequesterSlot.Generation == RequesterGeneration && RecipientSlot.Generation == RecipientGeneration;
    }
    private readonly ConditionalWeakTable<ConquerTcpConnection, InvitationSlot> interactionInvitations = new();

    // Shared pending/paired lifetime proof; deliberately does NOT call the
    // movement-specific helper, which additionally requires an accepted pair.
    internal bool IsInteractionInvitationParticipantsCurrent(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldSession actor, WorldSession partner)
    {
        var own = actor.NativeGameClient?.OrdinarySpellBinding;
        var peer = partner.NativeGameClient?.OrdinarySpellBinding;
        return own is not null && peer is not null && IsActionLifecycleCurrent(store, context) &&
            actor.Character.Uid != partner.Character.Uid && actor.AccountId != partner.AccountId &&
            ReferenceEquals(context.Connection, own.Connection) && ReferenceEquals(own.Store, store) && ReferenceEquals(peer.Store, store) &&
            ReferenceEquals(own.Recovery, _tradeRecovery) && ReferenceEquals(peer.Recovery, _tradeRecovery) &&
            !ReferenceEquals(own.Client, peer.Client) && !_tradeStates.HasConnectionEnded(peer.Connection) &&
            _creationLifetimes.TryGetValue(peer.Connection, out var lifetime) && lifetime.IsCurrent(peer.Connection) &&
            store.TryGet(own.Connection, out var current) && ReferenceEquals(current, actor) &&
            store.TryGet(peer.Connection, out var other) && ReferenceEquals(other, partner);
    }

    internal bool CanBeginInteractionInvitationPlain(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldSession actor, WorldSession partner, MsgAttackPacket packet)
    {
        if (!IsInteractionInvitationParticipantsCurrent(store, context, actor, partner) ||
            packet.Query.Uid != actor.Character.Uid || packet.Query.OpponentUid != partner.Character.Uid ||
            actor.Character.MapId == 5040 || packet.Query.AttackType != MsgAttackType.InteractionStopEffect &&
                !WorldPacketHandlers.AreInteractionInvitationParticipantsVisible(actor, partner)) return false;
        var own = actor.NativeGameClient!.OrdinarySpellBinding!;
        var peer = partner.NativeGameClient!.OrdinarySpellBinding!;
        interactionInvitations.TryGetValue(own.Connection, out var ownSlot);
        interactionInvitations.TryGetValue(peer.Connection, out var peerSlot);
        var state = ownSlot?.State;
        if (packet.Query.AttackType == MsgAttackType.InteractionRequest)
            return actor.InteractionPartnerUid is null && partner.InteractionPartnerUid is null && state is null && peerSlot?.State is null;
        if (state is null || !ReferenceEquals(state, peerSlot?.State) || !state.Contains(own) || !state.Contains(peer)) return false;
        if (packet.Query.AttackType == MsgAttackType.InteractionStopEffect)
            return HasCurrentInteractionPairProof(store, actor, partner);
        if (!state.IsLivePlain(InvitationPending) || actor.InteractionPartnerUid is not null || partner.InteractionPartnerUid is not null) return false;
        return packet.Query.AttackType == MsgAttackType.InteractionRefuse ||
            packet.Query.AttackType == MsgAttackType.InteractionAccept && ReferenceEquals(state.Recipient, own) && ReferenceEquals(state.Requester, peer);
    }

    internal bool HasCurrentInteractionPairProof(WorldSessionStore store, WorldSession actor, WorldSession partner)
    {
        var own = actor.NativeGameClient?.OrdinarySpellBinding; var peer = partner.NativeGameClient?.OrdinarySpellBinding;
        if (!ReferenceEquals(store, _sessions) || !Monitor.IsEntered(_tradeStates.SyncRoot) ||
            _ownedAdmissionRuntime is not { IsStopping: false } || _tradeRecovery.IsBlocked || own is null || peer is null ||
            !ReferenceEquals(own.Store, store) || !ReferenceEquals(peer.Store, store) ||
            !interactionInvitations.TryGetValue(own.Connection, out var ownSlot) ||
            !interactionInvitations.TryGetValue(peer.Connection, out var peerSlot) || ownSlot.State is not { } state ||
            !ReferenceEquals(state, peerSlot.State) || !state.Contains(own) || !state.Contains(peer) || !state.IsLivePlain(InvitationPaired) ||
            actor.InteractionPartnerUid != partner.Character.Uid || partner.InteractionPartnerUid != actor.Character.Uid ||
            _tradeStates.HasConnectionEnded(own.Connection) || _tradeStates.HasConnectionEnded(peer.Connection) ||
            !_creationLifetimes.TryGetValue(own.Connection, out var ownLifetime) || !ownLifetime.IsCurrent(own.Connection) ||
            !_creationLifetimes.TryGetValue(peer.Connection, out var peerLifetime) || !peerLifetime.IsCurrent(peer.Connection)) return false;
        return store.TryGet(own.Connection, out var current) && ReferenceEquals(current, actor) &&
            store.TryGet(peer.Connection, out var other) && ReferenceEquals(other, partner) &&
            store.IsBoundSpellOwnerCurrent(own) && store.IsBoundSpellOwnerCurrent(peer);
    }

    // Deny-only actual retirement hook. No gate acquisition, peer holder write,
    // lookup by UID, or stale-EOF removal of a newer binding/transition.
    internal void RetireInteractionInvitation(LegacyOrdinarySpellBinding binding)
    {
        if (!ReferenceEquals(binding.Store, _sessions) || !interactionInvitations.TryGetValue(binding.Connection, out var slot) ||
            Volatile.Read(ref slot.State) is not { } state || !state.Contains(binding)) return;
        Interlocked.CompareExchange(ref state.RetiredEndpoint, binding, null);
        Interlocked.Exchange(ref state.Retired, 1);
        if (state.Phase == InvitationPaired)
        {
            // The already allocated pair is the only cleanup evidence. Publish
            // it before freeing slots; never acquire or edit the peer holder.
            Volatile.Write(ref state.RequesterSlot.RetiredPair, state);
            Volatile.Write(ref state.RecipientSlot.RetiredPair, state);
        }
        Interlocked.CompareExchange(ref state.RequesterSlot.State, null, state);
        Interlocked.CompareExchange(ref state.RecipientSlot.State, null, state);
    }
}
