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

        var admission = await TryPrepareOwnedHawksAmbitionAdmissionAsync(
                context,
                session,
                playerSpell,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (admission is null)
        {
            return true;
        }

        var resourceSession = admission.Session;
        var output = admission.Output;
        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var resourceContext = context with { Session = resourceSession, OwnedHawksOutput = output };
        var interaction = new MsgAttackPacket(query with { Uid = casterUid });
        output.RequireCurrent(resourceContext);
        await resourceContext.Connection.SendAsync(interaction.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        output.RequireCurrent(resourceContext);

        using (var phase = output.CapturePhase(resourceContext))
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(phase.Context, phase.Session, cancellationToken).ConfigureAwait(false);
            output.RequireCurrent(phase.Context);
        }
        using (var phase = output.CapturePhase(resourceContext))
        {
            await SendLegacyCombatGearArchiveSkillUpdateAsync(phase.Context, phase.Session, LegacyHawksAmbitionFlag,
                    spell.Duration, damage: 0, spellLevel: 0, visible: false, cancellationToken).ConfigureAwait(false);
            output.RequireCurrent(phase.Context);
        }

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
        using (var phase = output.CapturePhase(resourceContext))
        {
            await SendPlayerSpellAnimationToVisibleAsync(phase.Context, phase.Session, animation, cancellationToken).ConfigureAwait(false);
            output.RequireCurrent(phase.Context);
        }
        using (var phase = output.CapturePhase(resourceContext))
        {
            await SendPlayerEquipmentAppearanceRefreshAsync(phase.Context, phase.Session, cancellationToken).ConfigureAwait(false);
            output.RequireCurrent(phase.Context);
        }

        WorldSession updatedSession;
        WorldPacketHandlerContext updatedContext;
        using (var phase = output.CapturePhase(resourceContext))
        {
            updatedSession = phase.Session;
            updatedContext = resourceContext with { Session = updatedSession, OwnedHawksPhase = null };
        } // A phase is retired BEFORE the legitimate XP writer replaces its holder.

        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                500,
                cancellationToken)
            .ConfigureAwait(false);

        output.RequireCurrent(updatedContext);

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
        updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                RuntimeLegacyHawksAmbitionExpiresAtUtcAttribute,
                nowUtc
                    .ToUniversalTime()
                    .AddSeconds(spell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));

        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyHawksAmbitionRemoveOnDeadAttribute,
            "true");
        return session with { Character = updatedCharacter };
    }
}
