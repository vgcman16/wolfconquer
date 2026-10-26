using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionPlan
{
    private LegacyTradeJournalChange[] CreateChanges()
    {
        var result = new List<LegacyTradeJournalChange>
        { new(LegacyTradeJournalFileKind.Character, account, images[LegacyTradeJournalFileKind.Character], characterAfter) };
        if (roots.Items is not null)
        {
            var before = images[LegacyTradeJournalFileKind.Items];
            if (before is not null)
                result.Add(new(LegacyTradeJournalFileKind.Items, account, before, before));
        }
        if (roots.Spells is not null)
        {
            var before = images[LegacyTradeJournalFileKind.Spells];
            var changed = !PlayerSpellSnapshotLoader.ToLegacyBytes(savedSpells).AsSpan()
                .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(normalization.Spells));
            if (before is not null || changed)
                result.Add(new(LegacyTradeJournalFileKind.Spells, account, before, changed ? spellAfter : before!));
        }
        if (roots.Proficiencies is not null)
            result.Add(new(LegacyTradeJournalFileKind.Proficiencies, account, images[LegacyTradeJournalFileKind.Proficiencies],
                images[LegacyTradeJournalFileKind.Proficiencies] ?? PlayerProficiencySnapshotLoader.ToLegacyBytes(profs)));
        return result.ToArray();
    }

}
