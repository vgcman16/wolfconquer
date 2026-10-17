using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal readonly record struct LegacyClassLevelStatProgressionChange(
    bool Changed,
    bool AppliedSourceStats,
    bool AddedAttributePoints,
    long AttributePointsAdded,
    string ClassName,
    long SourceStatLevel);

internal static class LegacyClassLevelStatProgression
{
    public static CharacterSnapshot RefreshVitals(
        CharacterSnapshot character,
        PlayerItemSnapshot playerItems,
        CombatGearSnapshot combatGear,
        WorldDataCatalog? worldData)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(playerItems);
        ArgumentNullException.ThrowIfNull(combatGear);

        var maxHitpoints = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
            character,
            currentHitpoints: 0,
            playerItems,
            combatGear,
            worldData);
        var maxMana = LegacyFirstCharacterStarterProfiles.CalculateMana(character.Class, character.Spirit);
        var updated = character with
        {
            MinHitPoints = maxHitpoints,
            MinMana = maxMana,
            MaxMana = maxMana,
        };

        var attributes = new Dictionary<string, string>(updated.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["MinHitPoints"] = updated.MinHitPoints.ToString(CultureInfo.InvariantCulture),
            ["MinMana"] = updated.MinMana.ToString(CultureInfo.InvariantCulture),
            ["MaxMana"] = updated.MaxMana.ToString(CultureInfo.InvariantCulture),
        };
        return updated with { Attributes = attributes };
    }

    public static CharacterSnapshot Apply(
        CharacterSnapshot character,
        WorldDataCatalog? worldData,
        long previousLevel,
        out LegacyClassLevelStatProgressionChange change)
    {
        ArgumentNullException.ThrowIfNull(character);

        change = default;
        if (worldData is null ||
            character.Level <= 0 ||
            !TryGetSourceClassName(character.Class, out var className))
        {
            return character;
        }

        var updated = character;
        var changed = false;
        var appliedSourceStats = false;
        var addedAttributePoints = false;
        var attributePointsAdded = 0L;
        var sourceStatLevel = 0L;

        if (character.Reborn == 0)
        {
            var sourceAutoStatMaxLevel = IsWater(character.Class) ? 110L : 120L;
            if (character.Level <= sourceAutoStatMaxLevel &&
                TryGetSourceStats(worldData, className, character.Level, out var sourceStats))
            {
                updated = WithSourceStats(updated, sourceStats);
                changed = changed || !ReferenceEquals(updated, character);
                appliedSourceStats = true;
                sourceStatLevel = character.Level;
            }

            if (character.Level > sourceAutoStatMaxLevel &&
                previousLevel < character.Level)
            {
                var freePointLevels = character.Level - Math.Max(previousLevel, sourceAutoStatMaxLevel);
                if (freePointLevels > 0)
                {
                    attributePointsAdded = checked(freePointLevels * 3);
                    updated = WithAttributePoints(updated, checked(updated.AttributePoints + attributePointsAdded));
                    changed = true;
                    addedAttributePoints = true;
                }
            }
        }
        else if (previousLevel < character.Level)
        {
            attributePointsAdded = checked((character.Level - previousLevel) * 3);
            if (attributePointsAdded > 0)
            {
                updated = WithAttributePoints(updated, checked(updated.AttributePoints + attributePointsAdded));
                changed = true;
                addedAttributePoints = true;
            }
        }

        change = new LegacyClassLevelStatProgressionChange(
            changed,
            appliedSourceStats,
            addedAttributePoints,
            attributePointsAdded,
            className,
            sourceStatLevel);
        return updated;
    }

    public static bool TryGetSourceClassName(long classCode, out string className)
    {
        className = classCode switch
        {
            >= 1000 and <= 1057 => "Trojan",
            >= 2000 and <= 2057 => "Warrior",
            >= 4000 and <= 4057 => "Archer",
            >= 5000 and <= 5057 => "Ninja",
            >= 6000 and <= 6057 => "Monk",
            >= 7000 and <= 7057 => "Pirate",
            >= 8000 and <= 8057 => "LongLee",
            >= 9000 and <= 9057 => "Thunderstriker",
            >= 10000 and <= 14057 => "Taoist",
            >= 16000 and <= 16057 => "WindWalker",
            _ => string.Empty,
        };
        return className.Length != 0;
    }

    private static bool IsWater(long classCode)
    {
        return classCode is >= 13002 and <= 13057;
    }

    private static bool TryGetSourceStats(
        WorldDataCatalog worldData,
        string className,
        long level,
        out ClassLevelStat sourceStats)
    {
        sourceStats = default!;
        if (level is < int.MinValue or > int.MaxValue ||
            !worldData.ClassLevelStatsByClassAndLevel.TryGetValue((className, checked((int)level)), out var found))
        {
            return false;
        }

        sourceStats = found;
        return true;
    }

    private static CharacterSnapshot WithSourceStats(CharacterSnapshot character, ClassLevelStat sourceStats)
    {
        if (character.Strength == sourceStats.Strength &&
            character.Agility == sourceStats.Agility &&
            character.Vitality == sourceStats.Vitality &&
            character.Spirit == sourceStats.Spirit)
        {
            return character;
        }

        var updated = character with
        {
            Strength = sourceStats.Strength,
            Agility = sourceStats.Agility,
            Vitality = sourceStats.Vitality,
            Spirit = sourceStats.Spirit,
        };
        return WithStatAttributes(updated);
    }

    private static CharacterSnapshot WithAttributePoints(CharacterSnapshot character, long attributePoints)
    {
        if (character.AttributePoints == attributePoints)
        {
            return character;
        }

        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Atributes"] = attributePoints.ToString(CultureInfo.InvariantCulture),
        };
        return character with
        {
            AttributePoints = attributePoints,
            Attributes = attributes,
        };
    }

    private static CharacterSnapshot WithStatAttributes(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Strength"] = character.Strength.ToString(CultureInfo.InvariantCulture),
            ["Agility"] = character.Agility.ToString(CultureInfo.InvariantCulture),
            ["Vitaliti"] = character.Vitality.ToString(CultureInfo.InvariantCulture),
            ["Spirit"] = character.Spirit.ToString(CultureInfo.InvariantCulture),
        };
        return character with { Attributes = attributes };
    }
}
