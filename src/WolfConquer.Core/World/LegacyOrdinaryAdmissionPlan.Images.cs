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
                result.Add(new(LegacyTradeJournalFileKind.Items, source.Uid, before, before));
        }
        if (roots.Spells is not null)
        {
            var before = images[LegacyTradeJournalFileKind.Spells];
            var changed = !PlayerSpellSnapshotLoader.ToLegacyBytes(savedSpells).AsSpan()
                .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(normalization.Spells));
            if (before is not null || changed)
                result.Add(new(LegacyTradeJournalFileKind.Spells, source.Uid, before, changed ? spellAfter : before!));
        }
        // A genuinely absent empty book remains an explicit read-only admission
        // input. The first real progression owns its later atomic creation.
        if (roots.Proficiencies is not null && images[LegacyTradeJournalFileKind.Proficiencies] is { } profBefore)
            result.Add(new(LegacyTradeJournalFileKind.Proficiencies, source.Uid, profBefore, profBefore));
        return result.ToArray();
    }

}
