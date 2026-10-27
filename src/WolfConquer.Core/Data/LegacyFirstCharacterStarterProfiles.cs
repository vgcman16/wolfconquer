namespace WolfConquer.Core.Data;

internal static class LegacyFirstCharacterStarterProfiles
{
    public const long TunedMoney = 100_000;
    public const long TunedConquerPoints = 0;
    public const long SourceMoney = 10_000_000;
    public const long SourceConquerPoints = 50_000;

    public static bool TryGet(long classCode, out LegacyFirstCharacterStarterProfile profile)
    {
        profile = (classCode / 1000) switch
        {
            1 => new LegacyFirstCharacterStarterProfile(
                Strength: 5,
                Vitality: 3,
                Agility: 2,
                Spirit: 0,
                SourceStrength: 155,
                SourceVitality: 92,
                SourceAgility: 60,
                SourceSpirit: 0,
                RingItemId: 150_003,
                RightWeaponItemId: 420_301,
                LeftWeaponItemId: 420_301,
                ArmorItemId: 132_009),
            2 => new LegacyFirstCharacterStarterProfile(
                Strength: 5,
                Vitality: 3,
                Agility: 2,
                Spirit: 0,
                SourceStrength: 205,
                SourceVitality: 42,
                SourceAgility: 60,
                SourceSpirit: 0,
                RingItemId: 150_003,
                RightWeaponItemId: 561_301,
                LeftWeaponItemId: 0,
                ArmorItemId: 132_009),
            4 => new LegacyFirstCharacterStarterProfile(
                Strength: 2,
                Vitality: 1,
                Agility: 7,
                Spirit: 0,
                SourceStrength: 60,
                SourceVitality: 32,
                SourceAgility: 215,
                SourceSpirit: 0,
                RingItemId: 150_003,
                RightWeaponItemId: 500_006,
                LeftWeaponItemId: 0,
                ArmorItemId: 132_009),
            5 => new LegacyFirstCharacterStarterProfile(
                Strength: 1,
                Vitality: 1,
                Agility: 8,
                Spirit: 0,
                SourceStrength: 30,
                SourceVitality: 46,
                SourceAgility: 231,
                SourceSpirit: 0,
                RingItemId: 150_003,
                RightWeaponItemId: 601_301,
                LeftWeaponItemId: 601_301,
                ArmorItemId: 132_009),
            6 => new LegacyFirstCharacterStarterProfile(
                Strength: 3,
                Vitality: 4,
                Agility: 1,
                Spirit: 2,
                SourceStrength: 108,
                SourceVitality: 68,
                SourceAgility: 30,
                SourceSpirit: 99,
                RingItemId: 150_003,
                RightWeaponItemId: 610_301,
                LeftWeaponItemId: 610_301,
                ArmorItemId: 132_009),
            7 => new LegacyFirstCharacterStarterProfile(
                Strength: 3,
                Vitality: 4,
                Agility: 1,
                Spirit: 2,
                SourceStrength: 108,
                SourceVitality: 68,
                SourceAgility: 30,
                SourceSpirit: 99,
                RingItemId: 150_003,
                RightWeaponItemId: 611_301,
                LeftWeaponItemId: 612_301,
                ArmorItemId: 132_009),
            8 => new LegacyFirstCharacterStarterProfile(
                Strength: 3,
                Vitality: 4,
                Agility: 1,
                Spirit: 2,
                SourceStrength: 108,
                SourceVitality: 68,
                SourceAgility: 30,
                SourceSpirit: 99,
                RingItemId: 150_003,
                RightWeaponItemId: 617_301,
                LeftWeaponItemId: 617_301,
                ArmorItemId: 138_313),
            9 => new LegacyFirstCharacterStarterProfile(
                Strength: 5,
                Vitality: 3,
                Agility: 2,
                Spirit: 0,
                SourceStrength: 76,
                SourceVitality: 121,
                SourceAgility: 110,
                SourceSpirit: 0,
                RingItemId: 150_003,
                RightWeaponItemId: 681_301,
                LeftWeaponItemId: 680_301,
                ArmorItemId: 102_313),
            10 => new LegacyFirstCharacterStarterProfile(
                Strength: 0,
                Vitality: 3,
                Agility: 2,
                Spirit: 5,
                SourceStrength: 0,
                SourceVitality: 42,
                SourceAgility: 60,
                SourceSpirit: 205,
                RingItemId: 152_005,
                RightWeaponItemId: 421_301,
                LeftWeaponItemId: 0,
                ArmorItemId: 132_009),
            16 => new LegacyFirstCharacterStarterProfile(
                Strength: 5,
                Vitality: 3,
                Agility: 2,
                Spirit: 0,
                SourceStrength: 155,
                SourceVitality: 92,
                SourceAgility: 60,
                SourceSpirit: 0,
                RingItemId: 150_003,
                RightWeaponItemId: 626_301,
                LeftWeaponItemId: 626_301,
                ArmorItemId: 101_319),
            _ => default,
        };
        return profile != default;
    }

