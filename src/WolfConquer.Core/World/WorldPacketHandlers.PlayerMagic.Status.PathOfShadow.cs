using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyPathOfShadowExpiresAtUtcAttribute = "LegacyPathOfShadowExpiresAtUtc";

    private const ushort LegacyPathOfShadowSpellId = 11_620;
    private const int LegacyPathOfShadowDurationSeconds = 2_592_000;

    private static async ValueTask<bool> HandleLegacyPlayerPathOfShadowAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyPathOfShadowSpellId)
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

        var hadPathOfShadow = HasLegacyStatusFlag(resourceSession.Character, LegacyPathOfShadowFlag);
        var updatedSession = ApplyLegacyPathOfShadowToggle(resourceSession, hadPathOfShadow, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy PathOfShadow AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyPathOfShadowFlag} toggledOn={!hadPathOfShadow} " +
                $"duration={LegacyPathOfShadowDurationSeconds} removeOnDead=False " +
                $"clearedKineticSpark={hadPathOfShadow}; source=AttachStatus.cs case 11620.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyPathOfShadowToggle(
        WorldSession session,
        bool hadPathOfShadow,
        DateTimeOffset nowUtc)
    {
        if (hadPathOfShadow)
        {
            var removedCharacter = WithoutCharacterAttribute(
                WithoutCharacterAttribute(
                    WithoutLegacyStatusFlag(
                        WithoutLegacyStatusFlag(session.Character, LegacyPathOfShadowFlag),
                        LegacyKineticSparkFlag),
                    RuntimeLegacyPathOfShadowExpiresAtUtcAttribute),
                RuntimeLegacyKineticSparkExpiresAtUtcAttribute);
            return session with { Character = removedCharacter };
        }

        var updatedCharacter = WithCharacterAttribute(
            WithLegacyStatusFlag(session.Character, LegacyPathOfShadowFlag),
            RuntimeLegacyPathOfShadowExpiresAtUtcAttribute,
            nowUtc
                .ToUniversalTime()
                .AddSeconds(LegacyPathOfShadowDurationSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
        return session with { Character = updatedCharacter };
    }
}
