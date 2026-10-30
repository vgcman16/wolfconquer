using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    private RuneCharacterPublication? committedRuneCharacter;

    internal RuneCharacterPublication PrepareRuneCharacterPublication(WorldSession proposed,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRuneOperationEvidence? rune,
        LegacyRuneContainerEvidence? container) => RuneCharacterPublication.Capture(this, proposed, changes, rune, container);

    internal sealed partial class RuneCharacterPublication
    {
        internal static RuneCharacterPublication Capture(LegacyPreparedSpellFileWrite writer, WorldSession proposed,
            IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRuneOperationEvidence? rune,
            LegacyRuneContainerEvidence? container)
        {
            if (writer.binding is null || (rune is null) == (container is null) ||
                rune is not null && !rune.IsCurrent() || container is not null && !container.IsCurrent()) throw Unavailable();
            var owned = writer.ValidateChanges(changes);
            if (owned.Length != 3 || owned[0].Kind != LegacyTradeJournalFileKind.Character ||
                owned[1].Kind != LegacyTradeJournalFileKind.Items || owned[2].Kind != LegacyTradeJournalFileKind.Spells ||
                owned[1].FileId != writer.expected.Character.Uid || owned[1].Before is null ||
                proposed.AccountId != writer.expected.AccountId || proposed.Character.Uid != writer.expected.Character.Uid ||
                !ReferenceEquals(proposed.NativeGameClient, writer.binding.Client)) throw Unavailable();
            var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(owned[0].Before!, out _));
            LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(owned[1].Before!);
            LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(owned[1].After);
            var items = PlayerItemSnapshotLoader.Parse(owned[1].Before!);
            var spells = owned[2].Before is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(owned[2].Before!);
            LegacyRuneSpellOrigins.RequireSavedRows(character, owned[2].Before);
            var projected = rune is not null
                ? WorldPacketHandlers.ProjectRecordedRuneOperation(character, items, spells, rune.Operation)
                : WorldPacketHandlers.ProjectRecordedRuneContainer(character, items, spells, container!.Operation);
            if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(character, writer.expected.Character) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(character, writer.expected.Character) ||
                !LegacyRuneSpellOrigins.Read(character).SequenceEqual(LegacyRuneSpellOrigins.Read(writer.expected.Character)) ||
                !WorldPacketHandlers.ItemSpellRawRecordsMatch(owned[1].Before!, PlayerItemSnapshotLoader.ToLegacyBytes(writer.expected.PlayerItems)) ||
                !WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(projected.Character, proposed.Character) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(projected.Character, proposed.Character) ||
                !LegacyRuneSpellOrigins.Read(projected.Character).SequenceEqual(LegacyRuneSpellOrigins.Read(proposed.Character)) ||
                !LegacyRuneSpellOrigins.ProjectCharacterImage(owned[0].Before!, projected.Character).AsSpan().SequenceEqual(owned[0].After) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items).AsSpan().SequenceEqual(owned[1].After) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(proposed.PlayerItems).AsSpan().SequenceEqual(owned[1].After) ||
                !LegacyRuneSpellOrigins.ProjectSpellImage(owned[2].Before, projected.Spells).AsSpan().SequenceEqual(owned[2].After) ||
                !LegacyRuneSpellOrigins.ProjectSpellImage(owned[2].Before, proposed.PlayerSpells).AsSpan().SequenceEqual(owned[2].After))
                throw Unavailable();
            return new(writer, proposed.Character, owned, rune, container);
        }
    }
}
