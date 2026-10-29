using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyCapriceRuneItemBase = 4_028_000;
    private const uint LegacyCapriceRuneItemMax = 4_028_027;
    private const uint LegacyConsolidationRuneItemBase = 4_036_200;
    private const uint LegacyConsolidationRuneItemMax = 4_036_209;
    private const string RuntimeLegacyCapriceRollAttribute = "LegacyCapriceRoll";
    private const string RuntimeLegacyCapriceBranchAttribute = "LegacyCapriceBranch";
    private const string RuntimeLegacyCapriceStampUtcAttribute = "LegacyCapriceStampUtc";
    private const string RuntimeLegacyConsolidationRollAttribute = "LegacyConsolidationRoll";
    private static readonly TimeSpan LegacyCapriceCooldown = TimeSpan.FromSeconds(15);

    private readonly record struct LegacyPlayerReceiveCapriceResult(
        bool StaminaTransferred,
        bool XpListRemoved);

    private static LegacyPlayerReceiveCapriceResult TryApplyLegacyPlayerReceiveCaprice(
        CharacterSnapshot attacker,
        CharacterSnapshot target,
        WorldSession? attackerSession,
        WorldSession targetSession,
        out CharacterSnapshot updatedAttacker,
        out CharacterSnapshot updatedTarget)
    {
        updatedAttacker = attacker;
        updatedTarget = target;
        if (attackerSession is null ||
            !TryGetEquippedLegacyCapriceLevel(attackerSession.PlayerItems, out var capriceLevel))
        {
            return default;
        }

        var now = DateTimeOffset.UtcNow;
        if (!IsLegacyCapriceCooldownReady(attacker, now))
        {
            return default;
        }

        var rateAccepted = CheckLegacyCapriceRateRoll(attacker, capriceLevel);
        var branch = rateAccepted ? ReadLegacyCapriceBranch(attacker) : 0;
        var consolidationBlocked = branch == 1 &&
            CheckLegacyConsolidationRateRoll(target, targetSession.PlayerItems);
        var targetStamina = 0UL;
        var attackerStamina = 0UL;
        // Validate the complete transfer before preparing even its cooldown.
        // Other branches and legitimate chance failures keep their old stamp.
        if (branch == 1 && !consolidationBlocked &&
            (!TryReadLegacyRuntimeStamina(target, out targetStamina) ||
             !TryReadLegacyRuntimeStamina(attacker, out attackerStamina) ||
             attackerStamina > ulong.MaxValue - 20))
        {
            return default;
        }

        updatedAttacker = WithCharacterAttribute(
            attacker,
            RuntimeLegacyCapriceStampUtcAttribute,
            now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        if (!rateAccepted)
        {
            return default;
        }

        if (branch == 1)
        {
            if (consolidationBlocked)
            {
                return default;
            }

            if (targetStamina < 20)
            {
                return default;
            }

            updatedTarget = WithCharacterAttribute(updatedTarget, "Stamina", targetStamina - 20);
            updatedAttacker = WithCharacterAttribute(updatedAttacker, "Stamina", attackerStamina + 20);
            return new LegacyPlayerReceiveCapriceResult(StaminaTransferred: true, XpListRemoved: false);
        }

        if (branch == 2 && HasLegacyStatusFlag(target, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            updatedTarget = WithoutLegacyStatusFlag(updatedTarget, LegacyRuntimeUpdateTicker.XpListFlag);
            return new LegacyPlayerReceiveCapriceResult(StaminaTransferred: false, XpListRemoved: true);
        }

        return default;
    }

    private static bool IsLegacyCapriceCooldownReady(CharacterSnapshot attacker, DateTimeOffset now)
    {
        return !TryReadLegacyDateTimeOffsetAttribute(
                attacker.Attributes,
                RuntimeLegacyCapriceStampUtcAttribute,
                out var stamp) ||
            now.ToUniversalTime() >= stamp.ToUniversalTime() + LegacyCapriceCooldown;
    }

    private static bool TryGetEquippedLegacyCapriceLevel(
        PlayerItemSnapshot playerItems,
        out byte runeLevel)
    {
        return TryGetEquippedLegacyRuneLevel(
            playerItems,
            LegacyCapriceRuneItemBase,
            LegacyCapriceRuneItemMax,
            out runeLevel);
    }

    private static bool TryGetEquippedLegacyConsolidationLevel(
        PlayerItemSnapshot playerItems,
        out byte runeLevel)
    {
        return TryGetEquippedLegacyRuneLevel(
            playerItems,
            LegacyConsolidationRuneItemBase,
            LegacyConsolidationRuneItemMax,
            out runeLevel);
    }

    private static bool TryGetEquippedLegacyRuneLevel(
        PlayerItemSnapshot playerItems,
        uint itemBase,
        uint itemMax,
        out byte runeLevel)
    {
        foreach (var rune in playerItems.RuneItems)
        {
            if (rune.Position < 101 ||
                rune.Position > 135 ||
                rune.ItemId <= itemBase ||
                rune.ItemId > itemMax)
            {
                continue;
            }

            runeLevel = checked((byte)(rune.ItemId - itemBase));
            return true;
        }

        runeLevel = 0;
        return false;
    }

    private static bool CheckLegacyCapriceRateRoll(CharacterSnapshot attacker, byte runeLevel)
    {
        var chance = runeLevel switch
        {
            1 => 5,
            2 => 7,
            3 => 9,
            4 => 11,
            5 => 13,
            6 => 15,
            7 => 17,
            8 => 19,
            9 => 21,
            10 => 23,
            11 => 25,
            12 => 27,
            13 => 29,
            14 => 31,
            15 => 33,
            >= 16 and <= 23 => 35,
            24 => 38,
            25 => 40,
            26 => 42,
            27 => 45,
            _ => 0,
        };
        return CheckLegacyPercentRoll(attacker.Attributes, RuntimeLegacyCapriceRollAttribute, chance);
    }

    private static bool CheckLegacyConsolidationRateRoll(
        CharacterSnapshot target,
        PlayerItemSnapshot playerItems)
    {
        if (!TryGetEquippedLegacyConsolidationLevel(playerItems, out var runeLevel))
        {
            return false;
        }

        var chance = runeLevel switch
        {
            1 => 35,
            2 => 40,
            3 => 45,
            4 => 55,
            5 => 60,
            6 => 65,
            7 => 70,
            8 => 75,
            9 => 80,
            _ => 0,
        };
        return CheckLegacyPercentRoll(target.Attributes, RuntimeLegacyConsolidationRollAttribute, chance);
    }

    private static int ReadLegacyCapriceBranch(CharacterSnapshot attacker)
    {
        if (TryReadAttributeInt(attacker.Attributes, RuntimeLegacyCapriceBranchAttribute) is { } branch &&
            branch is 1 or 2)
        {
            return branch;
        }

        return Random.Shared.Next(1, 3);
    }

    private static bool CheckLegacyPercentRoll(
        IReadOnlyDictionary<string, string> attributes,
        string forcedRollAttribute,
        int chance)
    {
        if (chance <= 0)
        {
            return false;
        }

        var roll = TryReadDoubleAttribute(attributes, out var forcedRoll, forcedRollAttribute)
            ? forcedRoll
            : Random.Shared.NextDouble() * 100.0;
        return roll < chance;
    }
}
