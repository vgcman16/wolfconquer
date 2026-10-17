using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> ExecuteLegacyPreparedCombatGearIronbone17650Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var attackerUid = session.Character.Uid;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
        {
            return true;
        }

        context = context with { Session = session };
        var casterX = CheckedUInt16(session.Character.X, nameof(session.Character.X));
        var casterY = CheckedUInt16(session.Character.Y, nameof(session.Character.Y));
        if (!TryCreateLegacyCombatGearIronbone17650Line(
                casterX, casterY, query.X, query.Y, spell.Range,
                CreateLegacyMoveLineLocationValidator(context, session), out var line))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy direct physical CombatGear Ironbone17650 " +
                    $"attackerUid={attackerUid} spellId={spell.Id}; invalid source query or final three endpoint cells.")
                .ConfigureAwait(false);
            return true;
        }

        var targets = new List<LegacySpellAnimationTarget>();
        session = await ReceiveLegacyCombatGearPhysicalLineMonstersAsync(
                context, session, attackerUid, line.Contains, spell, targets, cancellationToken)
            .ConfigureAwait(false);
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
        {
            return true;
        }

        context = context with { Session = session };
        var playerSession = await ReceiveLegacyCombatGearPhysicalLinePlayersAsync(
                context, session, attackerUid, line.Contains, spell, targets, cancellationToken,
                playerDamagePercent: 75)
            .ConfigureAwait(false);
        if (playerSession is null)
        {
            return true;
        }

        session = playerSession;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
        {
            return true;
        }

        context = context with { Session = session };
        var receivedSobSession = await ReceiveLegacyCombatGearPhysicalLineSobNpcsAsync(
                context, session, attackerUid, line.Contains, spell, targets, cancellationToken)
            .ConfigureAwait(false);
        if (receivedSobSession is null)
        {
            return true;
        }

        session = receivedSobSession;
        context = context with { Session = session };

        if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
        {
            return true;
        }

        context = context with { Session = session };
        var animation = new LegacySpellAnimationPacket(
            CheckedUInt32(attackerUid, nameof(attackerUid)),
            AttackedUid: 0,
            line.EndpointX,
            line.EndpointY,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            targets);
        session = await SendLegacyCombatGearIronbone17650AnimationAsync(
                context, session, attackerUid, animation, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy direct physical CombatGear Ironbone17650 " +
                $"attackerUid={attackerUid} spellId={spell.Id} targets={targets.Count} " +
                $"castX={line.EndpointX} castY={line.EndpointY}; " +
                "source=CombatGearAttack.cs case 17650; no Shift, explicit experience, or spell progression.")
            .ConfigureAwait(false);
        return true;
    }
}
