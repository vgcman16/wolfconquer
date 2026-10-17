using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyCastPrayExpiresAtUtcAttribute = "LegacyCastPrayExpiresAtUtc";

    private const string RuntimeLegacyCastPrayRemoveOnDeadAttribute = "LegacyCastPrayRemoveOnDead";
    private const ushort LegacyCastPrayAttachStatusSpellId = 9_876;
    private const int LegacyCastPrayDurationSeconds = 2_592_000;

    private static async ValueTask<bool> HandleLegacyPlayerCastPrayAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyCastPrayAttachStatusSpellId)
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

        var updatedSession = ApplyLegacyCastPrayStatus(resourceSession, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
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
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CastPray AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyCastPrayFlag} durationSeconds={LegacyCastPrayDurationSeconds} " +
                "removeOnDead=True; source=AttachStatus.cs case 9876.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyCastPrayStatus(
        WorldSession session,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(session.Character, LegacyCastPrayFlag),
                RuntimeLegacyCastPrayExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(LegacyCastPrayDurationSeconds)
                    .ToString("O", CultureInfo.InvariantCulture)),
            RuntimeLegacyCastPrayRemoveOnDeadAttribute,
            "true");
        return session with { Character = updatedCharacter };
    }
}
