using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyDirectPhysicalSobNpcRange = 7;
    private const int LegacyDirectArcherSobNpcRange = 18;

    private static async ValueTask<bool> TryHandleLegacyDirectSobNpcAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyDirectSobNpcAttackType(packet.Query.AttackType) ||
            !TryFindVisibleSobNpcByUid(
                context,
                session.Character,
                packet.Query.OpponentUid,
                out var sobNpc))
        {
            return false;
        }

        if (context.SobNpcStates is not { } sobNpcStates)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        SobNpcRuntimeStateResource,
                        "direct-sob-npc-attack",
                        "direct SOB NPC attack target was visible, but SOB runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", sobNpc.MapId),
                            LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                            LegacyParityGapDiagnostics.Fact("sobNpcKind", sobNpc.Kind),
                            LegacyParityGapDiagnostics.Fact("attackType", packet.Query.AttackType.ToString()),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("attackType", packet.Query.AttackType.ToString()),
                            LegacyParityGapDiagnostics.Fact("targetKind", "sob-npc"),
                        ]))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy direct SOB NPC attack " +
                    $"{FormatSession(context)} attackType={packet.Query.AttackType}; SOB runtime state is not loaded.")
                .ConfigureAwait(false);
            return true;
        }

        var distance = LegacyGetDistance(
            session.Character.X,
            session.Character.Y,
            sobNpc.X,
            sobNpc.Y);
        var range = packet.Query.AttackType == MsgAttackType.Archer
            ? LegacyDirectArcherSobNpcRange
            : LegacyDirectPhysicalSobNpcRange;
        if (distance > range)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer rejected legacy direct SOB NPC attack " +
                    $"{FormatSession(context)} attackType={packet.Query.AttackType} targetUid={sobNpc.Id}; " +
                    $"distance={distance} range={range}; source=MsgAttackPacket direct SOB caller range gate.")
                .ConfigureAwait(false);
            return true;
        }

        if (!CanLegacySobNpcMagicDamageTarget(context, session.Character, sobNpc, out var failureDiagnostic))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer rejected legacy direct SOB NPC target " +
                    $"{FormatSession(context)} attackType={packet.Query.AttackType} targetUid={sobNpc.Id}; " +
                    $"source=MsgAttackPacket direct SOB branch CanAttackNpc.Verified; {failureDiagnostic}.")
                .ConfigureAwait(false);
            return true;
        }

        var receiveGearOutcome = await TrySendLegacySobNpcReceiveCombatGearUpdateAsync(
                context with { Session = session },
                session,
                sobNpc,
                cancellationToken)
            .ConfigureAwait(false);
        if (receiveGearOutcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
        {
            return true;
        }

        session = receiveGearOutcome.Session;
        context = context with { Session = session };
        (session, _) = await TrySendLegacySobNpcReceiveNinjaSageModeAsync(
                context with { Session = session },
                session,
                context.WorldData,
                cancellationToken)
            .ConfigureAwait(false);
        session = WithLegacySobNpcReceiveHitAttributes(context, session, sobNpc);

        var requestedDamage = CalculateLegacyDirectSobNpcPhysicalDamage(session, context.WorldData);
        requestedDamage = ApplyLegacySobNpcReceiveDamageModifiers(session, requestedDamage);
        var deathBehavior = LegacySobNpcEventRules.GetDeathBehavior(sobNpc, context.WorldData?.StaticStatues);
        var result = sobNpcStates.ApplyDamage(sobNpc, requestedDamage, DateTimeOffset.UtcNow, deathBehavior);
        if (result.AlreadyDead || result.DamageApplied == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy direct SOB NPC attack " +
                    $"{FormatSession(context with { Session = session })} attackType={packet.Query.AttackType} " +
                    $"targetUid={sobNpc.Id}; alreadyDead={result.AlreadyDead} damageApplied={result.DamageApplied}.")
                .ConfigureAwait(false);
            return true;
        }

        var response = new MsgAttackPacket(packet.Query with
        {
            Damage = unchecked((int)result.DamageApplied),
            KilledMonster = 0,
            ResponseDamage = packet.Query.ResponseDamage,
            X = CheckedUInt16(sobNpc.X, nameof(sobNpc.X)),
            Y = CheckedUInt16(sobNpc.Y, nameof(sobNpc.Y)),
        });
        await SendFrameToVisiblePlayersAsync(context, session, response.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        if (result.Killed && LegacySobNpcEventRules.IsTrainingSob(sobNpc))
        {
            await SendLegacyTrainingSobNpcDeathUpdatesAsync(
                    context,
                    session,
                    sobNpc,
                    sobNpcStates.GetVisible(sobNpc),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!LegacySobNpcEventRules.IsTrainingSob(sobNpc))
        {
            session = await ApplyLegacySobNpcBoundedEventSideEffectsAsync(
                    context,
                    session,
                    sobNpc,
                    result,
                    deathBehavior,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyDirectSobNpcEventSideEffectGapAsync(
                    context with { Session = session },
                    sobNpc,
                    result,
                    packet.Query.AttackType)
                .ConfigureAwait(false);
        }

        session = await ApplyLegacyDirectSobNpcIncreaseExperienceAsync(
                context,
                session,
                CalculateLegacyDirectSobNpcReturnExperience(sobNpc, result),
                cancellationToken)
            .ConfigureAwait(false);

        session = await TryApplyLegacyWeaponProficiencyExperienceAsync(
                context,
                session,
                ClampLegacyProficiencyRawExperience(result.DamageApplied),
                cancellationToken,
                activity: "direct SOB NPC weapon proficiency experience")
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy direct SOB NPC attack " +
                $"{FormatSession(context with { Session = session })} targetUid={sobNpc.Id} " +
                $"attackType={packet.Query.AttackType} requestedDamage={requestedDamage} " +
                $"appliedDamage={result.DamageApplied} remainingHitpoints={result.RemainingHitpoints} " +
                $"killed={result.Killed}; source=MsgAttackPacket direct SOB Physical/Archer branch.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyDirectSobNpcAttackType(MsgAttackType attackType)
    {
        return attackType is MsgAttackType.Physical or MsgAttackType.Archer;
    }

    private static ulong CalculateLegacyDirectSobNpcReturnExperience(
        SobNpc sobNpc,
        SobNpcDamageResult result)
    {
        if (result.DamageApplied == 0)
        {
            return 0;
        }

        if (result.Killed)
        {
            return sobNpc.MapId == 1_039
                ? result.DamageApplied / 10
                : 0;
        }

        return sobNpc.MapId is 1_038 or 1_039
            ? result.DamageApplied / 1_000
            : 0;
    }

    private static async ValueTask<WorldSession> ApplyLegacyDirectSobNpcIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ulong returnExperience,
        CancellationToken cancellationToken)
    {
        if (returnExperience == 0)
        {
            return session;
        }

        var boundedExperience = returnExperience > uint.MaxValue
            ? uint.MaxValue
            : (uint)returnExperience;
        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, boundedExperience));
        var updatedSession = ApplyLegacyExperienceSideEffects(context, context.Connection, session, experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            if (experienceResult.OblivionExperienceGain != 0 ||
                experienceResult.HuntingBlessingGain != 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy direct SOB NPC experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} returnExperience={boundedExperience} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        "source=ReceiveAttack.Npc.Execute return -> IncreaseExperience.Up.")
                    .ConfigureAwait(false);
            }

            return updatedSession;
        }

        updatedSession = await ApplyLegacyFinalExperienceGainAsync(
                context,
                context.Connection,
                updatedSession,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy direct SOB NPC experience " +
                $"{FormatSession(context with { Session = updatedSession })} returnExperience={boundedExperience} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} experience={updatedSession.Character.Experience} " +
                $"level={updatedSession.Character.Level}; source=ReceiveAttack.Npc.Execute return -> IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
