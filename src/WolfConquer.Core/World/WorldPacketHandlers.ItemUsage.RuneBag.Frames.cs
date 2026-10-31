namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ItemSpellProjection PrepareOwnedRuneBagProjection(WorldSession stable,
        LegacyRuneBagEvidence evidence, byte[] echo)
    {
        var operation = evidence.Operation;
        var recipe = LegacyRuneBagRecipe.Prepare(stable.PlayerItems, operation.SelectedUid,
            operation.IntoBag, operation.Metadata);
        var plan = recipe.Plan;
        var projected = recipe.Project(plan.NewUidCount == 1 ? operation.Allocation?.First : null);
        if (!evidence.IsCurrent()) throw InvalidSpellMutation();
        var output = new List<byte[]>();
        if (!plan.IntoBag || !IsLegacyRuneItemId(plan.Target.ItemId))
            output.Add(new MsgItemUsagePacket(plan.Target.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes());
        output.Add(LegacyGameItemPacket.FromSnapshotItem(projected.ResultItem, evidence.Wire.RuneOverride,
            plan.MergeUid is not null ? LegacyGameItemPacket.UpdateItemMode : LegacyGameItemPacket.AddItemMode).ToFrameBytes());
        output.Add(echo.ToArray());
        // Neither direction mutates Character/Spells or performs a HP query in
        // its transaction. The actual incoming query/rank follows the echo.
        return new(stable with { PlayerItems = projected.Items }, output.ToArray(),
            Refresh: null, UpdateRank: false, Bag: evidence);
    }
}
