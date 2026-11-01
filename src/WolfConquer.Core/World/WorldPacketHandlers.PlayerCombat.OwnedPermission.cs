namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Pure permission calculation after actual before-input capture; it never
    // invokes the legacy random counter or publishes its rejection animation.
    private static bool CanOwnedBasicPhysicalDamageTarget(WorldPacketHandlerContext context, WorldSession actor,
        WorldSession target, DateTimeOffset protectedNow, out string reason)
    {
        reason = "ordinaryBasicPhysicalTargetUnavailable";
        if (!CanSeePlayer(actor.Character, target.Character) ||
            HasLegacyStatusFlag(actor.Character, LegacyNeptuneCurseFlag) ||
            !IsLegacyDirectPlayerPhysicalTargetInRange(actor, context.WorldData,
                LegacyGetDistance(actor.Character.X, actor.Character.Y, target.Character.X, target.Character.Y))) return false;
        if (TryGetLegacyCombatGearDamageBlockReason(target.Character, out reason)) return false;
        if (HasLegacyNinjaHellVortexEquipped(target.Character) && context.WorldData is { } data &&
            LegacyMagicRuntime.TryResolveLearnedSpell(target.PlayerSpells, data, LegacyHellVortexSpellId, out _, out var counter) &&
            IsLegacyHellVortexSpell(counter))
        { reason = "ordinaryBasicPhysicalUnjoined:hellVortexCounter"; return false; }
        return CanLegacyDirectPhysicalAfterCounter(context, actor, target, protectedNow, out reason);
    }
}
