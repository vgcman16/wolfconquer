using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyTradeJournalChange[] PrepareItemSpellImages(LegacySpellMutationInputs inputs,
        WorldSession updated, string characterPath, string itemPath, LegacyPreparedSpellFileWrite spellWrite)
    {
        if (!CharacterSnapshotFileBaseline.TryRead(characterPath, "itemSpell", out var baseline, out var reason) || baseline is null)
            throw new LegacySpellMutationRefusalException(reason);
        var text = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(baseline.Bytes))
            throw new LegacySpellMutationRefusalException("itemSpellCharacterEncodingUnproven");
        var disk = CharacterSnapshotParser.ParseText(text);
        var current = inputs.Stable.Character;
        if (!FullAccountCharacterScalarsEqual(disk, current) || !FullAccountStoredFieldsMatch(disk.Attributes, current.Attributes) ||
            !FullAccountStoredFieldsMatch(disk.PaidNobility, current.PaidNobility))
            throw new LegacySpellMutationRefusalException("itemSpellCharacterBaselineStale");
        if (!ItemSpellAttributesMatch(updated.Character.Attributes, current.Attributes) ||
            !ItemSpellAttributesMatch(current.Attributes, updated.Character.Attributes) ||
            !FullAccountStoredFieldsMatch(updated.Character.PaidNobility, current.PaidNobility) ||
            !FullAccountStoredFieldsMatch(current.PaidNobility, updated.Character.PaidNobility))
            throw new LegacySpellMutationRefusalException("itemSpellUnexpectedCharacterFields");
        var characterAfter = baseline.Bytes;
        if (updated.Character.MinHitPoints != current.MinHitPoints)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["MinHitPoints"] = updated.Character.MinHitPoints.ToString(CultureInfo.InvariantCulture) };
            var afterText = LegacyOrdinaryAdmissionCharacterText.Project(text, values, paid: null);
            characterAfter = encoding.GetPreamble().Concat(encoding.GetBytes(afterText)).ToArray();
            if (!FullAccountCharacterScalarsEqual(CharacterSnapshotParser.ParseText(afterText), updated.Character))
                throw new LegacySpellMutationRefusalException("itemSpellCharacterAfterMismatch");
        }
        if (!CharacterSnapshotFileBaseline.TryReadBytes(itemPath, "itemSpell", out var itemBefore, out reason))
            throw new LegacySpellMutationRefusalException(reason);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(itemBefore);
        var modeledBefore = PlayerItemSnapshotLoader.ToLegacyBytes(inputs.Stable.PlayerItems);
        if (!itemBefore.AsSpan().SequenceEqual(modeledBefore))
            throw new LegacySpellMutationRefusalException("itemSpellItemBaselineStaleOrUnrepresentable");
        var itemAfter = PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(itemAfter);
        var spellBefore = spellWrite.CopyBeforeImage();
        var spellAfter = spellWrite.CopyAfterImage();
        return [new(LegacyTradeJournalFileKind.Character, inputs.Stable.AccountId, baseline.Bytes, characterAfter),
            new(LegacyTradeJournalFileKind.Items, inputs.Stable.Character.Uid, itemBefore, itemAfter),
            new(LegacyTradeJournalFileKind.Spells, inputs.Stable.Character.Uid, spellBefore, spellAfter)];
    }

    private static bool ItemSpellAttributesMatch(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second) =>
        first.Where(pair => !pair.Key.Equals("MinHitPoints", StringComparison.OrdinalIgnoreCase))
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
