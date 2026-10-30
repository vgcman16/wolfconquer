using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeOwnedItemGraph
{
    internal TrainingWeaponsLease CaptureTrainingWeapons(
        WorldSessionStore.NativeSpellMutationLease issuer, LegacyOrdinarySpellBinding binding) =>
        new(this, issuer, binding);

    // A projection of the actual published graph, not a combat-cache builder.
    // The closed Store lease is its issuer and remains held for this lifetime.
    internal sealed class TrainingWeaponsLease : IDisposable
    {
        private readonly LegacyNativeOwnedItemGraph graph;
        private readonly WorldSessionStore.NativeSpellMutationLease issuer;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession expected;
        private readonly LegacyOwnedNativeItemImage image;
        private readonly object[] actors;
        private readonly int thread = Environment.CurrentManagedThreadId;
        private readonly Weapon? right, left;
        private StorageLease? storage;
        private int held;
        private bool disposed;
        internal uint RightWeaponId => right?.ItemId ?? 0;
        internal uint LeftWeaponId => left?.ItemId ?? 0;

        internal TrainingWeaponsLease(
            LegacyNativeOwnedItemGraph graph,
            WorldSessionStore.NativeSpellMutationLease issuer,
            LegacyOrdinarySpellBinding binding)
        {
            this.graph = graph;
            this.issuer = issuer;
            this.binding = binding;
            expected = issuer.ExpectedSession;
            image = graph.image;
            if (!issuer.HasTrainingOwnerPlain(binding, graph) ||
                Monitor.IsEntered(graph.Client.SpellAdmissionSyncRoot) ||
                Monitor.IsEntered(graph.Player.SpellAdmissionSyncRoot) ||
                Monitor.IsEntered(binding.Storage.MutationGate) ||
                Monitor.IsEntered(binding.Proficiencies.MutationGate))
                throw Invalid();
            actors = new[]
            {
                graph.Client.SpellAdmissionSyncRoot,
                graph.Player.SpellAdmissionSyncRoot,
                binding.Storage.MutationGate,
                binding.Proficiencies.MutationGate
            };
            try
            {
                for (; held < actors.Length; held++)
                    Monitor.Enter(actors[held]);
                storage = new StorageLease(graph);
                if (!OwnerCurrent())
                    throw Invalid();
                ushort rightPosition = expected.AlternateEquipmentActive ? (ushort)54 : (ushort)4;
                ushort leftPosition = expected.AlternateEquipmentActive ? (ushort)55 : (ushort)5;
                right = CaptureWeapon(rightPosition);
                left = CaptureWeapon(leftPosition);
                if (!IsCurrentPlain())
                    throw Invalid();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private Weapon? CaptureWeapon(ushort position)
        {
            PlayerItemSnapshotItem? selected = null;
            foreach (var row in expected.PlayerItems.EquipmentItems)
            {
                if (row is null)
                    throw Invalid();
                if (row.Position != position)
                    continue;
                if (selected is not null)
                    throw Invalid();
                selected = row;
            }
            if (selected is null)
                return null;
            if (!graph.Equipment.ClientItems.TryGetValue(selected.Uid, out var item) ||
                item is null || item.Uid != selected.Uid || item.ITEM_ID != selected.ItemId ||
                item.Position != position)
                throw Invalid();
            return new(selected, item, selected.Uid, selected.ItemId, position);
        }

        private bool OwnerCurrent() => !disposed && Environment.CurrentManagedThreadId == thread &&
            held == actors.Length && issuer.HasTrainingOwnerPlain(binding, graph) &&
            ReferenceEquals(graph.image, image) && ReferenceEquals(expected.PlayerItems, image.FinalSnapshot) &&
            graph.IsCurrentPlain();

        internal bool IsCurrentPlain() => OwnerCurrent() &&
            (right is null || right.IsCurrent(graph.Equipment)) &&
            (left is null || left.IsCurrent(graph.Equipment));

        internal bool MatchesCurrent(
            LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player,
            LegacyNativeEquipItemStorage equipment) =>
            ReferenceEquals(graph.Client, client) && ReferenceEquals(graph.Player, player) &&
            ReferenceEquals(graph.Equipment, equipment) && IsCurrentPlain();

        public void Dispose()
        {
            if (disposed)
                return;
            if (Environment.CurrentManagedThreadId != thread)
                throw Invalid();
            disposed = true;
            try { storage?.Dispose(); }
            finally
            {
                while (held > 0)
                    Monitor.Exit(actors[--held]);
            }
        }

        private sealed record Weapon(
            PlayerItemSnapshotItem Source,
            LegacyNativeGameItemRuntime Item,
            uint Uid,
            uint ItemId,
            ushort Position)
        {
            internal bool IsCurrent(LegacyNativeEquipItemStorage equipment) =>
                Source.Uid == Uid && Source.ItemId == ItemId && Source.Position == Position &&
                Item.Uid == Uid && Item.ITEM_ID == ItemId && Item.Position == Position &&
                equipment.ClientItems.TryGetValue(Uid, out var actual) && ReferenceEquals(actual, Item);
        }
    }
}
