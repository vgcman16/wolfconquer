using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyCrackStarExpiresAtUtcAttribute = "LegacyCrackStarExpiresAtUtc";

    private const ushort LegacyCrackStarAttachStatusSpellId = 15_380;
    private const string RuntimeLegacyCrackStarRemoveOnDeadAttribute = "LegacyCrackStarRemoveOnDead";

    private static async ValueTask<bool> HandleLegacyPlayerCrackStarAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyCrackStarAttachStatusSpellId)
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
        var isWindWalker = IsLegacyPromotionWindWalkerClass(resourceSession.Character.Class);
        var onMeleeAttack = IsLegacyWindWalkerMeleeBranch(resourceSession.Character);
        if (!isWindWalker || !onMeleeAttack)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CrackStar AttachStatus magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} " +
                    $"class={resourceSession.Character.Class} isWindWalker={isWindWalker} " +
                    $"onMeleeAttack={onMeleeAttack}; source=AttachStatus.cs case 15380 gate.")
                .ConfigureAwait(false);
            return true;
        }

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var interaction = new MsgAttackPacket(query with { Uid = casterUid });
        await resourceContext.Connection.SendAsync(interaction.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = ApplyLegacyCrackStarStatus(resourceSession, spell, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

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
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CrackStar AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyCrackStarFlag} duration={spell.Duration}; " +
                "source=AttachStatus.cs case 15380.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyCrackStarStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithLegacyStatusFlag(session.Character, LegacyCrackStarFlag);
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyCrackStarExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyCrackStarRemoveOnDeadAttribute,
            "true");
        return session with { Character = updatedCharacter };
    }
}
