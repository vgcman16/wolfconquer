using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyIronShieldExpiresAtUtcAttribute = "LegacyIronShieldExpiresAtUtc";
    private const ushort LegacyIronShieldAttachStatusSpellId = 14_160;
    private const string RuntimeLegacyIronShieldRemoveOnDeadAttribute = "LegacyIronShieldRemoveOnDead";
    private const string RuntimeLegacyIronShieldDamageAttribute = "IronShieldDamage";

    private static async ValueTask<bool> HandleLegacyPlayerIronShieldAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyIronShieldAttachStatusSpellId)
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

        var updatedSession = ApplyLegacyIronShieldStatus(resourceSession, spell, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyIronShieldAzureShieldUpdateToVisibleAsync(
                updatedContext,
                updatedSession,
                spell.Duration,
                spell.DamageOnMonster,
                playerSpell.Level,
                cancellationToken)
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

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy IronShield AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyIronShieldFlag} duration={spell.Duration} " +
                $"ironShieldDamage={spell.DamageOnMonster}; source=AttachStatus.cs case 14160.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyIronShieldStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(session.Character, LegacyIronShieldFlag),
                RuntimeLegacyIronShieldDamageAttribute,
                spell.DamageOnMonster),
            RuntimeLegacyIronShieldRemoveOnDeadAttribute,
            "true");
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyIronShieldExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return session with { Character = updatedCharacter };
    }

    private static async ValueTask SendLegacyIronShieldAzureShieldUpdateToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint duration,
        uint damageOnMonster,
        uint spellLevel,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AzureShield,
                    Value: 0,
                    Flag: checked((uint)LegacyIronShieldFlag),
                    Time: duration,
                    Damage: damageOnMonster,
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
