using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacySoulShackleExpiresAtUtcAttribute = "LegacySoulShackleExpiresAtUtc";
    internal const string RuntimeLegacySoulShackleRemoveOnDeadAttribute = "LegacySoulShackleRemoveOnDead";
    internal const string RuntimeLegacySoulShackleSturdinessRollAttribute = "LegacySoulShackleSturdinessRoll";
    internal const string RuntimeLegacySoulShackleFreeSoulRollAttribute = "LegacySoulShackleFreeSoulRoll";

    private const ushort LegacySoulShackleAttachStatusSpellId = 10_405;
    private const int LegacySacredBlessingStatusFlag = 0xFF;
    private const string LegacySoulShackleBattlePowerMessage =
        "Your BattlePower is lower than your opponent get a better BattlePower to be able to use this skill on him.";

    internal const int LegacySoulShackleStatusFlag = 0x6F;

    private static async ValueTask<bool> HandleLegacyPlayerSoulShackleAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacySoulShackleAttachStatusSpellId)
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var resourceContext = context with { Session = resourceSession };
        if (IsLegacySoulShackleGuildWarCastBlocked(resourceContext, resourceSession))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer blocked legacy SoulShackle AttachStatus magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id}; " +
                    "source NEWGUILDWAR ExorcimPillar owner-guild authorization failed.")
                .ConfigureAwait(false);
            return true;
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var targetMatched = TryGetVisibleSessionByUid(
            resourceContext,
            resourceSession,
            query.OpponentUid,
            out var activeTarget);
        var targetApplied = false;
        var effectiveDuration = spell.Duration;

        if (targetMatched)
        {
            var targetSession = activeTarget.Session;
            if (targetSession.Character.MinHitPoints > 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer blocked legacy SoulShackle AttachStatus magic " +
                        $"{FormatSession(resourceContext)} spellId={spell.Id} targetUid={query.OpponentUid}; " +
                        "source requires a dead visible-player target.")
                    .ConfigureAwait(false);
                return true;
            }

            var hasSoulShackle = HasLegacyStatusFlag(
                targetSession.Character,
                LegacySoulShackleStatusFlag);
            var hasSacredBlessing = HasLegacyStatusFlag(
                targetSession.Character,
                LegacySacredBlessingStatusFlag);
            if (!hasSoulShackle && !hasSacredBlessing)
            {
                if (IsLegacySoulShackleBattlePowerBlocked(resourceSession, targetSession))
                {
                    await context.Connection.SendAsync(
                            LegacyMsgMessagePacket.SystemTalk(LegacySoulShackleBattlePowerMessage).ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer blocked legacy SoulShackle AttachStatus magic " +
                            $"{FormatSession(resourceContext)} spellId={spell.Id} targetUid={query.OpponentUid}; " +
                            "target BattlePower exceeds caster BattlePower by at least 20.")
                        .ConfigureAwait(false);
                    return true;
                }

                if (HasLegacySoulShackleSturdinessAvoidance(targetSession, context.WorldData))
                {
                    await SendLegacySoulShackleAnimationAsync(
                            resourceContext,
                            resourceSession,
                            query,
                            playerSpell,
                            spell,
                            animationTargets,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled legacy SoulShackle Sturdiness avoidance " +
                            $"{FormatSession(resourceContext)} spellId={spell.Id} targetUid={query.OpponentUid}; " +
                            "sent empty animation without spell progress.")
                        .ConfigureAwait(false);
                    return true;
                }

                if (HasLegacySoulShackleFreeSoulAvoidance(targetSession.Character))
                {
                    var freeSoulTargetUid = CheckedUInt32(
                        targetSession.Character.Uid,
                        nameof(targetSession.Character.Uid));
                    await SendFrameToPlayerAndVisiblePeersAsync(
                            resourceContext,
                            activeTarget.Connection,
                            targetSession,
                            new LegacyMsgRefineEffectPacket(
                                    freeSoulTargetUid,
                                    freeSoulTargetUid,
                                    LegacyRefineEffect.FreeSoul)
                                .ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await SendLegacySoulShackleAnimationAsync(
                            resourceContext,
                            resourceSession,
                            query,
                            playerSpell,
                            spell,
                            animationTargets,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled legacy SoulShackle FreeSoul avoidance " +
                            $"{FormatSession(resourceContext)} spellId={spell.Id} targetUid={query.OpponentUid}; " +
                            "sent refine effect and empty animation without spell progress.")
                        .ConfigureAwait(false);
                    return true;
                }

                effectiveDuration = CalculateLegacySoulShackleDuration(
                    targetSession,
                    context.WorldData,
                    spell.Duration);
                var updatedTargetSession = ApplyLegacySoulShackleStatus(
                    targetSession,
                    effectiveDuration,
                    DateTimeOffset.UtcNow);
                context.Sessions?.Set(activeTarget.Connection, updatedTargetSession);
                var targetUid = CheckedUInt32(
                    updatedTargetSession.Character.Uid,
                    nameof(updatedTargetSession.Character.Uid));

                await activeTarget.Connection.SendAsync(
                        new LegacyMsgUpdatePacket(
                            targetUid,
                            [new LegacyMsgUpdateEntry(
                                LegacyMsgUpdateDataType.SoulShackle,
                                Value: 0,
                                Flag: LegacySoulShackleStatusFlag,
                                Time: effectiveDuration,
                                Damage: 0,
                                SpellLevel: playerSpell.Level)])
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        resourceContext,
                        activeTarget.Connection,
                        updatedTargetSession,
                        cancellationToken)
                    .ConfigureAwait(false);

                animationTargets.Add(new LegacySpellAnimationTarget(
                    targetUid,
                    Damage: 0,
                    Hit: true,
                    MsgAttackEffect.None));
                targetApplied = true;
            }
        }

        var progressSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                resourceContext,
                resourceSession,
                playerSpell,
                spell,
                spell.Duration,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacySoulShackleAnimationAsync(
                resourceContext with { Session = progressSession },
                progressSession,
                query,
                playerSpell,
                spell,
                animationTargets,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy SoulShackle AttachStatus magic " +
                $"{FormatSession(resourceContext with { Session = progressSession })} spellId={spell.Id} " +
                $"targetUid={query.OpponentUid} targetMatched={targetMatched} targetApplied={targetApplied} " +
                $"statusFlag={LegacySoulShackleStatusFlag} updateType={(uint)LegacyMsgUpdateDataType.SoulShackle} " +
                $"effectiveDuration={effectiveDuration} progressDamage={spell.Duration} animationTargets={animationTargets.Count}; " +
                "source=AttachStatus.cs case 10405 and MsgUpdate.cs DataType.SoulShackle.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacySoulShackleAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacySpellAnimationTarget> targets,
        CancellationToken cancellationToken)
    {
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            targets);
        await SendPlayerSpellAnimationToVisibleAsync(
                context,
                session,
                animation,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static WorldSession ApplyLegacySoulShackleStatus(
        WorldSession session,
        uint durationSeconds,
        DateTimeOffset now)
    {
        var expiresAt = now
            .ToUniversalTime()
            .AddSeconds(unchecked((int)durationSeconds))
            .ToString("O", CultureInfo.InvariantCulture);
        var character = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(session.Character, LegacySoulShackleStatusFlag),
                RuntimeLegacySoulShackleExpiresAtUtcAttribute,
                expiresAt),
            RuntimeLegacySoulShackleRemoveOnDeadAttribute,
            bool.TrueString);
        return session with { Character = character };
    }

    private static uint CalculateLegacySoulShackleDuration(
        WorldSession target,
        WorldDataCatalog? worldData,
        uint duration)
    {
        if (!TryGetLegacyEquippedRuneLevel(target.PlayerItems, worldData, "Evocation", out var level))
        {
            return duration;
        }

        var reduction = unchecked((byte)(20 + level * 5));
        return unchecked(duration - reduction);
    }

    private static bool HasLegacySoulShackleSturdinessAvoidance(
        WorldSession target,
        WorldDataCatalog? worldData)
    {
        if (!TryGetLegacyEquippedRuneLevel(target.PlayerItems, worldData, "Sturdiness", out var level))
        {
            return false;
        }

        var rate = unchecked((byte)(5 + level * 5));
        return CheckLegacyPoisonStarRateRoll(
            target.Character.Attributes,
            rate,
            RuntimeLegacySoulShackleSturdinessRollAttribute,
            "SoulShackleSturdinessRoll",
            "LegacySturdinessRoll",
            "SturdinessRoll");
    }

    private static bool HasLegacySoulShackleFreeSoulAvoidance(CharacterSnapshot target)
    {
        if (!TryReadDoubleAttribute(
                target.Attributes,
                out var chance,
                "PerfectionStatus.FreeSoul",
                "LegacyPerfectionStatus.FreeSoul",
                "PerfectionFreeSoul",
                "FreeSoul") ||
            chance <= 0)
        {
            return false;
        }

        if (chance >= 100)
        {
            return true;
        }

        var roll = TryReadDoubleAttribute(
                target.Attributes,
                out var forcedRoll,
                RuntimeLegacySoulShackleFreeSoulRollAttribute,
                "SoulShackleFreeSoulRoll",
                "LegacyFreeSoulRoll",
                "FreeSoulRoll")
            ? forcedRoll
            : Random.Shared.NextDouble() * 100.0;
        return roll < chance;
    }

    private static bool IsLegacySoulShackleBattlePowerBlocked(
        WorldSession caster,
        WorldSession target)
    {
        var casterBattlePower = CalculateLegacySoulShackleBattlePower(caster);
        var targetBattlePower = CalculateLegacySoulShackleBattlePower(target);
        return targetBattlePower >= casterBattlePower &&
            targetBattlePower - casterBattlePower >= 20;
    }

    private static ulong CalculateLegacySoulShackleBattlePower(WorldSession session)
    {
        var character = session.Character;
        var battlePower = ReadAttributeUInt64(character.Attributes, "BattlePower", "BattlePowerValue");
        if (battlePower != 0)
        {
            return battlePower;
        }

        var guildBattlePower = ReadAttributeUInt64(character.Attributes, "GuildBattlePower");
        var rawExtraBattlePower = ReadAttributeUInt64(character.Attributes, "ExtraBattlePower");
        var extraBattlePower = rawExtraBattlePower > guildBattlePower
            ? rawExtraBattlePower - guildBattlePower
            : 0;
        var levelPower = Math.Max(0, character.Level + (character.Reborn * 5));
        return checked((ulong)levelPower +
            extraBattlePower +
            LegacyBattlePowerCalculator.CalculateEquipmentBattlePower(
                session.PlayerItems,
                session.AlternateEquipmentActive));
    }

    private static bool IsLegacySoulShackleGuildWarCastBlocked(
        WorldPacketHandlerContext context,
        WorldSession caster)
    {
        if (caster.Character.MapId != LegacySobNpcEventRules.GuildWarMapId ||
            !IsLegacyNewGuildWarProcessAlive(caster.Character, context.GuildWarStates))
        {
            return false;
        }

        var casterGuildId = ReadAttributeUInt64(caster.Character.Attributes, "GuildID", "GuildId");
        if (casterGuildId == 0)
        {
            return true;
        }

        var ownerGuildId = ReadAttributeUInt64(
            caster.Character.Attributes,
            "LegacyNewGuildWarExorcimPillarGuildID",
            "NewGuildWarExorcimPillarGuildID",
            "ExorcimPillarGuildID");
        if (ownerGuildId == 0 &&
            context.WorldData?.SobNpcsById.TryGetValue(
                LegacySobNpcEventRules.GuildWarExorcimPillarUid,
                out var pillars) == true)
        {
            var pillar = pillars.FirstOrDefault(candidate =>
                candidate.MapId == LegacySobNpcEventRules.GuildWarMapId);
            if (pillar is not null)
            {
                ownerGuildId = ResolveLegacyNewGuildWarSobOwnerGuildId(pillar, context.SobNpcStates);
            }
        }

        return ownerGuildId != casterGuildId &&
            !ContainsLegacyRelationId(
                caster.Character.Attributes,
                ownerGuildId,
                "GuildAllyIDs",
                "GuildAllyIds",
                "GuildAllyGuildIDs",
                "GuildAllyGuildIds",
                "AllyGuildIDs",
                "AllyGuildIds");
    }
}
