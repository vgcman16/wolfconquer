using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneDecomposeProgressRow(uint Family, byte Level, uint Progress);
internal sealed record LegacyRuneDecomposeRewardCap(uint ItemId, ushort MaximumStackSize);
internal sealed record LegacyRuneDecomposePlan(PlayerItemSnapshotItem Target, uint RecycledProgress,
    uint EssenceItemId, ushort[] EssenceStacks, uint FragmentItemId, uint? FragmentStackUid, ushort FragmentStackSize)
{
    internal int NewUidCount => EssenceStacks.Length + (FragmentStackUid is null ? 1 : 0);
}

public static partial class WorldPacketHandlers
{
    // Pure quantities and selection only. No UID, color, owner capability,
    // reservation, file image or publication is issued by this computation.
    internal static LegacyRuneDecomposePlan PlanRecordedRuneDecompose(PlayerItemSnapshot before, uint uid,
        LegacyRuneDecomposeProgressRow progress, IReadOnlyDictionary<uint, LegacyRuneDecomposeRewardCap> caps)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(caps);
        if (uid == 0 || progress is null || before.RuneItems is null || before.InventoryItems is null)
            throw InvalidSpellMutation();
        PlayerItemSnapshotItem? target = null;
        uint bagCount = 0;
        foreach (var row in before.RuneItems)
        {
            if (row is null) throw InvalidSpellMutation();
            if (row.Position == 211) bagCount = checked(bagCount + 1);
            if (row.Uid != uid) continue;
            if (target is not null) throw InvalidSpellMutation();
            target = row;
        }
        if (target is null || target.StackSize > 1 || target.Locked != 0 || target.DepositedItems is null || target.DepositedItems.Count != 0)
            throw InvalidSpellMutation();
        uint family = target.ItemId / 10_000, level = target.ItemId % 100;
        if (family is < 401 or > 403 || level == 0 || level > GetLegacyMaxRuneLevel(target.ItemId) ||
            progress.Family != family || progress.Level != level ||
            target.Position is not (211 or 212) && !IsLegacyRuneEquipSlot(target.ItemId, target.Position))
            throw InvalidSpellMutation();
        ulong total = (ulong)progress.Progress + target.RuneExperience;
        if (total > uint.MaxValue) throw InvalidSpellMutation();
        uint recycled = checked((uint)(total * 4UL / 5UL));
        uint quantity = recycled / 10;
        uint essenceId = family switch { 401 => 4_060_001, 402 => 4_040_001, _ => 4_050_001 };
        uint fragmentId = family switch
        {
            401 => target.Bound == 0 ? 3_306_366U : 3_306_369U,
            402 => target.Bound == 0 ? 3_306_368U : 3_306_371U,
            _ => target.Bound == 0 ? 3_306_367U : 3_306_370U,
        };
        uint essenceCap = RequireCap(essenceId), fragmentCap = RequireCap(fragmentId);
        uint essenceRoots = quantity / essenceCap + (quantity % essenceCap == 0 ? 0U : 1U);
        ulong finalBagCount = (ulong)bagCount - (target.Position == 211 ? 1UL : 0UL) + essenceRoots;
        if (finalBagCount > 100) throw InvalidSpellMutation();

        uint? fragmentUid = null;
        ushort fragmentStack = 10;
        foreach (var row in before.InventoryItems)
        {
            if (row is null || row.Uid == uid) throw InvalidSpellMutation();
            if (fragmentUid is not null) continue;
            if (row.ItemId != fragmentId || row.Bound != 1 || row.Position != 0 || row.WarehouseId != 0) continue;
            uint next = Math.Max((uint)row.StackSize, 1U) + 10;
            if (next > fragmentCap) continue;
            if (row.Uid == 0) throw InvalidSpellMutation();
            fragmentUid = row.Uid; fragmentStack = checked((ushort)next);
        }
        if (fragmentUid is null && fragmentCap < 10 ||
            (ulong)before.InventoryItems.Count + (fragmentUid is null ? 1UL : 0UL) > 40)
            throw InvalidSpellMutation();

        // Capacity bounded the root count before allocating. Even a source-ABI
        // maximum total and effective cap1 cannot allocate an enormous array.
        var stacks = new ushort[checked((int)essenceRoots)];
        uint remaining = quantity;
        for (int index = 0; index < stacks.Length; index++)
        {
            stacks[index] = checked((ushort)Math.Min(remaining, essenceCap));
            remaining -= stacks[index];
        }
        return new(target, recycled, essenceId, stacks, fragmentId, fragmentUid, fragmentStack);

        uint RequireCap(uint id)
        {
            if (!caps.TryGetValue(id, out var cap) || cap is null || cap.ItemId != id) throw InvalidSpellMutation();
            return Math.Max((uint)cap.MaximumStackSize, 1U);
        }
    }
}
