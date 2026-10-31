using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneDecomposeEvidence
{
    internal void RequireJournalTargets(string users, string items, string spells,
        IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        if (!IsCurrent() || changes.Count != 3) throw Invalid();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string characterId = request.AccountId.ToString(CultureInfo.InvariantCulture), itemId = request.CharacterUid.ToString(CultureInfo.InvariantCulture);
        string[] expectedPaths = [before.CharacterPath, before.ItemPath, before.SpellsPath];
        string[] suppliedPaths = [Path.Combine(Path.GetFullPath(users), characterId + ".ini"),
            Path.Combine(Path.GetFullPath(items), itemId + ".bin"), Path.Combine(Path.GetFullPath(spells), itemId + ".bin")];
        if (expectedPaths.Where((path, index) => !path.Equals(suppliedPaths[index], comparison)).Any()) throw Invalid();
        var projected = WorldPacketHandlers.ProjectRecordedRuneDecompose(stable.Character, stable.PlayerItems, stable.PlayerSpells, Operation);
        var characterBefore = before.CopyCharacter(); var itemsBefore = before.CopyItems(); var spellsBefore = before.CopySpells();
        bool stored = stable.PlayerItems.RuneItems.Single(row => row.Uid == selectedUid).Position is 211 or 212;
        byte[]?[] originals = [characterBefore, itemsBefore, spellsBefore];
        byte[][] after = [stored ? characterBefore : LegacyRuneSpellOrigins.ProjectCharacterImage(characterBefore, projected.Character),
            PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items), LegacyRuneSpellOrigins.ProjectSpellImage(spellsBefore, projected.Spells)];
        LegacyTradeJournalFileKind[] kinds = [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells];
        for (int index = 0; index < changes.Count; index++)
        {
            var row = changes[index];
            if (row is null || row.Kind != kinds[index] || row.FileId != (index == 0 ? request.AccountId : request.CharacterUid) ||
                !Same(row.Before, originals[index]) || !row.After.AsSpan().SequenceEqual(after[index])) throw Invalid();
        }
        if (stored && spellsBefore is not null && !spellsBefore.AsSpan().SequenceEqual(after[2])) throw Invalid();
        if (!IsCurrent()) throw Invalid();
    }

    private static bool Same(byte[]? first, byte[]? second) => first is null ? second is null :
        second is not null && first.AsSpan().SequenceEqual(second);
}
