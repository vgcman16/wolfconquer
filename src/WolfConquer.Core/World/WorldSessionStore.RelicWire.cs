using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class RelicWireOutput { }
    internal sealed class RelicWirePermit { }
    private sealed class RelicWireState(WorldPacketHandlerContext context, SessionHolder holder,
        LegacyOrdinarySpellBinding binding, LegacyPreparedSpellFileWrite writer,
        LegacyRelicFuseOwnedEvidence? compose, LegacyRelicFuseReturnOwnedEvidence? returned, byte[][] frames)
    {
        internal readonly WorldPacketHandlerContext Context = context;
        internal readonly SessionHolder Holder = holder;
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal readonly LegacyPreparedSpellFileWrite Writer = writer;
        internal readonly LegacyRelicFuseOwnedEvidence? Compose = compose;
        internal readonly LegacyRelicFuseReturnOwnedEvidence? Return = returned;
        internal readonly byte[][] Frames = frames.Select(frame => frame.ToArray()).ToArray();
        internal WorldSession? Final;
        internal int Next;
        internal RelicWirePermit? Pending;
        internal bool Started;
        internal bool TransportCompleted;
        internal bool ManagedCompleted;
        internal bool HandedOff;
    }
    private readonly ConditionalWeakTable<RelicWireOutput, RelicWireState> relicWireOutputs = new();
    private readonly ConditionalWeakTable<RelicWirePermit, RelicWireState> relicWirePermits = new();

    // Called by the real Store publisher while its actor and holder are held.
    // The private table, not these frame bytes, grants subsequent authority.
    private RelicWireOutput PrepareRelicWire(WorldPacketHandlerContext context, SessionHolder holder,
        LegacyOrdinarySpellBinding binding, LegacyPreparedSpellFileWrite writer,
        LegacyRelicFuseOwnedEvidence? compose, LegacyRelicFuseReturnOwnedEvidence? returned, byte[][] frames)
    {
        if ((compose is null) == (returned is null) || frames.Length != 2 || frames.Any(frame => frame is null || frame.Length < 4) ||
            !context.Connection.HasRegisteredRelicWire || context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) != true)
            throw InvalidSpellPublication();
        var output = new RelicWireOutput();
        relicWireOutputs.Add(output, new(context, holder, binding, writer, compose, returned, frames));
        return output;
    }

    private void PublishRelicWire(RelicWireOutput output, WorldSession final)
    {
        if (!relicWireOutputs.TryGetValue(output, out var data) || data.Final is not null ||
            !ReferenceEquals(data.Holder.Read(), final) || !BindingIsCurrentPlain(data.Binding, data.Holder, final) ||
            !(data.Compose is { } compose ? data.Writer.HasCommittedRelicFuse(compose) :
                data.Return is { } returned && data.Writer.HasCommittedRelicFuseReturn(returned)))
            throw InvalidSpellPublication();
        data.Final = final;
    }

    internal bool TryIssueRelicWire(RelicWireOutput output, out RelicWirePermit? permit)
    {
        permit = null;
        if (!relicWireOutputs.TryGetValue(output, out var data)) return false;
        lock (data)
        {
            if (!RelicWireCurrent(data) || data.Pending is not null || data.Next >= data.Frames.Length) return false;
            var issued = new RelicWirePermit(); relicWirePermits.Add(issued, data);
            data.Pending = issued; data.Started = false; data.TransportCompleted = false; permit = issued; return true;
        }
    }

    private bool RelicWireCurrent(RelicWireState data) => data.HandedOff && RelicWireOwnerCurrent(data);

    private bool RelicWireOwnerCurrent(RelicWireState data) => data.Final is { } final &&
        ReferenceEquals(data.Context.Sessions, this) && data.Context.Connection.HasRegisteredRelicWire &&
        data.Context.TradeRecovery is { IsBlocked: false } && data.Context.Trades is { } trades &&
        !trades.HasConnectionEnded(data.Context.Connection) &&
        _sessions.TryGetValue(data.Context.Connection, out var holder) && ReferenceEquals(holder, data.Holder) &&
        ReferenceEquals(holder.Read(), final) && BindingIsCurrentPlain(data.Binding, holder, final) &&
        (data.Compose is { } compose ? data.Writer.HasCommittedRelicFuse(compose) :
            data.Return is { } returned && data.Writer.HasCommittedRelicFuseReturn(returned));

    internal bool TryStartRelicWire(RelicWirePermit permit, ConquerTcpConnection recipient,
        RootExpiryWireWriter writer, object activeStart, CancellationToken token, out ValueTask pending)
    {
        pending = default;
        if (!relicWirePermits.TryGetValue(permit, out var data) || !ReferenceEquals(data.Context.Connection, recipient) ||
            !ReferenceEquals(writer.Connection, recipient) || !recipient.IsRegisteredRootExpiryWire(writer) ||
            !writer.ConsumeActiveStart(activeStart) || !_connectionGates.TryGetValue(recipient, out var gate)) return false;
        if (data.Final is not { } expected) return false;
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        lock (gate)
        {
            using var native = EnterNativeConnectionGate();
            using var actors = new SpellActorScope(data.Binding);
            lock (data)
            {
                if (!ReferenceEquals(data.Pending, permit) || data.Started || !RelicWireCurrent(data) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, expected)) return false;
                token.ThrowIfCancellationRequested();
                data.Started = true;
                pending = writer.StartActualWire(data.Frames[data.Next], token);
                return true;
            }
        }
    }

    internal bool MarkRelicWireTransportComplete(RelicWirePermit permit, ConquerTcpConnection recipient,
        RootExpiryWireWriter writer, object completion)
    {
        if (!relicWirePermits.TryGetValue(permit, out var data) || !ReferenceEquals(data.Context.Connection, recipient) ||
            !ReferenceEquals(writer.Connection, recipient) || !recipient.IsRegisteredRootExpiryWire(writer) ||
            !writer.ConsumeRelicCompletion(completion)) return false;
        lock (data)
        {
            if (!ReferenceEquals(data.Pending, permit) || !data.Started || data.TransportCompleted) return false;
            data.TransportCompleted = true; return true;
        }
    }

    internal bool CompleteRelicWire(RelicWireOutput output, RelicWirePermit permit)
    {
        if (!relicWireOutputs.TryGetValue(output, out var data)) return false;
        lock (data)
        {
            if (!ReferenceEquals(data.Pending, permit) || !data.Started || !data.TransportCompleted || !RelicWireCurrent(data) ||
                !relicWirePermits.Remove(permit)) return false;
            data.Pending = null; data.Started = false; data.TransportCompleted = false; data.Next++;
            return true;
        }
    }
}
