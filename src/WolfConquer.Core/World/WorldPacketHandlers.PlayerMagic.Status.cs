using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacySupermanSpellId = 1_025;
    private const ushort LegacyCycloneSpellId = 1_110;
    private const ushort LegacyFatalStrikeAttachStatusSpellId = 6_011;
    private const ushort LegacySuperCycloneSpellId = 11_970;
    private static readonly TimeSpan LegacyCycloneDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan LegacyFatalStrikeDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LegacySuperCycloneDuration = TimeSpan.FromSeconds(40);

    private static async ValueTask<bool> HandleLegacyPlayerAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            return false;
        }

        if (await TryHandleLegacyCombatGearSelfStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerXpBuffAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerClassicBuffAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (TryGetLegacyAttachStatusOpenXpSkill(
                spell.Id,
                out var xpSkillStatusFlag,
                out var xpSkillExpiresAtAttribute,
                out var xpSkillDuration,
                out var xpSkillName,
                out var xpSkillRestoresStaminaToMaximum))
        {
            return await HandleLegacyPlayerOpenXpSkillMagicAsync(
                    context,
                    query,
                    playerSpell,
                    spell,
                    session,
                    xpSkillStatusFlag,
                    xpSkillExpiresAtAttribute,
                    xpSkillDuration,
                    xpSkillName,
                    xpSkillRestoresStaminaToMaximum,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryGetLegacySnowAttachStatusDefinition(spell.Id, out var snow))
        {
            return await HandleLegacyPlayerSnowAttachStatusMagicAsync(
                    context,
                    query,
                    playerSpell,
                    spell,
                    session,
                    snow,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (await HandleLegacyPlayerInfiniteMistAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerHawksAmbitionAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerRevengeTailAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerInfinityAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSacrificeAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerRiseOfTaoismAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerIronGuardAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerIronShieldAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerFineRainAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerMagicDefenderAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerCrackStarAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerBackfireAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPathOfShadowAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerDefensiveStanceAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerLightningShieldAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerImmortalForceAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerPoisonStarAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerSoulShackleAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerDragonSwingAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerAutoHuntAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerFlyAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerCastPrayAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerIntensifyAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (await HandleLegacyPlayerMudWallAttachStatusMagicAsync(
                context,
                query,
                playerSpell,
                spell,
                session,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        if (!TryGetLegacyAttachStatusFlag(spell.Id, out var statusFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player attach-status magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; unsupported attach status spell.")
                .ConfigureAwait(false);
            return true;
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
        var casterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        if (query.OpponentUid == 0)
        {
            var noTargetAnimation = LegacySpellAnimationPacket.ForPlayerMagic(
                casterUid,
                query.X,
                query.Y,
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                []);
            await SendPlayerSpellAnimationToVisibleAsync(
                    resourceContext,
                    resourceSession,
                    noTargetAnimation,
                    cancellationToken)
                .ConfigureAwait(false);

            await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                    resourceContext,
                    resourceSession,
                    playerSpell,
                    spell,
                    spell.Duration,
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy player attach-status magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} statusFlag={statusFlag} " +
                    $"targetUid=0 duration={spell.Duration}.")
                .ConfigureAwait(false);
            return true;
        }

        var targetIsCaster = query.OpponentUid == casterUid;
        var targetConnection = context.Connection;
        var targetSession = resourceSession;
        if (!targetIsCaster)
        {
            if (!TryGetVisibleSessionByUid(resourceContext, resourceSession, query.OpponentUid, out var activeTarget))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped legacy player attach-status magic " +
                        $"{FormatSession(resourceContext)} spellId={spell.Id} targetUid={query.OpponentUid}; target is not visible.")
                    .ConfigureAwait(false);
                return true;
            }

            targetConnection = activeTarget.Connection;
            targetSession = activeTarget.Session;
        }

        if (spell.Id == 1_090 && HasLegacyStatusFlag(targetSession.Character, LegacyMagicShieldFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player attach-status magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id}; status flag is already active.")
                .ConfigureAwait(false);
            return true;
        }

        targetSession = targetIsCaster ? resourceSession : targetSession;
        var updatedTargetCharacter = WithLegacyStatusFlag(targetSession.Character, statusFlag);
        var updatedTargetSession = targetSession with
        {
            Character = updatedTargetCharacter,
        };
        context.Sessions?.Set(targetConnection, updatedTargetSession);

        var targetContext = context with
        {
            Connection = targetConnection,
            Session = updatedTargetSession,
        };
        await SendPlayerStatusFlagVectorToVisibleAsync(targetContext, updatedTargetSession, cancellationToken)
            .ConfigureAwait(false);

        var targetUid = CheckedUInt32(updatedTargetSession.Character.Uid, nameof(updatedTargetSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(targetUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(targetContext, updatedTargetSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var experienceSession = targetIsCaster ? updatedTargetSession : resourceSession;
        await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                experienceSession,
                playerSpell,
                spell,
                spell.Duration,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy player attach-status magic " +
                $"{FormatSession(context)} spellId={spell.Id} statusFlag={statusFlag} duration={spell.Duration}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> HandleLegacyPlayerSnowAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        LegacySnowAttachStatusDefinition snow,
        CancellationToken cancellationToken)
    {
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

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var resourceContext = context with { Session = resourceSession };
        var interaction = new MsgAttackPacket(query with { Uid = casterUid });
        await resourceContext.Connection.SendAsync(interaction.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        if (HasLegacyStatusFlag(resourceSession.Character, snow.StatusFlag))
        {
            var toggledSession = resourceSession with
            {
                Character = WithoutLegacyStatusFlag(resourceSession.Character, snow.StatusFlag),
            };
            context.Sessions?.Set(context.Connection, toggledSession);
            var toggledContext = context with { Session = toggledSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(toggledContext, toggledSession, cancellationToken)
                .ConfigureAwait(false);
            if (snow.SendEquipmentRefreshOnRemove)
            {
                await SendPlayerEquipmentAppearanceRefreshAsync(toggledContext, toggledSession, cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    $"WorldServer handled legacy {snow.Name} attach-status toggle " +
                    $"{FormatSession(toggledContext)} spellId={spell.Id} removedStatusFlag={snow.StatusFlag} " +
                    $"equipmentRefreshPackets={(snow.SendEquipmentRefreshOnRemove ? 1 : 0)} " +
                    $"equipmentQueryMode={snow.EquipmentQueryMode}.")
                .ConfigureAwait(false);
            return true;
        }

        var hadFirstClearedStatus = HasLegacyStatusFlag(resourceSession.Character, snow.FirstClearedStatusFlag);
        var hadSecondClearedStatus = HasLegacyStatusFlag(resourceSession.Character, snow.SecondClearedStatusFlag);
        var clearedHealingSnow =
            GetLegacySnowClearedStatus(LegacyHealingSnowFlag, snow, hadFirstClearedStatus, hadSecondClearedStatus);
        var clearedChillingSnow =
            GetLegacySnowClearedStatus(LegacyChillingSnowFlag, snow, hadFirstClearedStatus, hadSecondClearedStatus);
        var clearedFreezingPelter =
            GetLegacySnowClearedStatus(LegacyFreezingPelterFlag, snow, hadFirstClearedStatus, hadSecondClearedStatus);
        var targetIsCaster = query.OpponentUid == casterUid;
        var updatedCharacter = resourceSession.Character;
        if (hadFirstClearedStatus)
        {
            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, snow.FirstClearedStatusFlag);
        }

        if (hadSecondClearedStatus)
        {
            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, snow.SecondClearedStatusFlag);
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        if (targetIsCaster)
        {
            updatedCharacter = WithLegacyStatusFlag(updatedCharacter, snow.StatusFlag);
            animationTargets.Add(new LegacySpellAnimationTarget(casterUid, 0, Hit: true, snow.AnimationEffect));
        }

        var updatedSession = resourceSession;
        var updatedContext = context with { Session = updatedSession };
        if (hadFirstClearedStatus || hadSecondClearedStatus || targetIsCaster)
        {
            updatedSession = resourceSession with
            {
                Character = updatedCharacter,
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);
        }

        if (targetIsCaster && snow.SendEquipmentRefreshOnAdd)
        {
            await SendPlayerEquipmentAppearanceRefreshAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                snow.SpellExperienceDamage(spell),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer handled legacy {snow.Name} attach-status magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} statusFlag={snow.StatusFlag} " +
                $"targetUid={query.OpponentUid} selfTarget={targetIsCaster} " +
                $"clearedStatusFlag{snow.FirstClearedStatusFlag}={hadFirstClearedStatus} " +
                $"clearedStatusFlag{snow.SecondClearedStatusFlag}={hadSecondClearedStatus} " +
                $"clearedHealingSnow={clearedHealingSnow} clearedChillingSnow={clearedChillingSnow} " +
                $"clearedFreezingPelter={clearedFreezingPelter} " +
                $"updateDamage={snow.SpellExperienceDamage(spell)} " +
                $"equipmentRefreshPackets={(targetIsCaster && snow.SendEquipmentRefreshOnAdd ? 1 : 0)} " +
                $"equipmentQueryMode={snow.EquipmentQueryMode}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool GetLegacySnowClearedStatus(
        int statusFlag,
        LegacySnowAttachStatusDefinition snow,
        bool hadFirstClearedStatus,
        bool hadSecondClearedStatus)
    {
        if (snow.FirstClearedStatusFlag == statusFlag)
        {
            return hadFirstClearedStatus;
        }

        return snow.SecondClearedStatusFlag == statusFlag && hadSecondClearedStatus;
    }

    private static bool TryGetLegacySnowAttachStatusDefinition(
        ushort spellId,
        out LegacySnowAttachStatusDefinition snow)
    {
        snow = spellId switch
        {
            LegacyHealingSnowSpellId => new LegacySnowAttachStatusDefinition(
                "HealingSnow",
                LegacyHealingSnowFlag,
                LegacyChillingSnowFlag,
                LegacyFreezingPelterFlag,
                MsgAttackEffect.None,
                "none",
                SendEquipmentRefreshOnAdd: false,
                SendEquipmentRefreshOnRemove: false,
                static spell => spell.Duration),
            LegacyChillingSnowSpellId => new LegacySnowAttachStatusDefinition(
                "ChillingSnow",
                LegacyChillingSnowFlag,
                LegacyHealingSnowFlag,
                LegacyFreezingPelterFlag,
                MsgAttackEffect.None,
                "none",
                SendEquipmentRefreshOnAdd: false,
                SendEquipmentRefreshOnRemove: false,
                static _ => 500),
            LegacyFreezingPelterSpellId => new LegacySnowAttachStatusDefinition(
                "FreezingPelter",
                LegacyFreezingPelterFlag,
                LegacyHealingSnowFlag,
                LegacyChillingSnowFlag,
                MsgAttackEffect.ResistWood,
                "local QueryEquipment-equivalent equipment refresh on add; removal CallItems:false suppresses appearance refresh",
                SendEquipmentRefreshOnAdd: true,
                SendEquipmentRefreshOnRemove: false,
                static spell => spell.Duration),
            _ => default,
        };

        return !string.IsNullOrEmpty(snow.Name);
    }

    private readonly record struct LegacySnowAttachStatusDefinition(
        string Name,
        int StatusFlag,
        int FirstClearedStatusFlag,
        int SecondClearedStatusFlag,
        MsgAttackEffect AnimationEffect,
        string EquipmentQueryMode,
        bool SendEquipmentRefreshOnAdd,
        bool SendEquipmentRefreshOnRemove,
        Func<LegacyMagicRuntimeSpell, ulong> SpellExperienceDamage);

    private static async ValueTask<bool> HandleLegacySourceSilentPlayerMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var capability = LegacyMagicSortCapabilities.Classify(spell.Sort, spell.Id);
        if (capability.Status != LegacyMagicModelingStatus.SourceSilentNoOp)
        {
            return false;
        }

        await context.Log.WriteLineAsync(
                "WorldServer skipped source-silent legacy player magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetUid={query.OpponentUid}; {capability.Diagnostic}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryGetLegacyAttachStatusOpenXpSkill(
        ushort spellId,
        out int statusFlag,
        out string expiresAtAttribute,
        out TimeSpan duration,
        out string skillName,
        out bool restoreStaminaToMaximum)
    {
        statusFlag = spellId switch
        {
            LegacyCycloneSpellId => LegacyCycloneFlag,
            LegacySupermanSpellId => LegacySupermanFlag,
            LegacyFatalStrikeAttachStatusSpellId => LegacyFatalStrikeFlag,
            LegacySuperCycloneSpellId => LegacySuperCycloneFlag,
            _ => -1,
        };

        expiresAtAttribute = spellId switch
        {
            LegacyCycloneSpellId => RuntimeLegacyCycloneExpiresAtUtcAttribute,
            LegacySupermanSpellId => RuntimeLegacySupermanExpiresAtUtcAttribute,
            LegacyFatalStrikeAttachStatusSpellId => RuntimeLegacyFatalStrikeExpiresAtUtcAttribute,
            LegacySuperCycloneSpellId => RuntimeLegacySuperCycloneExpiresAtUtcAttribute,
            _ => string.Empty,
        };

        duration = spellId switch
        {
            LegacyFatalStrikeAttachStatusSpellId => LegacyFatalStrikeDuration,
            LegacySuperCycloneSpellId => LegacySuperCycloneDuration,
            _ => LegacyCycloneDuration,
        };
        skillName = spellId switch
        {
            LegacyCycloneSpellId => "Cyclone",
            LegacySupermanSpellId => "Superman",
            LegacyFatalStrikeAttachStatusSpellId => "FatalStrike",
            LegacySuperCycloneSpellId => "SuperCyclone",
            _ => string.Empty,
        };
        restoreStaminaToMaximum = spellId == LegacySuperCycloneSpellId;

        return statusFlag >= 0;
    }

    private static bool TryGetLegacyAttachStatusFlag(ushort spellId, out int statusFlag)
    {
        statusFlag = spellId switch
        {
            30_000 => LegacyAzureShieldFlag,
            _ => -1,
        };

        return statusFlag >= 0;
    }
}
