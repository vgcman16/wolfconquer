using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyRevengeTailExpiresAtUtcAttribute = "LegacyRevengeTailExpiresAtUtc";
    private const string RuntimeLegacyRevengeTailRemoveOnDeadAttribute = "LegacyRevengeTailRemoveOnDead";
    private const string RuntimeLegacyRevengeTailStampSecondsAttribute = "LegacyRevengeTailStampSeconds";
    private const int LegacyRevengeTailDurationSeconds = 10;
    private const int LegacyRevengeTailStampSeconds = 20;
    private const uint LegacyRevengeTailDefaultChanges = 5;
    private const uint LegacyRevengeTailRevengeGaleChanges = 100_000;

    private static async ValueTask<bool> HandleLegacyPlayerRevengeTailAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyRevengeTailSpellId)
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

        if (HasLegacyStatusFlag(resourceSession.Character, LegacyRevengeTailFlag))
        {
            var toggledSession = resourceSession with
            {
                Character = ClearLegacyRevengeTailStatus(resourceSession.Character),
            };
            context.Sessions?.Set(context.Connection, toggledSession);
            var toggledContext = context with { Session = toggledSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(toggledContext, toggledSession, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy RevengeTail AttachStatus toggle " +
                    $"{FormatSession(toggledContext)} spellId={spell.Id} removedStatusFlag={LegacyRevengeTailFlag}; " +
                    "source=AttachStatus.cs case 13030.")
                .ConfigureAwait(false);
            return true;
        }

        var selfTarget = query.OpponentUid == casterUid;
        var updatedSession = resourceSession;
        var updatedContext = resourceContext;
        if (selfTarget)
        {
            var revengeTailChange = HasLegacyEquippedRune(
                resourceSession.PlayerItems,
                context.WorldData,
                "RevengeGale")
                ? LegacyRevengeTailRevengeGaleChanges
                : LegacyRevengeTailDefaultChanges;
            updatedSession = ApplyLegacyRevengeTailStatus(
                resourceSession,
                DateTimeOffset.UtcNow,
                revengeTailChange);
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);
        }

        var animationTargets = selfTarget
            ?
            [
                new LegacySpellAnimationTarget(
                    casterUid,
                    0,
                    Hit: true,
                    MsgAttackEffect.None),
            ]
            : Array.Empty<LegacySpellAnimationTarget>();
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

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy RevengeTail AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"selfTarget={selfTarget} statusFlag={LegacyRevengeTailFlag} " +
                $"revengeTailChange={ReadAttributeUInt32(updatedSession.Character.Attributes, RuntimeLegacyRevengeTailChangeAttribute)} " +
                $"duration={LegacyRevengeTailDurationSeconds} stampSeconds={LegacyRevengeTailStampSeconds}; " +
                "source=AttachStatus.cs case 13030.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyRevengeTailStatus(
        WorldSession session,
        DateTimeOffset nowUtc,
        uint revengeTailChange)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithCharacterAttribute(
                        WithLegacyStatusFlag(session.Character, LegacyRevengeTailFlag),
                        RuntimeLegacyRevengeTailChangeAttribute,
                        revengeTailChange),
                    RuntimeLegacyRevengeTailExpiresAtUtcAttribute,
                    nowUtc
                        .ToUniversalTime()
                        .AddSeconds(LegacyRevengeTailDurationSeconds)
                        .ToString("O", CultureInfo.InvariantCulture)),
                RuntimeLegacyRevengeTailRemoveOnDeadAttribute,
                "true"),
            RuntimeLegacyRevengeTailStampSecondsAttribute,
            LegacyRevengeTailStampSeconds);
        return session with { Character = updatedCharacter };
    }

    private static CharacterSnapshot ClearLegacyRevengeTailStatus(CharacterSnapshot character)
    {
        return WithoutCharacterAttribute(
            WithoutCharacterAttribute(
                WithoutCharacterAttribute(
                    WithCharacterAttribute(
                        WithoutLegacyStatusFlag(character, LegacyRevengeTailFlag),
                        RuntimeLegacyRevengeTailChangeAttribute,
                        0),
                    RuntimeLegacyRevengeTailExpiresAtUtcAttribute),
                RuntimeLegacyRevengeTailRemoveOnDeadAttribute),
            RuntimeLegacyRevengeTailStampSecondsAttribute);
    }
}
