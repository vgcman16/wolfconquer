using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyThundercloudPetAttackResult?>
        TryHandleLegacyThundercloudPlayerPetAttackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession target,
        LegacyMagicRuntimeSpell callPetSpell,
        LegacyMagicRuntimeSpell thundercloudAttackSpell,
        CancellationToken cancellationToken)
    {
        if (!CanLegacyPlayerMagicDamageTarget(
                context,
                session,
                target.Session,
                thundercloudAttackSpell,
                out _,
                out var failureEffect))
        {
            if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
            {
                await TrySendLegacyPlayerHellVortexCounterAsync(
                        context,
                        session,
                        target,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return null;
        }

        var updatedSession = await TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
                context,
                session,
                [new LegacyPlayerMagicTarget(target)],
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };

        if (context.Sessions?.TryGet(target.Connection, out var refreshedTarget) == true &&
            refreshedTarget is not null)
        {
            target = target with { Session = refreshedTarget };
        }

        var statusDefended =
            HasLegacyStatusFlag(target.Session.Character, LegacyShurikenVortexFlag) ||
            HasLegacyStatusFlag(target.Session.Character, LegacyManiacDanceFlag);
        var wildPhoenixDefended = false;
        if (!statusDefended)
        {
            (target, wildPhoenixDefended) = await TryApplyLegacyCombatGearWildPhoenixDefenderAsync(
                    context,
                    target,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        LegacyPlayerPlayerPhysicalSpellDamageResult rangeDamage;
        if (statusDefended)
        {
            rangeDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(
                CalculateLegacyPlayerRangeStatusReturnDamage(
                    updatedSession,
                    target.Session,
                    context.WorldData),
                MsgAttackEffect.None);
        }
        else if (wildPhoenixDefended)
        {
            rangeDamage = new LegacyPlayerPlayerPhysicalSpellDamageResult(1, MsgAttackEffect.None);
        }
        else
        {
            rangeDamage = CalculateLegacyDirectPlayerPlayerRangeDamageResult(
                updatedSession,
                target.Session,
                context.WorldData);
        }

        var unscaledDamage = rangeDamage.Damage;
        var effect = rangeDamage.Effect;
        if (!statusDefended &&
            !wildPhoenixDefended &&
            HasLegacyStatusFlag(target.Session.Character, LegacyDefensiveStanceFlag))
        {
            unscaledDamage = LegacyCombatDamageCalculator.DecreaseLegacyBless(unscaledDamage, 40);
            effect = MsgAttackEffect.Block;
        }
        else if (!statusDefended && !wildPhoenixDefended)
        {
            var attacker = updatedSession.Character;
            var backDamageEnteredAlive = attacker.MinHitPoints > 0;
            (context, target, attacker, var backDamageAbortReason) = await TryApplyLegacyPlayerReceiveHeavensWonderAsync(
                    context,
                    target,
                    attacker,
                    cancellationToken)
                .ConfigureAwait(false);
            if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return null;
            }
            (context, target, attacker, backDamageAbortReason) = await TryApplyLegacyPlayerReceiveRevengeTailAsync(
                    context,
                    target,
                    attacker,
                    unscaledDamage,
                    cancellationToken,
                    backDamageEnteredAlive: backDamageEnteredAlive)
                .ConfigureAwait(false);
            if (backDamageAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return null;
            }
            if (context.Session is { } refreshedAttacker)
            {
                updatedSession = refreshedAttacker;
            }
        }

        var requestedDamage = MulDivToUInt32(unscaledDamage, callPetSpell.Damage2, 223);
        var receiveOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
            context,
            target,
            updatedSession.Character,
            requestedDamage,
            cancellationToken)
        .ConfigureAwait(false);
        if (receiveOutcome.AttackerSession is { } currentReceiveAttacker)
        {
            updatedSession = currentReceiveAttacker;
            context = context with { Session = updatedSession };
        }
        if (receiveOutcome.Result is not { } result)
        {
            return null;
        }
        if (result.UpdatedAttackerSession is not null)
        {
            updatedSession = result.UpdatedAttackerSession;
            context = context with { Session = updatedSession };
        }

        context = context with { Session = updatedSession };
        (updatedSession, result) = await TrySendLegacyReceiveAttackPlayerSpeffectAttributesAsync(
                context,
                updatedSession,
                result,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        (updatedSession, _) = await TrySendLegacyPlayerReceiveNinjaSageModeAsync(
                context,
                updatedSession,
                context.WorldData,
                cancellationToken)
            .ConfigureAwait(false);

        var targetCharacter = result.UpdatedSession.Character;
        return new LegacyThundercloudPetAttackResult(
            updatedSession,
            "player",
            CheckedUInt16(targetCharacter.X, nameof(targetCharacter.X)),
            CheckedUInt16(targetCharacter.Y, nameof(targetCharacter.Y)),
            new LegacySpellAnimationTarget(
                CheckedUInt32(targetCharacter.Uid, nameof(targetCharacter.Uid)),
                requestedDamage,
                Hit: true,
                effect),
            PlayerResult: result);
    }
}
