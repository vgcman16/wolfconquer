using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyClassOwnedSpellGrantProfile
{
    public const ushort CycloneSpellId = 1_110;

    private static readonly IReadOnlyList<ushort> EmptySpellIds = Array.Empty<ushort>();

    private static readonly IReadOnlyList<ushort> TrojanLineageLoginSpellIds = Array.AsReadOnly(
    [
        CycloneSpellId
    ]);

    public static IReadOnlyList<ushort> GetSpellIds(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return GetSpellIds(character.Class, character.FirstClass, character.SecondClass);
    }

    public static IReadOnlyList<ushort> GetSpellIds(
        long currentClass,
        long firstClass,
        long secondClass)
    {
        return IsTrojanFamily(currentClass) || IsTrojanFamily(firstClass) || IsTrojanFamily(secondClass)
            ? TrojanLineageLoginSpellIds
            : EmptySpellIds;
    }

    private static bool IsTrojanFamily(long classCode)
    {
        return classCode is >= 1_000 and <= 1_057;
    }
}
