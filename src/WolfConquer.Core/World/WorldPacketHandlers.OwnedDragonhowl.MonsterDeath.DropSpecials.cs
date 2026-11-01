using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct DragonhowlSpecialDrop(uint ItemId, byte ScatterRange, bool SelectionAbsent = false);

    // DATA extraction from actual source branches. It neither publishes floors
    // nor calls any mutating death hook. The live drop owner consumes these exact
    // once-selected outcomes after the actual registered monster kill.
    private static bool TryPrepareDragonhowlSpecialDrops(WorldPacketHandlerContext context, WorldSession actor,
        LegacyMonsterInstance monster, Monster family, int roll, bool forced,
        out DragonhowlSpecialDrop[] drops, out bool suppressOrdinary, out string reason)
    {
        drops = []; suppressOrdinary = false; reason = string.Empty;
        var selected = new List<DragonhowlSpecialDrop>();
        // This branch interleaves floor publication with its own talk/effect and
        // generated relic body. It cannot be flattened into an empty floor list.
        if (monster.MapId == 10_250)
        { reason = "ownedDragonhowlUnjoined:deityDropOutputAndGeneratedBody"; return false; }
        if (IsLegacyMap1011Or1020Family3130Or3134DropBranch(monster, family) && CheckLegacyMapSpecialDropRate(actor.Character))
            selected.Add(new(SelectLegacyMap1011Or1020Family3130DropItemId(actor.Character), 4));
        if (IsLegacyMap11127SpecialDropMonster(monster) && !IsLegacyMap11127SpecialDropSuppressedByEarlierSourceBranch(monster, family))
        {
            int count = GetLegacyMonsterDeathDropCount(roll, forced);
            uint id = roll is > 100 and < 200 ? (roll % 4 == 0 ? 722_723U : roll % 4 == 2 ? 722_724U : 722_725U) :
                roll < 50 ? 722_732U : roll is > 300 and < 350 ? 722_736U : 0;
            if (id != 0) for (int i = 0; i < count; i++) selected.Add(new(id, 0));
        }

        bool bossFamily = IsLegacyGenerateBossFamilyMonster(monster);
        if (bossFamily && ShouldTryLegacyBossFamilyGeneratedDropHook(monster, family, actor.Character) &&
            TryCreateLegacyBossFamilyGeneratedDropRolls(actor.Character, out var generatedRolls))
        {
            // The existing deterministic source hook has no watcher counter.
            // The selector's catalog callback is DATA lookup, not acceptance of
            // a transaction or a grant. Exact selected membership is pinned by
            // DragonhowlDropRecipe before any UID is consumed.
            var selection = LegacyMonsterDeathDropSelector.Select(monster, family, generatedRolls,
                LegacyMonsterDeathDropContext.FromWorldData(context.WorldData!));
            if (selection.Kind is LegacyMonsterDeathDropKind.Item or LegacyMonsterDeathDropKind.SpecialItem)
                for (int i = 0; i < selection.DropCount; i++) selected.Add(new(selection.ItemId, 0));
            else if (generatedRolls.GeneratedBaseLevel is <= 99 && generatedRolls.GeneratedSort is { } sort)
            {
                // The existing hook supplies level-alter roll 50, so this is
                // exactly its unaltered candidate, including a real absent row.
                uint candidate = checked((uint)sort * 1000U + generatedRolls.GeneratedBaseLevel.Value * 10U +
                    (generatedRolls.GeneratedQuality ?? (byte)3));
                selected.Add(new(candidate, 0, SelectionAbsent: true));
            }
            suppressOrdinary = true; drops = selected.ToArray(); return true;
        }
        if (bossFamily || HasLegacySourceSpecialDropBranch(monster, family))
        {
            // This exact source branch has fixed item IDs and no candidate-pool
            // catalog dependency. Other boss/soul/Queen/relic branches retain a
            // pre-cost hold until their actual used pools and body owners join.
            if (!LegacyBossSpecialDeathDropPolicy.IsDragonIslandSpecialBranch(checked((uint)family.Id), checked((uint)monster.MapId)) ||
                LegacyBossSpecialDeathDropPolicy.IsQueenOfEvilSpecialBranch(checked((uint)family.Id), family.Name))
            { reason = "ownedDragonhowlUnjoined:bossGeneratedOrSelectedPoolRecipe"; return false; }
            var rolls = CreateLegacyBossSpecialDeathDropRolls(actor.Character);
            if (!LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(checked((uint)family.Id), checked((uint)monster.MapId),
                monster.Boss, family.Name, rolls, null, out var plan) || plan.GeneratedItems.Count != 0 || plan.PendingSourceBehaviors.Count != 0)
            { reason = "ownedDragonhowlUnjoined:pendingBossDropRecipe"; return false; }
            foreach (var item in plan.FixedItems)
            {
                if (item.Count < 0) throw new InvalidDataException("ownedDragonhowlSpecialDropCountInvalid");
                for (int i = 0; i < item.Count; i++) selected.Add(new(item.ItemId, item.ScatterRange));
            }
            suppressOrdinary = true;
        }
        drops = selected.ToArray(); return true;
    }
}
