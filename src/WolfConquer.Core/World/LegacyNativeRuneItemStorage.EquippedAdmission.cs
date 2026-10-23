namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    private readonly record struct EquippedContext(
        LegacyNativeGameClientRuntime Client, LegacyNativePlayerRuntime Player,
        LegacyNativeEquipItemStorage Equipment, uint Uid, bool Alternate, StorageSnapshot Storage);

    // Caller holds StorageSyncRoot through selection/publication of its query result.
    private bool TryCaptureEquipped(out EquippedContext context, out LegacyNativeGameItemRuntime[] snapshot)
    {
        context = default;
        snapshot = [];
        if (!TryResolveEquippedContext(out context) || !TryCaptureStorage(out var captured)) return false;
        context = context with { Storage = captured };
        var activeSlots = new HashSet<ushort>();
        foreach (var row in captured.Rows)
            if (row.Position is not (0 or 211 or 212) && !activeSlots.Add(row.Position)) return false;
        snapshot = captured.Rows.Select(row => row.Item).ToArray();
        return IsCurrent(context);
    }

    private bool TryResolveEquippedContext(out EquippedContext context)
    {
        context = default;
        var client = Owner;
        var player = client?.Player;
        var equipment = client?.Equipment;
        if (client is null || !ReferenceEquals(constructorOwner, client) ||
            !ReferenceEquals(client.Rune, this) || player is null ||
            !ReferenceEquals(player.Owner, client) || equipment is null || !equipment.IsOwnedBy(client)) return false;
        context = new(client, player, equipment, player.UID, equipment.Alternante, null!);
        return true;
    }

    private bool IsCurrent(EquippedContext context) =>
        ReferenceEquals(Owner, context.Client) && ReferenceEquals(constructorOwner, context.Client) &&
        ReferenceEquals(context.Client.Rune, this) && ReferenceEquals(context.Client.Player, context.Player) &&
        ReferenceEquals(context.Player.Owner, context.Client) && context.Player.UID == context.Uid &&
        ReferenceEquals(context.Client.Equipment, context.Equipment) && context.Equipment.IsOwnedBy(context.Client) &&
        context.Equipment.Alternante == context.Alternate && context.Storage is not null &&
        IsCurrentStorage(context.Storage);

    private static LegacyNativeGameItemRuntime[] SelectWorn(
        LegacyNativeGameItemRuntime[] snapshot, bool alternate)
    {
        var worn = new List<LegacyNativeGameItemRuntime>();
        foreach (var row in snapshot)
        {
            ushort position = row.Position;
            if (position != 211 && position != 0 && position != 212 &&
                (!alternate || position >= 120 || !snapshot.Any(candidate => candidate.Position == position + 20)) &&
                (alternate || position <= 120)) worn.Add(row);
        }
        return worn.ToArray();
    }
}
