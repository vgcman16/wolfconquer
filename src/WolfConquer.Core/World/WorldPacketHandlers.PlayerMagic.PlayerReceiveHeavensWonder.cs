using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyHeavensWonderBackDamageRollAttribute =
        "LegacyHeavensWonderBackDamageRoll";

    private static async ValueTask<LegacyPlayerMagicReceiveOutcome>
        ApplyLegacyBackDamageEligiblePlayerDamageAsync(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        CharacterSnapshot attacker,
        uint damage,
        CancellationToken cancellationToken = default,
        uint? appliedDamageOverride = null,
        bool applyMudWallInFinalReceive = true,
        ushort? postBackPhysicalSpellId = null,
        uint postBackPhysicalDamageDivisor = 1,
        uint postBackFlameDamageOnHuman = 0,
        ushort? postBackMagicSpellId = null,
        object? postBackNativeMagicCasterIdentity = null)
    {
        var originalAttackerSession = context.Session;
        var backDamageEnteredAlive = attacker.MinHitPoints > 0;
        (context, target, attacker, var abortReason) = await TryApplyLegacyPlayerReceiveHeavensWonderAsync(
                context,
                target,
                attacker,
                cancellationToken)
            .ConfigureAwait(false);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return new LegacyPlayerMagicReceiveOutcome(null,
                abortReason == LegacyPlayerMagicReceiveAbortReason.TargetLost ? context.Session : null, abortReason);
        }

        (context, target, attacker, abortReason) = await TryApplyLegacyPlayerReceiveRevengeTailAsync(
                context,
                target,
                attacker,
                damage,
                cancellationToken,
                backDamageEnteredAlive: backDamageEnteredAlive)
            .ConfigureAwait(false);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return new LegacyPlayerMagicReceiveOutcome(null,
                abortReason == LegacyPlayerMagicReceiveAbortReason.TargetLost ? context.Session : null, abortReason);
        }

        if (postBackMagicSpellId is 1_000 or 1_001 &&
            postBackNativeMagicCasterIdentity is { } nativeIdentity)
        {
            if (!TryRefreshLegacyNativeMagicQueryCaster(context, attacker.Uid, nativeIdentity,
                    out var nativeCaster))
                return new LegacyPlayerMagicReceiveOutcome(null, null,
                    LegacyPlayerMagicReceiveAbortReason.AttackerLost);
            context = context with { Session = nativeCaster };
            attacker = nativeCaster.Character;
        }
        var finalDamage = appliedDamageOverride ?? damage;
        if (postBackPhysicalSpellId is 16_330 or 16_340 or 16_540 or 16_550)
        {
            // Physical.OnPlayer applies these spell tails after BackDmg and MudWall.
            finalDamage = ApplyLegacyPlayerReceiveMudWall(
                target.Session,
                context.WorldData,
                finalDamage);
            applyMudWallInFinalReceive = false;

            if (postBackPhysicalSpellId is 16_330 or 16_340 &&
                context.Session is { } activeAttackerSession &&
                activeAttackerSession.Character.Uid == attacker.Uid)
            {
                var kylinResult = ApplyLegacyKylinSigilPhysicalSpellDamage(
                    context,
                    activeAttackerSession,
                    postBackPhysicalSpellId.Value,
                    finalDamage);
                context = context with { Session = kylinResult.Session };
                attacker = kylinResult.Session.Character;
                finalDamage = kylinResult.Damage;
            }

            if (postBackPhysicalSpellId is 16_540 or 16_550)
            {
                if ((long)target.Session.Character.MinHitPoints - finalDamage <= 1)
                {
                    finalDamage = 0;
                }

                // ThreadPool applies its player clamp after Physical.OnPlayer returns.
                finalDamage = ClampLegacyFlameOfDestructionPlayerDamage(
                    finalDamage,
                    postBackFlameDamageOnHuman);
            }

            if (postBackPhysicalDamageDivisor > 1)
            {
                finalDamage /= postBackPhysicalDamageDivisor;
            }
        }

        var outcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                context, target, attacker,
                postBackMagicSpellId is { } magicSpellId
                    ? CompleteLegacyNativeAttackPlayerPostBackDamage(attacker, magicSpellId, finalDamage)
                    : finalDamage,
                cancellationToken,
                applyMudWall: applyMudWallInFinalReceive,
                nativeIncomingClientIdentity: postBackNativeMagicCasterIdentity)
            .ConfigureAwait(false);
        var currentAttacker = outcome.AttackerSession ??
            (context.Session?.Character.Uid == attacker.Uid ? context.Session : null);
        if (outcome.Result is { UpdatedAttackerSession: null } result &&
            currentAttacker is { } refreshedAttackerSession &&
            !Equals(refreshedAttackerSession, originalAttackerSession))
        {
            outcome = outcome with
            {
                Result = result with { UpdatedAttackerSession = refreshedAttackerSession },
                AttackerSession = refreshedAttackerSession,
            };
        }

        return outcome;
    }

    private static async ValueTask<(
        WorldPacketHandlerContext Context,
        WorldActiveSession Target,
        CharacterSnapshot Attacker,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TryApplyLegacyPlayerReceiveHeavensWonderAsync(
            WorldPacketHandlerContext context,
            WorldActiveSession target,
            CharacterSnapshot attacker,
            CancellationToken cancellationToken)
    {
        if (attacker.MinHitPoints <= 0 ||
            context.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                target.Session.PlayerSpells,
                worldData,
                LegacyHeavensWonderSpellId,
                out var playerSpell,
                out var spell) ||
            !CheckLegacyPercentRoll(
                target.Session.Character.Attributes,
                RuntimeLegacyHeavensWonderBackDamageRollAttribute,
                chance: 11))
        {
            return (context, target, attacker, LegacyPlayerMagicReceiveAbortReason.None);
        }

        var attackerUid = attacker.Uid;
        var targetUid = CheckedUInt32(target.Session.Character.Uid, nameof(target.Session.Character.Uid));
        var targetContext = context with
        {
            Connection = target.Connection,
            Session = target.Session,
        };
        var query = new MsgAttackQuery(
            Uid: 0,
            OpponentUid: targetUid,
            X: 0,
            Y: 0,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellLevel: playerSpell.Level,
            SpellId: playerSpell.Id);

        await HandleLegacyPlayerCounterKill16350MagicAsync(
                targetContext,
                target.Session,
                query,
                playerSpell,
                spell,
                ignoreAttackStamp: true,
                cancellationToken)
            .ConfigureAwait(false);

        var abortReason = RefreshLegacyBackDamageParticipants(
            ref context, ref target, ref attacker, attackerUid, targetUid);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return CreateLegacyBackDamageAbort(context, target, attacker, attackerUid, abortReason);
        }

        await context.Log.WriteLineAsync(
                "WorldServer attempted legacy Heaven's Wonder receive-side automatic cast " +
                $"targetUid={targetUid} attackerUid={attacker.Uid} spellId={LegacyHeavensWonderSpellId} " +
                "queryX=0 queryY=0 ignoreAttackStamp=true chance=11; " +
                "source=matching DecompiledSource AttackHandler.Calculate/BackDmg.cs.")
            .ConfigureAwait(false);
        return (context, target, attacker, LegacyPlayerMagicReceiveAbortReason.None);
    }
}
