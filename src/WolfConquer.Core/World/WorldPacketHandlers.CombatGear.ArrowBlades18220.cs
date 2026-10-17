using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> ExecuteLegacyPreparedCombatGearArrowBlades18220Async(
        WorldPacketHandlerContext context, WorldSession session, MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell, LegacyMagicRuntimeSpell spell, CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session) ||
            !HasLegacyStatusFlag(session.Character, LegacyArrowBladesFlagRegistration.WaterFlag)) return true;
        context = context with { Session = session };
        var anchorX = query.X;
        var anchorY = query.Y;
        var player = context.Sessions is { } sessions
            ? FindLegacyMapBlockPlayer(sessions, session, query.OpponentUid) : null;
        if (player is not null)
        {
            anchorX = CheckedUInt16(player.Session.Character.X, nameof(anchorX));
            anchorY = CheckedUInt16(player.Session.Character.Y, nameof(anchorY));
        }
        else if (FindLegacyHellVortexSelectedMonster(context, session, query.OpponentUid) is { } monster)
        {
            anchorX = monster.X;
            anchorY = monster.Y;
        }
        var originX = session.Character.X;
        var originY = session.Character.Y;
        bool InLine(long x, long y) => LegacyGetDistance(originX, originY, x, y) <= 15 &&
            IsLegacyInLine(originX, originY, anchorX, anchorY, x, y, 15, 4);
        var targets = new List<LegacySpellAnimationTarget>();
        session = await ReceiveLegacyCombatGearPhysicalLineMonstersAsync(
                context, session, uid, InLine, spell, targets, cancellationToken, includeStoneCrackerUpdate: true)
            .ConfigureAwait(false);
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return true;
        context = context with { Session = session };
        var current = await ReceiveLegacyCombatGearPhysicalLinePlayersAsync(
                context, session, uid, InLine, spell, targets, cancellationToken).ConfigureAwait(false);
        if (current is null) return true;
        session = current;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return true;
        context = context with { Session = session };
        current = await ReceiveLegacyCombatGearPhysicalLineSobNpcsAsync(
                context, session, uid, InLine, spell, targets, cancellationToken).ConfigureAwait(false);
        if (current is null) return true;
        session = current;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session) || targets.Count == 0) return true;
        context = context with { Session = session };
        // Matching Base.CalculateSoul is identity, including the second monster enqueue call.
        var animation = new LegacySpellAnimationPacket(CheckedUInt32(uid, nameof(uid)), AttackedUid: 0,
            anchorX, anchorY, spell.Id, (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0, LevelHu: playerSpell.UseSpellSoul, SpellEffect: 0, targets);
        foreach (var frame in animation.ToFrames())
        {
            current = await SendLegacyArrowBladesVisibleFrameAsync(
                    context, session, uid, frame, cancellationToken).ConfigureAwait(false);
            if (current is null) return true;
            session = current;
            context = context with { Session = session };
        }
        current = await RemoveLegacyArrowBladesFlagAsync(
                context, session, uid, LegacyArrowBladesFlagRegistration.WaterFlag, cancellationToken)
            .ConfigureAwait(false);
        if (current is null) return true;
        session = current;
        current = await ReplaceLegacyArrowBladesFlagAsync(
                context with { Session = session }, session, uid, LegacyArrowBladesFlagRegistration.FireFlag, cancellationToken)
            .ConfigureAwait(false);
        if (current is null) return true;
        session = current;
        current = await SendLegacyArrowBladesOwnerUpdateAsync(
                context with { Session = session }, session, uid, LegacyArrowBladesFlagRegistration.FireFlag,
                (LegacyMsgUpdateDataType)137, unchecked((uint)spell.Damage2), cancellationToken).ConfigureAwait(false);
        if (current is null) return true;
        session = current;
        if (ReadAttributeUInt32(session.Character.Attributes, RuntimeLegacyCombatGearArrowBladesAttribute) >= 3)
        {
            current = await ReplaceLegacyArrowBladesFlagAsync(
                    context with { Session = session }, session, uid, LegacyArrowBladesFlagRegistration.WaterFlag,
                    cancellationToken).ConfigureAwait(false);
            if (current is null) return true;
            session = current;
            current = await SendLegacyArrowBladesOwnerUpdateAsync(
                    context with { Session = session }, session, uid, LegacyArrowBladesFlagRegistration.WaterFlag,
                    LegacyMsgUpdateDataType.AppendIcon, 2, cancellationToken).ConfigureAwait(false);
            if (current is null) return true;
            session = WithUpdatedLegacyCombatGearAttackerAttribute(
                context with { Session = current }, current, RuntimeLegacyCombatGearArrowBladesAttribute, 0);
        }
        return true;
    }
}
