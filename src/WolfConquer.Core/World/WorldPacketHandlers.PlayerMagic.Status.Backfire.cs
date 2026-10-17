using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyBackfireExpiresAtUtcAttribute = "LegacyBackfireExpiresAtUtc";

    private const int LegacyBackfireDurationSeconds = 10;

    private static async ValueTask<bool> HandleLegacyPlayerBackfireAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyBackfireSpellId)
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

        var updatedSession = ApplyLegacyBackfireStatus(resourceSession, DateTimeOffset.UtcNow);
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
                "WorldServer handled legacy Backfire AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyBackfireFlag} duration={LegacyBackfireDurationSeconds} " +
                "removeOnDead=False; source=AttachStatus.cs case 12680.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyBackfireStatus(
        WorldSession session,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithLegacyStatusFlag(session.Character, LegacyBackfireFlag),
            RuntimeLegacyBackfireExpiresAtUtcAttribute,
            nowUtc
                .ToUniversalTime()
                .AddSeconds(LegacyBackfireDurationSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
        return session with { Character = updatedCharacter };
    }
}
