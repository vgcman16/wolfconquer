using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCombatGearPirateArmedReceiveSpellId = 21_710;
    private const ushort LegacyCombatGearPirateSenseReceiveSpellId = 21_720;
    private const ushort LegacyCombatGearPirateDarkReceiveSpellId = 21_930;
    private const int LegacyCombatGearPirateArmedFlag = 0x19D;
    private const int LegacyCombatGearPirateSenseFlag = 0x19C;
    private const int LegacyCombatGearPirateAutoHuntFlag = 0x94;
    private const int LegacyCombatGearPirateFineRain2Flag = 0xD5;
    private const int LegacyCombatGearPirateRampageFlag = 0xD6;
    private const int LegacyCombatGearPirateSparkShieldFlag = 0xF1;
    private const string RuntimeLegacyCombatGearPirateSenseDamageOnHumanAttribute = "SenseDamageOnHuman";
    private const string RuntimeLegacyCombatGearPirateSenseDamage2Attribute = "SenseDamage2";
    private const string RuntimeLegacyCombatGearPirateAutoHuntAttribute = "OnAutoHunt";

    private static async ValueTask<(WorldSession AttackerSession, WorldSession DefenderSession, bool Applied)>
        TrySendLegacyCombatGearPirateReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ConquerTcpConnection defenderConnection,
            WorldSession defenderSession,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !HasLegacyPirateCombatGearEquipped(attackerSession))
        {
            return (attackerSession, defenderSession, false);
        }

        var targetX = CheckedUInt16(defenderSession.Character.X, nameof(defenderSession.Character.X));
        var targetY = CheckedUInt16(defenderSession.Character.Y, nameof(defenderSession.Character.Y));
        var updatedAttackerSession = attackerSession;
        var updatedDefenderSession = defenderSession;
        var applied = false;

        (updatedAttackerSession, var armedApplied) =
            await TrySendLegacyCombatGearPirateArmedReceivePlayerUpdateAsync(
                    context with { Session = updatedAttackerSession },
                    updatedAttackerSession,
                    worldData,
                    CheckedUInt32(updatedDefenderSession.Character.Uid, nameof(updatedDefenderSession.Character.Uid)),
                    targetX,
                    targetY,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= armedApplied;
        context = context with { Session = updatedAttackerSession };

        (updatedAttackerSession, var senseApplied) =
            await TrySendLegacyCombatGearPirateSenseReceivePlayerUpdateAsync(
                    context,
                    updatedAttackerSession,
                    worldData,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= senseApplied;
        context = context with { Session = updatedAttackerSession };

        (updatedDefenderSession, var darkApplied) =
            await TrySendLegacyCombatGearPirateDarkReceivePlayerUpdateAsync(
                    context,
                    updatedAttackerSession,
                    defenderConnection,
                    updatedDefenderSession,
                    worldData,
                    targetX,
                    targetY,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= darkApplied;

        (updatedAttackerSession, var blackJadeUseRuneApplied) =
            await TrySendLegacyCombatGearPirateBlackJadeUseRuneAsync(
                    context,
                    updatedAttackerSession,
                    worldData,
                    CheckedUInt32(updatedDefenderSession.Character.Uid, nameof(updatedDefenderSession.Character.Uid)),
                    targetX,
                    targetY,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= blackJadeUseRuneApplied;

        return (updatedAttackerSession, updatedDefenderSession, applied);
    }

    private static async ValueTask<(WorldSession Session, bool Applied)>
        TrySendLegacyCombatGearPirateArmedReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldDataCatalog worldData,
            uint defenderUid,
            ushort targetX,
            ushort targetY,
            CancellationToken cancellationToken)
    {
        if (!TryResolveLegacyPlayerMagicRuntimeSpell(
                session,
                worldData,
                LegacyCombatGearPirateArmedReceiveSpellId,
                out var playerSpell,
                out var spell))
        {
            return (session, false);
        }

        await SendLegacyCombatGearPirateReceiveInteractionAsync(
                context,
                session,
                defenderUid,
                playerSpell,
                spell,
                targetX,
                targetY,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedCharacter = WithCharacterAttribute(
            WithLegacySourceAddSpellFlag(session.Character, LegacyCombatGearPirateArmedFlag, removeOnDead: true),
            spell.Name,
            spell.Damage);
        var updatedSession = session with
        {
            Character = updatedCharacter,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                LegacyCombatGearPirateArmedFlag,
                spell.Duration,
                spell.Damage,
                spellLevel: 0,
                visible: true,
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearPirateReceiveAnimationAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                originX: 0,
                originY: 0,
                targetHit: true,
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearPirateEquipmentRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Update player Pirate Armed " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} statusFlag={LegacyCombatGearPirateArmedFlag} " +
                $"gDamage={spell.Damage} equipmentRefreshPackets=1; source=ReceiveAttack.Player.Execute -> " +
                "client.CombatGear.Update(attacked) -> MyCombatGear.Update Pirate -> CombatGearAttack 21710.")
            .ConfigureAwait(false);
        return (updatedSession, true);
    }

    private static async ValueTask<(WorldSession Session, bool Applied)>
        TrySendLegacyCombatGearPirateSenseReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldDataCatalog worldData,
            CancellationToken cancellationToken)
    {
        if (!TryResolveLegacyPlayerMagicRuntimeSpell(
                session,
                worldData,
                LegacyCombatGearPirateSenseReceiveSpellId,
                out var playerSpell,
                out var spell) ||
            HasLegacyStatusFlag(session.Character, LegacyCombatGearPirateSenseFlag))
        {
            return (session, false);
        }

        var updatedSession = session with
        {
            Character = WithCharacterAttribute(
                WithCharacterAttribute(
                    WithLegacySourceAddSpellFlag(session.Character, LegacyCombatGearPirateSenseFlag, removeOnDead: true),
                    RuntimeLegacyCombatGearPirateSenseDamageOnHumanAttribute,
                    spell.DamageOnHuman),
                RuntimeLegacyCombatGearPirateSenseDamage2Attribute,
                spell.Damage2),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                LegacyCombatGearPirateSenseFlag,
                spell.Duration,
                spell.DamageOnHuman,
                spell.Damage2,
                visible: true,
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearPirateReceiveAnimationAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                originX: 0,
                originY: 0,
                targetHit: true,
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearPirateEquipmentRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Update player Pirate Sense " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} statusFlag={LegacyCombatGearPirateSenseFlag} " +
                $"duration={spell.Duration} damageOnHuman={spell.DamageOnHuman} damage2={spell.Damage2} " +
                "equipmentRefreshPackets=1; " +
                "source=ReceiveAttack.Player.Execute -> client.CombatGear.Update(attacked) -> " +
                "MyCombatGear.Update Pirate -> CombatGearAttack 21720.")
            .ConfigureAwait(false);
        return (updatedSession, true);
    }

    private static async ValueTask<(WorldSession DefenderSession, bool Applied)>
        TrySendLegacyCombatGearPirateDarkReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ConquerTcpConnection defenderConnection,
            WorldSession defenderSession,
            WorldDataCatalog worldData,
            ushort targetX,
            ushort targetY,
            CancellationToken cancellationToken)
    {
        if (!TryResolveLegacyPlayerMagicRuntimeSpell(
                attackerSession,
                worldData,
                LegacyCombatGearPirateDarkReceiveSpellId,
                out var playerSpell,
                out var spell) ||
            !CanSeePlayer(attackerSession.Character, defenderSession.Character))
        {
            return (defenderSession, false);
        }

        await SendLegacyCombatGearPirateReceiveInteractionAsync(
                context,
                attackerSession,
                CheckedUInt32(defenderSession.Character.Uid, nameof(defenderSession.Character.Uid)),
                playerSpell,
                spell,
                targetX,
                targetY,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedCharacter = defenderSession.Character;
        var removed = new List<string>();
        RemoveFlagIfPresent(LegacyStigmaFlag, "Stigma");
        RemoveFlagIfPresent(LegacyMagicShieldFlag, "MagicShield");
        RemoveFlagIfPresent(LegacyAzureShieldFlag, "AzureShield");
        RemoveFlagIfPresent(LegacyCombatGearPirateAutoHuntFlag, "AutoHunt");
        RemoveFlagIfPresent(LegacyFineRain1Flag, "FineRain1");
        RemoveFlagIfPresent(LegacyCombatGearPirateFineRain2Flag, "FineRain2");
        RemoveFlagIfPresent(LegacyCombatGearPirateRampageFlag, "Rampage");
        RemoveFlagIfPresent(LegacySlayerFlag, "Slayer");
        RemoveFlagIfPresent(LegacyImmortalForceFlag, "ImmortalForce");
        RemoveFlagIfPresent(LegacyLightningShieldActivatedFlag, "LightningShieldActivated");
        RemoveFlagIfPresent(LegacyCombatGearPirateSparkShieldFlag, "SparkShield");

        if (ReadAttributeBool(updatedCharacter.Attributes, RuntimeLegacyCombatGearPirateAutoHuntAttribute))
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyCombatGearPirateAutoHuntAttribute,
                "0");
            removed.Add(RuntimeLegacyCombatGearPirateAutoHuntAttribute);
        }

        var updatedDefenderSession = defenderSession;
        if (removed.Count > 0)
        {
            updatedDefenderSession = defenderSession with
            {
                Character = updatedCharacter,
            };
            context.Sessions?.Set(defenderConnection, updatedDefenderSession);

            var defenderContext = context with
            {
                Connection = defenderConnection,
                Session = updatedDefenderSession,
            };
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    defenderContext,
                    defenderConnection,
                    updatedDefenderSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var defenderEquipmentContext = context with
        {
            Connection = defenderConnection,
            Session = updatedDefenderSession,
        };
        await SendLegacyCombatGearPirateEquipmentRefreshAsync(
                defenderEquipmentContext,
                updatedDefenderSession,
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearPirateReceiveAnimationAsync(
                context with { Session = attackerSession },
                attackerSession,
                playerSpell,
                spell,
                CheckedUInt32(defenderSession.Character.Uid, nameof(defenderSession.Character.Uid)),
                targetX,
                targetY,
                targetHit: true,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Update player Pirate Dark " +
                $"{FormatSession(context with { Session = attackerSession })} defenderUid={defenderSession.Character.Uid} " +
                $"spellId={spell.Id} removedCount={removed.Count} removed=[{string.Join(',', removed)}] " +
                "defenderEquipmentRefreshPackets=1; " +
                "source=ReceiveAttack.Player.Execute -> client.CombatGear.Update(attacked) -> " +
                "MyCombatGear.Update Pirate -> CombatGearAttack 21930.")
            .ConfigureAwait(false);
        return (updatedDefenderSession, true);

        void RemoveFlagIfPresent(int flag, string name)
        {
            if (!HasLegacyStatusFlag(updatedCharacter, flag))
            {
                return;
            }

            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, flag);
            removed.Add(name);
        }
    }

    private static async ValueTask<(WorldSession Session, bool Applied)>
        TrySendLegacyCombatGearPirateDarkReceiveSobNpcUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            WorldDataCatalog worldData,
            uint sobNpcUid,
            ushort targetX,
            ushort targetY,
            CancellationToken cancellationToken)
    {
        if (!TryResolveLegacyPlayerMagicRuntimeSpell(
                attackerSession,
                worldData,
                LegacyCombatGearPirateDarkReceiveSpellId,
                out var playerSpell,
                out var spell))
        {
            return (attackerSession, false);
        }

        await SendLegacyCombatGearPirateReceiveInteractionAsync(
                context,
                attackerSession,
                sobNpcUid,
                playerSpell,
                spell,
                targetX,
                targetY,
                cancellationToken)
            .ConfigureAwait(false);

        var attackerUid = CheckedUInt32(attackerSession.Character.Uid, nameof(attackerSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            attackerUid,
            targetX,
            targetY,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            []);
        await SendPlayerSpellAnimationToVisibleAsync(context, attackerSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Update SOB Pirate Dark no-op " +
                $"{FormatSession(context with { Session = attackerSession })} sobNpcUid={sobNpcUid} " +
                $"spellId={spell.Id} targetX={targetX} targetY={targetY}; " +
                "source=ReceiveAttack.Npc.Execute -> client.CombatGear.Update(attacked) -> " +
                "MyCombatGear.Update Pirate -> CombatGearAttack 21930 player-view miss still sends interaction/animation.")
            .ConfigureAwait(false);
        return (attackerSession, true);
    }

    private static async ValueTask SendLegacyCombatGearPirateReceiveInteractionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint defenderUid,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        ushort targetX,
        ushort targetY,
        CancellationToken cancellationToken)
    {
        var attackerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var interaction = new MsgAttackPacket(new MsgAttackQuery(
            attackerUid,
            defenderUid,
            targetX,
            targetY,
            MsgAttackType.None,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            MsgAttackEffect.None,
            Unknown: 0,
            SpellLevel: (ushort)Math.Min(playerSpell.Level, ushort.MaxValue),
            SpellId: spell.Id));
        await context.Connection.SendAsync(interaction.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyCombatGearPirateReceiveAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession attackerSession,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        uint targetUid,
        ushort originX,
        ushort originY,
        bool targetHit,
        CancellationToken cancellationToken)
    {
        var attackerUid = CheckedUInt32(attackerSession.Character.Uid, nameof(attackerSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            attackerUid,
            originX,
            originY,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(targetUid, 0, targetHit, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(context, attackerSession, animation, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyCombatGearPirateEquipmentRefreshAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                    session.PlayerItems,
                    session.AlternateEquipmentActive).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

}
