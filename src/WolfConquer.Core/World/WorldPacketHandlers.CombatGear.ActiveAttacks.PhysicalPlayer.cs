using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyDirectPhysicalCombatGearPlayerReplacementAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        WorldActiveSession primary,
        long attackerUid,
        long targetUid,
        CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Physical)
        {
            return false;
        }

        if (!TryRefreshLegacyDirectPhysicalParticipants(context, attackerUid, targetUid, ref session, ref primary))
        {
            return true;
        }

        context = context with { Session = session };
        if (!HasLegacyCombatGearActiveAttackSource(session))
        {
            return false;
        }

        if (!TrySelectLegacyDirectPhysicalCombatGearSpell(
                session, context.WorldData, primary.Session.Character,
                out var gearItemId, out var selectedSpellId, out var piratePassive))
        {
            return false;
        }

        if (context.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells, worldData, selectedSpellId, out var playerSpell, out var spell))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy direct physical CombatGear replacement " +
                    $"{FormatSession(context)} playerUid={targetUid} gearItemId={gearItemId} " +
                    $"selectedSpellId={selectedSpellId}; unavailable CombatGear spell/catalog row; " +
                    "source=MyCombatGear.CheckCombatGear -> MsgAttackPacket.ProcescMagic(ignoreStamp:true).")
                .ConfigureAwait(false);
        }
        else
        {
            if (piratePassive)
            {
                session = session with { LegacyRandomSpell = selectedSpellId };
                context.Sessions?.Set(context.Connection, session);
                context = context with { Session = session };
            }

            var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                    context, session, spell, ignoreAttackStamp: true, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (resourceSession is not null)
            {
                session = resourceSession;
                if (!TryRefreshLegacyDirectPhysicalParticipants(
                        context, attackerUid, targetUid, ref session, ref primary))
                {
                    return true;
                }

                context = context with { Session = session };
                if (session.Character.MinHitPoints > 0)
                {
                    var query = new MsgAttackQuery(
                        packet.Query.Uid,
                        CheckedUInt32(targetUid, nameof(targetUid)),
                        CheckedUInt16(primary.Session.Character.X, nameof(primary.Session.Character.X)),
                        CheckedUInt16(primary.Session.Character.Y, nameof(primary.Session.Character.Y)),
                        MsgAttackType.Magic,
                        Damage: 0,
                        KilledMonster: 0,
                        ResponseDamage: 0,
                        Effect: MsgAttackEffect.None,
                        Unknown: 0,
                        SpellId: selectedSpellId);
                    if (!await TryExecuteLegacyDirectPhysicalCombatGearSpellAsync(
                            context, session, query, playerSpell, spell, cancellationToken).ConfigureAwait(false))
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer skipped legacy direct physical CombatGear replacement " +
                                $"{FormatSession(context)} playerUid={targetUid} gearItemId={gearItemId} " +
                                $"selectedSpellId={selectedSpellId}; unavailable source-specific CombatGear execution handler; " +
                                "source=MyCombatGear.CheckCombatGear consumed selected nonzero spell.")
                            .ConfigureAwait(false);
                    }
                }
            }
        }

        if (piratePassive)
        {
            return true;
        }

        if (TryRefreshLegacyDirectPhysicalParticipants(
                context, attackerUid, targetUid, ref session, ref primary))
        {
            context = context with { Session = session };
            await TrySendLegacyCombatGearTharnCutterJumpEffectAsync(
                    context, session, primary, cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }
}
