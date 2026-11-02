using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class ReviverWirePermit { }
    private enum ReviverWireKind { Owner, Animation, Spawn }
    private sealed record ReviverWireData(CommittedOwnedReviver Receipt, WorldServerFrameHandler.ReviverAudience Recipient,
        ReviverWireKind Kind, int Index);
    private readonly ConditionalWeakTable<ReviverWirePermit, ReviverWireData> reviverWires = new();

    private ReviverWirePermit IssueReviverWire(CommittedOwnedReviver receipt,
        WorldServerFrameHandler.ReviverAudience recipient, ReviverWireKind kind, int index)
    {
        var permit = new ReviverWirePermit();
        reviverWires.Add(permit, new(receipt, recipient, kind, index)); return permit;
    }

    internal bool TryStartOwnedReviverWire(ReviverWirePermit permit, ConquerTcpConnection recipient,
        RootExpiryWireWriter writer, object start, CancellationToken token, out ValueTask write)
    {
        write = default;
        if (!reviverWires.TryGetValue(permit, out var wire) || !ReferenceEquals(wire.Recipient.Connection, recipient) ||
            !committedOwnedRevivers.TryGetValue(wire.Receipt, out var data) || Volatile.Read(ref data.Started) != 1 ||
            !ReferenceEquals(writer.Connection, recipient) || !recipient.IsRegisteredRootExpiryWire(writer) ||
            !writer.ConsumeActiveStart(start) || !_connectionGates.TryGetValue(data.Connection, out var actorGate) ||
            !_connectionGates.TryGetValue(recipient, out var peerGate)) return false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (!TryGet(data.Connection, out var candidateActor) || !TryGet(recipient, out var candidatePeer)) return false;
            lock (data.Frame.RequireActionLifecycleGate(this))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, candidateActor, candidatePeer);
                object first = GetConnectionGateOrder(data.Connection) <= GetConnectionGateOrder(recipient) ? actorGate : peerGate;
                object second = ReferenceEquals(first, actorGate) ? peerGate : actorGate;
                lock (first) lock (second)
                {
                    using var native = EnterNativeConnectionGate();
                    if (!_sessions.TryGetValue(data.Connection, out var actorHolder) || actorHolder.Read() is not { } actor ||
                        !_sessions.TryGetValue(recipient, out var peerHolder) || peerHolder.Read() is not { } peer) return false;
                    if (!ReferenceEquals(actor, candidateActor) || !ReferenceEquals(peer, candidatePeer)) continue;
                    if (!ReferenceEquals(actorHolder.NativeClientIdentity, data.Identity) ||
                        !ReferenceEquals(actorHolder.SpellBinding, data.Binding) || !BindingIsCurrentPlain(data.Binding, actorHolder, actor) ||
                        !ReferenceEquals(peerHolder.NativeClientIdentity, wire.Recipient.Identity) ||
                        peer.Character.Uid != wire.Recipient.Uid || actor.Character.MinHitPoints <= 0) return false;
                    using var actors = new SpellActorScope(data.Binding);
                    var vector = data.Binding.Player.BitVector;
                    if (vector is null) return false;
                    lock (vector.SyncRoot)
                    {
                        byte[] bytes;
                        try
                        {
                            _ = LegacyWindWalkerStatusProjection.Read(actor.Character);
                            var actorFields = LegacyNativeAttributeMapProof.CaptureRaw(actor.Character.Attributes);
                            var peerFields = LegacyNativeAttributeMapProof.CaptureRaw(peer.Character.Attributes);
                            if (LegacyPlayerDeathState.HasStatusFlag(actor.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag) ||
                                data.Binding.Player.HitPoints != actor.Character.MinHitPoints ||
                                data.Binding.Player.Stamina != LegacyStaminaState.RequireCurrent(actor.Character) ||
                                !data.Query.IsCurrentPlain(data.Binding, actor)) return false;
                            if (wire.Kind == ReviverWireKind.Owner && !ReferenceEquals(recipient, data.Connection)) return false;
                            if (wire.Kind == ReviverWireKind.Animation && !WorldServerFrameHandler.ReviverAudienceCanSeeMonster(peer, data.Monster.Instance)) return false;
                            if (wire.Kind == ReviverWireKind.Spawn && (ReferenceEquals(recipient, data.Connection) ||
                                !WorldServerFrameHandler.ReviverAudienceCanSeePlayer(peer, actor))) return false;
                            var current = data.Frame.PrepareReviverFrames(actor, data.Binding.Player, data.Monster.Instance, data.Maximums.MaximumHitpoints);
                            bytes = wire.Kind switch
                            {
                                ReviverWireKind.Owner when (uint)wire.Index < (uint)current.Owner.Length => current.Owner[wire.Index],
                                ReviverWireKind.Animation when (uint)wire.Index < (uint)data.Frames.Animation.Length => data.Frames.Animation[wire.Index],
                                ReviverWireKind.Spawn when wire.Index == 0 => current.Spawn,
                                _ => throw new InvalidOperationException("reviverWireStageInvalid"),
                            };
                            if (!actorFields.IsCurrentPlain() || !peerFields.IsCurrentPlain() ||
                                !data.Query.IsCurrentPlain(data.Binding, actor) || !reviverWires.Remove(permit)) return false;
                        }
                        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                        { return false; }
                        token.ThrowIfCancellationRequested();
                        write = writer.StartActualWire(bytes, token); return true;
                    }
                }
            }
        }
    }

    private WorldSessionPublicationException? FenceInterruptedReviver(OwnedReviverData data, Exception cause)
    {
        if (!_connectionGates.TryGetValue(data.Connection, out var gate)) return null;
        while (true)
        {
            if (!TryGet(data.Connection, out var candidate)) return null;
            lock (data.Frame.RequireActionLifecycleGate(this))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, candidate);
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    if (!_sessions.TryGetValue(data.Connection, out var holder) || holder.Read() is not { } actual) return null;
                    if (!ReferenceEquals(actual, candidate)) continue;
                    if (!ReferenceEquals(holder.NativeClientIdentity, data.Identity) ||
                        !ReferenceEquals(holder.SpellBinding, data.Binding) || !BindingIsCurrentPlain(data.Binding, holder, actual) ||
                        actual.Character.MinHitPoints <= 0) return null;
                    try { if (LegacyPlayerDeathState.HasStatusFlag(actual.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag)) return null; }
                    catch (Exception malformed) when (malformed is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                    { cause = new AggregateException(cause, malformed); }
                    using var actors = new SpellActorScope(data.Binding);
                    var failure = new WorldSessionPublicationException(this, data.Connection, data.Identity, data.Binding, actual,
                        WorldSessionPublicationStage.ProcContinuation, cause);
                    BlockPublishedRecovery(failure);
                    try { FencePublishedOwnerUnderGate(failure); } catch (Exception secondary) { failure.RecordFencingFailure(secondary); }
                    return failure;
                }
            }
        }
    }

    internal async ValueTask<int?> PublishOwnedReviverAsync(CommittedOwnedReviver receipt, CancellationToken token)
    {
        if (!committedOwnedRevivers.TryGetValue(receipt, out var data) || Interlocked.CompareExchange(ref data.Started, 1, 0) != 0) return null;
        try
        {
            var owner = new WorldServerFrameHandler.ReviverAudience(data.Connection, data.After.Character.Uid, data.Identity);
            for (int i = 0; i < data.Frames.Owner.Length; i++)
            {
                var permit = IssueReviverWire(receipt, owner, ReviverWireKind.Owner, i);
                if (!await data.Connection.SendOwnedReviverAsync(this, permit, token).ConfigureAwait(false))
                {
                    var failure = FenceInterruptedReviver(data, new InvalidOperationException("reviverCommittedOutputInterrupted"));
                    if (failure is not null) throw failure;
                    return null;
                }
            }
            int viewers = 0;
            foreach (var peer in data.Audience)
            {
                bool sent = data.Frames.Animation.Length > 0;
                for (int i = 0; i < data.Frames.Animation.Length; i++)
                {
                    var permit = IssueReviverWire(receipt, peer, ReviverWireKind.Animation, i);
                    if (!await peer.Connection.SendOwnedReviverAsync(this, permit, token).ConfigureAwait(false)) { sent = false; break; }
                }
                if (sent) viewers++;
            }
            foreach (var peer in data.Audience)
            {
                if (ReferenceEquals(peer.Connection, data.Connection)) continue;
                var permit = IssueReviverWire(receipt, peer, ReviverWireKind.Spawn, 0);
                _ = await peer.Connection.SendOwnedReviverAsync(this, permit, token).ConfigureAwait(false);
            }
            return viewers;
        }
        catch (WorldSessionPublicationException) { throw; }
        catch (Exception error)
        {
            WorldSessionPublicationException? failure;
            try { failure = FenceInterruptedReviver(data, error); }
            catch (Exception secondary) { throw new AggregateException(error, secondary); }
            if (failure is not null) throw failure;
            throw;
        }
        finally { committedOwnedRevivers.Remove(receipt); }
    }
}
