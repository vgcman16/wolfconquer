using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Historical data only. The actual catalog witness must strictly parse raw46;
// this typed row does not establish adoption, ownership or UID authority.
internal sealed record LegacyRuneBagMetadata(uint ItemId, ushort RawMaximumStackSize)
{
    internal uint EffectiveMaximumStackSize => ItemType.GetLegacyEquipmentPosition(ItemId) is 15 or 16
        ? 1U : Math.Max((uint)RawMaximumStackSize, 1U);

    internal void Validate()
    {
        if (ItemId is 0 or > int.MaxValue)
            throw new InvalidDataException("Invalid recorded Rune bag item metadata.");
    }
}

internal sealed record LegacyRuneBagPlan(PlayerItemSnapshotItem Target, bool IntoBag,
    uint? MergeUid, ushort? MergedStackSize)
{
    internal int NewUidCount => !IntoBag && MergeUid is null ? 1 : 0;
}

public static partial class WorldPacketHandlers
{
    // Pure selection and quantities. No catalog owner, UID issuance, RNG,
    // clock, file write or live publication is supplied by this computation.
    internal static LegacyRuneBagPlan PlanRecordedRuneBag(PlayerItemSnapshot before, uint uid,
        bool intoBag, LegacyRuneBagMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(metadata);
        metadata.Validate();
        var physical = IndexRuneBagPhysicalItems(before);
        if (uid == 0 || !physical.TryGetValue(uid, out var selected) ||
            selected.ItemId != metadata.ItemId || selected.WarehouseId != 0 ||
            before.InventoryItems.Count > 40 || before.RuneItems.Count(row => row.Position == 211) > 100)
            throw InvalidSpellMutation();

        var source = intoBag ? before.InventoryItems : before.RuneItems;
        if (!source.Any(row => ReferenceEquals(row, selected)) || selected.Position != (intoBag ? 0 : 211))
            throw InvalidSpellMutation();
        uint maximum = metadata.EffectiveMaximumStackSize;
        uint quantity = EffectiveRuneBagQuantity(selected, maximum);
        if (intoBag)
        {
            if (before.RuneItems.Count(row => row.Position == 211) >= 100) throw InvalidSpellMutation();
            return new(selected, true, null, null);
        }

        PlayerItemSnapshotItem? receiver = null;
        ushort? merged = null;
        foreach (var row in before.InventoryItems)
        {
            if (row.ItemId != selected.ItemId) continue;
            uint receiverQuantity = EffectiveRuneBagQuantity(row, maximum);
            if (row.Position != 0 || row.WarehouseId != 0 ||
                receiverQuantity + quantity > maximum ||
                receiver is not null && receiver.Uid <= row.Uid ||
                !RuneBagPayloadsCanMerge(selected, row)) continue;
            receiver = row;
            merged = checked((ushort)(receiverQuantity + quantity));
        }
        if (receiver is not null) return new(selected, false, receiver.Uid, merged);
        if (before.InventoryItems.Count >= 40) throw InvalidSpellMutation();
        return new(selected, false, null, null);
    }

    private static uint EffectiveRuneBagQuantity(PlayerItemSnapshotItem row, uint maximum)
    {
        uint quantity = Math.Max((uint)row.StackSize, 1U);
        if (quantity > maximum) throw InvalidSpellMutation();
        return quantity;
    }

    private static bool RuneBagPayloadsCanMerge(PlayerItemSnapshotItem selected, PlayerItemSnapshotItem receiver)
    {
        // No known stack semantics can retain both sets of physical child UIDs.
        // A child-bearing root instead takes the complete fresh-rekey branch.
        if (selected.DepositedItems.Count != 0 || receiver.DepositedItems.Count != 0 ||
            !string.Equals(selected.OwnerName, receiver.OwnerName, StringComparison.Ordinal) ||
            !string.Equals(selected.Signature, receiver.Signature, StringComparison.Ordinal)) return false;
        return RuneBagNormalizedPayload(selected).AsSpan().SequenceEqual(RuneBagNormalizedPayload(receiver));
    }

    private static byte[] RuneBagNormalizedPayload(PlayerItemSnapshotItem row) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(new([row.WithUid(1).WithPosition(0).WithStackSize(1)], [], [], [], []));

    internal static Dictionary<uint, PlayerItemSnapshotItem> IndexRuneBagPhysicalItems(PlayerItemSnapshot before)
    {
        var result = new Dictionary<uint, PlayerItemSnapshotItem>();
        IReadOnlyList<PlayerItemSnapshotItem>[] containers = [before.InventoryItems, before.RelicFuseItems,
            before.MythsoulBagItems, before.EquipmentItems, before.RuneItems, before.WardrobeItems, before.WarehouseItems];
        foreach (var container in containers)
        {
            if (container is null) throw InvalidSpellMutation();
            foreach (var row in container)
            {
                Add(row);
                foreach (var child in row.DepositedItems)
                {
                    Add(child);
                    if (child.DepositedItems.Count != 0) throw InvalidSpellMutation();
                }
            }
        }
        return result;

        void Add(PlayerItemSnapshotItem row)
        {
            if (row is null || row.Uid == 0 || row.ItemId == 0 || row.DepositedItems is null ||
                row.OwnerName is null || row.Signature is null || !result.TryAdd(row.Uid, row))
                throw InvalidSpellMutation();
        }
    }
}
