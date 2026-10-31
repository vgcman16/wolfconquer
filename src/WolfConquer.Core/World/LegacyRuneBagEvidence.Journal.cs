using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneBagEvidence
{
    internal void RequireJournalTargets(string users, string items, string spells, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        if (!IsCurrent() || changes.Count != 3) throw Invalid();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string account = stable.AccountId.ToString(CultureInfo.InvariantCulture), uid = stable.Character.Uid.ToString(CultureInfo.InvariantCulture);
        string[] paths = [Path.Combine(Path.GetFullPath(users), account + ".ini"), Path.Combine(Path.GetFullPath(items), uid + ".bin"),
            Path.Combine(Path.GetFullPath(spells), uid + ".bin")];
        if (!before.CharacterPath.Equals(paths[0], comparison) || !before.ItemPath.Equals(paths[1], comparison) ||
            !before.SpellsPath.Equals(paths[2], comparison)) throw Invalid();
        var projected = WorldPacketHandlers.ProjectRecordedRuneBag(stable.Character, stable.PlayerItems, stable.PlayerSpells, Operation);
        byte[]?[] originals = [before.CopyCharacter(), before.CopyItems(), before.CopySpells()];
        byte[][] after = [before.CopyCharacter(), PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items),
            LegacyRuneSpellOrigins.ProjectSpellImage(originals[2], projected.Spells)];
        LegacyTradeJournalFileKind[] kinds = [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells];
        for (int index = 0; index < 3; index++)
        {
            var row = changes[index];
            if (row.Kind != kinds[index] || row.FileId != (index == 0 ? stable.AccountId : stable.Character.Uid) ||
                !Same(row.Before, originals[index]) || !row.After.AsSpan().SequenceEqual(after[index])) throw Invalid();
        }
        if (originals[2] is { } spellBefore && !spellBefore.AsSpan().SequenceEqual(after[2]) || !IsCurrent()) throw Invalid();
    }
    private static bool Same(byte[]? first, byte[]? second) => first is null ? second is null :
        second is not null && first.AsSpan().SequenceEqual(second);
}
