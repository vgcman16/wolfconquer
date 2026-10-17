using System.Globalization;

namespace WolfConquer.Core.Data;

public sealed record ItemType(int Id, string Name, IReadOnlyList<string> Fields)
{
    public const int GoldWorthFieldIndex = 11;
    public const int RequiredClassFieldIndex = 2;
    public const int RequiredProficiencyFieldIndex = 3;
    public const int RequiredLevelFieldIndex = 4;
    public const int RequiredGenderFieldIndex = 5;
    public const int RequiredStrengthFieldIndex = 6;
    public const int RequiredAgilityFieldIndex = 7;
    public const int FrequencyFieldIndex = 16;
    public const int ItemHPFieldIndex = 18;
    public const int ItemMPFieldIndex = 19;
    public const int DurabilityFieldIndex = 21;
    public const int ConquerPointsWorthFieldIndex = 36;
    public const int RuneSpellIdFieldIndex = 25;
    public const int PurificationLevelFieldIndex = 55;
    public const int PurificationMeteorNeedFieldIndex = 56;

    public uint GoldWorth => TryGetUIntField(GoldWorthFieldIndex, out var value) ? value : 0;

    public uint RequiredClass => TryGetUIntField(RequiredClassFieldIndex, out var value) ? value : 0;

    public uint RequiredProficiency => TryGetUIntField(RequiredProficiencyFieldIndex, out var value) ? value : 0;

    public uint RequiredLevel => TryGetUIntField(RequiredLevelFieldIndex, out var value) ? value : 0;

    public uint RequiredGender => TryGetUIntField(RequiredGenderFieldIndex, out var value) ? value : 0;

    public uint RequiredStrength => TryGetUIntField(RequiredStrengthFieldIndex, out var value) ? value : 0;

    public uint RequiredAgility => TryGetUIntField(RequiredAgilityFieldIndex, out var value) ? value : 0;

    public uint Frequency => TryGetUIntField(FrequencyFieldIndex, out var value) ? value : 0;

    public uint ItemHP => TryGetUIntField(ItemHPFieldIndex, out var value) ? value : 0;

    public uint ItemMP => TryGetUIntField(ItemMPFieldIndex, out var value) ? value : 0;

    public uint LegacyDurability => TryGetUIntField(DurabilityFieldIndex, out var value) ? value : 0;

    public uint ConquerPointsWorth => TryGetUIntField(ConquerPointsWorthFieldIndex, out var value) ? value : 0;

    public uint RuneSpellId => TryGetUIntField(RuneSpellIdFieldIndex, out var value) ? value : 0;

    public uint PurificationLevel => TryGetUIntField(PurificationLevelFieldIndex, out var value) ? value : 0;

    public uint PurificationMeteorNeed => TryGetUIntField(PurificationMeteorNeedFieldIndex, out var value) ? value : 0;

    public bool IsLegacyAccessory
    {
        get
        {
            var position = GetLegacyEquipmentPosition(checked((uint)Id));
            return position is 15 or 16;
        }
    }

