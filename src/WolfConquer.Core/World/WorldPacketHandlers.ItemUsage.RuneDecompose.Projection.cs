using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneDecomposeProjection(CharacterSnapshot Character, PlayerItemSnapshot Items,
    PlayerSpellSnapshot Spells, IReadOnlyList<ushort> RemovedSpells, IReadOnlyList<PlayerItemSnapshotItem> Essences,
    PlayerItemSnapshotItem Fragment, ushort FragmentMode);

public static partial class WorldPacketHandlers
{
    internal static LegacyRuneDecomposeProjection ProjectRecordedRuneDecompose(CharacterSnapshot character,
        PlayerItemSnapshot before, PlayerSpellSnapshot spells, LegacyRuneDecomposeOperation operation)
    {
        operation.Validate();
        if (character.Uid != operation.Request.CharacterUid) throw InvalidSpellMutation();
        var caps = operation.RewardMetadata.ToDictionary(row => row.ItemId,
            row => new LegacyRuneDecomposeRewardCap(row.ItemId, row.MaximumStackSize));
        var plan = PlanRecordedRuneDecompose(before, operation.SelectedUid, operation.Progress, caps);
        RequireRuneDecomposePlacement(before);
        // All37 current adopted cumulative rows are >=100 and yield >=8
        // essence. Do not fabricate a positive zero-output producer variant.
        if (plan.EssenceStacks.Length == 0 || plan.NewUidCount != operation.Request.Count ||
            !operation.RewardMetadata.Select(row => row.ItemId).SequenceEqual(new[] { plan.EssenceItemId, plan.FragmentItemId }.Order()) ||
            (plan.FragmentStackUid is null) != (operation.FragmentColor is not null)) throw InvalidSpellMutation();
        bool stored = plan.Target.Position is 211 or 212;
        uint[] required = stored ? [] : LegacyRuneOperation.RequiredMetadata(before, operation.SelectedUid);
        if (!operation.RuneMetadata.Select(row => row.ItemId).SequenceEqual(required)) throw InvalidSpellMutation();

        ulong next = operation.Allocation.First;
        var essences = new List<PlayerItemSnapshotItem>();
        foreach (var stack in plan.EssenceStacks)
            essences.Add(new() { Uid = checked((uint)next++), ItemId = plan.EssenceItemId, StackSize = stack,
                Position = 211, Durability = 1, MaximumDurability = 1 });
        var inventory = before.InventoryItems.ToArray();
        PlayerItemSnapshotItem fragment;
        ushort fragmentMode;
        if (plan.FragmentStackUid is { } uid)
        {
            int index = Array.FindIndex(inventory, row => row.Uid == uid);
            if (index < 0) throw InvalidSpellMutation();
            fragment = inventory[index].WithStackSize(plan.FragmentStackSize); inventory[index] = fragment;
            fragmentMode = LegacyGameItemPacket.UpdateItemMode;
        }
        else
        {
            var descriptor = operation.RewardMetadata.Single(row => row.ItemId == plan.FragmentItemId);
            fragment = new() { Uid = checked((uint)next++), ItemId = plan.FragmentItemId, StackSize = plan.FragmentStackSize,
                Position = 0, Bound = 1, Color = operation.FragmentColor!.Value,
                Durability = descriptor.Durability!.Value, MaximumDurability = descriptor.Durability.Value };
            inventory = inventory.Append(fragment).ToArray(); fragmentMode = LegacyGameItemPacket.AddItemMode;
        }
        if (next != (ulong)operation.Allocation.First + (uint)operation.Allocation.Count) throw InvalidSpellMutation();
        // Keep the physical bucket order used by admission and saved-image
        // parsing. A surviving212 row must follow the new211 rewards. Stable
        // ordering within211 preserves existing bag insertion order.
        var runes = before.RuneItems.Where(row => row.Uid != plan.Target.Uid).Concat(essences)
            .OrderBy(row => row.Position).ThenBy(row => row.Position == 211 ? 0U : row.Uid).ToArray();
        var items = before with { InventoryItems = inventory, RuneItems = runes };
        var spellState = ProjectRuneDecomposeSpells(character, before, items, spells, plan.Target, operation.RuneMetadata);
        return new(spellState.Character, items, spellState.Spells,
            spellState.Removed, essences.ToArray(), fragment, fragmentMode);
    }

    internal static void RequireRuneDecomposePlacement(PlayerItemSnapshot before)
    {
        foreach (var row in before.RuneItems)
            if (row is null || row.Position is not (211 or 212) && !IsLegacyRuneEquipSlot(row.ItemId, row.Position))
                throw InvalidSpellMutation();
    }
}
