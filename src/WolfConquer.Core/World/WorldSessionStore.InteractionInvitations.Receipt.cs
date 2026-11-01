using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class InteractionInvitationReceipt
    {
        private readonly WorldSessionStore store;
        private readonly WorldServerFrameHandler.InteractionInvitationPublication transition;
        private readonly LegacyOrdinarySpellBinding own, peer;
        private readonly byte[] frame, swapped;
        internal WorldSession Owner { get; }
        internal WorldSession Partner { get; }
        internal ConquerTcpConnection OwnerConnection => own.Connection;
        internal ConquerTcpConnection PartnerConnection => peer.Connection;
        internal ReadOnlyMemory<byte> Frame => frame;
        internal ReadOnlyMemory<byte> Swapped => swapped;
        internal uint Dance => transition.Dance;

        internal InteractionInvitationReceipt(WorldSessionStore store,
            WorldServerFrameHandler.InteractionInvitationPublication transition, WorldSession owner, WorldSession partner,
            LegacyOrdinarySpellBinding own, LegacyOrdinarySpellBinding peer)
        {
            this.store = store; this.transition = transition; this.own = own; this.peer = peer;
            Owner = owner; Partner = partner;
            var output = transition.OutputPacket;
            frame = output.ToFrameBytes();
            swapped = transition.Kind == MsgAttackType.InteractionStopEffect
                ? new MsgAttackPacket(output.Query with { Uid = output.Query.OpponentUid, OpponentUid = checked((uint)owner.Character.Uid) }).ToFrameBytes()
                : [];
        }

        internal bool IsCurrent()
        {
            var context = transition.Context with { Session = Owner };
            using var scope = InteractionPairScope.TryEnter(store, context, new(PartnerConnection, Partner));
            return scope is not null && ReferenceEquals(scope.Frame, transition.Frame) &&
                ReferenceEquals(scope.Own, own) && ReferenceEquals(scope.Peer, peer) && transition.IsPublishedCurrentPlain();
        }
    }
}
