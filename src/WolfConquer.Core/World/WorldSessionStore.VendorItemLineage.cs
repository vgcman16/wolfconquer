using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class VendorItemLineage
    {
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly SessionHolder holder;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeOwnedItemGraph graph;
        private readonly LegacyOwnedNativeItemImage.Node node;

        private VendorItemLineage(WorldSessionStore store, ConquerTcpConnection connection, SessionHolder holder,
            LegacyOrdinarySpellBinding binding, LegacyOwnedNativeItemImage.Node node)
        { this.store = store; this.connection = connection; this.holder = holder; this.binding = binding; graph = binding.Items; this.node = node; }

        internal static bool TryCapture(WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected,
            LegacyItemSaveGraph.Closure closure, out VendorItemLineage? lineage)
        {
            lineage = null;
            if (!store._connectionGates.TryGetValue(connection, out var gate) || !Monitor.IsEntered(gate) ||
                !IsCurrentThreadInsideAnyConnectionGate || !store._sessions.TryGetValue(connection, out var holder) ||
                !ReferenceEquals(holder.Read(), expected)) return false;
            if (holder.SpellBinding is not { } binding) return store.IsUnboundTradeItemOwnerCurrent(connection, expected);
            if (!Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !store.BindingIsCurrentPlain(binding, holder, expected) || !binding.Items.IsCurrentPlain() ||
                !binding.Items.Image.TryGetCanonicalInventoryNode(closure.Source, out var node)) return false;
            var issued = new VendorItemLineage(store, connection, holder, binding, node);
            if (!issued.MatchesCurrent(store, connection, expected, closure.Source)) return false;
            lineage = issued;
            return true;
        }

        internal bool MatchesCurrent(WorldSessionStore actualStore, ConquerTcpConnection actualConnection,
            WorldSession expected, PlayerItemSnapshotItem row)
        {
            if (!ReferenceEquals(store, actualStore) || !ReferenceEquals(connection, actualConnection) ||
                !store._sessions.TryGetValue(connection, out var actualHolder) || !ReferenceEquals(holder, actualHolder) ||
                !store.BindingIsCurrentPlain(binding, holder, expected) || !ReferenceEquals(graph, binding.Items) ||
                !ReferenceEquals(expected.PlayerItems, graph.Image.FinalSnapshot) ||
                !graph.Image.TryGetCanonicalInventoryNode(row, out var current) || !ReferenceEquals(node, current) ||
                !ReferenceEquals(graph.Inventory.ClientItems.Comparer, EqualityComparer<uint>.Default) ||
                !graph.Inventory.ClientItems.TryGetValue(node.Item.Uid, out var item) || !ReferenceEquals(item, node.Item) ||
                !graph.PhysicalOwnerIsCurrentPlain()) return false;
            if (store._connectionGates.TryGetValue(connection, out var gate) && Monitor.IsEntered(gate) && !graph.IsCurrentPlain()) return false;
            return store.BindingIsCurrentPlain(binding, holder, expected);
        }
    }
}
