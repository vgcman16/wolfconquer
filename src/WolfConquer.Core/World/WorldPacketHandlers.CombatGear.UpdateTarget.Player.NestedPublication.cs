using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession,
        LegacyPlayerMagicReceiveAbortReason AbortReason, CharacterSnapshot PublishedCharacter, bool Published)>
        SendLegacyCombatGearMagneticPlayerPrefixAsync(
            WorldPacketHandlerContext defenderContext, ConquerTcpConnection attackerConnection,
            WorldSession attackerSession, WorldSession defenderSession,
            LegacyNestedProcAuthorization authorization,
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
            return (defenderSession, attackerSession, reason, defenderSession.Character, false);

        if (!ReferenceEquals(defenderSession.Character, authorization.Owner) ||
            !ReferenceEquals(attackerSession.Character, authorization.Target))
            return (defenderSession, attackerSession, reason, defenderSession.Character, false);

        // Source CombatGearAttack 20220 enters this self prefix BEFORE target lookup.
        var proposed = defenderSession with
        {
            Character = WithLegacySourceAddSpellFlag(defenderSession.Character,
                LegacyVenomFlag | LegacyMagicShieldFlag, removeOnDead: true),
        };
        var uid = CheckedUInt32(defenderUid, nameof(defenderUid));
        var vector = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(uid,
            CreateLegacyRuntimeStatusFlagVector(proposed.Character)).ToFrameBytes();
        var archive = new LegacyMsgUpdatePacket(uid, [new(LegacyMsgUpdateDataType.ArchiveSkill,
            Value: 0, Flag: LegacyVenomFlag | LegacyMagicShieldFlag, Time: 10)]).ToFrameBytes();
        if (authorization.Receipt is { } committed)
        {
            if (!committed.TryAdvanceEffect(defenderSession, proposed, out var prefix))
                return (defenderSession, attackerSession, Refresh(), defenderSession.Character, false);
            authorization.Receipt = prefix; defenderSession = prefix!.Published;
        }
        else { defenderSession = proposed; defenderContext.Sessions?.Set(defenderContext.Connection, defenderSession); }
        var publishedCharacter = defenderSession.Character; // Capture OUR publication before any transport callback.
        defenderContext = defenderContext with { Session = defenderSession };
        if (authorization.Receipt is { } vectorOwner)
        {
            if (!await SendLegacyProcVisibleAsync(defenderContext, vectorOwner, vector, cancellationToken).ConfigureAwait(false))
                return (defenderSession, attackerSession, Refresh(), publishedCharacter, false);
        }
        else await SendPlayerStatusFlagVectorToVisibleAsync(defenderContext, defenderSession, cancellationToken).ConfigureAwait(false);
        reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None)
            return (defenderSession, attackerSession, reason, publishedCharacter, false);

        if (authorization.Receipt is { } archiveOwner)
        {
            if (!await SendLegacyProcVisibleAsync(defenderContext, archiveOwner, archive, cancellationToken).ConfigureAwait(false))
                return (defenderSession, attackerSession, Refresh(), publishedCharacter, false);
        }
        else await SendLegacyCombatGearArchiveSkillUpdateAsync(
                defenderContext, defenderSession, LegacyVenomFlag | LegacyMagicShieldFlag,
                duration: 10, damage: 0, spellLevel: 0, visible: true, cancellationToken)
            .ConfigureAwait(false);
        reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None)
            return (defenderSession, attackerSession, reason, publishedCharacter, false);

        await defenderContext.Log.WriteLineAsync(
                "WorldServer sent legacy CombatGear UpdateTarget passive status " +
                $"{FormatSession(defenderContext)} spellId={LegacyCombatGearMagneticLightSpellId} " +
                $"statusFlag={LegacyVenomFlag | LegacyMagicShieldFlag} duration=10; " +
                "equipmentRefreshPackets=0; " +
                "source=MyCombatGear.UpdateTarget -> MyCombatGear.ProcescMagic(ignoreStamp).")
            .ConfigureAwait(false);
        reason = Refresh();
        return (defenderSession, attackerSession, reason, publishedCharacter, true);
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
