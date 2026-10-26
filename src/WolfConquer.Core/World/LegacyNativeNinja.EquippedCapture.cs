using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    internal EquippedSigilCapture CaptureEquippedSigil(LegacyNativeGameClientRuntime client, ItemType type) =>
        EquippedSigilCapture.Create(this, client, unchecked((uint)type));

    // A valid capture can explicitly represent absence. Invalid required state
    // throws before a consumer can interpret it as an unequipped sigil.
    internal sealed class EquippedSigilCapture
    {
        private readonly LegacyNativeNinja ninja;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint job;
        private readonly LegacyNativeEquipItemStorage? equipment;
        private readonly bool alternate;
        private readonly ConcurrentDictionary<uint, Item>? items;
        private readonly EquippedItemRow[]? rows;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? table;
        private readonly EquippedMetadataRow[]? metadata;

        internal bool Found { get; }
        internal int Power { get; }
        internal uint Damage { get; }

        private EquippedSigilCapture(LegacyNativeNinja ninja, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, uint job, LegacyNativeEquipItemStorage? equipment,
            bool alternate, ConcurrentDictionary<uint, Item>? items, EquippedItemRow[]? rows,
            Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? table, EquippedMetadataRow[]? metadata,
            bool found, int power, uint damage)
        {
            this.ninja = ninja; this.client = client; this.player = player; this.job = job;
            this.equipment = equipment; this.alternate = alternate; this.items = items; this.rows = rows;
            this.table = table; this.metadata = metadata;
            Found = found; Power = power; Damage = damage;
        }

        internal static EquippedSigilCapture Create(LegacyNativeNinja ninja,
            LegacyNativeGameClientRuntime client, uint itemId)
        {
            ArgumentNullException.ThrowIfNull(client);
            try
            {
                var player = client.Player;
                if (player is null || !ReferenceEquals(player.Owner, client) || !ninja.IsCombatOwnedBy(client))
                    throw UnavailableEquippedSigil();
                var job = player.Class;
                if (!LegacyNativeAtributesStatus.IsNinja(job))
                    return RequireCurrent(new(ninja, client, player, job, null, false, null, null, null, null, false, 0, 0));

                var equipment = client.Equipment;
                var items = ninja.Items;
                if (equipment is null || !equipment.IsOwnedBy(client) || items is null)
                    throw UnavailableEquippedSigil();
                var alternate = equipment.Alternante;
                var rows = CaptureEquippedItems(items);
                var selected = SelectEquippedItem(rows, itemId, alternate);
                Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? table = null;
                EquippedMetadataRow[]? metadata = null;
                var power = 0;
                uint damage = 0;
                if (selected is { } item)
                {
                    table = LegacyNativeNinjaFile.gouyu_type;
                    if (table is null) throw UnavailableEquippedSigil();
                    metadata = CaptureEquippedMetadata(table);
                    var match = metadata.FirstOrDefault(row => row.ItemId == item.ItemId && row.Level == item.Level);
                    if (match.Row is null) throw UnavailableEquippedSigil();
                    power = match.Power;
                    damage = match.Damage;
                }

                return RequireCurrent(new(ninja, client, player, job, equipment, alternate,
                    items, rows, table, metadata, selected.HasValue, power, damage));
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            {
                throw new InvalidDataException("Equipped sigil state changed during capture.", error);
            }
        }

        internal bool IsCurrent()
        {
            try
            {
                if (!ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) ||
                    player.Class != job || !ninja.IsCombatOwnedBy(client)) return false;
                if (!LegacyNativeAtributesStatus.IsNinja(job)) return true;
                return equipment is not null && ReferenceEquals(client.Equipment, equipment) &&
                    equipment.IsOwnedBy(client) && equipment.Alternante == alternate &&
                    items is not null && rows is not null && ReferenceEquals(ninja.Items, items) &&
                    EquippedItemsCurrent(items, rows) &&
                    (metadata is null || table is not null && ReferenceEquals(LegacyNativeNinjaFile.gouyu_type, table) &&
                        EquippedMetadataCurrent(table, metadata));
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            {
                return false;
            }
        }

        private static EquippedSigilCapture RequireCurrent(EquippedSigilCapture capture)
        {
            if (!capture.IsCurrent()) throw UnavailableEquippedSigil();
            return capture;
        }
    }

    private static InvalidDataException UnavailableEquippedSigil() =>
        new("Equipped sigil requires current owned item state and real matching metadata.");
}
