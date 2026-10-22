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
            return false;
        return await ExecuteLegacyNativeRiseOfTaoismQueryCastAsync(
            context, query, playerSpell, spell, session, cancellationToken).ConfigureAwait(false);
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
