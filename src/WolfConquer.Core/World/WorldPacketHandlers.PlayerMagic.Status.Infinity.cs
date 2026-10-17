using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyFlyExpiresAtUtcAttribute = "LegacyFlyExpiresAtUtc";
    internal const string RuntimeLegacyInfinityExpiresAtUtcAttribute = "LegacyInfinityExpiresAtUtc";
    private const ushort LegacyInfinityAttachStatusSpellId = 14_380;
    private const string RuntimeLegacyFlyRemoveOnDeadAttribute = "LegacyFlyRemoveOnDead";
    private const string RuntimeLegacyInfinityRemoveOnDeadAttribute = "LegacyInfinityRemoveOnDead";

    private static async ValueTask<bool> HandleLegacyPlayerInfinityAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyInfinityAttachStatusSpellId)
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

        var nowUtc = DateTimeOffset.UtcNow;
        var flySession = ApplyLegacyFlyStatus(resourceSession, spell, nowUtc);
        context.Sessions?.Set(context.Connection, flySession);
        var flyContext = context with { Session = flySession };
        await SendPlayerStatusFlagVectorToVisibleAsync(flyContext, flySession, cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = ApplyLegacyInfinityStatus(flySession, spell, nowUtc);
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

        await SendLegacyInfinityUpdateToVisibleAsync(
                updatedContext,
                updatedSession,
                spell.Duration,
                playerSpell.Level,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Infinity AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"flyFlag={LegacyFlyFlag} infinityFlag={LegacyInfinityFlag} duration={spell.Duration} " +
                $"infinitySDamage={spell.DamageOnHuman} infinityDamage={spell.DamageOnMonster}; " +
                "source=AttachStatus.cs case 14380.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyFlyStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithLegacyStatusFlag(session.Character, LegacyFlyFlag);
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyFlyExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        updatedCharacter = WithCharacterAttribute(updatedCharacter, RuntimeLegacyFlyRemoveOnDeadAttribute, "true");
        return session with { Character = updatedCharacter };
    }

    private static WorldSession ApplyLegacyInfinityStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(session.Character, LegacyInfinityFlag),
                "InfinitySDamage",
                spell.DamageOnHuman),
            "InfinityDamage",
            spell.DamageOnMonster);
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyInfinityExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        updatedCharacter = WithCharacterAttribute(updatedCharacter, RuntimeLegacyInfinityRemoveOnDeadAttribute, "true");
        return session with { Character = updatedCharacter };
    }

    private static async ValueTask SendLegacyInfinityUpdateToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint duration,
        uint spellLevel,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.Infinity,
                    Value: 0,
                    Flag: checked((uint)LegacyInfinityFlag),
                    Time: duration,
                    Damage: 0,
                    SpellLevel: spellLevel),
            ]);

        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                session,
                packet.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
