using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyAttackStatus15560SpellId = 15_560;
    private const int LegacyDuelFlag = 0xE1;
    private const string RuntimeLegacyDuelExpiresAtUtcAttribute = "LegacyDuelExpiresAtUtc";

    private static async ValueTask<bool> HandleLegacyPlayerAttackStatus15560MagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttackStatus ||
            spell.Id != LegacyAttackStatus15560SpellId)
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

        var animationTargets = new List<LegacySpellAnimationTarget>();
        var handledTarget = false;
        var resourceContext = context with { Session = resourceSession };
        if (TryGetVisibleSessionByUid(resourceContext, resourceSession, query.OpponentUid, out var target) &&
            LegacyGetDistance(
                resourceSession.Character.X,
                resourceSession.Character.Y,
                target.Session.Character.X,
                target.Session.Character.Y) < spell.Range &&
            CanLegacyPlayerMagicDamageTarget(resourceContext, resourceSession, target.Session, spell, out _))
        {
            handledTarget = true;
            var duelPercent = unchecked((byte)spell.Damage);
            var updatedTargetCharacter = WithCharacterAttribute(
                WithLegacyStatusFlag(target.Session.Character, LegacyDuelFlag),
                "DuelPercent",
                duelPercent);
            if (spell.Duration > 0)
            {
                updatedTargetCharacter = WithCharacterAttribute(
                    updatedTargetCharacter,
                    RuntimeLegacyDuelExpiresAtUtcAttribute,
                    DateTimeOffset.UtcNow.AddSeconds(spell.Duration).ToString("O"));
            }

            var updatedTargetSession = target.Session with { Character = updatedTargetCharacter };
            context.Sessions?.Set(target.Connection, updatedTargetSession);
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    resourceContext,
                    target.Connection,
                    updatedTargetSession,
                    cancellationToken)
                .ConfigureAwait(false);

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(updatedTargetSession.Character.Uid, nameof(updatedTargetSession.Character.Uid)),
                Damage: 0,
                Hit: true,
                MsgAttackEffect.None));
        }

        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid)),
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(resourceContext, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                resourceContext,
                resourceSession,
                playerSpell,
                spell,
                totalDamage: 0,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AttackStatus 15560 magic " +
                $"{FormatSession(resourceContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} handledTarget={handledTarget} " +
                $"targets={animationTargets.Count} totalDamage=0.")
            .ConfigureAwait(false);
        return true;
    }
}
