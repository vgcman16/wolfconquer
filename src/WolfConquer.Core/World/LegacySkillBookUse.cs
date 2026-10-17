using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public enum LegacySkillBookUseStatus
{
    NotSkillBook,
    UnsupportedSkillBook,
    MissingSpellMetadata,
    Recognized
}

public sealed record LegacySkillBookUseClassification(
    uint ItemId,
    LegacySkillBookUseStatus Status,
    PlayerSpellSnapshotEntry? SpellEntry)
{
    public bool IsSkillBook => Status != LegacySkillBookUseStatus.NotSkillBook;

    public ushort? SpellId => SpellEntry?.Id;
}

public static class LegacySkillBookUse
{
    private const ushort DefaultSpellLevel = 0;
    private const int DefaultSpellExperience = 0;
    private const byte DefaultPreviousLevel = 0;
    private const byte DefaultSoulLevel = 0;
    private const byte DefaultUseSpellSoul = 0;

    public static LegacySkillBookUseClassification Classify(uint itemId)
    {
        return ClassifyCore(itemId, magicBySpellId: null);
    }

    public static LegacySkillBookUseClassification Classify(uint itemId, WorldDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return ClassifyCore(itemId, catalog.MagicByTypeId);
    }

    public static LegacySkillBookUseClassification Classify(
        uint itemId,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>> magicBySpellId)
    {
        ArgumentNullException.ThrowIfNull(magicBySpellId);

        return ClassifyCore(itemId, magicBySpellId);
    }

    public static bool TryCreateSpellEntry(uint itemId, out PlayerSpellSnapshotEntry spellEntry)
    {
        var classification = Classify(itemId);
        if (classification.Status == LegacySkillBookUseStatus.Recognized)
        {
            spellEntry = classification.SpellEntry!;
            return true;
        }

        spellEntry = default!;
        return false;
    }

    public static PlayerSpellSnapshot AddOrUpdateSpell(
        PlayerSpellSnapshot playerSpells,
        PlayerSpellSnapshotEntry spellEntry)
    {
        ArgumentNullException.ThrowIfNull(playerSpells);
        ArgumentNullException.ThrowIfNull(spellEntry);

        var spells = new List<PlayerSpellSnapshotEntry>(playerSpells.Spells.Count + 1);
        var updated = false;
        foreach (var current in playerSpells.Spells)
        {
            if (current.Id == spellEntry.Id)
            {
                if (!updated)
                {
                    spells.Add(spellEntry);
                    updated = true;
                }

                continue;
            }

            spells.Add(current);
        }

        if (!updated)
        {
            spells.Add(spellEntry);
        }

        return new PlayerSpellSnapshot(spells);
    }

    private static LegacySkillBookUseClassification ClassifyCore(
        uint itemId,
        IReadOnlyDictionary<int, IReadOnlyList<MagicType>>? magicBySpellId)
    {
        if (!IsLegacySkillBookCandidate(itemId))
        {
            return new LegacySkillBookUseClassification(
                itemId,
                LegacySkillBookUseStatus.NotSkillBook,
                SpellEntry: null);
        }

        if (!TryGetSourceBackedSpellId(itemId, out var spellId))
        {
            return new LegacySkillBookUseClassification(
                itemId,
                LegacySkillBookUseStatus.UnsupportedSkillBook,
                SpellEntry: null);
        }

        if (magicBySpellId is not null &&
            (!magicBySpellId.TryGetValue(spellId, out var spellLevels) || spellLevels.Count == 0))
        {
            return new LegacySkillBookUseClassification(
                itemId,
                LegacySkillBookUseStatus.MissingSpellMetadata,
                SpellEntry: null);
        }

        return new LegacySkillBookUseClassification(
            itemId,
            LegacySkillBookUseStatus.Recognized,
            CreateDefaultSpellEntry(spellId));
    }

    private static PlayerSpellSnapshotEntry CreateDefaultSpellEntry(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            DefaultSpellLevel,
            DefaultSpellExperience,
            DefaultPreviousLevel,
            DefaultSoulLevel,
            DefaultUseSpellSoul);
    }

    private static bool IsLegacySkillBookCandidate(uint itemId)
    {
        return (itemId >= 725_000 && itemId <= 725_044) ||
            itemId is 1_060_101 or 721_157 or 721_158 or 3_007_566 or 3_007_567 or 3_007_568 ||
            (itemId >= 3_006_017 && itemId <= 3_006_020);
    }

    private static bool TryGetSourceBackedSpellId(uint itemId, out ushort spellId)
    {
        // Source: MrRayzo.Game.MsgServer.MsgItemUsuagePacket.UseItem MySpells.Add branches.
        spellId = itemId switch
        {
            725_000 => 1_000,
            725_001 => 1_001,
            725_002 => 1_002,
            725_003 => 1_005,
            725_004 => 1_010,
            725_005 => 1_045,
            725_010 => 1_046,
            725_011 => 1_250,
            725_012 => 1_260,
            725_013 => 1_290,
            725_014 => 1_300,
            725_015 => 1_350,
            725_016 => 1_360,
            725_018 => 1_380,
            725_019 => 1_385,
            725_020 => 1_390,
            725_021 => 1_395,
            725_022 => 1_400,
            725_023 => 1_405,
            725_024 => 1_410,
            725_025 => 1_320,
            725_026 => 5_010,
            725_027 => 5_020,
            725_028 => 5_001,
            725_029 => 5_030,
            725_030 => 5_040,
            725_031 => 5_050,
            725_040 => 7_000,
            725_041 => 7_010,
            725_042 => 7_020,
            725_043 => 7_030,
            725_044 => 7_040,
            721_157 => 11_000,
            721_158 => 11_005,
            1_060_101 => 1_165,
            3_006_017 => 12_380,
            3_006_018 => 12_370,
            3_006_019 => 12_400,
            3_006_020 => 12_390,
            3_007_566 => 12_570,
            3_007_567 => 12_560,
            3_007_568 => 12_550,
            _ => 0
        };

        return spellId != 0;
    }
}
