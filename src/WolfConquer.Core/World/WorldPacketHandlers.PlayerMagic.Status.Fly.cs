using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyFlyAttachStatusSpellId = 8_003;
    private const string LegacyFlyBlockedMessage = "You can't use this skill right now!";

    private static async ValueTask<bool> HandleLegacyPlayerFlyAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyFlyAttachStatusSpellId)
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

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var resourceContext = context with { Session = resourceSession };
        if (IsLegacyFlyBlocked(resourceSession))
        {
            await resourceContext.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(LegacyFlyBlockedMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer blocked legacy Fly AttachStatus magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} spellLevel={playerSpell.Level}; " +
                    "source=AttachStatus.cs case 8003 transform/Ride/PathOfShadow gate.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = ApplyLegacyFlyStatus(resourceSession, spell, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: 0,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(casterUid, spell.Duration, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Fly AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyFlyFlag} duration={spell.Duration} removeOnDead=True " +
                "source=AttachStatus.cs case 8003.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyFlyBlocked(WorldSession session)
    {
        return ReadAttributeUInt64(session.Character.Attributes, "TransformationID", "TransformationId") != 0 ||
            HasLegacyStatusFlag(session.Character, LegacyRideFlag) ||
            HasLegacyStatusFlag(session.Character, LegacyPathOfShadowFlag);
    }
}
