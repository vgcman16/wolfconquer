using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool PassesLegacyRebornEquipBypass(CharacterSnapshot character, ItemType itemType)
    {
        return character.Reborn > 0 && character.Level >= 70 && itemType.RequiredLevel <= 70;
    }

    private static bool PassesLegacyItemGenderRequirement(CharacterSnapshot character, ItemType itemType)
    {
        var characterGender = character.Body / 1000;
        return itemType.RequiredGender == 0 ||
            characterGender >= 0 && (ulong)characterGender == itemType.RequiredGender;
    }

    private static bool PassesLegacyItemClassRequirement(CharacterSnapshot character, ItemType itemType)
        => PassesLegacyItemClassRequirement(character, itemType.RequiredClass);

    private static bool PassesLegacyItemClassRequirement(CharacterSnapshot character, uint requiredClass)
    {
        if (requiredClass == 0)
        {
            return true;
        }

        var classCode = character.Class;
        return requiredClass switch
        {
            >= 1000 and <= 1005 => classCode >= requiredClass && classCode <= 1057,
            >= 2000 and <= 2005 => classCode >= requiredClass && classCode <= 2057,
            >= 4000 and <= 4005 => classCode >= requiredClass && classCode <= 4057,
            >= 5000 and <= 5005 => classCode >= requiredClass && classCode <= 5057,
            >= 6000 and <= 6005 => classCode >= requiredClass && classCode <= 6057,
            >= 7000 and <= 7005 => classCode >= requiredClass && classCode <= 7057,
            >= 8000 and <= 8005 => classCode >= requiredClass && classCode <= 8057,
            >= 9000 and <= 9005 => classCode >= requiredClass && classCode <= 9057,
            >= 13000 and <= 13005 => classCode >= requiredClass && classCode <= 13057,
            >= 14000 and <= 14005 => classCode >= requiredClass && classCode <= 14057,
            10000 or 19000 => classCode >= 10000,
            >= 16000 and <= 16005 => classCode >= requiredClass && classCode <= 16057,
            _ => false,
        };
    }

    private static bool PassesLegacyItemLevelRequirement(CharacterSnapshot character, ItemType itemType)
        => PassesLegacyItemLevelRequirement(character, itemType.RequiredLevel);

    private static bool PassesLegacyItemLevelRequirement(CharacterSnapshot character, uint requiredLevel)
        => requiredLevel == 0 || character.Level >= requiredLevel;

    private static bool PassesLegacyItemStatRequirement(CharacterSnapshot character, ItemType itemType)
    {
        return character.Strength >= itemType.RequiredStrength && character.Agility >= itemType.RequiredAgility;
    }
}
