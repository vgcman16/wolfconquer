using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class StaminaPublication
    {
        internal sealed class WirePermit { }
        private sealed record WireData(ConquerTcpConnection Recipient, object RecipientIdentity,
            WorldSession RecipientSession, byte[] Frame, byte[] FrameImage);
        private readonly ConditionalWeakTable<WirePermit, WireData> wirePermits = new();

        private WirePermit? IssueWire(ConquerTcpConnection recipient, WorldSession expected, byte[] frame)
        {
            if (!TryCurrent(out var owner) ||
                !LegacyNativeClientPublication.TryCaptureIdentity(store, recipient, expected.Character.Uid, out var identity) ||
                !LegacyNativeClientPublication.TryCurrent(store, recipient, expected.Character.Uid, identity, out var target) ||
                !ReferenceEquals(target, expected) ||
                (!ReferenceEquals(recipient, context.Connection) &&
                    !WorldPacketHandlers.WindWalkerViewerVisible(owner.Character, target.Character))) return null;
            var permit = new WirePermit();
            wirePermits.Add(permit, new(recipient, identity, target, frame, frame.ToArray()));
            return permit;
        }

        // A private actual-publication permit and the installed transport's
        // one-use active gate start are both needed. No caller validator is used.
        internal bool TryStartWire(WirePermit permit, ConquerTcpConnection recipient,
            RootExpiryWireWriter writer, object start, CancellationToken token, out ValueTask write)
        {
            write = default;
            if (!wirePermits.TryGetValue(permit, out var data) || !ReferenceEquals(recipient, data.Recipient) ||
                !ReferenceEquals(writer.Connection, recipient) || !recipient.IsRegisteredRootExpiryWire(writer) ||
                !writer.ConsumeActiveStart(start) || !store._connectionGates.TryGetValue(context.Connection, out var actorGate) ||
                !store._connectionGates.TryGetValue(recipient, out var recipientGate)) return false;
            while (true)
            {
              token.ThrowIfCancellationRequested();
              if (!store.TryGet(context.Connection, out var candidateActor) || !store.TryGet(recipient, out var candidatePeer)) return false;
              lock (readiness.LifecycleGate)
              {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, candidateActor, candidatePeer);
                object first = GetConnectionGateOrder(context.Connection) <= GetConnectionGateOrder(recipient) ? actorGate : recipientGate;
                object second = ReferenceEquals(first, actorGate) ? recipientGate : actorGate;
                lock (first) lock (second)
                {
                    using var native = EnterNativeConnectionGate();
                    if (!committed || !readiness.Frame.IsStaminaReadinessCurrent(readiness) ||
                        !store._sessions.TryGetValue(context.Connection, out var actorHolder) ||
                        actorHolder.Read() is not { } actor ||
                        !store._sessions.TryGetValue(recipient, out var peerHolder) || peerHolder.Read() is not { } peer) return false;
                    if (!ReferenceEquals(actor, candidateActor) || !ReferenceEquals(peer, candidatePeer)) continue;
                    if (!ReferenceEquals(actorHolder.SpellBinding, readiness.Binding) ||
                        !store.BindingIsCurrentPlain(readiness.Binding, actorHolder, actor) ||
                        (!ReferenceEquals(recipient, context.Connection) && !ReferenceEquals(candidatePeer, data.RecipientSession)) ||
                        !ReferenceEquals(peerHolder.NativeClientIdentity, data.RecipientIdentity)) return false;
                    using var actors = new SpellActorScope(readiness.Binding);
                    if (!ReferenceEquals(readiness.Binding.Player.BitVector, flags.Vector)) return false;
                    lock (flags.Vector.SyncRoot)
                    {
                        try
                        {
                            if (!step.IsCommittedPlain() || !flags.IsCurrentPlain(readiness.Binding.Player.BitVector) ||
                                readiness.Binding.Player.Stamina != step.Stamina ||
                                LegacyStaminaState.RequireCurrent(actor.Character) != step.Stamina ||
                                fan is not null && !store.WindWalkerOwnerForStaminaPlain(readiness.Binding, flags, fan) ||
                                !ReferenceEquals(recipient, context.Connection) &&
                                    !WorldPacketHandlers.WindWalkerViewerVisible(actor.Character, candidatePeer.Character) ||
                                !data.Frame.AsSpan().SequenceEqual(data.FrameImage) || !wirePermits.Remove(permit)) return false;
                        }
                        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                        { return false; }
                        token.ThrowIfCancellationRequested();
                        write = writer.StartActualWire(data.Frame, token);
                        return true;
                    }
                }
              }
            }
        }

        private WorldSessionPublicationException? FenceInterruptedOwner(Exception cause)
        {
            if (!store.TryGet(context.Connection, out var candidate)) return null;
            lock (readiness.LifecycleGate)
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, candidate);
                if (!store._connectionGates.TryGetValue(context.Connection, out var gate)) return null;
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    if (!store._sessions.TryGetValue(context.Connection, out var holder) ||
                        !ReferenceEquals(holder.Read(), candidate) || !ReferenceEquals(holder.SpellBinding, readiness.Binding) ||
                        !store.BindingIsCurrentPlain(readiness.Binding, holder, candidate) ||
                        !readiness.Frame.IsStaminaReadinessCurrent(readiness)) return null;
                    using var actors = new SpellActorScope(readiness.Binding);
                    if (!ReferenceEquals(readiness.Binding.Player.BitVector, flags.Vector)) return null;
                    lock (flags.Vector.SyncRoot)
                    {
                        if (!step.IsCommittedPlain() || !flags.IsCurrentPlain(readiness.Binding.Player.BitVector) ||
                            readiness.Binding.Player.Stamina != step.Stamina) return null;
                        var failure = new WorldSessionPublicationException(store, context.Connection,
                            readiness.Binding.NativeIdentity, readiness.Binding, candidate,
                            WorldSessionPublicationStage.ProcContinuation, cause);
                        store.BlockPublishedRecovery(failure);
                        try { store.FencePublishedOwnerUnderGate(failure); }
                        catch (Exception secondary) { failure.RecordFencingFailure(secondary); }
                        return failure;
                    }
                }
            }
        }
        private WorldSession? RefuseInterruptedOwner()
        {
            var failure = FenceInterruptedOwner(new InvalidOperationException("staminaCommittedOutputInterrupted"));
            if (failure is not null) throw failure;
            return null;
        }

    }
}
