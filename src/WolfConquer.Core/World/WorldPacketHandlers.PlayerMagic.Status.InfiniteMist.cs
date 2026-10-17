using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyInfiniteMistRemoveOnDeadAttribute = "LegacyInfiniteMistRemoveOnDead";

    private static async ValueTask<bool> HandleLegacyPlayerInfiniteMistAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyInfiniteMistSpellId)
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

        var resourceContext = context with { Session = resourceSession };
        if (!IsLegacyNinjaClass(resourceSession.Character.Class))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy InfiniteMist AttachStatus magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id}; caster is not Ninja.")
                .ConfigureAwait(false);
            return true;
        }

        if (!TryGetVisibleSessionByUid(
                resourceContext,
                resourceSession,
                query.OpponentUid,
                out var activeTarget))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy InfiniteMist AttachStatus magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} targetUid={query.OpponentUid}; target is not visible.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedTargetSession = ApplyLegacyInfiniteMistAttachStatus(activeTarget.Session, spell, DateTimeOffset.UtcNow);
        context.Sessions?.Set(activeTarget.Connection, updatedTargetSession);
        var targetContext = context with
        {
            Connection = activeTarget.Connection,
            Session = updatedTargetSession,
        };

        await SendPlayerStatusFlagVectorToVisibleAsync(targetContext, updatedTargetSession, cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            X: 0,
            Y: 0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(resourceContext, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy InfiniteMist AttachStatus magic " +
                $"{FormatSession(resourceContext)} targetUid={updatedTargetSession.Character.Uid} " +
                $"spellId={spell.Id} spellLevel={playerSpell.Level} statusFlag={LegacyInfiniteMistFlag} " +
                $"duration={spell.Duration}; source=AttachStatus.cs case 16380.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyInfiniteMistAttachStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithLegacyStatusFlag(session.Character, LegacyInfiniteMistFlag);
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyInfiniteMistExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyInfiniteMistRemoveOnDeadAttribute,
            "true");
        return session with { Character = updatedCharacter };
    }
}
