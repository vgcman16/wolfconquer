using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyReverseMagicRuneItemBase = 4_033_900;
    private const uint LegacyReverseMagicRuneItemMax = 4_033_909;
    private const string RuntimeLegacyReverseMagicRollAttribute = "LegacyReverseMagicRoll";

    private static bool TryApplyLegacyPlayerReceiveReverseMagic(
        CharacterSnapshot attacker,
        WorldSession? attackerSession,
        WorldSession targetSession,
        out CharacterSnapshot updatedAttacker)
    {
        updatedAttacker = attacker;
        if (attackerSession is null ||
            !TryGetEquippedLegacyReverseMagicLevel(targetSession.PlayerItems, out var runeLevel) ||
            !CheckLegacyReverseMagicRateRoll(attacker, runeLevel))
        {
            return false;
        }

        updatedAttacker = WithCharacterAttribute(attacker, "XPCount", 0UL);
        foreach (var xpSkillFlag in LegacyXpSkillFlags)
        {
            updatedAttacker = WithoutLegacyStatusFlag(updatedAttacker, xpSkillFlag);
        }

        return true;
    }

    private static bool TryGetEquippedLegacyReverseMagicLevel(
        PlayerItemSnapshot playerItems,
        out byte runeLevel)
    {
        foreach (var rune in playerItems.RuneItems)
        {
            if (rune.Position < 101 ||
                rune.Position > 135 ||
                rune.ItemId <= LegacyReverseMagicRuneItemBase ||
                rune.ItemId > LegacyReverseMagicRuneItemMax)
            {
                continue;
            }

            runeLevel = checked((byte)(rune.ItemId - LegacyReverseMagicRuneItemBase));
            return true;
        }

        runeLevel = 0;
        return false;
    }

    private static bool CheckLegacyReverseMagicRateRoll(CharacterSnapshot attacker, byte runeLevel)
    {
        var chance = runeLevel == 9
            ? 5.0
            : runeLevel * 0.5;
        var roll = TryReadDoubleAttribute(
            attacker.Attributes,
            out var forcedRoll,
            RuntimeLegacyReverseMagicRollAttribute)
            ? forcedRoll
            : Random.Shared.NextDouble() * 100.0;
        return roll < chance;
    }
}
