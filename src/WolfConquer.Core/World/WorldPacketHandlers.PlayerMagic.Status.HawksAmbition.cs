using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyHawksAmbitionSpellId = 15_920;
    private const string RuntimeLegacyHawksAmbitionExpiresAtUtcAttribute = "LegacyHawksAmbitionExpiresAtUtc";
    private const string RuntimeLegacyHawksAmbitionRemoveOnDeadAttribute = "LegacyHawksAmbitionRemoveOnDead";

    private static async ValueTask<bool> HandleLegacyPlayerHawksAmbitionAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyHawksAmbitionSpellId)
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

        if (HasLegacyStatusFlag(resourceSession.Character, LegacyHawksAmbitionFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy HawksAmbition AttachStatus magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id}; status flag is already active.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = ApplyLegacyHawksAmbitionStatus(resourceSession, spell, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                LegacyHawksAmbitionFlag,
                spell.Duration,
                damage: 0,
                spellLevel: 0,
                visible: false,
                cancellationToken)
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
        await SendPlayerEquipmentAppearanceRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                500,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy HawksAmbition AttachStatus magic " +
                $"{FormatSession(updatedContext with { Session = updatedSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} statusFlag={LegacyHawksAmbitionFlag} " +
                $"duration={spell.Duration} hawksAmbitionPower={spell.Damage}; " +
                "source=AttachStatus.cs case 15920.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyHawksAmbitionStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            WithLegacyStatusFlag(session.Character, LegacyHawksAmbitionFlag),
            "HawksAmbitionPower",
            spell.Damage);
        if (spell.Duration > 0)
        {
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyHawksAmbitionExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyHawksAmbitionRemoveOnDeadAttribute,
            "true");
        return session with { Character = updatedCharacter };
    }
}
