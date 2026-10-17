using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyCombatGearPlayerReceiveUpdateTargetAttempts = 2;


    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession, int Applied,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TrySendLegacyCombatGearUpdateTargetPlayerAttemptAsync(
            WorldPacketHandlerContext defenderContext,
            ConquerTcpConnection attackerConnection,
            WorldSession attackerSession,
            WorldSession defenderSession,
            int attempt,
            CancellationToken cancellationToken)
    {
        var attackerUid = attackerSession.Character.Uid;
        var defenderUid = defenderSession.Character.Uid;
        var updatedDefenderSession = defenderSession;
        var updatedAttackerSession = attackerSession;
        var applied = 0;

        foreach (var spellId in EnumerateLegacyCombatGearUpdateTargetSpells(updatedDefenderSession))
        {
            if (spellId is LegacyCombatGearMagneticLightSpellId or LegacyCombatGearAblazeBladeSpellId)
            {
                (updatedDefenderSession, updatedAttackerSession, var appliedDamageSpell, var abortReason) =
                    await TrySendLegacyCombatGearUpdateTargetPlayerDamageSpellAsync(
                            defenderContext,
                            attackerConnection,
                            updatedAttackerSession,
                            updatedDefenderSession,
                            spellId,
                            attempt,
                            cancellationToken)
                        .ConfigureAwait(false);
                if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
                {
                    return (updatedDefenderSession, updatedAttackerSession, applied, abortReason);
                }

                defenderContext = defenderContext with { Session = updatedDefenderSession };
                if (appliedDamageSpell)
                {
                    applied++;
                }
            }
            else
            {
                var (spellSession, appliedSpell) = await TrySendLegacyCombatGearUpdateTargetPlayerStatusSpellAsync(
                        defenderContext,
                        updatedDefenderSession,
                        spellId,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedDefenderSession = spellSession;
                var abortReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
                    ref defenderContext, attackerConnection, ref updatedAttackerSession,
                    ref updatedDefenderSession, attackerUid, defenderUid);
                if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
                {
                    return (updatedDefenderSession, updatedAttackerSession, applied, abortReason);
                }
                defenderContext = defenderContext with { Session = updatedDefenderSession };
                if (appliedSpell)
                {
                    applied++;
                }
            }
        }

        return (updatedDefenderSession, updatedAttackerSession, applied, LegacyPlayerMagicReceiveAbortReason.None);
    }

    private static async ValueTask<(WorldSession Session, bool Applied)>
        TrySendLegacyCombatGearUpdateTargetPlayerStatusSpellAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            ushort spellId,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !TryResolveLegacyCombatGearUpdateTargetSpell(session, worldData, spellId, out var playerSpell, out var spell) ||
            !CheckLegacyCombatGearPassiveRateRoll(session.Character, worldData, spellId, playerSpell.Level))
        {
            return (session, false);
        }

        return spellId switch
        {
            LegacyCombatGearArmorOfImmunitySpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyCombatGearLightningMagicShieldTop4WeeklyFlag,
                        duration: 2,
                        sendArchiveSkill: true,
                        archiveVisible: false,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken)
                    .ConfigureAwait(false), true),
            LegacyCombatGearHunterSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyCombatGearLightningMagicShieldTop4WeeklyFlag,
                        duration: 10,
                        sendArchiveSkill: false,
                        archiveVisible: false,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearDivineEmptinessSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyAzureShieldFlag | LegacyPaperDanceFlag,
                        spell.Duration,
                        sendArchiveSkill: true,
                        archiveVisible: true,
                        archiveDamage: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 35),
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearWaterAegisSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyAzureShieldFlag,
                        duration: 10,
                        sendArchiveSkill: false,
                        archiveVisible: false,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearFlowKnackSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyTopWaterTaoistFlag | LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag,
                        spell.Duration,
                        sendArchiveSkill: true,
                        archiveVisible: true,
                        archiveDamage: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 10),
                        archiveSpellLevel: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 44),
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearSolidBulwarkSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyFireAuraFlag | LegacyHawksAmbitionFlag,
                        duration: 5,
                        sendArchiveSkill: true,
                        archiveVisible: false,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: false,
                        cancellationToken)
                    .ConfigureAwait(false), true),
            LegacyCombatGearFantasyKnackSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyBlackbeardsRageFlag | LegacyHawksAmbitionFlag,
                        spell.Duration,
                        sendArchiveSkill: true,
                        archiveVisible: true,
                        archiveDamage: 0,
                        archiveSpellLevel: 0,
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            LegacyCombatGearFlameShieldSpellId =>
                (await SendLegacyCombatGearPassiveStatusAsync(
                        context,
                        session,
                        playerSpell,
                        spellId,
                        LegacyConfusedFlag | LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag,
                        spell.Duration,
                        sendArchiveSkill: true,
                        archiveVisible: true,
                        archiveDamage: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 35),
                        archiveSpellLevel: ReadLegacyMagicUInt32FieldOrDefault(worldData, spellId, playerSpell.Level, 36),
                        animationTargetSelf: true,
                        cancellationToken,
                        sendEquipmentRefresh: true)
                    .ConfigureAwait(false), true),
            _ => (session, false),
        };
    }

    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession, bool Applied,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TrySendLegacyCombatGearUpdateTargetPlayerDamageSpellAsync(
            WorldPacketHandlerContext defenderContext,
            ConquerTcpConnection attackerConnection,
            WorldSession attackerSession,
            WorldSession defenderSession,
            ushort spellId,
            int attempt,
            CancellationToken cancellationToken)
    {
        if (defenderContext.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(defenderSession.PlayerSpells, worldData,
                spellId, out var playerSpell, out var spell) ||
            !CheckLegacyCombatGearPassiveRateRoll(defenderSession.Character, worldData, spellId, playerSpell.Level))
        {
            return (defenderSession, attackerSession, false, LegacyPlayerMagicReceiveAbortReason.None);
        }

        var admission = await TryAdmitLegacyCombatGearNestedPlayerDamageProcAsync(
                defenderContext, attackerConnection, attackerSession, defenderSession, spell, cancellationToken)
            .ConfigureAwait(false);
        defenderSession = admission.DefenderSession;
        attackerSession = admission.AttackerSession;
        if (admission.AbortReason != LegacyPlayerMagicReceiveAbortReason.None || !admission.Admitted)
            return (defenderSession, attackerSession, false, admission.AbortReason);
        defenderContext = defenderContext with { Session = defenderSession };

        return spellId switch
        {
            LegacyCombatGearMagneticLightSpellId =>
                await TrySendLegacyCombatGearMagneticLightPlayerAsync(
                        defenderContext,
                        attackerConnection,
                        attackerSession,
                        defenderSession,
                        playerSpell,
                        spell,
                        attempt,
                        cancellationToken)
                    .ConfigureAwait(false),
            LegacyCombatGearAblazeBladeSpellId =>
                await TrySendLegacyCombatGearAblazeBladePlayerAsync(
                        defenderContext,
                        attackerConnection,
                        attackerSession,
                        defenderSession,
                        playerSpell,
                        spell,
                        attempt,
                        cancellationToken)
                    .ConfigureAwait(false),
            _ => (defenderSession, attackerSession, false, LegacyPlayerMagicReceiveAbortReason.None),
        };
    }

    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession, bool Applied,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TrySendLegacyCombatGearAblazeBladePlayerAsync(
            WorldPacketHandlerContext defenderContext,
            ConquerTcpConnection attackerConnection,
            WorldSession attackerSession,
            WorldSession defenderSession,
            PlayerSpellSnapshotEntry playerSpell,
            LegacyMagicRuntimeSpell spell,
            int attempt,
            CancellationToken cancellationToken)
    {
        var attackerUid = attackerSession.Character.Uid;
        var defenderUid = defenderSession.Character.Uid;

        var targetGate = await TryEnterLegacyCombatGearNestedPlayerTargetAsync(
                defenderContext, attackerConnection, attackerSession, defenderSession, spell, cancellationToken)
            .ConfigureAwait(false);
        defenderSession = targetGate.DefenderSession;
        attackerSession = targetGate.AttackerSession;
        defenderContext = defenderContext with { Session = defenderSession };
        if (targetGate.AbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            return (defenderSession, attackerSession, false, targetGate.AbortReason);
        if (!targetGate.Allowed)
        {
            var empty = await SendLegacyCombatGearNestedPlayerAnimationAsync(
                    defenderContext, attackerConnection, attackerSession, defenderSession,
                    playerSpell, spell, [], cancellationToken)
                .ConfigureAwait(false);
            return (empty.DefenderSession, empty.AttackerSession, true, empty.AbortReason);
        }

        var damage = CalculateLegacyPlayerPlayerMagicDamage(
            defenderSession,
            attackerSession,
            spell,
            defenderContext.WorldData);
        damage = MulDivToUInt32(damage, 75, 100);

        var outcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                defenderContext,
                new WorldActiveSession(attackerConnection, attackerSession),
                defenderSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
        if (outcome.Result is not { } result)
        {
            // The nested counter's roles are reversed relative to this incoming hit.
            var reason = outcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost
                ? LegacyPlayerMagicReceiveAbortReason.TargetLost
                : LegacyPlayerMagicReceiveAbortReason.AttackerLost;
            var refreshedReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
                ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
                attackerUid, defenderUid);
            if (refreshedReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                reason = refreshedReason; // Original incoming attacker loss has priority.
            }

            return (defenderSession, attackerSession, false, reason);
        }

        attackerSession = result.UpdatedSession;
        defenderSession = outcome.AttackerSession ?? result.UpdatedAttackerSession ?? defenderSession;
        defenderContext = defenderContext with { Session = defenderSession };
        var abortReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (defenderSession, attackerSession, false, abortReason);
        }

        result = result with
        {
            ActiveSession = result.ActiveSession with { Session = attackerSession },
            UpdatedSession = attackerSession,
            UpdatedAttackerSession = defenderSession,
        };

        var targetUid = CheckedUInt32(attackerSession.Character.Uid, nameof(attackerSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(defenderSession.Character.Uid, nameof(defenderSession.Character.Uid)),
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(targetUid, result.DamageApplied, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(defenderContext, defenderSession, animation, cancellationToken)
            .ConfigureAwait(false);

        abortReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (defenderSession, attackerSession, true, abortReason);
        }

        result = result with
        {
            ActiveSession = result.ActiveSession with { Session = attackerSession },
            UpdatedSession = attackerSession,
        };
        await SendLegacyPlayerMagicDamageUpdatesAsync(defenderContext, defenderSession, result, cancellationToken)
            .ConfigureAwait(false);

        abortReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (defenderSession, attackerSession, true, abortReason);
        }

        await LogLegacyCombatGearUpdateTargetPlayerDamageBranchAsync(
                defenderContext,
                attackerSession,
                defenderSession,
                spell.Id,
                "AblazeBlade",
                attempt,
                result.DamageApplied,
                pushed: false)
            .ConfigureAwait(false);
        abortReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (defenderSession, attackerSession, true, abortReason);
        }

        return (defenderSession, attackerSession, true, LegacyPlayerMagicReceiveAbortReason.None);
    }

    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession, bool Applied,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TrySendLegacyCombatGearMagneticLightPlayerAsync(
            WorldPacketHandlerContext defenderContext,
            ConquerTcpConnection attackerConnection,
            WorldSession attackerSession,
            WorldSession defenderSession,
            PlayerSpellSnapshotEntry playerSpell,
            LegacyMagicRuntimeSpell spell,
            int attempt,
            CancellationToken cancellationToken)
    {
        const uint animationDamage = 15_000;
        var attackerUid = attackerSession.Character.Uid;
        var defenderUid = defenderSession.Character.Uid;

        var prefix = await SendLegacyCombatGearMagneticPlayerPrefixAsync(
                defenderContext, attackerConnection, attackerSession, defenderSession, cancellationToken)
            .ConfigureAwait(false);
        defenderSession = prefix.DefenderSession;
        attackerSession = prefix.AttackerSession;
        defenderContext = defenderContext with { Session = defenderSession };
        if (prefix.AbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            return (defenderSession, attackerSession, false, prefix.AbortReason);

        var targetGate = await TryEnterLegacyCombatGearNestedPlayerTargetAsync(
                defenderContext, attackerConnection, attackerSession, defenderSession, spell, cancellationToken)
            .ConfigureAwait(false);
        defenderSession = targetGate.DefenderSession;
        attackerSession = targetGate.AttackerSession;
        defenderContext = defenderContext with { Session = defenderSession };
        if (targetGate.AbortReason != LegacyPlayerMagicReceiveAbortReason.None)
            return (defenderSession, attackerSession, false, targetGate.AbortReason);
        if (!targetGate.Allowed)
        {
            var empty = await SendLegacyCombatGearNestedPlayerAnimationAsync(
                    defenderContext, attackerConnection, attackerSession, defenderSession,
                    playerSpell, spell, [], cancellationToken)
                .ConfigureAwait(false);
            return (empty.DefenderSession, empty.AttackerSession, true, empty.AbortReason);
        }

        var damage = CalculateLegacyPlayerPlayerMagicDamage(
            defenderSession,
            attackerSession,
            spell,
            defenderContext.WorldData);
        var outcome = await ApplyLegacyPlayerMagicDamageToPlayerAsync(
                defenderContext,
                new WorldActiveSession(attackerConnection, attackerSession),
                defenderSession.Character,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
        if (outcome.Result is not { } result)
        {
            // The nested counter's roles are reversed relative to this incoming hit.
            var reason = outcome.AbortReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost
                ? LegacyPlayerMagicReceiveAbortReason.TargetLost
                : LegacyPlayerMagicReceiveAbortReason.AttackerLost;
            var refreshedReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
                ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
                attackerUid, defenderUid);
            if (refreshedReason == LegacyPlayerMagicReceiveAbortReason.AttackerLost)
            {
                reason = refreshedReason; // Original incoming attacker loss has priority.
            }

            return (defenderSession, attackerSession, false, reason);
        }

        attackerSession = result.UpdatedSession;
        defenderSession = outcome.AttackerSession ?? result.UpdatedAttackerSession ?? defenderSession;
        defenderContext = defenderContext with { Session = defenderSession };
        var abortReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (defenderSession, attackerSession, false, abortReason);
        }

        result = result with
        {
            ActiveSession = result.ActiveSession with { Session = attackerSession },
            UpdatedSession = attackerSession,
            UpdatedAttackerSession = defenderSession,
        };

        ushort targetNewX = 0;
        ushort targetNewY = 0;
        var pushed = false;
        if (TryCalculateLegacyPlayerPushbackPosition(
                defenderContext,
                result.UpdatedSession,
                ReadAttributeUInt32(defenderSession.Character.Attributes, "Angle", "Facing"),
                distance: 2,
                out var pushedX,
                out var pushedY,
                out var guildWarEndpointCheck))
        {
            targetNewX = pushedX;
            targetNewY = pushedY;
            pushed = true;
            var pushedAttackerSession = result.UpdatedSession with
            {
                Character = WithLegacyPlayerAttackProtection(
                    result.UpdatedSession.Character with
                    {
                        X = pushedX,
                        Y = pushedY,
                    },
                    DateTimeOffset.UtcNow,
                    TimeSpan.FromSeconds(2)),
            };
            attackerSession = pushedAttackerSession;
            defenderContext.Sessions?.Set(attackerConnection, pushedAttackerSession);
            result = result with
            {
                ActiveSession = result.ActiveSession with { Session = pushedAttackerSession },
                UpdatedSession = pushedAttackerSession,
            };
        }
        else if (guildWarEndpointCheck.Applies && !guildWarEndpointCheck.Allowed)
        {
            await SendLegacyGuildWarPullbackAsync(
                    defenderContext,
                    attackerConnection,
                    result.UpdatedSession,
                    guildWarEndpointCheck,
                    "CombatGear MagneticLight",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        abortReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (defenderSession, attackerSession, false, abortReason);
        }

        result = result with
        {
            ActiveSession = result.ActiveSession with { Session = attackerSession },
            UpdatedSession = attackerSession,
        };
        var publication = await SendLegacyCombatGearNestedPlayerAnimationAsync(
                defenderContext, attackerConnection, attackerSession, defenderSession,
                playerSpell, spell,
                [
                    new LegacySpellAnimationTarget(
                        CheckedUInt32(attackerUid, nameof(attackerUid)), animationDamage,
                        Hit: true, MsgAttackEffect.None, targetNewX, targetNewY),
                ],
                cancellationToken)
            .ConfigureAwait(false);
        defenderSession = publication.DefenderSession;
        attackerSession = publication.AttackerSession;
        defenderContext = defenderContext with { Session = defenderSession };
        abortReason = publication.AbortReason;
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (defenderSession, attackerSession, true, abortReason);
        }

        result = result with
        {
            ActiveSession = result.ActiveSession with { Session = attackerSession },
            UpdatedSession = attackerSession,
        };
        await SendLegacyPlayerMagicDamageUpdatesAsync(defenderContext, defenderSession, result, cancellationToken)
            .ConfigureAwait(false);

        abortReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (defenderSession, attackerSession, true, abortReason);
        }

        await LogLegacyCombatGearUpdateTargetPlayerDamageBranchAsync(
                defenderContext,
                attackerSession,
                defenderSession,
                spell.Id,
                "MagneticLight",
                attempt,
                result.DamageApplied,
                pushed)
            .ConfigureAwait(false);
        abortReason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        if (abortReason != LegacyPlayerMagicReceiveAbortReason.None)
        {
            return (defenderSession, attackerSession, true, abortReason);
        }

        return (defenderSession, attackerSession, true, LegacyPlayerMagicReceiveAbortReason.None);
    }

    private static LegacyPlayerMagicReceiveAbortReason RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
        ref WorldPacketHandlerContext defenderContext,
        ConquerTcpConnection attackerConnection,
        ref WorldSession attackerSession,
        ref WorldSession defenderSession,
        long attackerUid,
        long defenderUid)
    {
        // These are ORIGINAL incoming roles, opposite the nested counter receive.
        // Identity fences do not rerun alive/range/PK or source branch selection.
        if (defenderContext.Sessions is not { } sessions)
        {
            return attackerSession.Character.Uid != attackerUid
                ? LegacyPlayerMagicReceiveAbortReason.AttackerLost
                : defenderSession.Character.Uid != defenderUid
                    ? LegacyPlayerMagicReceiveAbortReason.TargetLost
                    : LegacyPlayerMagicReceiveAbortReason.None;
        }

        if (!sessions.TryGet(attackerConnection, out var currentAttacker) ||
            currentAttacker is null || currentAttacker.Character.Uid != attackerUid)
        {
            return LegacyPlayerMagicReceiveAbortReason.AttackerLost;
        }

        attackerSession = currentAttacker;
        if (!sessions.TryGet(defenderContext.Connection, out var currentDefender) ||
            currentDefender is null || currentDefender.Character.Uid != defenderUid)
        {
            return LegacyPlayerMagicReceiveAbortReason.TargetLost;
        }

        defenderSession = currentDefender;
        defenderContext = defenderContext with { Session = currentDefender };
        return LegacyPlayerMagicReceiveAbortReason.None;
    }

    private static async ValueTask LogLegacyCombatGearUpdateTargetPlayerDamageBranchAsync(
        WorldPacketHandlerContext context,
        WorldSession attackerSession,
        WorldSession defenderSession,
        ushort spellId,
        string spellName,
        int attempt,
        uint damageApplied,
        bool pushed)
    {
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CombatGear UpdateTarget player damage branch " +
                $"{FormatSession(context)} attackerUid={attackerSession.Character.Uid} " +
                $"defenderUid={defenderSession.Character.Uid} spellId={spellId} spellName={spellName} " +
                $"attempt={attempt} damageApplied={damageApplied} pushed={pushed}; " +
                "recursive ReceiveAttack.Player.Execute side-effect debt remains bounded; " +
                "source=ReceiveAttack.Player.Execute -> MyCombatGear.UpdateTarget -> CombatGearAttack.")
            .ConfigureAwait(false);
    }
}
