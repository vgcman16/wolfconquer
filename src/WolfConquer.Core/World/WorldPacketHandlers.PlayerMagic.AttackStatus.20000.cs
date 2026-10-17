using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyAttackStatusVenomSpellId = 20_000;
    private const string RuntimeLegacyVenomDamageAttribute = "Venom";
    private const string RuntimeLegacyVenomExpiresAtUtcAttribute = "LegacyVenomExpiresAtUtc";
    private static readonly TimeSpan LegacyAttackStatusVenomDuration = TimeSpan.FromSeconds(6);

    private static async ValueTask<bool> HandleLegacyPlayerAttackStatus20000MagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttackStatus ||
            spell.Id != LegacyAttackStatusVenomSpellId)
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

        var resourceContext = context with { Session = resourceSession };
        if (!TryGetVisibleSessionByUid(resourceContext, resourceSession, query.OpponentUid, out var activeTarget))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy AttackStatus Venom magic " +
                    $"{FormatSession(resourceContext)} spellId={spell.Id} targetUid={query.OpponentUid}; " +
                    "source AttackStatus.cs case 20000 returns after CanUseSpell.Verified when the visible player target is absent.")
                .ConfigureAwait(false);
            return true;
        }

        await ApplyLegacyAttackStatusVenomAsync(
                resourceContext,
                resourceSession,
                activeTarget,
                playerSpell,
                spell,
                "handled legacy AttackStatus Venom magic",
                string.Empty,
                "source AttackStatus.cs case 20000 applies AddFlag(Venom, 6, RemoveOnDead: true), " +
                "sets target Venom from Spell.DamageOnHuman, enqueues self zero-damage animation, and does not call CanAttackPlayer.Verified.",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> ApplyLegacyAttackStatusVenomAsync(
        WorldPacketHandlerContext context,
        WorldSession casterSession,
        WorldActiveSession activeTarget,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        string actionDiagnostic,
        string extraDiagnostic,
        string sourceDiagnostic,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now + LegacyAttackStatusVenomDuration;
        var updatedTargetCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(activeTarget.Session.Character, LegacyVenomFlag),
                RuntimeLegacyVenomDamageAttribute,
                spell.DamageOnHuman),
            RuntimeLegacyVenomExpiresAtUtcAttribute,
            expiresAt.ToString("O", CultureInfo.InvariantCulture));
        var updatedTargetSession = activeTarget.Session with
        {
            Character = updatedTargetCharacter,
        };
        context.Sessions?.Set(activeTarget.Connection, updatedTargetSession);

        await SendPlayerStatusFlagVectorToVisibleAsync(
                context,
                activeTarget.Connection,
                updatedTargetSession,
                cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(casterSession.Character.Uid, nameof(casterSession.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            casterUid,
            0,
            0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            0,
            playerSpell.UseSpellSoul,
            0,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: false, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(context, casterSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer {actionDiagnostic} " +
                $"{FormatSession(context)} spellId={spell.Id} targetUid={updatedTargetSession.Character.Uid} " +
                $"statusFlag={LegacyVenomFlag} venom={spell.DamageOnHuman} expiresAtUtc={expiresAt:O}; " +
                extraDiagnostic +
                sourceDiagnostic)
            .ConfigureAwait(false);
        return updatedTargetSession;
    }
}
