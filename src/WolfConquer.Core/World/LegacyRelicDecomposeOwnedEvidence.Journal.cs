using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicDecomposeOwnedEvidence
{
    internal void RequireJournalWriter(LegacyPreparedSpellFileWrite actual)
    {
        if (!ReferenceEquals(actual, writer) || !DependenciesCurrent()) throw Invalid();
        before.RequireRelicDecomposeOwnerHeld(context, stable);
    }
    internal bool TryCreateJournalData(out LegacyRelicDecomposeJournalOperation? operation)
    {
        operation = null;
        if (pending is null || allocation is null || projectedAfter is null || !HasCurrentAllocation()) return false;
        operation = new(pending, allocation, request.CopyOriginalFrame(), recipe.Selections.ToArray(), recipe.RewardDurability);
        return true;
    }
    internal void RequireJournalTargets(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        before.RequireRelicDecomposeOwnerHeld(context, stable);
        if (!DependenciesCurrent() || !HasCurrentAllocation() || changes.Count != 3 || projectedAfter is null ||
            !SamePath(before.CharacterPath, Path.Combine(usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini")) ||
            !SamePath(before.ItemPath, Path.Combine(itemsRoot, characterUid.ToString(CultureInfo.InvariantCulture) + ".bin")) ||
            !SamePath(before.SpellsPath, Path.Combine(spellsRoot, characterUid.ToString(CultureInfo.InvariantCulture) + ".bin"))) throw Invalid();
        var character = changes.SingleOrDefault(row => row.Kind == LegacyTradeJournalFileKind.Character);
        var items = changes.SingleOrDefault(row => row.Kind == LegacyTradeJournalFileKind.Items);
        var spells = changes.SingleOrDefault(row => row.Kind == LegacyTradeJournalFileKind.Spells);
        var characterBefore = before.CopyCharacter(); var itemsBefore = before.CopyItems(); var spellsBefore = before.CopySpells();
        if (character is null || items is null || spells is null || character.FileId != accountId || items.FileId != characterUid ||
            spells.FileId != characterUid || character.Before is null || items.Before is null ||
            !character.Before.AsSpan().SequenceEqual(characterBefore) || !character.After.AsSpan().SequenceEqual(characterBefore) ||
            !items.Before.AsSpan().SequenceEqual(itemsBefore) || !items.After.AsSpan().SequenceEqual(projectedAfter) ||
            (spellsBefore is null ? spells.Before is not null || !spells.After.AsSpan().SequenceEqual(new byte[sizeof(int)]) :
                spells.Before is null || !spells.Before.AsSpan().SequenceEqual(spellsBefore) || !spells.After.AsSpan().SequenceEqual(spellsBefore))) throw Invalid();
    }
    private static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
