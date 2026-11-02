using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryStartPlayerTickXp(WorldServerFrameHandler frame, WorldServerFrameHandler.PlayerTickXpPermit permit,
        ConquerTcpConnection connection, object identity, RootExpiryWireWriter writer, object activeStart,
        CancellationToken token, out ValueTask write)
    {
        write = default;
        if (!ReferenceEquals(writer.Connection, connection) || !connection.IsRegisteredRootExpiryWire(writer) ||
            !writer.ConsumeActiveStart(activeStart) || !_connectionGates.TryGetValue(connection, out var gate)) return false;
        while (true)
        {
          token.ThrowIfCancellationRequested();
          if (!TryGet(connection, out var candidate)) return false;
          lock (frame.RequireActionLifecycleGate(this))
          {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, candidate);
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(connection, out var holder)) return false;
                if (!ReferenceEquals(holder.Read(), candidate)) continue;
                if (!ReferenceEquals(holder.NativeClientIdentity, identity) || holder.SpellBinding is not { } binding ||
                    !BindingIsCurrentPlain(binding, holder, candidate)) return false;
                using var actors = new SpellActorScope(binding);
                if (!WorldServerFrameHandler.TryReadPlayerTickXpWire(permit, frame, this, connection, identity, candidate, true, out var bytes)) return false;
                token.ThrowIfCancellationRequested();
                write = writer.StartActualWire(bytes, token); return true;
            }
          }
        }
    }
    internal WorldSessionPublicationException? FenceInterruptedPlayerTickXp(WorldServerFrameHandler frame,
        WorldServerFrameHandler.PlayerTickXpPermit permit, ConquerTcpConnection connection, object identity, Exception cause)
    {
        if (!_connectionGates.TryGetValue(connection, out var gate) || !TryGet(connection, out var candidate)) return null;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, candidate);
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), candidate) ||
                    !ReferenceEquals(holder.NativeClientIdentity, identity) || holder.SpellBinding is not { } binding ||
                    !BindingIsCurrentPlain(binding, holder, candidate)) return null;
                using var actors = new SpellActorScope(binding);
                if (!WorldServerFrameHandler.TryReadPlayerTickXpWire(permit, frame, this, connection, identity, candidate, false, out _)) return null;
                var failure = new WorldSessionPublicationException(this, connection, identity, binding, candidate,
                    WorldSessionPublicationStage.ProcContinuation, cause);
                BlockPublishedRecovery(failure);
                try { FencePublishedOwnerUnderGate(failure); }
                catch (Exception secondary) { failure.RecordFencingFailure(secondary); }
                return failure;
            }
        }
    }

}
