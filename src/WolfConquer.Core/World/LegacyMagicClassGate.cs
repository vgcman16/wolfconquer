using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public readonly record struct LegacyMagicClassGateResult(
    ushort SpellId,
    bool HasClassGate,
    IReadOnlyList<int> CharacterFamilies,
    IReadOnlyList<int> RequiredFamilies);

public static class LegacyMagicClassGate
{
    public static bool CanUsePlayerSpell(
        CharacterSnapshot character,
        ushort spellId,
        IReadOnlyDictionary<int, IReadOnlyList<MagicTypeOperationEntry>>? operationsBySpellId,
        out LegacyMagicClassGateResult result)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (operationsBySpellId is null ||
            !operationsBySpellId.TryGetValue(spellId, out var operationRows))
        {
            result = CreateResult(character, spellId, hasClassGate: false, requiredFamilies: []);
            return true;
        }

        return CanUsePlayerSpell(character, spellId, operationRows, out result);
    }

    public static bool CanUsePlayerSpell(
        CharacterSnapshot character,
        ushort spellId,
        IReadOnlyList<MagicTypeOperationEntry> operationRows,
        out LegacyMagicClassGateResult result)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(operationRows);

        var classGateRows = operationRows
            .Where(IsClassGateRow)
            .ToArray();
        var requiredFamilies = classGateRows
            .SelectMany(GetRequiredFamilies)
            .Distinct()
            .OrderBy(family => family)
            .ToArray();
        if (requiredFamilies.Length == 0)
        {
            result = CreateResult(character, spellId, hasClassGate: false, requiredFamilies);
            return true;
        }

        var characterFamilies = GetCharacterClassFamilies(character);
        result = new LegacyMagicClassGateResult(
            spellId,
            HasClassGate: true,
            characterFamilies.All,
            requiredFamilies);
        return classGateRows.Any(row => RowMatchesCharacter(row, character, characterFamilies));
    }

    public static string FormatFamilies(IReadOnlyList<int> families)
    {
        return families.Count == 0
            ? "none"
            : string.Join(",", families.Select(family => family.ToString(CultureInfo.InvariantCulture)));
    }

    private static LegacyMagicClassGateResult CreateResult(
        CharacterSnapshot character,
        ushort spellId,
        bool hasClassGate,
        IReadOnlyList<int> requiredFamilies)
    {
        return new LegacyMagicClassGateResult(
            spellId,
            hasClassGate,
            GetCharacterClassFamilies(character).All,
            requiredFamilies);
    }

    private static CharacterClassFamilies GetCharacterClassFamilies(CharacterSnapshot character)
    {
        var currentFamilies = GetClassFamilies(character.Class);
        var families = new HashSet<int>();
        foreach (var family in currentFamilies)
        {
            families.Add(family);
        }

        var previousFamilies = new HashSet<int>();
        if (character.Reborn >= 1)
        {
            AddClassFamilies(previousFamilies, character.FirstClass);
        }

        if (character.Reborn >= 2)
        {
            AddClassFamilies(previousFamilies, character.SecondClass);
        }

        foreach (var family in previousFamilies)
        {
            families.Add(family);
        }

        return new CharacterClassFamilies(
            currentFamilies,
            previousFamilies.OrderBy(family => family).ToArray(),
            families.OrderBy(family => family).ToArray());
    }

    private static IReadOnlyList<int> GetClassFamilies(long classCode)
    {
        var families = new HashSet<int>();
        AddClassFamilies(families, classCode);
        return families.OrderBy(family => family).ToArray();
    }

    private static bool IsClassGateRow(MagicTypeOperationEntry row)
    {
        return row.Mode == 1 && row.Group > 0;
    }

    private static IEnumerable<int> GetRequiredFamilies(MagicTypeOperationEntry row)
    {
        yield return row.Group;
        if (IsRebornLineageGate(row) && row.Level > 0)
        {
            yield return row.Level;
        }
    }

    private static bool RowMatchesCharacter(
        MagicTypeOperationEntry row,
        CharacterSnapshot character,
        CharacterClassFamilies characterFamilies)
    {
        if (!IsRebornLineageGate(row))
        {
            return characterFamilies.All.Contains(row.Group);
        }

        if (character.Reborn < RequiredRebornCount(row) ||
            !characterFamilies.Current.Contains(row.Group))
        {
            return false;
        }

        return row.Level <= 0 || characterFamilies.Previous.Contains(row.Level);
    }

    private static bool IsRebornLineageGate(MagicTypeOperationEntry row)
    {
        return row.Flag is 6 or 7;
    }

    private static int RequiredRebornCount(MagicTypeOperationEntry row)
    {
        return row.Flag == 7 ? 2 : 1;
    }

    private static void AddClassFamilies(HashSet<int> families, long classCode)
    {
        var family = NormalizeClassFamily(classCode);
        if (family == 0)
        {
            return;
        }

        families.Add(family);
        if (family == 10)
        {
            families.Add(13);
            families.Add(14);
        }
    }

    private static int NormalizeClassFamily(long classCode)
    {
        if (classCode <= 0)
        {
            return 0;
        }

        if (classCode >= 1_000)
        {
            return checked((int)(classCode / 1_000));
        }

        return classCode >= 10
            ? checked((int)(classCode / 10))
            : checked((int)classCode);
    }

    private readonly record struct CharacterClassFamilies(
        IReadOnlyList<int> Current,
        IReadOnlyList<int> Previous,
        IReadOnlyList<int> All);
}
