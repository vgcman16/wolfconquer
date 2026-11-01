namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class DerivedSpellQueryPublication
    {
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly WorldServerFrameHandler frame;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyPreparedDerivedSpellQueryWrite writer;
        private readonly LegacyNativeSpellStorage.PreparedSessionSpellMap graph;
        private readonly byte[] response;
        internal WorldSession Published { get; }
        internal DerivedSpellQueryPublication(WorldSessionStore store, WorldPacketHandlerContext context, WorldServerFrameHandler frame,
            LegacyOrdinarySpellBinding binding, LegacyPreparedDerivedSpellQueryWrite writer, WorldSession published,
            LegacyNativeSpellStorage.PreparedSessionSpellMap graph)
        {
            this.store = store; this.context = context; this.frame = frame; this.binding = binding; this.writer = writer;
            Published = published; this.graph = graph; response = writer.CopyResponse();
        }
        private bool IsCurrent()
        {
            lock (frame.RequireActionLifecycleGate(store))
            {
                using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, Published);
                if (!store._connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
                lock (gate)
                {
                    using var native = EnterNativeConnectionGate();
                    using var actors = new SpellActorScope(binding);
                    return frame.IsActionLifecycleCurrent(store, context) && writer.IsPublishedCurrentPlain(Published) &&
                        graph.HasCurrentCommittedGraph() && writer.IsPublishedCurrentPlain(Published);
                }
            }
        }
        internal async ValueTask<bool> SendAsync(CancellationToken cancellationToken)
        {
            if (!IsCurrent()) return false;
            await binding.Connection.SendAsync(response, cancellationToken).ConfigureAwait(false);
            return IsCurrent();
        }
    }
}
