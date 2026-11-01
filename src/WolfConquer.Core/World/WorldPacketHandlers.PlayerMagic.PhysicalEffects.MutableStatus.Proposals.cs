using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // DATA only. These projections neither publish a flag nor authorize a cast.
    // The actual proc retains this once-calculated sequence before its costs.
    internal sealed record DragonhowlMutableStatusProposal(WorldSession ActorBefore, WorldSession TargetBefore,
        WorldSession ActorAfter, WorldSession TargetAfter, bool EnteredCriticalBlock, bool PublishesFrontBreak);

    internal static DragonhowlMutableStatusProposal PrepareDragonhowlMutableStatus(
        WorldPacketHandlerContext context, WorldSession actor, WorldSession target, WorldDataCatalog catalog) =>
        PrepareDragonhowlMutableStatusCore(context, actor, target, catalog, null);

    internal static DragonhowlMutableStatusProposal PrepareDragonhowlMutableStatus(
        WorldPacketHandlerContext context, WorldSession actor, WorldSession target, WorldDataCatalog catalog,
        DragonhowlReceiveCalculationData calculation) =>
        PrepareDragonhowlMutableStatusCore(context, actor, target, catalog, calculation);

    private static DragonhowlMutableStatusProposal PrepareDragonhowlMutableStatusCore(
        WorldPacketHandlerContext context, WorldSession actor, WorldSession target, WorldDataCatalog catalog,
        DragonhowlReceiveCalculationData? calculation)
    {
        var fields = calculation?.ReadActorFields(actor, catalog) ?? CalculateLegacyRuntimeStatusFields(actor, catalog);
        if (fields[LegacyPlayerStatusPacket.CriticalStrikeIndex] == 0)
            return new(actor, target, actor, target, false, false);
        var afterActor = actor;
        bool front = !HasLegacyStatusFlag(actor.Character, LegacyPhysicalFrontBreakStatusFlag) &&
            TryGetLegacyEligibleEquippedRuneLevel(actor, catalog, "Front Break", out _);
        if (front)
        {
            TryGetLegacyEligibleEquippedRuneLevel(actor, catalog, "Front Break", out var level);
            afterActor = actor with
            {
                Character = WithLegacyStatusFlag(actor.Character, LegacyPhysicalFrontBreakStatusFlag),
                PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(actor) with
                {
                    Breakthrough = AddLegacyPhysicalMutableStatusPercent(
                        fields[LegacyPlayerStatusPacket.BreakthroughIndex], level == 9 ? 10U : level),
                    FrontBreakExpiresAtUtc = (context.TimeProvider ?? throw new InvalidOperationException(
                        "ownedDragonhowlClockMissing")).GetUtcNow().AddSeconds(10),
                },
            };
        }
        // Preserve the source draw even when the selected rune chance is zero.
        TryGetLegacyEligibleEquippedRuneLevel(target, catalog, "Impregnability", out var impreg);
        uint chance = impreg switch { 1 => 10, 2 => 11, 3 => 12, 4 => 13, 5 => 14, 6 => 15, 7 => 16, 8 => 18, 9 => 20, _ => 0 };
        var afterTarget = target;
        if (CheckLegacyPlayerPlayerPhysicalEffectRate(chance,
                ReadLegacyPhysicalEffectRoll(target.Character, RuntimeLegacyPhysicalImpregnabilityRollAttribute)))
        {
            uint percent = impreg switch { 1 => 30, 2 => 32, 3 => 34, 4 => 36, 5 => 38, 6 => 40, 7 => 42, 8 => 45, 9 => 50, _ => 0 };
            var targetFields = calculation?.ReadTargetFields(target, catalog) ?? CalculateLegacyRuntimeStatusFields(target, catalog);
            afterTarget = target with { PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(target) with
            {
                Counteraction = AddLegacyPhysicalMutableStatusPercent(
                    targetFields[LegacyPlayerStatusPacket.CounteractionIndex], percent),
            } };
        }
        return new(actor, target, afterActor, afterTarget, true, front);
    }
}
