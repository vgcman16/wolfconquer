using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        SendLegacyCombatGearMagneticPlayerPrefixAsync(
            WorldPacketHandlerContext defenderContext, ConquerTcpConnection attackerConnection,
            WorldSession attackerSession, WorldSession defenderSession,
            CancellationToken cancellationToken)
    {
        var attackerUid = attackerSession.Character.Uid;
        var defenderUid = defenderSession.Character.Uid;
        LegacyPlayerMagicReceiveAbortReason Refresh() =>
            RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
                ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
                attackerUid, defenderUid);
        var reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None)
            return (defenderSession, attackerSession, reason);

        // Source CombatGearAttack 20220 enters this self prefix BEFORE target lookup.
        defenderSession = defenderSession with
        {
            Character = WithLegacySourceAddSpellFlag(defenderSession.Character,
                LegacyVenomFlag | LegacyMagicShieldFlag, removeOnDead: true),
        };
        defenderContext.Sessions?.Set(defenderContext.Connection, defenderSession);
        defenderContext = defenderContext with { Session = defenderSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(defenderContext, defenderSession, cancellationToken)
            .ConfigureAwait(false);
        reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None)
            return (defenderSession, attackerSession, reason);

        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                defenderContext, defenderSession, LegacyVenomFlag | LegacyMagicShieldFlag,
                duration: 10, damage: 0, spellLevel: 0, visible: true, cancellationToken)
            .ConfigureAwait(false);
        reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None)
            return (defenderSession, attackerSession, reason);

        await defenderContext.Log.WriteLineAsync(
                "WorldServer sent legacy CombatGear UpdateTarget passive status " +
                $"{FormatSession(defenderContext)} spellId={LegacyCombatGearMagneticLightSpellId} " +
                $"statusFlag={LegacyVenomFlag | LegacyMagicShieldFlag} duration=10; " +
                "equipmentRefreshPackets=0; " +
                "source=MyCombatGear.UpdateTarget -> MyCombatGear.ProcescMagic(ignoreStamp).")
            .ConfigureAwait(false);
        reason = Refresh();
        return (defenderSession, attackerSession, reason);
    }

    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession,
        LegacyPlayerMagicReceiveAbortReason AbortReason)>
        SendLegacyCombatGearNestedPlayerAnimationAsync(
            WorldPacketHandlerContext defenderContext, ConquerTcpConnection attackerConnection,
            WorldSession attackerSession, WorldSession defenderSession,
            PlayerSpellSnapshotEntry playerSpell, LegacyMagicRuntimeSpell spell,
            IReadOnlyList<LegacySpellAnimationTarget> targets, CancellationToken cancellationToken)
    {
        var attackerUid = attackerSession.Character.Uid;
        var defenderUid = defenderSession.Character.Uid;
        var reason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        if (reason != LegacyPlayerMagicReceiveAbortReason.None)
            return (defenderSession, attackerSession, reason);

        var magnetic = spell.Id == LegacyCombatGearMagneticLightSpellId;
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(defenderUid, nameof(defenderUid)), 0, 0, spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue), playerSpell.UseSpellSoul,
            targets, bombFlag: magnetic ? (byte)1 : (byte)0);
        if (magnetic)
            animation = animation with { AttackedUid = CheckedUInt32(attackerUid, nameof(attackerUid)) };
        await SendPlayerSpellAnimationToVisibleAsync(
                defenderContext, defenderSession, animation, cancellationToken)
            .ConfigureAwait(false);
        reason = RefreshLegacyCombatGearUpdateTargetPlayerParticipants(
            ref defenderContext, attackerConnection, ref attackerSession, ref defenderSession,
            attackerUid, defenderUid);
        return (defenderSession, attackerSession, reason);
    }
}
