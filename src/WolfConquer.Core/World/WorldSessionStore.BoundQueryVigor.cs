namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryRecordBoundQueryVigor(WorldPacketHandlerContext context, WorldSession expected,
        LegacyOrdinarySpellBinding binding, WorldPacketHandlers.BoundQueryVigorPurpose purpose,
        LegacyNativeMagicQueryClient.QueryEpoch? epoch, LegacyNativeMagicQueryClient.CompletedMaximums completed)
    {
        if (!ReferenceEquals(context.Sessions, this) || !ReferenceEquals(context.Connection, binding.Connection) ||
            !_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
        lock (gate)
        {
            using var native = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                !ReferenceEquals(holder.SpellBinding, binding) || !BindingIsCurrentPlain(binding, holder, expected) ||
                !WorldPacketHandlers.ConsumeBoundQueryVigor(purpose, expected, binding, epoch, completed, out var metadata) ||
                metadata is null) return false;
            binding.RecordBoundQueryVigor(expected, checked((ushort)LegacyEquipmentQueryBounds.ReadVigor(expected.Character)), completed, metadata);
            return true;
        }
    }
}
