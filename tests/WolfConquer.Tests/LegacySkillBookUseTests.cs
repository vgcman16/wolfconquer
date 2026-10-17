using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySkillBookUseTests
{
    [Fact]
    public void RecognizedSkillBookReturnsDefaultSpellEntry()
    {
        var classification = LegacySkillBookUse.Classify(725_016);

        Assert.Equal(LegacySkillBookUseStatus.Recognized, classification.Status);
        Assert.True(classification.IsSkillBook);
        Assert.Equal((ushort?)1_360, classification.SpellId);
        Assert.Equal(Entry(1_360), classification.SpellEntry);
        Assert.True(LegacySkillBookUse.TryCreateSpellEntry(725_016, out var spellEntry));
        Assert.Equal(Entry(1_360), spellEntry);
    }

    [Fact]
    public void NonSkillItemIsIgnored()
    {
        var classification = LegacySkillBookUse.Classify(1_000_000);

        Assert.Equal(LegacySkillBookUseStatus.NotSkillBook, classification.Status);
        Assert.False(classification.IsSkillBook);
        Assert.Null(classification.SpellId);
        Assert.Null(classification.SpellEntry);
        Assert.False(LegacySkillBookUse.TryCreateSpellEntry(1_000_000, out _));
    }

    [Fact]
    public void AddOrUpdateSpellUsesLegacySpellAddReplacementSemantics()
    {
        var playerSpells = new PlayerSpellSnapshot(
        [
            Entry(1_000, level: 1, experience: 50),
            Entry(1_360, level: 3, experience: 99, previousLevel: 2, soulLevel: 4, useSpellSoul: 2),
        ]);
        Assert.True(LegacySkillBookUse.TryCreateSpellEntry(725_016, out var learnedSpell));

        var updated = LegacySkillBookUse.AddOrUpdateSpell(playerSpells, learnedSpell);

        Assert.Equal([1_000U, 1_360U], updated.SpellIds.ToArray());
        Assert.Equal(Entry(1_000, level: 1, experience: 50), updated.Spells[0]);
        Assert.Equal(Entry(1_360), updated.Spells[1]);
    }

    [Fact]
    public void UnsupportedSkillBookRangeReturnsUnsupportedWithoutEntry()
    {
        var classification = LegacySkillBookUse.Classify(725_006);

        Assert.Equal(LegacySkillBookUseStatus.UnsupportedSkillBook, classification.Status);
        Assert.True(classification.IsSkillBook);
        Assert.Null(classification.SpellId);
        Assert.Null(classification.SpellEntry);
    }

    [Fact]
    public void RecognizedSkillBookReportsMissingMagicMetadataWhenCatalogLookupMisses()
    {
        var classification = LegacySkillBookUse.Classify(
            725_016,
            new Dictionary<int, IReadOnlyList<MagicType>>());

        Assert.Equal(LegacySkillBookUseStatus.MissingSpellMetadata, classification.Status);
        Assert.True(classification.IsSkillBook);
        Assert.Null(classification.SpellId);
        Assert.Null(classification.SpellEntry);
    }

    private static PlayerSpellSnapshotEntry Entry(
        ushort id,
        ushort level = 0,
        int experience = 0,
        byte previousLevel = 0,
        byte soulLevel = 0,
        byte useSpellSoul = 0)
    {
        return new PlayerSpellSnapshotEntry(id, level, experience, previousLevel, soulLevel, useSpellSoul);
    }
}