    public static LegacyFirstCharacterStarterStats CreateStats(long classCode)
    {
        var attributes = TryGet(classCode, out var profile)
            ? new LegacyFirstCharacterStarterAttributes(profile.Strength, profile.Agility, profile.Vitality, profile.Spirit)
            : new LegacyFirstCharacterStarterAttributes(5, 2, 3, 0);
        return new LegacyFirstCharacterStarterStats(
            attributes.Strength,
            attributes.Agility,
            attributes.Vitality,
            attributes.Spirit,
            CalculateHitpoints(
                classCode,
                attributes.Strength,
                attributes.Agility,
                attributes.Vitality,
                attributes.Spirit),
            CalculateMana(classCode, attributes.Spirit));
    }

    public static long CalculateMana(long classCode, long spirit)
    {
        var normalizedSpirit = Math.Max(0, spirit);
        if (classCode is >= 13_005 and <= 13_057 or >= 14_005 and <= 14_057)
        {
            return normalizedSpirit * 30;
        }

        return classCode switch
        {
            13_002 or 14_002 => normalizedSpirit * 15,
            13_003 or 14_003 => normalizedSpirit * 20,
            13_004 or 14_004 => normalizedSpirit * 25,
            _ => normalizedSpirit * 5,
        };
    }

    public static uint CalculateHitpoints(long classCode, long strength, long agility, long vitality, long spirit)
    {
        var str = Math.Max(0, strength);
        var agi = Math.Max(0, agility);
        var vit = Math.Max(0, vitality);
        var spi = Math.Max(0, spirit);

        var hitpoints = classCode switch
        {
            >= 1005 and <= 1058 => (agi * 3.45) + (spi * 3.45) + (str * 3.45) + (vit * 27.6),
            1001 => (agi * 3.15) + (spi * 3.15) + (str * 3.15) + (vit * 25.2),
            1002 => (agi * 3.24) + (spi * 3.24) + (str * 3.24) + (vit * 25.9),
            1003 => (agi * 3.3) + (spi * 3.3) + (str * 3.3) + (vit * 26.4),
            1004 => (agi * 3.36) + (spi * 3.36) + (str * 3.36) + (vit * 26.8),
            _ => (agi * 3) + (spi * 3) + (str * 3) + (vit * 24),
        };
        return unchecked((uint)(ushort)hitpoints);
    }
}

internal readonly record struct LegacyFirstCharacterStarterProfile(
    long Strength,
    long Vitality,
    long Agility,
    long Spirit,
    long SourceStrength,
    long SourceVitality,
    long SourceAgility,
    long SourceSpirit,
    uint RingItemId,
    uint RightWeaponItemId,
    uint LeftWeaponItemId,
    uint ArmorItemId);

internal readonly record struct LegacyFirstCharacterStarterStats(
    long Strength,
    long Agility,
    long Vitality,
    long Spirit,
    uint Hitpoints,
    long Mana);

internal readonly record struct LegacyFirstCharacterStarterAttributes(
    long Strength,
    long Agility,
    long Vitality,
    long Spirit);
