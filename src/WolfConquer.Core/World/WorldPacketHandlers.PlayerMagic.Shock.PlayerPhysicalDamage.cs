namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyShockPlayerPhysicalDamageResult(
        WorldSession Session,
        WorldActiveSession Target,
        LegacyPlayerPlayerPhysicalSpellDamageResult? DamageResult,
        LegacyPlayerMagicReceiveAbortReason AbortReason = LegacyPlayerMagicReceiveAbortReason.None);

    private static async ValueTask<LegacyShockPlayerPhysicalDamageResult>
        CalculateLegacyShockPlayerPhysicalDamageAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldActiveSession target,
            LegacyMagicRuntimeSpell spell,
            CancellationToken cancellationToken)
    {
        if (HasLegacyStatusFlag(target.Session.Character, LegacyShurikenVortexFlag) ||
            HasLegacyStatusFlag(target.Session.Character, LegacyManiacDanceFlag))
        {
            return new(session, target, new LegacyPlayerPlayerPhysicalSpellDamageResult(1, MsgAttackEffect.None));
        }

        (target, var wildPhoenixDefended) = await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                context,
                target,
                cancellationToken)
            .ConfigureAwait(false);
        if (wildPhoenixDefended)
        {
            return new(session, target, new LegacyPlayerPlayerPhysicalSpellDamageResult(1, MsgAttackEffect.None));
        }

        var physicalDamage = CalculateLegacyPlayerPlayerPhysicalSpellDamageResult(
            session,
            target.Session,
            spell,
            context.WorldData,
            damageDivisor: 1);
        var breakthrough = ApplyLegacyPlayerPlayerPhysicalSpellBreakthroughBranch(
            session,
            target.Session,
            spell,
            context.WorldData,
            physicalDamage);
        physicalDamage = ApplyLegacyPlayerPlayerPhysicalDefensiveStance(target.Session, breakthrough.DamageResult);
        physicalDamage = ApplyLegacyPlayerPlayerPhysicalSpellInfinity(session, spell, physicalDamage);
        physicalDamage = ApplyLegacyPlayerPlayerPhysicalSpellNoBreakthroughBonus(breakthrough.State, physicalDamage);

        // Physical.BackDmg sees the unscaled hit, before MudWall and Shock's caller-side 75 percent.
        var attacker = session.Character;
        var enteredAlive = attacker.MinHitPoints > 0;
        (context, target, attacker, var backDamageAbortReason) = await TryApplyLegacyPlayerReceiveHeavensWonderAsync(
                context,
                target,
                attacker,
                cancellationToken)
            .ConfigureAwait(false);
        if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return new(context.Session ?? session, target, null, backDamageAbortReason);
        }
        (context, target, attacker, backDamageAbortReason) = await TryApplyLegacyPlayerReceiveRevengeTailAsync(
                context,
                target,
                attacker,
                physicalDamage.Damage,
                cancellationToken,
                backDamageEnteredAlive: enteredAlive)
            .ConfigureAwait(false);
        if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return new(context.Session ?? session, target, null, backDamageAbortReason);
        }
        session = context.Session ?? session;
        physicalDamage = physicalDamage with
        {
            Damage = ApplyLegacyPlayerReceiveMudWall(target.Session, context.WorldData, physicalDamage.Damage),
        };
        return new(session, target, physicalDamage);
    }
}
