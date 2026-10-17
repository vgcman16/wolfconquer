using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const byte LegacyCombatGearTriple17490Range = 15;
    private const byte LegacyCombatGearTriple17490Width = 2;

    private readonly record struct LegacyCombatGearTriple17490Line(
        long OriginX, long OriginY, ushort AnchorX, ushort AnchorY);

    private static async ValueTask<bool> ExecuteLegacyPreparedCombatGearTriple17490Async(
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
        var anchored = GetLegacyCombatGearDirectLineAnchoredQuery(context, session, query);
        var line = new LegacyCombatGearTriple17490Line(
            session.Character.X, session.Character.Y, anchored.X, anchored.Y);
        bool IsInLine(long x, long y) => IsLegacyCombatGearTriple17490InLine(line, x, y);
        var targets = new List<LegacySpellAnimationTarget>();

        session = await ReceiveLegacyCombatGearPhysicalLineMonstersAsync(
                context, session, attackerUid, IsInLine, spell, targets, cancellationToken)
            .ConfigureAwait(false);
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, attackerUid, ref session))
        {
            return true;
        }

        context = context with { Session = session };
        var playerSession = await ReceiveLegacyCombatGearPhysicalLinePlayersAsync(
                context, session, attackerUid, IsInLine, spell, targets, cancellationToken)
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
                context, session, attackerUid, IsInLine, spell, targets, cancellationToken)
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
        if (targets.Count != 0)
        {
            var animation = new LegacySpellAnimationPacket(
                CheckedUInt32(attackerUid, nameof(attackerUid)),
                AttackedUid: 0,
                line.AnchorX,
                line.AnchorY,
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                NextSpell: 0,
                LevelHu: playerSpell.UseSpellSoul,
                SpellEffect: 0,
                targets);
            await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy direct physical CombatGear Triple17490 " +
                $"attackerUid={attackerUid} spellId={spell.Id} targets={targets.Count} " +
                $"castX={line.AnchorX} castY={line.AnchorY}; " +
                "source=CombatGearAttack.cs case 17490; no explicit experience or spell progression.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyCombatGearTriple17490InLine(
        LegacyCombatGearTriple17490Line line,
        long targetX,
        long targetY)
    {
        return LegacyGetDistance(line.OriginX, line.OriginY, targetX, targetY) <= LegacyCombatGearTriple17490Range &&
            IsLegacyInLine(
                line.OriginX, line.OriginY, line.AnchorX, line.AnchorY,
                targetX, targetY, LegacyCombatGearTriple17490Range, LegacyCombatGearTriple17490Width);
    }
}
