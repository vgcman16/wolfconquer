using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool IsLegacyRootFirstFamily(LegacyMagicRuntimeSpell spell) =>
        spell.Id == 18_220 ||
        (!IsLegacyNativeCombatGearSpellId(spell.Id) &&
         spell.Sort is LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack or LegacyMagicSort.Bomb or LegacyMagicSort.Recruit);

    // The native Pervade executor is a separate required prerequisite. Do not invent a
    // no-cost decision here and then fall through to the ordinary unchanged handler.
    private static bool IsLegacyRootFirstFamilyInvocation(WorldSession session, LegacyMagicRuntimeSpell spell) =>
        IsLegacyRootFirstFamily(spell) &&
        !(spell.Sort == LegacyMagicSort.Bomb && spell.Id == 3_090) &&
        !(spell.Sort == LegacyMagicSort.Attack && TryGetCurrentRightWeapon(session, out var weapon) &&
          weapon.Effect == LegacyWeaponEffectPoison);

    // Native CombatGear.CheckSpells uses Enum.IsDefined(SpellID), not its runtime sort.
    private static bool IsLegacyNativeCombatGearSpellId(ushort id) => id is
        0x3304 or 0x3D4A or 0x177A or 0x4434 or 0x443E or 0x4448 or 0x4452 or
        0x44E8 or 0x44F2 or 0x44D4 or 0x445C or 0x4466 or 0x4470 or 0x447A or
        0x44C0 or 0x44CA or 0x4484 or 0x448E or 0x4498 or 0x44A2 or 0x44DE or
        0x44FC or 0x44AC or 0x43F8 or 0x43F9 or 0x472C or 0x4A24 or 0x2AC6 or
        0x2EAE or 0x2EAF or 0x2EB0 or 0x2AB2 or 0x4402 or 0x2A8A or 0x43EE or
        0x43EF or 0x43F0 or 0x4916 or 0x492A or 0x48C6 or 0x4718 or 0x4704 or
        0x4902 or 0x46F0 or 0x4EE8 or 0x4EF2 or 0x4EFC or 0x4F06 or 0x4F10 or
        0x4F1A or 0x4F24 or 0x4F2E or 0x4F38 or 0x4F42 or 0x4F4C or 0x4F56 or
        0x4F60 or 0x4F6A or 0x4F74 or 0x4F7E or 0x4F88 or 0x4F92 or 0x4F9C or
        0x4FA6 or 0x5154 or 0x547E or 0x53F2 or 0x54CE or 0x54D8 or 0x5406 or
        0x54A6 or 0x54B0 or 0x54C4 or 0x541A or 0x5460 or 0x54BA or 0x5492 or
        0x5488 or 0x549C or 0x5474 or 0x5636 or 0x546A or 0x5640 or 0x564A or
        0x5654 or 0x565E or 0x54E2 or 0x54EC or 0x54F6 or 0x5500 or 0x550A or
        0x5514 or 0x551E or 0x5528 or 0x5532 or 0x553C or 0x5456 or 0x5410 or
        0x542E or 0x55AA or 0x5438 or 0x5442 or 0x544C or 0x55A0 or 0x5424 or
        0x5618 or 0x5622 or 0x562C;

    private static bool IsLegacyRootFirstFamilyRequest(
        WorldSession session, WorldDataCatalog worldData, MsgAttackQuery input)
    {
        if (TryResolveLegacyPlayerMagicAttackQuery(session, worldData, input,
                out _, out _, out _, out var selected, out _, deferFirstFamilyToken: true)) return IsLegacyRootFirstFamilyInvocation(session, selected);
        // Resolution can fail because the learned book is absent. Ride still precedes that native lookup.
        foreach (var candidate in new[] { input, MsgAttackPacket.DecodeMagicAttack(input),
                     input with { SpellId = input.Damage is > 0 and <= ushort.MaxValue ? (ushort)input.Damage : (ushort)0 } })
        {
            if (!worldData.MagicByTypeId.TryGetValue(candidate.SpellId, out var rows)) continue;
            foreach (var row in rows)
                if (row.Fields.Count > 8 && ushort.TryParse(row.Fields[8], out var level) &&
                    LegacyMagicRuntime.TryResolveNativeLoadedSpell(
                        worldData, candidate.SpellId, level, out var spell, out _))
                    return IsLegacyRootFirstFamilyInvocation(session, spell);
        }
        return false; // No catalog family is known; this bounded prerequisite cannot classify it.
    }

    private static async ValueTask<bool> TryRejectLegacyRootFirstFamilyRideAsync(
        WorldPacketHandlerContext context, WorldSession session, MsgAttackQuery input, CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData || !IsLegacyRootFirstFamilyRequest(session, worldData, input) ||
            !HasLegacyStatusFlag(session.Character, LegacyRideFlag) ||
            (LegacyPhysicalStatusOverlay.ForOwner(session).RidingCropItemId ?? 0U) != 0) return false;
        var uid = session.Character.Uid;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return true;
        session = session with { Character = ClearLegacyMountRideStatus(session.Character) };
        context.Sessions?.Set(context.Connection, session);
        await SendLegacyArrowBladesVectorAsync(context with { Session = session }, session, uid, cancellationToken)
            .ConfigureAwait(false);
        return true; // Native AllowUseSpellOnSteed rejects this entered invocation after removing Ride.
    }

    private static bool HasPreparedLegacyRootFirstFamilyInvocation(
        WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell) =>
        context.RootFirstFamilyInvocation is { Prepared: true } capture &&
        ReferenceEquals(capture.Connection, context.Connection) && capture.OwnerUid == session.Character.Uid &&
        capture.SpellId == spell.Id && capture.Level == spell.Level;
}
