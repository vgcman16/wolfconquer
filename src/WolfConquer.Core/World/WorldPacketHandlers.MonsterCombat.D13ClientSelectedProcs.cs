using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleVisibleDx8D13ClientSelectedWeaponProcMonsterAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        LegacyMonsterInstance monster,
        CancellationToken cancellationToken)
    {
        if (context.ProtocolProfile != ConquerProtocolProfile.VisibleDx8D13 ||
            context.ClientCompatibility?.DisableLegacyWeaponProcMonsterAttacks != true ||
            packet.Query.AttackType != MsgAttackType.Physical ||
            packet.Query.Damage < 0 ||
            !VisibleDx8D13AutomaticSkillSelectors.IsKnownSelector((uint)packet.Query.Damage) ||
            context.WorldData is not { } worldData)
        {
            return false;
        }

        var selectedSpellId = checked((ushort)packet.Query.Damage);
        if (!LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                selectedSpellId,
                out var playerSpell,
                out var spell) ||
            !IsModeledVisibleDx8D13ClientSelectedProcSpell(spell) ||
            !HasVisibleDx8D13ClientSelectedProcWeapon(session, spell.WeaponType))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer rejected D13 client-selected weapon proc adapter " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} selector={selectedSpellId}; " +
                    "reason=selector requires learned exact metadata, a source-backed local runtime, and a matching equipped weapon; " +
                    "fallback=normal physical attack.")
                .ConfigureAwait(false);
            return false;
        }

        if (IsLegacyChopPerimeterSpell(spell.Id))
        {
            await TryDispatchLegacyCheckMeleeWindstormMonsterProcAsync(
                    context,
                    packet,
                    session,
                    monster,
                    worldData,
                    selectedSpellId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (context.Sessions is { } sessions &&
                sessions.TryGet(context.Connection, out var currentSession) &&
                currentSession is not null)
            {
                session = currentSession;
            }
        }

        await DispatchLegacyWeaponProcMonsterAttackAsync(
                context with { Session = session },
                packet,
                session,
                monster,
                playerSpell,
                spell,
                $"VisibleDx8D13 client physical selector {selectedSpellId}",
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer adapted D13 client-selected weapon proc " +
                $"{FormatSession(context)} monsterUid={monster.Uid} selector={selectedSpellId} " +
                "inboundOpcode=2135 outboundAnimationOpcode=2239; " +
                "source=D13 FUN_00ACD62B selector base plus random-times-ten.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsModeledVisibleDx8D13ClientSelectedProcSpell(LegacyMagicRuntimeSpell spell)
    {
        return (spell.Sort == LegacyMagicSort.Perimeter &&
                spell.Id is
                    12_210 or 12_220 or
                    12_580 or 12_590 or
                    12_740 or 12_750 or
                    13_040 or
                    14_570 or 14_580 or
                    15_670 or 15_680) ||
            (spell.Sort == LegacyMagicSort.Strike && spell.Id is 12_600 or 13_050) ||
            (spell.Sort == LegacyMagicSort.StraightFist && spell.Id == 12_760) ||
            (spell.Sort == LegacyMagicSort.Rectangle && spell.Id is 13_060 or 15_690);
    }

    private static bool HasVisibleDx8D13ClientSelectedProcWeapon(
        WorldSession session,
        uint configuredWeaponTypes)
    {
        return (TryGetCurrentWeaponType(
                session,
                normalizedPosition: 4,
                mapLegacyBladeToSword: true,
                out var rightWeaponType) &&
            LegacyMagicWeaponTypeMatches(configuredWeaponTypes, rightWeaponType)) ||
            (TryGetCurrentWeaponType(
                session,
                normalizedPosition: 5,
                mapLegacyBladeToSword: false,
                out var leftWeaponType) &&
            LegacyMagicWeaponTypeMatches(configuredWeaponTypes, leftWeaponType));
    }
}
