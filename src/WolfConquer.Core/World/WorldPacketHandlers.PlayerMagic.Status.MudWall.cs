using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyMudWallPassiveSpellId = 16_450;
    private const string RuntimeLegacyMudWallExpiresAtUtcAttribute = "LegacyMudWallExpiresAtUtc";
    private const string RuntimeLegacyMudWallRemoveOnDeadAttribute = "LegacyMudWallRemoveOnDead";

    private static async ValueTask<bool> HandleLegacyPlayerMudWallAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        _ = query;
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id is not (LegacyMudWallSpellId or LegacyMudWallPassiveSpellId))
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

        var updatedSession = ApplyLegacyMudWallStatus(resourceSession, spell, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                LegacyMudWallFlag,
                spell.Duration,
                damage: 0,
                spellLevel: 0,
                visible: false,
                cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
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
        await SendPlayerEquipmentAppearanceRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy MudWall AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} statusFlag={LegacyMudWallFlag} " +
                $"duration={spell.Duration} archiveSkillPackets=1 equipmentRefreshPackets=1; " +
                "source=AttachStatus.cs case 16440/16450.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyMudWallStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithLegacyStatusFlag(session.Character, LegacyMudWallFlag);
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyMudWallExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyMudWallRemoveOnDeadAttribute,
            "true");
        return session with { Character = updatedCharacter };
    }
}
