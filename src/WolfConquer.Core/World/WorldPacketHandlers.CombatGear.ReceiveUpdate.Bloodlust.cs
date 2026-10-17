using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCombatGearBloodlustImmersionSpellId = 17_600;
    private const ushort LegacyCombatGearBloodlustInsoucianceSpellId = 17_610;
    private const int LegacyCombatGearImmersionFlag = 0x12F;
    private const int LegacyCombatGearInsoucianceFlag = 0x130;
    private const string RuntimeLegacyCombatGearImmersionPowerAttribute = "ImmersionPower";
    private const string RuntimeLegacyCombatGearInsouciancePowerAttribute = "InsouciancePower";

    private static async ValueTask<(WorldSession AttackerSession, bool Applied)>
        TrySendLegacyCombatGearBloodlustReceivePlayerUpdateAsync(
            WorldPacketHandlerContext context,
            WorldSession attackerSession,
            ushort targetX,
            ushort targetY,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            !HasLegacyCombatGearEquippedForClass(
                attackerSession.CombatGear ?? CombatGearSnapshot.Empty,
                attackerSession.Character.Class,
                LegacyCombatGearBloodlustItemId))
        {
            return (attackerSession, false);
        }

        var updatedAttackerSession = attackerSession;
        var applied = false;

        (updatedAttackerSession, var immersionApplied) =
            await TrySendLegacyCombatGearBloodlustReceiveSpellAsync(
                    context with { Session = updatedAttackerSession },
                    updatedAttackerSession,
                    worldData,
                    LegacyCombatGearBloodlustImmersionSpellId,
                    LegacyCombatGearImmersionFlag,
                    RuntimeLegacyCombatGearImmersionPowerAttribute,
                    targetX,
                    targetY,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= immersionApplied;

        (updatedAttackerSession, var insoucianceApplied) =
            await TrySendLegacyCombatGearBloodlustReceiveSpellAsync(
                    context with { Session = updatedAttackerSession },
                    updatedAttackerSession,
                    worldData,
                    LegacyCombatGearBloodlustInsoucianceSpellId,
                    LegacyCombatGearInsoucianceFlag,
                    RuntimeLegacyCombatGearInsouciancePowerAttribute,
                    targetX,
                    targetY,
                    cancellationToken)
                .ConfigureAwait(false);
        applied |= insoucianceApplied;

        return (updatedAttackerSession, applied);
    }

    private static async ValueTask<(WorldSession Session, bool Applied)>
        TrySendLegacyCombatGearBloodlustReceiveSpellAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldDataCatalog worldData,
            ushort spellId,
            int statusFlag,
            string powerAttributeName,
            ushort targetX,
            ushort targetY,
            CancellationToken cancellationToken)
    {
        if (!TryResolveLegacyPlayerMagicRuntimeSpell(session, worldData, spellId, out var playerSpell, out var spell))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CombatGear Bloodlust receive player buff " +
                    $"{FormatSession(context)} spellId={spellId}; spell is not available.")
                .ConfigureAwait(false);
            return (session, false);
        }

        var duration = spellId == LegacyCombatGearBloodlustInsoucianceSpellId
            ? 10U
            : spell.Duration;
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(
                WithLegacySourceAddSpellFlag(session.Character, statusFlag, removeOnDead: true),
                powerAttributeName,
                spell.Damage),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendLegacyCombatGearBloodlustReceiveInteractionAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                targetX,
                targetY,
                cancellationToken)
            .ConfigureAwait(false);
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                statusFlag,
                duration,
                damage: 0,
                spellLevel: 0,
                visible: false,
                cancellationToken)
            .ConfigureAwait(false);

        var attackerUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            attackerUid,
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(attackerUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Bloodlust receive player buff " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={statusFlag} duration={duration} {powerAttributeName}={spell.Damage}; " +
                "source=ReceiveAttack.Player.Execute -> client.CombatGear.Update(attacked) -> " +
                "MyCombatGear.Update Bloodlust -> CombatGearAttack 17600/17610.")
            .ConfigureAwait(false);
        return (updatedSession, true);
    }

    private static async ValueTask SendLegacyCombatGearBloodlustReceiveInteractionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        ushort targetX,
        ushort targetY,
        CancellationToken cancellationToken)
    {
        var attackerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var interaction = new MsgAttackPacket(new MsgAttackQuery(
            attackerUid,
            attackerUid,
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
}
