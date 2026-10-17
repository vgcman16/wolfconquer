namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyMoveLinePlayerPhysicalDamageResult(
        WorldActiveSession Target,
        LegacyPlayerPlayerPhysicalSpellDamageResult DamageResult);

    private static async ValueTask<LegacyMoveLinePlayerPhysicalDamageResult>
        CalculateLegacyMoveLinePlayerPhysicalDamageAsync(
            WorldPacketHandlerContext context,
            WorldSession attacker,
            WorldActiveSession target,
            LegacyMagicRuntimeSpell spell,
            CancellationToken cancellationToken)
    {
        // Matching Physical.OnPlayer returns before WildPhoenix and the normal
        // physical tail for these statuses. MoveLine still scales/enqueues 1.
        if (HasLegacyStatusFlag(target.Session.Character, LegacyShurikenVortexFlag) ||
            HasLegacyStatusFlag(target.Session.Character, LegacyManiacDanceFlag))
        {
            return new(target, new(1, MsgAttackEffect.None));
        }

        (target, var wildPhoenixDefended) =
            await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                    context,
                    target,
                    cancellationToken)
                .ConfigureAwait(false);
        if (wildPhoenixDefended)
        {
            return new(target, new(1, MsgAttackEffect.None));
        }

        var damageResult = CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
            attacker,
            target.Session,
            spell,
            context.WorldData,
            damageDivisor: 1);
        var breakthrough = ApplyLegacyPlayerPlayerPhysicalSpellBreakthroughBranch(
            attacker,
            target.Session,
            spell,
            context.WorldData,
            damageResult);
        damageResult = ApplyLegacyPlayerPlayerPhysicalDefensiveStance(
            target.Session,
            breakthrough.DamageResult);
        damageResult = ApplyLegacyPlayerPlayerPhysicalSpellInfinity(
            attacker,
            spell,
            damageResult);
        damageResult = ApplyLegacyPlayerPlayerPhysicalSpellNoBreakthroughBonus(
            breakthrough.State,
            damageResult);
        return new(target, damageResult);
    }
}
