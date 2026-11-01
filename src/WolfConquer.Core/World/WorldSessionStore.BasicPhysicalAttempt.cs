using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryBeginBasicPhysical(WorldPacketHandlerContext context, MsgAttackPacket packet,
        WorldActiveSession target, out WorldServerFrameHandler.BasicPhysicalAttempt? attempt)
    {
        attempt = null;
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame || context.Session is not { } expected ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual) ||
            ReferenceEquals(context.Connection, target.Connection)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, target.Session);
            if (!_connectionGates.TryGetValue(context.Connection, out var ownGate) ||
                !_connectionGates.TryGetValue(target.Connection, out var peerGate)) return false;
            bool ownFirst = GetConnectionGateOrder(context.Connection) < GetConnectionGateOrder(target.Connection);
            lock (ownFirst ? ownGate : peerGate)
            lock (ownFirst ? peerGate : ownGate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var ownHolder) || !ReferenceEquals(ownHolder.Read(), expected) ||
                    ownHolder.SpellBinding is not { } own || !BindingIsCurrentPlain(own, ownHolder, expected) ||
                    !_sessions.TryGetValue(target.Connection, out var peerHolder) || !ReferenceEquals(peerHolder.Read(), target.Session) ||
                    peerHolder.SpellBinding is not { } peer || !BindingIsCurrentPlain(peer, peerHolder, target.Session) ||
                    ReferenceEquals(own.Client, peer.Client)) return false;
                using var first = new SpellActorScope(ownFirst ? own : peer);
                using var second = new SpellActorScope(ownFirst ? peer : own);
                if (!BindingIsCurrentPlain(own, ownHolder, expected) || !BindingIsCurrentPlain(peer, peerHolder, target.Session)) return false;
                return frame.TryBeginBasicPhysical(actual, packet, target, out attempt);
            }
        }
    }
}
