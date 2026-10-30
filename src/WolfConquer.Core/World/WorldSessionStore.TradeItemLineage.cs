using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCaptureTradeItemLineage(ConquerTcpConnection connection, WorldSession expected,
        LegacyItemSaveGraph.Closure closure, out TradeItemLineage? lineage) =>
        TradeItemLineage.TryCapture(this, connection, expected, closure, out lineage);

    internal bool IsUnboundTradeItemOwnerCurrent(ConquerTcpConnection connection, WorldSession expected) =>
        _sessions.TryGetValue(connection, out var holder) && holder.SpellBinding is null &&
        ReferenceEquals(holder.Read(), expected) && connection.NativeGameClient?.OrdinarySpellBinding is null &&
        expected.NativeGameClient?.OrdinarySpellBinding is null &&
        connection.NativeGameClient?.Player?.HasOwnedVitalsAttachment != true &&
        expected.NativeGameClient?.Player?.HasOwnedVitalsAttachment != true;

    // Issued only inside the real offer pair commit, never from UID/value data.
    internal sealed class TradeItemLineage
    {
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly SessionHolder holder;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeOwnedItemGraph graph;
        private readonly LegacyOwnedNativeItemImage.Node node;

        private TradeItemLineage(WorldSessionStore store, ConquerTcpConnection connection,
            SessionHolder holder, LegacyOrdinarySpellBinding binding, LegacyOwnedNativeItemImage.Node node)
        {
            this.store = store; this.connection = connection; this.holder = holder;
            this.binding = binding; graph = binding.Items; this.node = node;
        }

        internal static bool TryCapture(WorldSessionStore store, ConquerTcpConnection connection,
            WorldSession expected, LegacyItemSaveGraph.Closure closure, out TradeItemLineage? lineage)
        {
            lineage = null;
            if (!store._connectionGates.TryGetValue(connection, out var gate) || !Monitor.IsEntered(gate) ||
                !IsCurrentThreadInsideAnyConnectionGate ||
                !store._sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected))
                return false;
            if (holder.SpellBinding is not { } binding)
            {
                // Same genuinely unbound compatibility as central publication.
                // A retired/foreign physical attachment is never this fallback.
                return store.IsUnboundTradeItemOwnerCurrent(connection, expected);
            }
            if (!Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
                !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !store.BindingIsCurrentPlain(binding, holder, expected) || !binding.Items.IsCurrentPlain() ||
                !binding.Items.Image.TryGetCanonicalInventoryNode(closure.Source, out var node)) return false;
            var prepared = new TradeItemLineage(store, connection, holder, binding, node);
            if (!prepared.MatchesCurrent(store, connection, expected, closure.Source)) return false;
            lineage = prepared;
            return true;
        }

        // Early confirmation is a bounded pure check. The same check is repeated
        // by the real file-lock validator under the complete pair publication
        // gates before WAL; it is not a new independent mutation capability.
        internal bool MatchesCurrent(WorldSessionStore actualStore, ConquerTcpConnection actualConnection,
            WorldSession expected, PlayerItemSnapshotItem canonicalRow)
        {
            if (!ReferenceEquals(store, actualStore) || !ReferenceEquals(connection, actualConnection) ||
                !store._sessions.TryGetValue(connection, out var actualHolder) || !ReferenceEquals(actualHolder, holder) ||
                !store.BindingIsCurrentPlain(binding, holder, expected) || !ReferenceEquals(binding.Items, graph) ||
                !ReferenceEquals(expected.PlayerItems, graph.Image.FinalSnapshot) ||
                !graph.Image.TryGetCanonicalInventoryNode(canonicalRow, out var current) || !ReferenceEquals(current, node) ||
                !ReferenceEquals(graph.Inventory.ClientItems.Comparer, EqualityComparer<uint>.Default) ||
                !graph.Inventory.ClientItems.TryGetValue(node.Item.Uid, out var actualItem) || !ReferenceEquals(actualItem, node.Item))
                return false;
            // A real pair callback owns all graph gates. Recheck complete live
            // membership there, after callback-bearing trade policy lookups.
            if (store._connectionGates.TryGetValue(connection, out var gate) && Monitor.IsEntered(gate) &&
                !graph.IsCurrentPlain()) return false;
            return store.BindingIsCurrentPlain(binding, holder, expected);
        }
    }
}
