using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyThundercloudPetAttackResult?>
        TryHandleLegacyThundercloudSobNpcPetAttackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc target,
        LegacyMagicRuntimeSpell callPetSpell,
        LegacyMagicRuntimeSpell thundercloudAttackSpell,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.SobNpcStates is not { } sobNpcStates ||
            !CanLegacySobNpcMagicDamageTarget(context, session.Character, target, out _))
        {
            return null;
        }

        var rangeDamage = CalculateLegacyThundercloudSobNpcRangeDamage(session, context.WorldData);
        var requestedDamage = MulDivToUInt32(rangeDamage.Damage, callPetSpell.Damage2, 100);
        var updatedSession = await TryConsumeLegacySlayerAsync(
                context,
                session,
                rangeDamage.ConsumesSlayer,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(target);
        var result = sobNpcStates.ApplyDamage(target, requestedDamage, now, deathBehavior);
        if (result.AlreadyDead)
        {
            return null;
        }

        if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(target))
        {
            await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                    context,
                    updatedSession,
                    target,
                    sobNpcStates.GetVisible(target),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!LegacySobNpcEventRules.IsTrainingSob(target))
        {
            await LogLegacySobNpcEventSideEffectGapAsync(
                    context,
                    target,
                    result,
                    thundercloudAttackSpell)
                .ConfigureAwait(false);
        }

        return new LegacyThundercloudPetAttackResult(
            updatedSession,
            "sob-npc",
            CheckedUInt16(target.X, nameof(target.X)),
            CheckedUInt16(target.Y, nameof(target.Y)),
            new LegacySpellAnimationTarget(
                CheckedUInt32(target.Id, nameof(target.Id)),
                requestedDamage,
                Hit: true,
                rangeDamage.Effect),
            CalculateLegacySobNpcExperienceDamage(target, result));
    }
}
