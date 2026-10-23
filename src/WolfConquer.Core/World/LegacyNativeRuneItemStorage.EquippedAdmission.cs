using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    private readonly record struct EquippedContext(
        LegacyNativeGameClientRuntime Client, LegacyNativePlayerRuntime Player,
        LegacyNativeEquipItemStorage Equipment, uint Uid, bool Alternate);

    // Query admission, not a transaction against unrelated raw public-field writers.
    private bool TryCaptureEquipped(out EquippedContext context, out LegacyNativeGameItemRuntime[] snapshot)
    {
        context = default;
        snapshot = [];
        var client = Owner;
        var player = client?.Player;
        var equipment = client?.Equipment;
        if (client is null || !ReferenceEquals(constructorOwner, client) ||
            !ReferenceEquals(client.Rune, this) || player is null ||
            !ReferenceEquals(player.Owner, client) || equipment is null || !equipment.IsOwnedBy(client))
            return false;
        context = new(client, player, equipment, player.UID, equipment.Alternante);
        var cache = objects;
        var authoritative = items;
        if (cache is null || authoritative is null) return false;
        snapshot = cache.ToArray();
        // Unique cache identities + exact dictionary aliases + equal cardinality
        // prove a complete physical view, not a valid-looking truncated prefix.
        if (authoritative.Count != snapshot.Length) return false;
        var seen = new HashSet<uint>();
        var activeSlots = new HashSet<ushort>();
        foreach (var row in snapshot)
        {
            if (row is null || row.Uid == 0 || !seen.Add(row.Uid) ||
                !authoritative.TryGetValue(row.Uid, out var owned) || !ReferenceEquals(owned, row))
                return false;
            // Native Free/Equip require one physical owner per equipped position.
            // Inactive inventory/bag/storage entries legitimately share0/211/212.
            if (row.Position is not 0 and not 211 and not 212 && !activeSlots.Add(row.Position))
                return false;
        }
        return ReferenceEquals(objects, cache) && ReferenceEquals(items, authoritative) &&
            authoritative.Count == snapshot.Length && IsCurrent(context);
    }

    private bool IsCurrent(EquippedContext context) =>
        ReferenceEquals(Owner, context.Client) && ReferenceEquals(constructorOwner, context.Client) &&
        ReferenceEquals(context.Client.Rune, this) && ReferenceEquals(context.Client.Player, context.Player) &&
        ReferenceEquals(context.Player.Owner, context.Client) && context.Player.UID == context.Uid &&
        ReferenceEquals(context.Client.Equipment, context.Equipment) && context.Equipment.IsOwnedBy(context.Client) &&
        context.Equipment.Alternante == context.Alternate;

    private static LegacyNativeGameItemRuntime[] SelectWorn(
        LegacyNativeGameItemRuntime[] snapshot, bool alternate)
    {
        var worn = new List<LegacyNativeGameItemRuntime>();
        foreach (var row in snapshot)
        {
            ushort position = row.Position;
            if (position != 211 && position != 0 && position != 212 &&
                (!alternate || position >= 120 || !snapshot.Any(candidate => candidate.Position == position + 20)) &&
                (alternate || position <= 120))
                worn.Add(row);
        }
        return worn.ToArray();
    }
}
