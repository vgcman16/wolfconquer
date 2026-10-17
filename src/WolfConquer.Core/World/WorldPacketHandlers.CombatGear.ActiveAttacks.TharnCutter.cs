using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static byte GetLegacyCombatGearTharnCutterActiveBombFlag(ushort spellId, CharacterSnapshot character)
    {
        if (!IsLegacyCombatGearTharnCutterSigilFollowUpSpell(spellId))
        {
            return 0;
        }

        Span<byte> candidates = stackalloc byte[5];
        var count = 0;
        if (HasLegacyStatusFlag(character, LegacyCombatGearPrisonSigilFutilityCursedFlag))
        {
            candidates[count++] = 1;
        }

        if (HasLegacyStatusFlag(character, LegacyCombatGearWhirlSigilAuraCursedFlag))
        {
            candidates[count++] = 2;
        }

        if (HasLegacyStatusFlag(character, LegacyCombatGearTop8WeeklyHawksAmbitionFlag))
        {
            candidates[count++] = 3;
        }

        if (HasLegacyStatusFlag(character, LegacyCombatGearTop4WeeklyHawksAmbitionFlag))
        {
            candidates[count++] = 4;
        }

        if (HasLegacyStatusFlag(character, LegacyCombatGearTop2WeeklyHawksAmbitionFlag))
        {
            candidates[count++] = 5;
        }

        if (count == 0)
        {
            return 0;
        }

        var selected = candidates[count == 1 ? 0 : Random.Shared.Next(count)];
        return selected switch
        {
            1 => 1,
            2 => 2,
            3 => 4,
            4 => 8,
            5 => 16,
            _ => 0,
        };
    }


    private static bool IsLegacyCombatGearTharnCutterSigilFollowUpSpell(ushort spellId)
    {
        return spellId == LegacyCombatGearCrackShotSpellId ||
            LegacyCombatGearTharnCutterAttackSpells.Contains(spellId);
    }


    private static async ValueTask<WorldSession> TryGrantLegacyCombatGearTharnCutterMissingSigilAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldDataCatalog worldData,
        ushort activeSpellId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyCombatGearTharnCutterSigilFollowUpSpell(activeSpellId) ||
            !TryResolveLegacyPlayerMagicRuntimeSpell(
                session,
                worldData,
                LegacyCombatGearTharnCutterElementalArrowSpellId,
                out var playerSpell,
                out _))
        {
            return session;
        }

        if (!TrySelectLegacyCombatGearTharnCutterMissingSigil(
                session.Character,
                worldData,
                playerSpell.Level,
                out var statusFlag,
                out var archiveDamage,
                out var archiveSpellLevel,
                out var sigilName))
        {
            return session;
        }

        var updatedSession = session with
        {
            Character = WithLegacySourceAddSpellFlag(session.Character, statusFlag, removeOnDead: true),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                statusFlag,
                duration: 10,
                damage: archiveDamage,
                spellLevel: archiveSpellLevel,
                visible: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer granted legacy CombatGear TharnCutter missing sigil " +
                $"{FormatSession(updatedContext)} activeSpellId={activeSpellId} " +
                $"sourceSpellId={LegacyCombatGearTharnCutterElementalArrowSpellId} sigil={sigilName} " +
                $"statusFlag={statusFlag} damage={archiveDamage} spellLevel={archiveSpellLevel}; " +
                "source=CombatGearAttack TharnCutter -> 18160 AddSpellFlag/ArchiveSkill.")
            .ConfigureAwait(false);
        return updatedSession;
    }


    private static bool TrySelectLegacyCombatGearTharnCutterMissingSigil(
        CharacterSnapshot character,
        WorldDataCatalog worldData,
        ushort spellLevel,
        out int statusFlag,
        out uint archiveDamage,
        out uint archiveSpellLevel,
        out string sigilName)
    {
        Span<byte> candidates = stackalloc byte[5];
        var count = 0;
        if (!HasLegacyStatusFlag(character, LegacyCombatGearPrisonSigilFutilityCursedFlag))
        {
            candidates[count++] = 1;
        }

        if (!HasLegacyStatusFlag(character, LegacyCombatGearWhirlSigilAuraCursedFlag))
        {
            candidates[count++] = 2;
        }

        if (!HasLegacyStatusFlag(character, LegacyCombatGearTop8WeeklyHawksAmbitionFlag))
        {
            candidates[count++] = 3;
        }

        if (!HasLegacyStatusFlag(character, LegacyCombatGearTop4WeeklyHawksAmbitionFlag))
        {
            candidates[count++] = 4;
        }

        if (!HasLegacyStatusFlag(character, LegacyCombatGearTop2WeeklyHawksAmbitionFlag))
        {
            candidates[count++] = 5;
        }

        if (count == 0)
        {
            statusFlag = 0;
            archiveDamage = 0;
            archiveSpellLevel = 0;
            sigilName = "";
            return false;
        }

        var selected = candidates[count == 1 ? 0 : Random.Shared.Next(count)];
        var spellId = LegacyCombatGearTharnCutterElementalArrowSpellId;
        switch (selected)
        {
            case 1:
                statusFlag = LegacyCombatGearPrisonSigilFutilityCursedFlag;
                archiveDamage = ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, spellLevel, 10);
                archiveSpellLevel = 0;
                sigilName = "PrisonSigilFutility|Cursed";
                return true;
            case 2:
                statusFlag = LegacyCombatGearWhirlSigilAuraCursedFlag;
                archiveDamage = ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, spellLevel, 35);
                archiveSpellLevel = ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, spellLevel, 36);
                sigilName = "WhirlSigilAura|Cursed";
                return true;
            case 3:
                statusFlag = LegacyCombatGearTop8WeeklyHawksAmbitionFlag;
                archiveDamage = ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, spellLevel, 37);
                archiveSpellLevel = archiveDamage;
                sigilName = "Top8Weekly|HawksAmbition";
                return true;
            case 4:
                statusFlag = LegacyCombatGearTop4WeeklyHawksAmbitionFlag;
                archiveDamage = ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, spellLevel, 43);
                archiveSpellLevel = 0;
                sigilName = "Top4Weekly|HawksAmbition";
                return true;
            case 5:
                statusFlag = LegacyCombatGearTop2WeeklyHawksAmbitionFlag;
                archiveDamage = ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, spellLevel, 44);
                archiveSpellLevel = 0;
                sigilName = "Top2Weekly|HawksAmbition";
                return true;
            default:
                statusFlag = 0;
                archiveDamage = 0;
                archiveSpellLevel = 0;
                sigilName = "";
                return false;
        }
    }


    private static async ValueTask<(WorldSession Session, LegacyPlayerMagicReceiveAbortReason AbortReason)> TrySendLegacyCombatGearTharnCutterNoJumpEffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint targetUid,
        ushort targetX,
        ushort targetY,
        CancellationToken cancellationToken,
        WorldActiveSession? playerTarget = null)
    {
        var prisonSigil = HasLegacyStatusFlag(session.Character, LegacyCombatGearPrisonSigilFutilityCursedFlag);
        var whirlSigil = HasLegacyStatusFlag(session.Character, LegacyCombatGearWhirlSigilAuraCursedFlag);
        var top8 = HasLegacyStatusFlag(session.Character, LegacyCombatGearTop8WeeklyHawksAmbitionFlag);
        var top4 = HasLegacyStatusFlag(session.Character, LegacyCombatGearTop4WeeklyHawksAmbitionFlag);
        var top2 = HasLegacyStatusFlag(session.Character, LegacyCombatGearTop2WeeklyHawksAmbitionFlag);

        var effect = 0;
        var flagsToRemove = Array.Empty<int>();
        if (LegacyGetDistance(session.Character.X, session.Character.Y, targetX, targetY) <= 1 &&
            prisonSigil &&
            whirlSigil &&
            top8 &&
            top4 &&
            top2)
        {
            effect = 6;
            flagsToRemove =
            [
                LegacyCombatGearPrisonSigilFutilityCursedFlag,
                LegacyCombatGearWhirlSigilAuraCursedFlag,
                LegacyCombatGearTop8WeeklyHawksAmbitionFlag,
                LegacyCombatGearTop4WeeklyHawksAmbitionFlag,
                LegacyCombatGearTop2WeeklyHawksAmbitionFlag,
            ];
        }
        else if (prisonSigil && CheckLegacyCombatGearTharnCutterNoJumpRateRoll(session.Character))
        {
            effect = 1;
        }
        else if (whirlSigil && CheckLegacyCombatGearTharnCutterNoJumpRateRoll(session.Character))
        {
            effect = 2;
        }
        else if (top8 && CheckLegacyCombatGearTharnCutterNoJumpRateRoll(session.Character))
        {
            effect = 3;
        }
        else if (top4 && CheckLegacyCombatGearTharnCutterNoJumpRateRoll(session.Character))
        {
            effect = 4;
        }
        else if (top2 && CheckLegacyCombatGearTharnCutterNoJumpRateRoll(session.Character))
        {
            effect = 5;
        }

        if (effect == 0)
        {
            return (session, LegacyPlayerMagicReceiveAbortReason.None);
        }

        var updatedSession = session;
        if (flagsToRemove.Length != 0)
        {
            var updatedCharacter = WithoutLegacyStatusFlags(session.Character, flagsToRemove);
            updatedSession = session with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, updatedSession);
        }

        var updatedContext = context with { Session = updatedSession };
        if (flagsToRemove.Length != 0)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);
        }

        var uid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        if (effect is 1 or 6)
        {
            var prisonAnimation = new LegacySpellAnimationPacket(
                uid,
                uid,
                targetX,
                targetY,
                LegacyCombatGearTharnCutterPrisonSpellId,
                0,
                0,
                0,
                0,
                [new LegacySpellAnimationTarget(uid, 0, Hit: true, MsgAttackEffect.None)]);
            await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, prisonAnimation, cancellationToken)
                .ConfigureAwait(false);
        }

        if (effect is 3 or 6)
        {
            IReadOnlyList<LegacySpellAnimationTarget> top8Targets = [];
            if (playerTarget is { } activePlayerTarget)
            {
                var top8Outcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                    updatedContext,
                    activePlayerTarget,
                    updatedSession.Character,
                    1_000,
                    cancellationToken)
                .ConfigureAwait(false);
                if (top8Outcome.AttackerSession is { } currentAttacker)
                {
                    updatedSession = currentAttacker;
                    updatedContext = updatedContext with { Session = updatedSession };
                }

                if (top8Outcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
                {
                    return (updatedSession, top8Outcome.AbortReason);
                }

                if (top8Outcome.Result is { DamageApplied: not 0 } top8Result)
                {
                    top8Targets =
                    [
                        new LegacySpellAnimationTarget(
                            CheckedUInt32(
                                top8Result.UpdatedSession.Character.Uid,
                                nameof(top8Result.UpdatedSession.Character.Uid)),
                            top8Result.DamageApplied,
                            Hit: true,
                            MsgAttackEffect.None),
                    ];
                }
            }

            var top8Animation = new LegacySpellAnimationPacket(
                uid,
                0,
                targetX,
                targetY,
                LegacyCombatGearTharnCutterTop8SpellId,
                1,
                0,
                0,
                0,
                top8Targets);
            await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, top8Animation, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy CombatGear TharnCutter no-jump effect " +
                $"{FormatSession(updatedContext)} targetUid={targetUid} effect={effect} " +
                $"removedFlags={string.Join(',', flagsToRemove)}; " +
                "source=CombatGearAttack.cs case 18690 -> MyCombatGear.TharnCutter(jmp:false).")
            .ConfigureAwait(false);
        return (updatedSession, LegacyPlayerMagicReceiveAbortReason.None);
    }


    private static bool CheckLegacyCombatGearTharnCutterNoJumpRateRoll(CharacterSnapshot character)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyCombatGearTharnCutterNoJumpRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < LegacyCombatGearTharnCutterNoJumpRatePercent;
    }


    private static async ValueTask<WorldSession> TrySendLegacyCombatGearTharnCutterJumpEffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance target,
        CancellationToken cancellationToken)
    {
        var prisonSigil = HasLegacyStatusFlag(session.Character, LegacyCombatGearPrisonSigilFutilityCursedFlag);
        var whirlSigil = HasLegacyStatusFlag(session.Character, LegacyCombatGearWhirlSigilAuraCursedFlag);
        var top8 = HasLegacyStatusFlag(session.Character, LegacyCombatGearTop8WeeklyHawksAmbitionFlag);
        var top4 = HasLegacyStatusFlag(session.Character, LegacyCombatGearTop4WeeklyHawksAmbitionFlag);
        var top2 = HasLegacyStatusFlag(session.Character, LegacyCombatGearTop2WeeklyHawksAmbitionFlag);

        var effect = 0;
        var flagsToRemove = Array.Empty<int>();
        if (LegacyGetDistance(session.Character.X, session.Character.Y, target.X, target.Y) <= 1 &&
            prisonSigil &&
            whirlSigil &&
            top8 &&
            top4 &&
            top2)
        {
            effect = 6;
            flagsToRemove =
            [
                LegacyCombatGearPrisonSigilFutilityCursedFlag,
                LegacyCombatGearWhirlSigilAuraCursedFlag,
                LegacyCombatGearTop8WeeklyHawksAmbitionFlag,
                LegacyCombatGearTop4WeeklyHawksAmbitionFlag,
                LegacyCombatGearTop2WeeklyHawksAmbitionFlag,
            ];
        }
        else if (prisonSigil)
        {
            effect = 1;
            flagsToRemove = [LegacyCombatGearPrisonSigilFutilityCursedFlag];
        }
        else if (whirlSigil)
        {
            effect = 2;
            flagsToRemove = [LegacyCombatGearWhirlSigilAuraCursedFlag];
        }
        else if (top8)
        {
            effect = 3;
            flagsToRemove = [LegacyCombatGearTop8WeeklyHawksAmbitionFlag];
        }
        else if (top4)
        {
            effect = 4;
            flagsToRemove = [LegacyCombatGearTop4WeeklyHawksAmbitionFlag];
        }
        else if (top2)
        {
            effect = 5;
            flagsToRemove = [LegacyCombatGearTop2WeeklyHawksAmbitionFlag];
        }

        if (effect == 0)
        {
            return session;
        }

        var updatedCharacter = WithoutLegacyStatusFlags(session.Character, flagsToRemove);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        var uid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        if (effect is 1 or 6)
        {
            var prisonAnimation = new LegacySpellAnimationPacket(
                uid,
                uid,
                target.X,
                target.Y,
                LegacyCombatGearTharnCutterPrisonSpellId,
                0,
                0,
                0,
                0,
                [new LegacySpellAnimationTarget(uid, 0, Hit: true, MsgAttackEffect.None)]);
            await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, prisonAnimation, cancellationToken)
                .ConfigureAwait(false);
        }

        if (effect is 3 or 6)
        {
            var top8Animation = new LegacySpellAnimationPacket(
                uid,
                0,
                target.X,
                target.Y,
                LegacyCombatGearTharnCutterTop8SpellId,
                1,
                0,
                0,
                0,
                []);
            await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, top8Animation, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy CombatGear TharnCutter jump effect " +
                $"{FormatSession(updatedContext)} monsterUid={target.Uid} effect={effect} " +
                $"removedFlags={string.Join(',', flagsToRemove)}; source=MyCombatGear.CheckCombatGear -> MyCombatGear.TharnCutter(jmp:true).")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
