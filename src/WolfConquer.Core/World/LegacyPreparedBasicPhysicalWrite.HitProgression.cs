namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalWrite
{
    internal LegacyTradeJournalChange CaptureHitProgressionSpells(WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit,
        LegacyTradeJournalChange character)
    {
        if (!Plan.IsActualBasic(hit) || !IsCurrent() || character.Kind != LegacyTradeJournalFileKind.Character ||
            character.FileId != Binding.AccountId || !Same(character.Before, characterBefore) || !Same(character.After, characterAfter))
            throw Unavailable();
        return new(LegacyTradeJournalFileKind.Spells, Binding.Uid, spellsBefore?.ToArray(), spellsAfter.ToArray());
    }
    internal void AcceptCommittedHitProgression(LegacyPreparedBasicPhysicalHitProgressionWrite actual)
    {
        if (!actual.IsCommittedDeathWriter(this)) throw Unavailable();
        committed = Operation;
        spells.PublishCommittedPhysicalDeath(this);
    }
}
