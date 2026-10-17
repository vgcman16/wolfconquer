using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly AsyncLocal<HashSet<uint>?> LegacyRevengeTailHellVortexCasters = new();

    private static async ValueTask<(
        WorldPacketHandlerContext Context,
        WorldActiveSession Target,
        CharacterSnapshot Attacker,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TryApplyLegacyPlayerReceiveRevengeTailAsync(
            WorldPacketHandlerContext context,
            WorldActiveSession target,
            CharacterSnapshot attacker,
            uint incomingDamage,
            CancellationToken cancellationToken,
            bool backDamageEnteredAlive = false)
    {
        var changes = ReadAttributeUInt32(
            target.Session.Character.Attributes,
            RuntimeLegacyRevengeTailChangeAttribute,
            "RevengeTailChange");
        // BackDmg checks alive once, before a potentially lethal Heaven's Wonder.
        if ((attacker.MinHitPoints <= 0 && !backDamageEnteredAlive) ||
            context.Session is not { } attackerSession ||
            context.WorldData is not { } worldData ||
            !HasLegacyStatusFlag(target.Session.Character, LegacyRevengeTailFlag) ||
            changes == 0 ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                target.Session.PlayerSpells,
                worldData,
                LegacyRevengeTailSpellId,
                out var playerSpell,
                out var spell) ||
            incomingDamage >= spell.Damage)
        {
            return (context, target, attacker, LegacyPlayerMagicReceiveAbortReason.None);
        }

        var attackerUid = attacker.Uid;
        var targetUid = target.Session.Character.Uid;
        var counterCasterSession = target.Session;
        var updatedAttackerSession = attackerSession;
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var gateAccepted = CanLegacyPlayerMagicDamageTarget(
            context,
            attackerSession,
            target.Session,
            spell,
            out var failureDiagnostic,
            out var failureEffect);
        if (gateAccepted)
        {
            var counterContext = context with
            {
                Connection = target.Connection,
                Session = target.Session,
            };
            var counterOutcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    counterContext,
                    new WorldActiveSession(context.Connection, attackerSession),
                    target.Session.Character,
                    spell.Damage,
                    cancellationToken)
                .ConfigureAwait(false);
            if (counterOutcome.Result is not { } counterResult)
            {
                var reason = counterOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost
                    ? LegacyPlayerMagicReceiveAbortReason.TargetLost
                    : LegacyPlayerMagicReceiveAbortReason.AttackerLost;
                return CreateLegacyBackDamageAbort(context, target, attacker, attackerUid, reason);
            }

            updatedAttackerSession = counterResult.UpdatedSession;
            counterCasterSession = counterOutcome.AttackerSession ??
                counterResult.UpdatedAttackerSession ?? counterCasterSession;

            counterContext = counterContext with { Session = counterCasterSession };
            await SendLegacyPlayerMagicPreAnimationUpdatesAsync(
                    counterContext,
                    counterCasterSession,
                    counterResult,
                    cancellationToken)
                .ConfigureAwait(false);
            var preAnimationAbortReason = RefreshLegacyBackDamageParticipants(
                ref context, ref target, ref attacker, attackerUid, targetUid);
            if (preAnimationAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return CreateLegacyBackDamageAbort(context, target, attacker, attackerUid, preAnimationAbortReason);
            }

            if (context.Sessions is not null)
            {
                updatedAttackerSession = context.Session ?? updatedAttackerSession;
                counterCasterSession = target.Session;
            }
            counterContext = counterContext with { Session = counterCasterSession };
            await SendLegacyPlayerMagicDamageUpdatesAsync(
                    counterContext,
                    counterCasterSession,
                    counterResult,
                    cancellationToken)
                .ConfigureAwait(false);
            var damagePublicationAbortReason = RefreshLegacyBackDamageParticipants(
                ref context, ref target, ref attacker, attackerUid, targetUid);
            if (damagePublicationAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return CreateLegacyBackDamageAbort(context, target, attacker, attackerUid, damagePublicationAbortReason);
            }

            if (context.Sessions is not null)
            {
                updatedAttackerSession = context.Session ?? updatedAttackerSession;
                counterCasterSession = target.Session;
            }
            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(
                    updatedAttackerSession.Character.Uid,
                    nameof(updatedAttackerSession.Character.Uid)),
                spell.Damage,
                Hit: true,
                MsgAttackEffect.None));
        }
        else if (failureEffect == LegacyPlayerMagicGateFailureEffect.ForbiddenPirate)
        {
            await SendScalarUpdateAsync(
                    context,
                    attackerSession.Character,
                    LegacyMsgUpdateDataType.ForbiddenPirate,
                    1,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (failureEffect == LegacyPlayerMagicGateFailureEffect.HellVortexCounter)
        {
            // Reentrant Tail gates must not restart HellVortex's bounded counter cascade.
            var previousCasters = LegacyRevengeTailHellVortexCasters.Value;
            var activeCasters = previousCasters ?? new HashSet<uint>();
            LegacyRevengeTailHellVortexCasters.Value = activeCasters;
            try
            {
                await TrySendLegacyPlayerHellVortexCounterAsync(
                        context,
                        attackerSession,
                        target,
                        cancellationToken,
                        activeCasters)
                    .ConfigureAwait(false);
            }
            finally
            {
                LegacyRevengeTailHellVortexCasters.Value = previousCasters;
            }

            var counterAbortReason = RefreshLegacyBackDamageParticipants(
                ref context, ref target, ref attacker, attackerUid, targetUid);
            if (counterAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return CreateLegacyBackDamageAbort(context, target, attacker, attackerUid, counterAbortReason);
            }

            if (context.Sessions is not null)
            {
                updatedAttackerSession = context.Session ?? updatedAttackerSession;
                counterCasterSession = target.Session;
            }
        }

        var beforeAnimationAbortReason = RefreshLegacyBackDamageParticipants(
            ref context, ref target, ref attacker, attackerUid, targetUid);
        if (beforeAnimationAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return CreateLegacyBackDamageAbort(context, target, attacker, attackerUid, beforeAnimationAbortReason);
        }

        if (context.Sessions is not null)
        {
            updatedAttackerSession = context.Session ?? updatedAttackerSession;
            counterCasterSession = target.Session;
        }
        var counterCasterUid = CheckedUInt32(
            counterCasterSession.Character.Uid,
            nameof(counterCasterSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            counterCasterUid,
            CheckedUInt16(counterCasterSession.Character.X, nameof(counterCasterSession.Character.X)),
            CheckedUInt16(counterCasterSession.Character.Y, nameof(counterCasterSession.Character.Y)),
            LegacyRevengeTailSpellId,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets,
            bombFlag: 1);
        var counterCasterContext = context with
        {
            Connection = target.Connection,
            Session = counterCasterSession,
        };
        await SendPlayerSpellAnimationToVisibleAsync(
                counterCasterContext,
                counterCasterSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        // Send can reenter receives; only the same original participants continue.
        var animationAbortReason = RefreshLegacyBackDamageParticipants(
            ref context, ref target, ref attacker, attackerUid, targetUid);
        if (animationAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return CreateLegacyBackDamageAbort(context, target, attacker, attackerUid, animationAbortReason);
        }

        if (context.Sessions is not null)
        {
            updatedAttackerSession = context.Session ?? updatedAttackerSession;
            counterCasterSession = target.Session;
        }

        // BackDmg decrements the live signed counter after nested casts complete.
        var remainingChanges = SafeReadAttributeInt64(
            counterCasterSession.Character.Attributes,
            RuntimeLegacyRevengeTailChangeAttribute,
            "RevengeTailChange") - 1;
        var updatedCounterCasterCharacter = WithCharacterAttribute(
            counterCasterSession.Character,
            RuntimeLegacyRevengeTailChangeAttribute,
            remainingChanges.ToString(CultureInfo.InvariantCulture));
        var removedFlag = remainingChanges == 0 &&
            HasLegacyStatusFlag(updatedCounterCasterCharacter, LegacyRevengeTailFlag);
        if (removedFlag)
        {
            updatedCounterCasterCharacter = WithoutLegacyStatusFlag(
                updatedCounterCasterCharacter,
                LegacyRevengeTailFlag);
        }

        counterCasterSession = counterCasterSession with { Character = updatedCounterCasterCharacter };
        context.Sessions?.Set(target.Connection, counterCasterSession);
        counterCasterContext = counterCasterContext with { Session = counterCasterSession };
        if (removedFlag)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    counterCasterContext,
                    counterCasterSession,
                    cancellationToken)
                .ConfigureAwait(false);
            var flagPublicationAbortReason = RefreshLegacyBackDamageParticipants(
                ref context, ref target, ref attacker, attackerUid, targetUid);
            if (flagPublicationAbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            {
                return CreateLegacyBackDamageAbort(context, target, attacker, attackerUid, flagPublicationAbortReason);
            }

            if (context.Sessions is not null)
            {
                updatedAttackerSession = context.Session ?? updatedAttackerSession;
                counterCasterSession = target.Session;
            }
        }

        context = context with { Session = updatedAttackerSession };
        target = target with { Session = counterCasterSession };
        attacker = updatedAttackerSession.Character;
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy player RevengeTail back damage " +
                $"counterCasterUid={counterCasterUid} attackerUid={attacker.Uid} " +
                $"spellId={LegacyRevengeTailSpellId} damage={spell.Damage} " +
                $"gateAccepted={gateAccepted} gateFailure={failureDiagnostic} " +
                $"changesRemaining={remainingChanges} removedFlag={removedFlag}; " +
                "source=matching DecompiledSource AttackHandler.Calculate/BackDmg.cs lines 36-62.")
            .ConfigureAwait(false);
        return (context, target, attacker, LegacyPlayerMagicReceiveAbortReason.None);
    }
}
