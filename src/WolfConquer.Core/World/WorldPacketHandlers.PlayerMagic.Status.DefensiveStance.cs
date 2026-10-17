using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyDefensiveStanceExpiresAtUtcAttribute = "LegacyDefensiveStanceExpiresAtUtc";

    private const ushort LegacyDefensiveStanceSpellId = 11_160;
    private const int LegacyDefensiveStanceFlag = 0x7E;

    private static async ValueTask<bool> HandleLegacyPlayerDefensiveStanceAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyDefensiveStanceSpellId)
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

        var hadRide = HasLegacyStatusFlag(resourceSession.Character, LegacyRideFlag);
        var hadDefensiveStance = HasLegacyStatusFlag(resourceSession.Character, LegacyDefensiveStanceFlag);
        var updatedSession = ApplyLegacyDefensiveStanceToggle(
            resourceSession,
            spell,
            hadRide,
            hadDefensiveStance,
            DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        if (!hadDefensiveStance)
        {
            await SendLegacyDefensiveStanceUpdateToVisibleAsync(
                    updatedContext,
                    updatedSession,
                    spell,
                    playerSpell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy DefensiveStance AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyDefensiveStanceFlag} toggledOn={!hadDefensiveStance} " +
                $"duration={spell.Duration} damage={spell.Damage} removedRide={hadRide} " +
                "removeOnDead=False; source=AttachStatus.cs case 11160.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyDefensiveStanceToggle(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        bool hadRide,
        bool hadDefensiveStance,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = hadRide
            ? WithoutLegacyStatusFlag(session.Character, LegacyRideFlag)
            : session.Character;

        if (hadDefensiveStance)
        {
            updatedCharacter = WithoutCharacterAttribute(
                WithoutLegacyStatusFlag(updatedCharacter, LegacyDefensiveStanceFlag),
                RuntimeLegacyDefensiveStanceExpiresAtUtcAttribute);
            return session with { Character = updatedCharacter };
        }

        updatedCharacter = WithLegacyStatusFlag(updatedCharacter, LegacyDefensiveStanceFlag);
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyDefensiveStanceExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return session with { Character = updatedCharacter };
    }

    private static async ValueTask SendLegacyDefensiveStanceUpdateToVisibleAsync(
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
                    LegacyMsgUpdateDataType.DefensiveStance,
                    Value: 0,
                    Flag: checked((uint)LegacyDefensiveStanceFlag),
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
