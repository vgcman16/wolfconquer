using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyShieldBlockExpiresAtUtcAttribute =
        "LegacyShieldBlockExpiresAtUtc";

    private const ushort LegacyShieldBlockSpellId = 10_470;
    private const string RuntimeLegacyShieldBlockDamageAttribute = "ShieldBlockDamage";
    private const string RuntimeLegacyShieldBlockRemoveOnDeadAttribute =
        "LegacyShieldBlockRemoveOnDead";

    private static async Task<bool> HandleLegacyPlayerShieldBlockMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.ShieldBlock ||
            spell.Id != LegacyShieldBlockSpellId)
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

        var updatedSession = ApplyLegacyShieldBlockStatus(
            resourceSession,
            spell,
            DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyShieldBlockAzureShieldUpdateToVisibleAsync(
                updatedContext,
                updatedSession,
                spell,
                playerSpell,
                cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(
            updatedSession.Character.Uid,
            nameof(updatedSession.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: 0,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(
                updatedContext,
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy ShieldBlock magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} statusFlag={LegacyShieldBlockFlag} " +
                $"duration={spell.Duration} shieldBlockDamage={spell.Damage} removeOnDead=True; " +
                "source=ShieldBlock.cs spell 10470.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyShieldBlockStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(session.Character, LegacyShieldBlockFlag),
                RuntimeLegacyShieldBlockDamageAttribute,
                spell.Damage),
            RuntimeLegacyShieldBlockRemoveOnDeadAttribute,
            "true");
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyShieldBlockExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return session with { Character = updatedCharacter };
    }

    private static async ValueTask SendLegacyShieldBlockAzureShieldUpdateToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        PlayerSpellSnapshotEntry playerSpell,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AzureShield,
                    Value: 0,
                    Flag: checked((uint)LegacyShieldBlockFlag),
                    Time: spell.Duration,
                    Damage: spell.Damage,
                    SpellLevel: playerSpell.Level),
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
