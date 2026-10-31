using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneDecomposeEvidence
{
    private void PreflightDerivableImages(LegacyRuneDecomposePlan plan)
    {
        // Prove all existing-root changes without fabricating an identifier for
        // a future reward. New templates have fixed positions/defaults and only
        // already range-checked item IDs, ushort caps/durability and byte color.
        var items = stable.PlayerItems with
        { RuneItems = stable.PlayerItems.RuneItems.Where(row => row.Uid != selectedUid).ToArray() };
        if (plan.FragmentStackUid is { } uid)
        {
            items = items with { InventoryItems = items.InventoryItems.Select(row => row.Uid == uid
                ? row.WithStackSize(plan.FragmentStackSize) : row).ToArray() };
            var fragment = items.InventoryItems.Single(row => row.Uid == uid);
            _ = LegacyGameItemPacket.FromSnapshotItem(fragment, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes();
        }
        var image = PlayerItemSnapshotLoader.ToLegacyBytes(items);
        if (!LegacyItemSaveGraph.TryCapture(items, out var graph) || !graph.HasBytes(image)) throw Invalid();
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(image);
        var spells = WorldPacketHandlers.PrepareRuneDecomposeSpellDependencies(stable.Character, stable.PlayerItems,
            stable.PlayerSpells, plan.Target, runeMetadata);
        if (plan.Target.Position is not (211 or 212))
            _ = LegacyRuneSpellOrigins.ProjectCharacterImage(before.CopyCharacter(), spells.Character);
        var spellBefore = before.CopySpells();
        var spellAfter = LegacyRuneSpellOrigins.ProjectSpellImage(spellBefore, spells.Spells);
        if (plan.Target.Position is 211 or 212 && spellBefore is not null && !spellBefore.AsSpan().SequenceEqual(spellAfter))
            throw Invalid();
        _ = checked((uint)stable.AccountId); _ = checked((uint)stable.Character.Uid);
    }
}
