namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // The real commit caller cannot arm this record by passing matching DATA:
    // it invokes the existing managed completion on the exact Store-issued
    // output, writer and published holder while the real Frame is still active.
    internal void CompleteManagedRelicOutput(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession published, LegacyPreparedSpellFileWrite writer, RelicWireOutput output)
    {
        if (!relicWireOutputs.TryGetValue(output, out var data) || !ReferenceEquals(data.Context, context) ||
            !ReferenceEquals(data.Writer, writer) || !ReferenceEquals(data.Final, published) ||
            context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) != true)
            throw new LegacyCommittedSpellPublicationException();
        CompleteManagedItemSpellImage(context, expected, published, writer);
        lock (data)
        {
            if (data.ManagedCompleted || data.HandedOff || !RelicWireOwnerCurrent(data) ||
                context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) != true)
                throw new LegacyCommittedSpellPublicationException();
            data.ManagedCompleted = true;
        }
    }

    // One-use transfer from this real registered committed output. The actual
    // Frame lease is a sealed WorldOperationBarrier lease; ownership check
    // prevents disposal of any caller-selected IDisposable.
    internal bool TryHandoffRelicOutput(RelicWireOutput output)
    {
        if (!relicWireOutputs.TryGetValue(output, out var data) || data.Final is not { } final ||
            !_connectionGates.TryGetValue(data.Context.Connection, out var gate)) return false;
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, final);
        lock (gate)
        {
            using var native = EnterNativeConnectionGate();
            using var actors = new SpellActorScope(data.Binding);
            lock (data)
            {
                var operation = data.Context.TradeOperations;
                var lease = data.Context.FrameOperationLease;
                if (!data.ManagedCompleted || data.HandedOff || data.Pending is not null || data.Next != 0 ||
                    operation is null || !operation.OwnsActiveExclusiveLease(lease) ||
                    !RelicWireOwnerCurrent(data) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, final, final)) return false;
                lease!.Dispose();
                if (operation.OwnsActiveExclusiveLease(lease)) throw new LegacyCommittedSpellPublicationException();
                data.HandedOff = true;
                return true;
            }
        }
    }
}
