using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCombatGearCrackMantraReceiveSpellId = 20_380;
    private const ushort LegacyCombatGearDeadwoodCurseReceiveSpellId = 20_330;
    private const int LegacyCombatGearCrackMantraPhysicalFlag = 0x7A | LegacyHawksAmbitionFlag;
    private const int LegacyCombatGearCrackMantraMagicFlag =
        LegacyFreezeFlag | LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag;
    private const int LegacyCombatGearDeadwoodCurseFlag = 0x7E | LegacyHawksAmbitionFlag;
    private const uint LegacyCombatGearCrackMantraDuration = 5;
    private const string RuntimeLegacyCombatGearCrackMantra1Attribute = "CrackMantra1";
    private const string RuntimeLegacyCombatGearCrackMantra2Attribute = "CrackMantra2";
    private const string RuntimeLegacyCombatGearDeadwoodCurseDamageOnHumanAttribute =
        "DeadwoodCurseDamageOnHuman";
    private const string RuntimeLegacyCombatGearDeadwoodCurseDamage2Attribute = "DeadwoodCurseDamage2";
    private const string RuntimeLegacyCombatGearTaoistRollAttribute = "LegacyCombatGearTaoistRoll";
    private const string RuntimeLegacyCombatGearTaoistCrackMantraRollAttribute =
        RuntimeLegacyCombatGearTaoistRollAttribute + ".20380.CrackMantra";

    private static async ValueTask<(WorldSession AttackerSession, WorldSession DefenderSession, bool Applied)>
        TrySendLegacyCombatGearTaoistReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ConquerTcpConnection defenderConnection,
            WorldSession defenderSession,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData)
        {
            return (attackerSession, defenderSession, false);
        }

        var updatedAttackerSession = attackerSession;
        var updatedDefenderSession = defenderSession;
        var applied = false;

        if (HasLegacyCombatGearEquippedForClass(
                updatedAttackerSession.CombatGear ?? CombatGearSnapshot.Empty,
                updatedAttackerSession.Character.Class,
                LegacyCombatGearTaoist3ItemId))
        {
            (updatedAttackerSession, updatedDefenderSession, var crackMantraApplied) =
                await TrySendLegacyCombatGearCrackMantraReceivePlayerUpdateAsync(
                        context with { Session = updatedAttackerSession },
                        updatedAttackerSession,
                        defenderConnection,
                        updatedDefenderSession,
                        worldData,
                        cancellationToken)
                    .ConfigureAwait(false);
            applied |= crackMantraApplied;
            context = context with { Session = updatedAttackerSession };
        }

        if (HasLegacyCombatGearEquippedForClass(
                updatedAttackerSession.CombatGear ?? CombatGearSnapshot.Empty,
                updatedAttackerSession.Character.Class,
                LegacyCombatGearTaoist1ItemId))
        {
            (updatedAttackerSession, updatedDefenderSession, var deadwoodCurseApplied) =
                await TrySendLegacyCombatGearDeadwoodCurseReceivePlayerUpdateAsync(
                        context with { Session = updatedAttackerSession },
                        updatedAttackerSession,
                        defenderConnection,
                        updatedDefenderSession,
                        worldData,
                        cancellationToken)
                    .ConfigureAwait(false);
            applied |= deadwoodCurseApplied;
        }

        return (updatedAttackerSession, updatedDefenderSession, applied);
    }

    private static async ValueTask<(WorldSession AttackerSession, WorldSession DefenderSession, bool Applied)>
        TrySendLegacyCombatGearCrackMantraReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ConquerTcpConnection defenderConnection,
            WorldSession defenderSession,
            WorldDataCatalog worldData,
            CancellationToken cancellationToken)
    {
        if (!TryResolveLegacyPlayerMagicRuntimeSpell(
                attackerSession,
                worldData,
                LegacyCombatGearCrackMantraReceiveSpellId,
                out var playerSpell,
                out var spell) ||
            !CanSeePlayer(attackerSession.Character, defenderSession.Character))
        {
            return (attackerSession, defenderSession, false);
        }

        var updatedDefenderSession = defenderSession;
        var appliedStatus = false;
        var attackerContext = context with { Session = attackerSession };
        if (CanLegacyPlayerMagicDamageTarget(attackerContext, attackerSession, defenderSession, spell, out _))
        {
            var physicalCrack = CheckLegacyCombatGearTaoistCrackMantraRoll(attackerSession.Character);
            var statusFlag = physicalCrack
                ? LegacyCombatGearCrackMantraPhysicalFlag
                : LegacyCombatGearCrackMantraMagicFlag;
            var powerAttribute = physicalCrack
                ? RuntimeLegacyCombatGearCrackMantra1Attribute
                : RuntimeLegacyCombatGearCrackMantra2Attribute;
            var power = physicalCrack ? spell.DamageOnHuman : spell.Damage2;

            updatedDefenderSession = defenderSession with
            {
                Character = WithCharacterAttribute(
                    WithLegacySourceAddSpellFlag(defenderSession.Character, statusFlag, removeOnDead: true),
                    powerAttribute,
                    power),
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
            await SendLegacyCombatGearArchiveSkillUpdateAsync(
                    defenderContext,
                    updatedDefenderSession,
                    statusFlag,
                    LegacyCombatGearCrackMantraDuration,
                    power,
                    spellLevel: 0,
                    visible: true,
                    cancellationToken)
                .ConfigureAwait(false);
            appliedStatus = true;
        }

        var defenderEquipmentContext = context with
        {
            Connection = defenderConnection,
            Session = updatedDefenderSession,
        };
        await SendLegacyCombatGearTaoistEquipmentRefreshAsync(
                defenderEquipmentContext,
                updatedDefenderSession,
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearTaoistReceiveAnimationAsync(
                attackerContext,
                attackerSession,
                playerSpell,
                spell,
                CheckedUInt32(updatedDefenderSession.Character.Uid, nameof(updatedDefenderSession.Character.Uid)),
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearTaoistEquipmentRefreshAsync(
                defenderEquipmentContext,
                updatedDefenderSession,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Update player Taoist CrackMantra " +
                $"{FormatSession(attackerContext)} defenderUid={updatedDefenderSession.Character.Uid} " +
                $"spellId={spell.Id} appliedStatus={appliedStatus} defenderEquipmentRefreshPackets=2; " +
                "source=ReceiveAttack.Player.Execute -> " +
                "client.CombatGear.Update(attacked) -> MyCombatGear.Update Taoist3 -> CombatGearAttack 20380.")
            .ConfigureAwait(false);
        return (attackerSession, updatedDefenderSession, true);
    }

    private static async ValueTask<(WorldSession AttackerSession, WorldSession DefenderSession, bool Applied)>
        TrySendLegacyCombatGearDeadwoodCurseReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ConquerTcpConnection defenderConnection,
            WorldSession defenderSession,
            WorldDataCatalog worldData,
            CancellationToken cancellationToken)
    {
        _ = defenderConnection;

        var defenderUid = CheckedUInt32(defenderSession.Character.Uid, nameof(defenderSession.Character.Uid));
        (var updatedAttackerSession, var applied) =
            await TrySendLegacyCombatGearDeadwoodCurseReceiveUpdateAsync(
                    context,
                    attackerSession,
                    worldData,
                    defenderUid,
                    cancellationToken)
                .ConfigureAwait(false);

        return (updatedAttackerSession, defenderSession, applied);
    }

    private static async ValueTask<(WorldSession Session, bool Applied)>
        TrySendLegacyCombatGearDeadwoodCurseReceiveUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            WorldDataCatalog worldData,
            uint targetUid,
            CancellationToken cancellationToken)
    {
        if (!TryResolveLegacyPlayerMagicRuntimeSpell(
                attackerSession,
                worldData,
                LegacyCombatGearDeadwoodCurseReceiveSpellId,
                out var playerSpell,
                out var spell))
        {
            return (attackerSession, false);
        }

        var updatedAttackerSession = attackerSession with
        {
            Character = WithCharacterAttribute(
                WithCharacterAttribute(
                    WithLegacySourceAddSpellFlag(attackerSession.Character, LegacyCombatGearDeadwoodCurseFlag, removeOnDead: true),
                    RuntimeLegacyCombatGearDeadwoodCurseDamageOnHumanAttribute,
                    spell.DamageOnHuman),
                RuntimeLegacyCombatGearDeadwoodCurseDamage2Attribute,
                spell.Damage2),
        };
        context.Sessions?.Set(context.Connection, updatedAttackerSession);
        var updatedContext = context with { Session = updatedAttackerSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedAttackerSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedAttackerSession,
                LegacyCombatGearDeadwoodCurseFlag,
                spell.Duration,
                spell.DamageOnHuman,
                spell.Damage2,
                visible: true,
                cancellationToken)
            .ConfigureAwait(false);

        var attackerUid = CheckedUInt32(
            updatedAttackerSession.Character.Uid,
            nameof(updatedAttackerSession.Character.Uid));
        await SendLegacyCombatGearTaoistReceiveAnimationAsync(
                updatedContext,
                updatedAttackerSession,
                playerSpell,
                spell,
                attackerUid,
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearTaoistEquipmentRefreshAsync(
                updatedContext,
                updatedAttackerSession,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Update player Taoist DeadwoodCurse " +
                $"{FormatSession(updatedContext)} targetUid={targetUid} spellId={spell.Id} " +
                $"duration={spell.Duration} damageOnHuman={spell.DamageOnHuman} damage2={spell.Damage2}; " +
                "equipmentRefreshPackets=1; " +
                "source=ReceiveAttack.Player.Execute -> client.CombatGear.Update(attacked) -> " +
                "MyCombatGear.Update Taoist1 -> CombatGearAttack 20330.")
            .ConfigureAwait(false);
        return (updatedAttackerSession, true);
    }

    private static async ValueTask SendLegacyCombatGearTaoistReceiveAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession attackerSession,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        uint targetUid,
        CancellationToken cancellationToken)
    {
        var attackerUid = CheckedUInt32(attackerSession.Character.Uid, nameof(attackerSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            attackerUid,
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(targetUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(context, attackerSession, animation, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyCombatGearTaoistEquipmentRefreshAsync(
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

    private static bool CheckLegacyCombatGearTaoistCrackMantraRoll(CharacterSnapshot character)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyCombatGearTaoistCrackMantraRollAttribute,
            RuntimeLegacyCombatGearTaoistRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < 50;
    }
}
