namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal sealed class InteractionInvitationPublication
    {
        private readonly WorldServerFrameHandler frame;
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly WorldSession owner, partner;
        private readonly MsgAttackPacket packet;
        private readonly InvitationSlot ownSlot, peerSlot;
        private readonly InvitationState? beforeState;
        private readonly InvitationState transition;
        private readonly long ownBefore, peerBefore, ownAfter, peerAfter;
        private bool committed;
        internal MsgAttackType Kind => packet.Query.AttackType;
        internal uint Dance => transition.Dance;
        internal WorldServerFrameHandler Frame => frame;
        internal WorldPacketHandlerContext Context => context;
        internal WorldSession OwnerBefore => owner;
        internal WorldSession PartnerBefore => partner;
        internal MsgAttackPacket OutputPacket => Kind is MsgAttackType.InteractionAccept or MsgAttackType.InteractionStopEffect
            ? new(packet.Query with { ResponseDamage = Dance }) : packet;

        private InteractionInvitationPublication(WorldServerFrameHandler frame, WorldSessionStore store,
            WorldPacketHandlerContext context, WorldSession owner, WorldSession partner, MsgAttackPacket packet)
        {
            this.frame = frame; this.store = store; this.context = context;
            this.owner = owner; this.partner = partner; this.packet = packet;
            var own = owner.NativeGameClient!.OrdinarySpellBinding!; var peer = partner.NativeGameClient!.OrdinarySpellBinding!;
            ownSlot = frame.interactionInvitations.GetValue(own.Connection, static _ => new());
            peerSlot = frame.interactionInvitations.GetValue(peer.Connection, static _ => new());
            ownBefore = ownSlot.Generation; peerBefore = peerSlot.Generation;
            ownAfter = checked(ownBefore + 1); peerAfter = checked(peerBefore + 1);
            beforeState = ownSlot.State;
            transition = Kind == MsgAttackType.InteractionRequest
                ? new(own, ownSlot, peer, peerSlot, packet.Query.ResponseDamage) : beforeState!;
        }

        internal static InteractionInvitationPublication? Prepare(WorldServerFrameHandler frame, WorldSessionStore store,
            WorldPacketHandlerContext context, WorldSession owner, WorldSession partner, MsgAttackPacket packet)
        {
            if (!frame.CanBeginInteractionInvitationPlain(store, context, owner, partner, packet)) return null;
            var plan = new InteractionInvitationPublication(frame, store, context, owner, partner, packet);
            return plan.IsCurrentPlain() ? plan : null;
        }

        internal bool IsCurrentPlain() => !committed && ownSlot.Generation == ownBefore && peerSlot.Generation == peerBefore &&
            ReferenceEquals(ownSlot.State, beforeState) && ReferenceEquals(peerSlot.State, beforeState) &&
            frame.CanBeginInteractionInvitationPlain(store, context, owner, partner, packet);

        // Called only after BOTH real prepared Store/native publications pass
        // final proof. All transition storage/generations are preallocated.
        internal void PublishPrepared()
        {
            int phase = Kind switch
            {
                MsgAttackType.InteractionRequest => InvitationPending,
                MsgAttackType.InteractionAccept => InvitationPaired,
                _ => InvitationClosed,
            };
            transition.Phase = phase;
            ownSlot.Generation = ownAfter; peerSlot.Generation = peerAfter;
            if (ReferenceEquals(transition.RequesterSlot, ownSlot))
            { transition.RequesterGeneration = ownAfter; transition.RecipientGeneration = peerAfter; }
            else { transition.RequesterGeneration = peerAfter; transition.RecipientGeneration = ownAfter; }
            var next = phase == InvitationClosed ? null : transition;
            Volatile.Write(ref ownSlot.State, next); Volatile.Write(ref peerSlot.State, next);
            committed = true;
        }

        internal bool IsPublishedCurrentPlain()
        {
            if (!committed || ownSlot.Generation != ownAfter || peerSlot.Generation != peerAfter || Volatile.Read(ref transition.Retired) != 0)
                return false;
            return Kind is MsgAttackType.InteractionRefuse or MsgAttackType.InteractionStopEffect
                ? transition.Phase == InvitationClosed && ownSlot.State is null && peerSlot.State is null
                : transition.IsLivePlain(Kind == MsgAttackType.InteractionRequest ? InvitationPending : InvitationPaired);
        }
    }
}
