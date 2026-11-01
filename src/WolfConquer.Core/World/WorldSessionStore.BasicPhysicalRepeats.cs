using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsActualBasicPhysicalServerRequest(WorldPacketHandlerContext context, MsgAttackPacket packet) =>
        framePersistenceOwner?.IsActualBasicPhysicalServerRequest(context, packet) == true;

    internal bool TryCaptureDueBasicPhysical(WorldServerFrameHandler frame, ConquerTcpConnection connection,
        out WorldServerFrameHandler.BasicPhysicalRequest? request, out MsgAttackPacket? packet,
        out WorldServerFrameHandler.BasicPhysicalRepeatTicket? ticket)
    {
        request = null; packet = null; ticket = null;
        if (!ReferenceEquals(framePersistenceOwner, frame)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(connection));
            if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(connection, out var holder) || holder.Read() is not { } current ||
                    holder.SpellBinding is not { } binding || !BindingIsCurrentPlain(binding, holder, current)) return false;
                using var actors = new SpellActorScope(binding);
                return frame.TryIssueDueBasicPhysicalRequest(this, current, out request, out packet, out ticket);
            }
        }
    }

    internal void StopBasicPhysicalRepeat(WorldServerFrameHandler frame, ConquerTcpConnection connection,
        WorldServerFrameHandler.BasicPhysicalRepeatTicket ticket)
    {
        if (!ReferenceEquals(framePersistenceOwner, frame)) return;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(connection));
            if (!_connectionGates.TryGetValue(connection, out var gate)) return;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(connection, out var holder) || holder.Read() is not { } current ||
                    holder.SpellBinding is not { } binding || !BindingIsCurrentPlain(binding, holder, current)) return;
                using var actors = new SpellActorScope(binding);
                if (!frame.IsBasicPhysicalRepeatTicketCurrent(this, ticket, binding)) return;
                var context = frame.PrepareBasicPhysicalRuntimeContext(this, new(connection, current));
                var cancellation = PrepareBasicPhysicalCancellation(frame, context, binding, current);
                if (cancellation.IsCurrentPlain()) cancellation.PublishPrepared();
            }
        }
    }
}
