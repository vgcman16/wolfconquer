using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyTradeJournalChange[] PrepareItemSpellImages(LegacySpellMutationInputs inputs,
        WorldSession updated, ItemSpellBeforeImages beforeImages, LegacyPreparedSpellFileWrite spellWrite,
        LegacyRuneOperation? rune = null, LegacyRuneContainerOperation? container = null, LegacyRuneComposeOperation? compose = null,
        LegacyRuneChanceOperation? chance = null)
    {
        var characterBefore = beforeImages.CopyCharacter();
        var current = inputs.Stable.Character;
        if (!ItemSpellAttributesMatch(updated.Character.Attributes, current.Attributes, rune is not null || container is not null || compose is not null || chance is not null) ||
            !ItemSpellAttributesMatch(current.Attributes, updated.Character.Attributes, rune is not null || container is not null || compose is not null || chance is not null) ||
            !FullAccountStoredFieldsMatch(updated.Character.PaidNobility, current.PaidNobility) ||
            !FullAccountStoredFieldsMatch(current.PaidNobility, updated.Character.PaidNobility))
            throw new LegacySpellMutationRefusalException("itemSpellUnexpectedCharacterFields");
        var characterAfter = characterBefore;
        if (rune is not null || container is not null || compose is not null || chance is not null)
        {
            if ((rune is not null ? 1 : 0) + (container is not null ? 1 : 0) + (compose is not null ? 1 : 0) + (chance is not null ? 1 : 0) != 1) throw InvalidSpellMutation();
            var projected = chance is not null
                ? ProjectRecordedRuneChance(current, inputs.Stable.PlayerItems, inputs.Stable.PlayerSpells, chance).Character
                : compose is not null
                ? ProjectRecordedRuneCompose(current, inputs.Stable.PlayerItems, inputs.Stable.PlayerSpells, compose).Character
                : rune is not null
                ? ProjectRecordedRuneOperation(current, inputs.Stable.PlayerItems, inputs.Stable.PlayerSpells, rune).Character
                : ProjectRecordedRuneContainer(current, inputs.Stable.PlayerItems, inputs.Stable.PlayerSpells, container!).Character;
            if (!FullAccountCharacterScalarsEqual(projected, updated.Character) ||
                !FullAccountStoredFieldsMatch(projected.Attributes, updated.Character.Attributes) ||
                !FullAccountStoredFieldsMatch(updated.Character.Attributes, projected.Attributes)) throw InvalidSpellMutation();
            characterAfter = LegacyRuneSpellOrigins.ProjectCharacterImage(characterBefore, projected);
            LegacyRuneSpellOrigins.RequireSavedRows(current, spellWrite.CopyBeforeImage());
        }
        else if (updated.Character.MinHitPoints != current.MinHitPoints)
        {
            characterAfter = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(characterBefore, updated.Character);
            var afterText = CharacterSnapshotFileEncoding.Decode(characterAfter, out _);
            if (!FullAccountCharacterScalarsEqual(CharacterSnapshotParser.ParseText(afterText), updated.Character))
                throw new LegacySpellMutationRefusalException("itemSpellCharacterAfterMismatch");
        }
        var itemBefore = beforeImages.CopyItems();
        var itemAfter = PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(itemAfter);
        var spellBefore = spellWrite.CopyBeforeImage();
        if (!SpellMutationImagesEqual(spellBefore, beforeImages.CopySpells())) throw InvalidSpellMutation();
        var spellAfter = spellWrite.CopyAfterImage();
        return [new(LegacyTradeJournalFileKind.Character, inputs.Stable.AccountId, characterBefore, characterAfter),
            new(LegacyTradeJournalFileKind.Items, inputs.Stable.Character.Uid, itemBefore, itemAfter),
            new(LegacyTradeJournalFileKind.Spells, inputs.Stable.Character.Uid, spellBefore, spellAfter)];
    }

    // The actual loader sorts some groups. Compare complete physical records by
    // identity instead of their order, retaining each deposited row's exact
    // parent identity. Perfection records remain full-byte proofs.
    // The catalog validates both images' complete bounds and unique identities.
    internal static bool ItemSpellRawRecordsMatch(byte[] saved, byte[] modeled)
    {
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(modeled);
        var left = ItemSpellRawSections(saved);
        var right = ItemSpellRawSections(modeled);
        if (left.Items.Count != right.Items.Count) return false;
        foreach (var (uid, row) in left.Items)
        {
            if (!right.Items.TryGetValue(uid, out var other) || row.Parent != other.Parent ||
                !saved.AsSpan(row.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize)
                    .SequenceEqual(modeled.AsSpan(other.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize)))
                return false;
        }
        return Equal(left.Perfections, right.Perfections);

        bool Equal(Dictionary<uint, (int Offset, int Length)> first, Dictionary<uint, (int Offset, int Length)> second)
        {
            if (first.Count != second.Count) return false;
            foreach (var (uid, slice) in first)
            {
                if (!second.TryGetValue(uid, out var other) || slice.Length != other.Length ||
                    !saved.AsSpan(slice.Offset, slice.Length).SequenceEqual(modeled.AsSpan(other.Offset, other.Length)))
                    return false;
            }
            return true;
        }
    }

    private static (Dictionary<uint, (int Offset, uint Parent)> Items,
        Dictionary<uint, (int Offset, int Length)> Perfections) ItemSpellRawSections(byte[] image)
    {
        // Caller validates the complete catalog first. The layout is the
        // actual DBItem ABI: uint UID at zero and deposit count at byte 136.
        const int depositCountOffset = 136;
        var items = new Dictionary<uint, (int, uint)>();
        var perfections = new Dictionary<uint, (int, int)>();
        int offset = sizeof(int);
        int count = BinaryPrimitives.ReadInt32LittleEndian(image);
        for (int i = 0; i < count; i++)
        {
            uint uid = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset));
            uint deposits = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset + depositCountOffset));
            items.Add(uid, (offset, 0));
            offset = checked(offset + PlayerItemSnapshotLoader.LegacyDbItemSize);
            for (uint child = 0; child < deposits; child++)
            {
                uint childUid = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset));
                items.Add(childUid, (offset, uid));
                offset = checked(offset + PlayerItemSnapshotLoader.LegacyDbItemSize);
            }
        }
        int perfectionCount = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(offset));
        offset += sizeof(int);
        for (int i = 0; i < perfectionCount; i++)
        {
            uint uid = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset));
            perfections.Add(uid, (offset, PlayerItemSnapshotLoader.LegacyPerfectionSize));
            offset += PlayerItemSnapshotLoader.LegacyPerfectionSize;
        }
        return (items, perfections);
    }

    private static bool ItemSpellAttributesMatch(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second,
        bool rune = false) =>
        first.Where(pair => !pair.Key.Equals("MinHitPoints", StringComparison.OrdinalIgnoreCase) &&
            (!rune || !pair.Key.Equals(LegacyRuneSpellOrigins.Key, StringComparison.OrdinalIgnoreCase)))
            .All(pair => second.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static bool ItemSpellBeforeImagesMatch(string[] paths, LegacyTradeJournalChange[] changes)
    {
        if (!CharacterSnapshotFileBaseline.TryReadBytes(paths[0], "itemSpell", out var character, out _) ||
            !CharacterSnapshotFileBaseline.TryReadBytes(paths[1], "itemSpell", out var items, out _)) return false;
        var spells = ReadLegacySpellMutationFile(paths[2]);
        return character.AsSpan().SequenceEqual(changes[0].Before) && items.AsSpan().SequenceEqual(changes[1].Before) &&
            SpellMutationImagesEqual(spells, changes[2].Before);
    }
}
