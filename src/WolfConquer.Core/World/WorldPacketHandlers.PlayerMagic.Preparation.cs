using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyGraceOfHeavenSpellId = 12_560;
    private const int LegacyHeavensWrathFlag = 0xEC;

    private static ValueTask<WorldSession?> TryPrepareLegacyPlayerMagicCastAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        return TryPrepareLegacyPlayerMagicCastAsync(
            context,
            session,
            spell,
            ignoreAttackStamp: false,
            cancellationToken: cancellationToken,
            expectedNativeClientIdentity: expectedNativeClientIdentity);
    }

    private static bool TryGetLegacyPlayerMagicAttackStampCadence(
        LegacyMagicRuntimeSpell spell,
        out TimeSpan cadence)
    {
        if (spell.Id == 1_045)
        {
            cadence = TimeSpan.FromMilliseconds(500);
            return true;
        }

        if (spell.Id == 1_115)
        {
            cadence = TimeSpan.FromMilliseconds(spell.CoolDown == 0 ? 500 : spell.CoolDown);
            return true;
        }

        if (spell.Sort == LegacyMagicSort.WhirlwindKick)
        {
            var cooldownMilliseconds = spell.Id == 10_415
                ? 800
                : spell.CoolDown == 0
                    ? 500
                    : spell.CoolDown;
            if (cooldownMilliseconds is > 1_000 and < 3_000)
            {
                cooldownMilliseconds = 2_500;
            }

            cadence = TimeSpan.FromMilliseconds(cooldownMilliseconds);
            return true;
        }

        if (LegacyMagicSortCapabilities.IsSupportedPounceRuntimeSpell(spell.Sort, spell.Id))
        {
            cadence = TimeSpan.FromMilliseconds(spell.CoolDown);
            return true;
        }

        if (LegacyMagicSortCapabilities.IsSupportedThunderRampageRuntimeSpell(spell.Sort, spell.Id))
        {
            cadence = TimeSpan.FromMilliseconds(500);
            return true;
        }

        if (LegacyMagicSortCapabilities.IsSupportedRemoveBuffersRuntimeSpell(spell.Sort, spell.Id))
        {
            cadence = TimeSpan.FromMilliseconds(500);
            return true;
        }

        if (LegacyMagicSortCapabilities.IsSupportedPerimeterSpecialRuntimeSpell(spell.Sort, spell.Id))
        {
            cadence = TimeSpan.FromMilliseconds(500);
            return true;
        }

        cadence = default;
        return false;
    }

    private static bool HasLegacyGraceOfHeavenResourceBypass(WorldSession session)
    {
        return session.PlayerSpells.ContainsSpell(LegacyGraceOfHeavenSpellId) &&
            HasLegacyEquippedMonkEpicWeapon(session.PlayerItems, position: 4) &&
            HasLegacyEquippedMonkEpicWeapon(session.PlayerItems, position: 5);
    }

    private static bool HasLegacyEquippedMonkEpicWeapon(PlayerItemSnapshot playerItems, ushort position)
    {
        return playerItems.EquipmentItems.Any(item =>
            item.Position == position &&
            item.ItemId is >= 622_000 and <= 622_439);
    }
}
