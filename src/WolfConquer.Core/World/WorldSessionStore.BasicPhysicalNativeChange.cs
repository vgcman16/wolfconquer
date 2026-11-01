using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class BasicPhysicalNativeChange
    {
        private readonly WorldSessionStore owner;
        // This empty token is inert until the actual Store's private table
        // registers an issued native proposal for it.
        internal BasicPhysicalNativeChange(WorldSessionStore owner) => this.owner = owner;
        internal bool TryReadPreparedProposal(LegacyOrdinarySpellBinding binding,
            out LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? before,
            out LegacyNativeGameClientRuntime.BasicPhysicalNativeKind kind, out MsgAttackQuery? request, out DateTime? stamp)
        {
            before = null; kind = default; request = null; stamp = null;
            if (!owner.basicPhysicalChanges.TryGetValue(this, out var data) || !ReferenceEquals(data.Binding, binding)) return false;
            before = data.Before; kind = data.Kind; request = data.Request; stamp = data.Stamp;
            return data.IsCurrentPlain();
        }
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding) =>
            owner.basicPhysicalChanges.TryGetValue(this, out var data) && ReferenceEquals(data.Binding, binding) &&
            data.Retirement is null && data.IsCurrentPlain();
        internal bool IsRetirementCancellationPlain(LegacyOrdinarySpellBinding binding) =>
            owner.basicPhysicalChanges.TryGetValue(this, out var data) && ReferenceEquals(data.Binding, binding) &&
            data.Retirement is not null && data.IsCurrentPlain();
    }

    private sealed class BasicPhysicalChangeData(WorldSessionStore store, WorldServerFrameHandler frame,
        LegacyOrdinarySpellBinding binding, WorldSession expected,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot before, long generation,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeKind kind, MsgAttackQuery? request, DateTime? stamp,
        WorldPacketHandlerContext? context, BasicPhysicalRetirement? retirement)
    {
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal readonly LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot Before = before;
        internal readonly LegacyNativeGameClientRuntime.BasicPhysicalNativeKind Kind = kind;
        internal readonly MsgAttackQuery? Request = request;
        internal readonly DateTime? Stamp = stamp;
        internal readonly BasicPhysicalRetirement? Retirement = retirement;
        internal bool IsCurrentPlain() => ReferenceEquals(store.framePersistenceOwner, frame) &&
            (Retirement is not null ? store.IsActualBasicPhysicalRetirement(Retirement, binding, expected, before) :
                context is not null && frame.IsActionLifecycleCurrent(store, context) &&
                store._sessions.TryGetValue(binding.Connection, out var holder) && ReferenceEquals(holder.Read(), expected) &&
                store.BindingIsCurrentPlain(binding, holder, expected) &&
                frame.IsBasicPhysicalNativeSnapshotCurrent(binding, before, generation));
    }
    private readonly ConditionalWeakTable<BasicPhysicalNativeChange, BasicPhysicalChangeData> basicPhysicalChanges = new();

    private BasicPhysicalNativeChange IssueBasicPhysicalChange(WorldServerFrameHandler frame,
        LegacyOrdinarySpellBinding binding, WorldSession expected,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot before, long generation,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeKind kind, MsgAttackQuery? request, DateTime? stamp,
        WorldPacketHandlerContext? context, BasicPhysicalRetirement? retirement = null)
    {
        var change = new BasicPhysicalNativeChange(this);
        basicPhysicalChanges.Add(change, new(this, frame, binding, expected, before, generation, kind, request, stamp, context, retirement));
        return change;
    }
}
