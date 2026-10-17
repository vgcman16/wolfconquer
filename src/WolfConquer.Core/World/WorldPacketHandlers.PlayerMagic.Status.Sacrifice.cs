using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacySacrificeExpiresAtUtcAttribute = "LegacySacrificeExpiresAtUtc";
    private const ushort LegacySacrificeAttachStatusSpellId = 14_320;
    private const int LegacySacrificeDurationSeconds = 2_592_000;

    private static async ValueTask<bool> HandleLegacyPlayerSacrificeAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacySacrificeAttachStatusSpellId)
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

        if (HasLegacyStatusFlag(resourceSession.Character, LegacySacrificeFlag))
        {
            var toggledSession = resourceSession with
            {
                Character = ClearLegacySacrificeStatus(resourceSession.Character),
            };
            context.Sessions?.Set(context.Connection, toggledSession);
            var toggledContext = context with { Session = toggledSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(toggledContext, toggledSession, cancellationToken)
                .ConfigureAwait(false);
            await SendPlayerEquipmentAppearanceRefreshAsync(toggledContext, toggledSession, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled legacy Sacrifice AttachStatus toggle " +
                    $"{FormatSession(toggledContext)} spellId={spell.Id} removedStatusFlag={LegacySacrificeFlag} " +
                    "equipmentRefreshPackets=1 source=AttachStatus.cs case 14320.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = ApplyLegacySacrificeStatus(resourceSession, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

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
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);
        await SendPlayerEquipmentAppearanceRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                500,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Sacrifice AttachStatus magic " +
                $"{FormatSession(updatedContext with { Session = updatedSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} statusFlag={LegacySacrificeFlag} " +
                $"duration={LegacySacrificeDurationSeconds} removeOnDead=False; " +
                "source=AttachStatus.cs case 14320.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacySacrificeStatus(
        WorldSession session,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithLegacyStatusFlag(session.Character, LegacySacrificeFlag),
            RuntimeLegacySacrificeExpiresAtUtcAttribute,
            nowUtc
                .ToUniversalTime()
                .AddSeconds(LegacySacrificeDurationSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
        return session with { Character = updatedCharacter };
    }

    private static CharacterSnapshot ClearLegacySacrificeStatus(CharacterSnapshot character)
    {
        return WithoutCharacterAttribute(
            WithoutLegacyStatusFlag(character, LegacySacrificeFlag),
            RuntimeLegacySacrificeExpiresAtUtcAttribute);
    }
}
