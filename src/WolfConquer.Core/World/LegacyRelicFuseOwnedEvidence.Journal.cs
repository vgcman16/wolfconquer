using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicFuseOwnedEvidence
{
    // Checked by the genuine journal owner under its actual saved-file gates.
    // Matching this data does not itself commit a file or native holder.
    internal void RequireJournalWriter(LegacyPreparedSpellFileWrite writer)
    {
        if (!HasCurrentRuleLease() || !before.IsRelicFuseWriter(writer) || !DependenciesCurrent()) throw Invalid();
        before.RequireRelicFuseOwnerHeld(context, stable);
    }
    internal void RequireJournalTargets(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        before.RequireRelicFuseOwnerHeld(context, stable);
        if (!HasCurrentRuleLease() || !DependenciesCurrent() || !TryCreateJournalData(out var operation) || operation is null ||
            !TryCreateRetainedProjection(out var projection, out _) || projection is null || changes.Count != 3 ||
            !SamePath(before.CharacterPath, Path.Combine(usersRoot, accountId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini")) ||
            !SamePath(before.ItemPath, Path.Combine(itemsRoot, characterUid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin")) ||
            !SamePath(before.SpellsPath, Path.Combine(spellsRoot, characterUid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin"))) throw Invalid();
        var character = changes.SingleOrDefault(row => row.Kind == LegacyTradeJournalFileKind.Character);
        var items = changes.SingleOrDefault(row => row.Kind == LegacyTradeJournalFileKind.Items);
        var spells = changes.SingleOrDefault(row => row.Kind == LegacyTradeJournalFileKind.Spells);
        var savedCharacter = before.CopyCharacter(); var savedItems = before.CopyItems(); var savedSpells = before.CopySpells();
        if (character is null || items is null || spells is null || character.FileId != accountId ||
            items.FileId != characterUid || spells.FileId != characterUid ||
            character.Before is null || items.Before is null ||
            !character.Before.AsSpan().SequenceEqual(savedCharacter) || !character.After.AsSpan().SequenceEqual(savedCharacter) ||
            !items.Before.AsSpan().SequenceEqual(savedItems) || !items.After.AsSpan().SequenceEqual(projection.CopyAfterImage()) ||
            (savedSpells is null ? spells.Before is not null || !spells.After.AsSpan().SequenceEqual(new byte[sizeof(int)]) :
                spells.Before is null || !spells.Before.AsSpan().SequenceEqual(savedSpells) || !spells.After.AsSpan().SequenceEqual(savedSpells)))
            throw Invalid();
    }

    private static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
