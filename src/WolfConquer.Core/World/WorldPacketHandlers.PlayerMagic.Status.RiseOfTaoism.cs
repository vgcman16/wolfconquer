using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyRiseOfTaoismExpiresAtUtcAttribute = "LegacyRiseOfTaoismExpiresAtUtc";
    private const ushort LegacyRiseOfTaoismAttachStatusSpellId = 15_440;
    private const string RuntimeLegacyRiseOfTaoismRemoveOnDeadAttribute = "LegacyRiseOfTaoismRemoveOnDead";
    private const string RuntimeLegacyRiseOfTaoismExtraMagicDamageAttribute = "RiseofTaoismExtraMDamage";
    private const uint LegacyRiseOfTaoismFineRainDamage = 50_000;

    private static async ValueTask<bool> HandleLegacyPlayerRiseOfTaoismAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyRiseOfTaoismAttachStatusSpellId)
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

        var updatedSession = ApplyLegacyRiseOfTaoismStatus(resourceSession, spell, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyRiseOfTaoismFineRainUpdateToVisibleAsync(
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
                "WorldServer handled legacy RiseofTaoism AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyRiseOfTaoismFlag} duration={spell.Duration} " +
                $"riseofTaoismExtraMDamage={spell.Damage} fineRainDamage={LegacyRiseOfTaoismFineRainDamage}; " +
                "source=AttachStatus.cs case 15440.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyRiseOfTaoismStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(session.Character, LegacyRiseOfTaoismFlag),
                RuntimeLegacyRiseOfTaoismExtraMagicDamageAttribute,
                spell.Damage),
            RuntimeLegacyRiseOfTaoismRemoveOnDeadAttribute,
            "true");
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyRiseOfTaoismExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return session with { Character = updatedCharacter };
    }

    private static async ValueTask SendLegacyRiseOfTaoismFineRainUpdateToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint duration,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.FineRain,
                    Value: 0,
                    Flag: checked((uint)LegacyRiseOfTaoismFlag),
                    Time: duration,
                    Damage: LegacyRiseOfTaoismFineRainDamage,
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
