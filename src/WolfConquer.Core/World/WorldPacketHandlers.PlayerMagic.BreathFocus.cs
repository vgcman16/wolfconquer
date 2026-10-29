using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyBreathFocusSpellId = 11_960;

    private static async ValueTask<bool> HandleLegacyPlayerBreathFocusMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.BreathFocus ||
            spell.Id != LegacyBreathFocusSpellId)
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

        if (!TryReadLegacyRuntimeStamina(resourceSession.Character, out var stamina))
        {
            await context.Log.WriteLineAsync("WorldServer interrupted legacy BreathFocus restoration after accepted admission: " +
                "requiredStaminaInvalid; accepted accounting retained, no refund or retry authorization.").ConfigureAwait(false);
            return true;
        }
        var updatedSession = resourceSession;
        if (stamina < 100)
        {
            var restoredStamina = (ushort)Math.Min(100UL, stamina + spell.Damage);
            updatedSession = await ApplyLegacyStaminaToPlayerAsync(
                    context,
                    context.Connection,
                    resourceSession,
                    restoredStamina,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedContext = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                totalDamage: 1_000,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X)),
            CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y)),
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            []);
        await SendPlayerSpellAnimationToVisibleAsync(
                updatedContext,
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        var staminaDiagnostic = TryReadLegacyRuntimeStamina(updatedSession.Character, out var finalStamina)
            ? finalStamina.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unavailableAfterPublication";
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy BreathFocus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"stamina={staminaDiagnostic} progressDamage=1000.")
            .ConfigureAwait(false);
        return true;
    }
}
