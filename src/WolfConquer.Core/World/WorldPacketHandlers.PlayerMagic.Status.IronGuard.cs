using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyIronGuardExpiresAtUtcAttribute = "LegacyIronGuardExpiresAtUtc";
    private const ushort LegacyIronGuardAttachStatusSpellId = 15_470;
    private const string RuntimeLegacyIronGuardRemoveOnDeadAttribute = "LegacyIronGuardRemoveOnDead";
    private const string RuntimeLegacyIronGuardFuryStrikeHitpointsAttribute = "FuryStrikeHP";
    private const string RuntimeLegacyIronGuardFuryStrikeAttackAttribute = "FuryStrikeAttack";

    private static async ValueTask<bool> HandleLegacyPlayerIronGuardAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyIronGuardAttachStatusSpellId)
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

        var updatedSession = ApplyLegacyIronGuardStatus(resourceSession, spell, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyIronGuardSkillCountdownUpdateToVisibleAsync(
                updatedContext,
                updatedSession,
                spell.Duration,
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
        await SendPlayerEquipmentAppearanceRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy IronGuard AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyIronGuardFlag} duration={spell.Duration} " +
                $"furyStrikeHP={spell.DamageOnHuman} furyStrikeAttack={unchecked((byte)spell.Damage)}; " +
                "source=AttachStatus.cs case 15470.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyIronGuardStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithLegacyStatusFlag(session.Character, LegacyIronGuardFlag),
                    RuntimeLegacyIronGuardFuryStrikeHitpointsAttribute,
                    spell.DamageOnHuman),
                RuntimeLegacyIronGuardFuryStrikeAttackAttribute,
                unchecked((byte)spell.Damage)),
            RuntimeLegacyIronGuardRemoveOnDeadAttribute,
            "true");
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyIronGuardExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return session with { Character = updatedCharacter };
    }

    private static async ValueTask SendLegacyIronGuardSkillCountdownUpdateToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint duration,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.SkillCountdown,
                    Value: 0,
                    Flag: checked((uint)LegacyIronGuardFlag),
                    Time: duration,
                    Damage: 0,
                    SpellLevel: 0),
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
