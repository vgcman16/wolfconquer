using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyAutoHuntExpiresAtUtcAttribute = "LegacyAutoHuntExpiresAtUtc";
    internal const string RuntimeLegacyAutoHuntStampSecondsAttribute = "LegacyAutoHuntStampSeconds";

    private const ushort LegacyAutoHuntAttachStatusSpellId = 12_270;
    private const int LegacyAutoHuntAttachStatusFlag = 0x94;
    private const int LegacyAutoHuntAttachStatusDurationSeconds = 2_592_000;
    private const int LegacyAutoHuntAttachStatusStampSeconds = 8;

    private static async ValueTask<bool> HandleLegacyPlayerAutoHuntAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyAutoHuntAttachStatusSpellId)
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
        var interaction = new MsgAttackPacket(query with { Uid = casterUid });
        await resourceContext.Connection.SendAsync(interaction.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        if (HasLegacyStatusFlag(resourceSession.Character, LegacyAutoHuntAttachStatusFlag))
        {
            var toggledSession = resourceSession with
            {
                Character = ClearLegacyAutoHuntAttachStatus(resourceSession.Character),
            };
            context.Sessions?.Set(context.Connection, toggledSession);
            var toggledContext = context with { Session = toggledSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(toggledContext, toggledSession, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy AutoHunt AttachStatus toggle " +
                    $"{FormatSession(toggledContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                    $"removedStatusFlag={LegacyAutoHuntAttachStatusFlag}; " +
                    "source=AttachStatus.cs case 12270.")
                .ConfigureAwait(false);
            return true;
        }

        var targetIsCaster = query.OpponentUid == casterUid;
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var updatedSession = resourceSession;
        var updatedContext = resourceContext;
        if (targetIsCaster)
        {
            updatedSession = ApplyLegacyAutoHuntAttachStatus(resourceSession, DateTimeOffset.UtcNow);
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };

            await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);

            animationTargets.Add(new LegacySpellAnimationTarget(
                casterUid,
                0,
                Hit: true,
                MsgAttackEffect.None));
        }

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var progressSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                spell.Duration,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AutoHunt AttachStatus magic " +
                $"{FormatSession(updatedContext with { Session = progressSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} targetUid={query.OpponentUid} selfTarget={targetIsCaster} " +
                $"statusFlag={LegacyAutoHuntAttachStatusFlag} toggledOn={targetIsCaster} " +
                $"durationSeconds={LegacyAutoHuntAttachStatusDurationSeconds} " +
                $"stampSeconds={LegacyAutoHuntAttachStatusStampSeconds}; " +
                "source=AttachStatus.cs case 12270.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyAutoHuntAttachStatus(
        WorldSession session,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(session.Character, LegacyAutoHuntAttachStatusFlag),
                RuntimeLegacyAutoHuntExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(LegacyAutoHuntAttachStatusDurationSeconds)
                    .ToString("O", CultureInfo.InvariantCulture)),
            RuntimeLegacyAutoHuntStampSecondsAttribute,
            LegacyAutoHuntAttachStatusStampSeconds);

        return session with { Character = updatedCharacter };
    }

    private static CharacterSnapshot ClearLegacyAutoHuntAttachStatus(CharacterSnapshot character)
    {
        return WithoutCharacterAttribute(
            WithoutCharacterAttribute(
                WithoutLegacyStatusFlag(character, LegacyAutoHuntAttachStatusFlag),
                RuntimeLegacyAutoHuntExpiresAtUtcAttribute),
            RuntimeLegacyAutoHuntStampSecondsAttribute);
    }
}
