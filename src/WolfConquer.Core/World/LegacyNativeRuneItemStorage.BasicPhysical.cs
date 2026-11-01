namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    internal readonly record struct BasicPhysicalRuneRow(uint Uid, uint ItemId, ushort Position);

    // A failed capture is NOT an empty worn set or permission to attack.
    internal sealed partial class BasicPhysicalWornSnapshot
    {
        private readonly LegacyNativeRuneItemStorage storage;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession expected;
        private readonly EquippedContext context;
        private readonly BasicPhysicalRuneRow[] rows;
        internal uint Class { get; }
        internal ushort PlayerLevel { get; }
        internal byte Reborn { get; }
        internal BasicPhysicalRuneRow[] Rows => rows.ToArray();

        private BasicPhysicalWornSnapshot(LegacyNativeRuneItemStorage storage, LegacyOrdinarySpellBinding binding,
            WorldSession expected, EquippedContext context, LegacyNativeGameItemRuntime[] selected)
        {
            this.storage = storage; this.binding = binding; this.expected = expected; this.context = context;
            Class = binding.Player.Class; PlayerLevel = binding.Player.Level; Reborn = binding.Player.Reborn;
            rows = selected.Select(row => new BasicPhysicalRuneRow(row.Uid, row.ITEM_ID, row.Position)).ToArray();
        }

        internal static bool TryCapture(LegacyNativeRuneItemStorage storage, LegacyOrdinarySpellBinding binding,
            WorldSession expected, out BasicPhysicalWornSnapshot? snapshot)
        {
            snapshot = null;
            if (!OwnerIsCurrentPlain(storage, binding, expected) || InvertedStorageOrder(binding.Items)) return false;
            var graph = binding.Items;
            lock (graph.Warehouse)
            lock (storage.StorageSyncRoot)
            lock (graph.Relic.SyncRoot)
            lock (graph.Gear.SyncRoot)
            {
                if (!OwnerIsCurrentPlain(storage, binding, expected) || !graph.IsCurrentPlain() || !PlainStorage(storage) ||
                    !storage.TryCaptureEquipped(out var context, out var all)) return false;
                var captured = new BasicPhysicalWornSnapshot(storage, binding, expected, context, SelectWorn(all, context.Alternate));
                if (!captured.CurrentUnderStorageGates()) return false;
                snapshot = captured; return true;
            }
        }

        internal bool IsCurrentPlain()
        {
            if (!OwnerIsCurrentPlain(storage, binding, expected) || InvertedStorageOrder(binding.Items)) return false;
            var graph = binding.Items;
            lock (graph.Warehouse)
            lock (storage.StorageSyncRoot)
            lock (graph.Relic.SyncRoot)
            lock (graph.Gear.SyncRoot)
                return CurrentUnderStorageGates();
        }

        private bool CurrentUnderStorageGates() => CurrentUnderStorageGates(expected);

        private bool CurrentUnderStorageGates(WorldSession current) => OwnerIsCurrentPlain(storage, binding, current) &&
            binding.Items.IsCurrentPlain() && PlainStorage(storage) && storage.IsCurrent(context) &&
            binding.Player.Class == Class && binding.Player.Level == PlayerLevel && binding.Player.Reborn == Reborn;

        private static bool OwnerIsCurrentPlain(LegacyNativeRuneItemStorage storage, LegacyOrdinarySpellBinding binding,
            WorldSession expected) => WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate && !binding.IsRetired &&
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
            ReferenceEquals(binding.Items.Rune, storage) && ReferenceEquals(binding.Client.Rune, storage) &&
            ReferenceEquals(binding.Client.Player, binding.Player) && ReferenceEquals(binding.Player.Owner, binding.Client) &&
            ReferenceEquals(expected.NativeGameClient, binding.Client) && expected.AccountId == binding.AccountId &&
            expected.Character.Uid == binding.Uid && expected.Character.Class == binding.Player.Class &&
            expected.Character.Level == binding.Player.Level && expected.Character.Reborn == binding.Player.Reborn &&
            binding.Client.Equipment?.Alternante == expected.AlternateEquipmentActive &&
            binding.Store.TryGet(binding.Connection, out var actual) && ReferenceEquals(actual, expected) &&
            binding.Store.IsBoundSpellOwnerCurrent(binding);

        private static bool PlainStorage(LegacyNativeRuneItemStorage storage) =>
            storage.items is not null && storage.items.GetType() == typeof(Dictionary<uint, LegacyNativeGameItemRuntime>) &&
            ReferenceEquals(storage.items.Comparer, EqualityComparer<uint>.Default) && storage.objects is not null &&
            storage.objects.GetType() == typeof(LegacyNativeGameItemRuntime[]);

        private static bool InvertedStorageOrder(LegacyNativeOwnedItemGraph graph) =>
            Monitor.IsEntered(graph.Rune.StorageSyncRoot) && !Monitor.IsEntered(graph.Warehouse) ||
            Monitor.IsEntered(graph.Relic.SyncRoot) && !Monitor.IsEntered(graph.Rune.StorageSyncRoot) ||
            Monitor.IsEntered(graph.Gear.SyncRoot) && !Monitor.IsEntered(graph.Relic.SyncRoot);
    }
}