    public static ushort GetLegacyEquipmentPosition(uint itemId)
    {
        if (itemId >= 203003 && itemId <= 203009)
        {
            return 18;
        }

        if ((itemId >= 200731 && itemId <= 200772) ||
            itemId is 205007 or 205005 or 205004 or 205000 or 205902 or 205901 or 205003 ||
            (itemId >= 200000 && itemId <= 200726) ||
            itemId is 200696 or 200699 or 200579 or 200697 or 200705 or 200677 or 200688 or 200682 or 200647 or 200691 or 200670)
        {
            return 17;
        }

        if ((itemId >= 360323 && itemId <= 360401) ||
            (itemId >= 350001 && itemId <= 360291) ||
            itemId is 205003 or 350174 or 350175 or 360311 or 360312 or 360319 or 360320 or 360321 or 360322 or 360318 or 360305 or 360309 or 360310 or 360297)
        {
            return 15;
        }

        if ((itemId >= 360323 && itemId <= 360401) ||
            (itemId >= 370001 && itemId <= 380046) ||
            itemId is 205003 or 350174 or 350175 or 360311 or 360312 or 360319 or 360320 or 360321 or 360322 or 360318 or 360305 or 360309 or 360310 or 360297)
        {
            return 16;
        }

        if ((itemId >= 199185 && itemId <= 199255) ||
            itemId is 199325 or 196885 or 196865 or 196845 or 196815 or 196705 or 196755 or 196785 or 196345 or 196305 or 196275 or 196225 or 196595 or 196675 or 196635 or 196625 or 196615 or 196365 or 134155 or 131155 or 133155 or 130155 or 195795 or 195845)
        {
            return 9;
        }

        if ((itemId >= 111003 && itemId <= 118309) ||
            (itemId >= 123000 && itemId <= 123309) ||
            (itemId >= 141003 && itemId <= 145309) ||
            (itemId >= 146000 && itemId <= 148309) ||
            (itemId >= 170000 && itemId <= 170309))
        {
            return 1;
        }

        if (itemId >= 120001 && itemId <= 121269)
        {
            return 2;
        }

        if ((itemId >= 130003 && itemId <= 139309) ||
            (itemId >= 101000 && itemId <= 102309))
        {
            return 3;
        }

        if (itemId >= 150000 && itemId <= 152279)
        {
            return 6;
        }

        if (itemId >= 160013 && itemId <= 160249)
        {
            return 8;
        }

        if (itemId >= 181305 && itemId <= 196785)
        {
            return 9;
        }

        if (itemId >= 201003 && itemId <= 201009)
        {
            return 10;
        }

        if (itemId >= 202003 && itemId <= 202009)
        {
            return 11;
        }

        if (itemId == 300000)
        {
            return 12;
        }

        if ((itemId >= 410003 && itemId <= 613429) ||
            IsTrojanEpicWeapon(itemId) ||
            IsMonkEpicWeapon(itemId) ||
            IsNinjaEpicWeapon(itemId) ||
            (itemId >= 617000 && itemId <= 617439) ||
            IsTaoistEpicWeapon(itemId) ||
            IsWarriorEpicWeapon(itemId) ||
            IsWindWalkerWeapon(itemId) ||
            IsPirateEpicWeapon(itemId) ||
            itemId / 1000 == 681)
        {
            return 4;
        }

        if (IsLegacyShield(itemId) ||
            IsLegacyArrow(itemId) ||
            (itemId >= 612000 && itemId <= 613429) ||
            IsWarriorEpicWeapon(itemId) ||
            IsWindWalkerWeapon(itemId) ||
            (itemId >= 611000 && itemId <= 611439) ||
            IsTrojanEpicWeapon(itemId) ||
            IsNinjaEpicWeapon(itemId) ||
            IsMonkEpicWeapon(itemId) ||
            (itemId >= 617000 && itemId <= 617439) ||
            IsLegacyHossu(itemId) ||
            itemId / 1000 == 680)
        {
            return 5;
        }

        if ((itemId >= 2100025 && itemId <= 2100125) ||
            itemId is 2100165 or 2100175 or 2100185 or 2100195 or 2100205 or 2100005 or 2100245 or 2168735 or 2168896 or 2168725 or 2168715 or 2168705)
        {
            return 7;
        }

        if (itemId >= 204003 && itemId <= 204009)
        {
            return 19;
        }

        if (itemId / 10000 == 401)
        {
            return 101;
        }

        if (itemId / 10000 == 402)
        {
            return 102;
        }

        if (itemId / 10000 == 403)
        {
            return 103;
        }

        return itemId / 10000 == 410 ? (ushort)13 : (ushort)0;
    }

    public static bool IsLegacyArrow(uint itemId)
    {
        return itemId >= 1050000 && itemId <= 1051000;
    }

    public static byte GetLegacySashCapacity(uint itemId)
    {
        return itemId switch
        {
            1_100_009 => 12,
            1_100_006 => 6,
            1_100_003 => 3,
            _ => 0,
        };
    }

    public static bool IsLegacySash(uint itemId)
    {
        return GetLegacySashCapacity(itemId) > 0;
    }

    public static bool IsLegacyShield(uint itemId)
    {
        return itemId >= 900000 && itemId <= 900309;
    }

    public static bool IsLegacyKnife(uint itemId)
    {
        return itemId >= 613000 && itemId <= 613429;
    }

    public static bool IsLegacyHookScythe(uint itemId)
    {
        return itemId / 1000 == 511;
    }

    public static bool IsLegacyPistol(uint itemId)
    {
        return itemId >= 612000 && itemId <= 612439;
    }

    public static bool IsLegacyRapier(uint itemId)
    {
        return itemId >= 611000 && itemId <= 611439;
    }

    public static bool IsLegacyDagger(uint itemId)
    {
        return itemId >= 490003 && itemId <= 490149;
    }

    public static bool IsLegacyBacksword(uint itemId)
    {
        return itemId >= 421003 && itemId <= 421439;
    }

    public static bool IsLegacyHossu(uint itemId)
    {
        return itemId >= 619000 && itemId <= 619439;
    }

    public static bool IsLegacyBow(uint itemId)
    {
        return itemId / 1000 == 500;
    }

    public static bool IsLegacyTwoHandWeapon(uint itemId)
    {
        var leadingDigit = itemId;
        while (leadingDigit >= 10)
        {
            leadingDigit /= 10;
        }

        return leadingDigit == 5 || IsLegacyBacksword(itemId);
    }

    private bool TryGetUIntField(int fieldIndex, out uint value)
    {
        value = 0;

        return Fields.Count > fieldIndex
            && uint.TryParse(Fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsTrojanEpicWeapon(uint itemId)
    {
        return itemId >= 614000 && itemId <= 614439;
    }

    private static bool IsMonkEpicWeapon(uint itemId)
    {
        return itemId >= 622000 && itemId <= 622439;
    }

    private static bool IsNinjaEpicWeapon(uint itemId)
    {
        return itemId == 616 || itemId >= 616000 && itemId <= 616439;
    }

    private static bool IsTaoistEpicWeapon(uint itemId)
    {
        return itemId >= 620003 && itemId <= 620439;
    }

    private static bool IsWarriorEpicWeapon(uint itemId)
    {
        return itemId / 1000 == 624;
    }

    private static bool IsWindWalkerWeapon(uint itemId)
    {
        return itemId >= 626003 && itemId <= 626439;
    }

    private static bool IsPirateEpicWeapon(uint itemId)
    {
        return itemId / 1000 is 670 or 671;
    }

}
