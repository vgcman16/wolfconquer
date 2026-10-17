using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class DataParserTests
{
    [Fact]
    public void AtAtParserPreservesTrailingEmptyField()
    {
        var fields = AtAtTableParser.SplitRow("101000@@MysticWindrobe@@16000@@Windrobe@@None@@");

        Assert.Equal(["101000", "MysticWindrobe", "16000", "Windrobe", "None", ""], fields);
    }

    [Fact]
    public void LotteryDictionaryParserReadsUniformAtAtRows()
    {
        var entry = LotteryParser.ParseDictionaryRow(
            "dict_lottery.txt",
            17,
            "17@@2@@3@@18007@@0@@2@@");

        Assert.Equal(17, entry.Id);
        Assert.Equal(2, entry.Type);
        Assert.Equal(3, entry.SubType);
        Assert.Equal(18007, entry.Value);
        Assert.Equal(0, entry.Unknown0);
        Assert.Equal(2, entry.Weight);
        Assert.Equal("", entry.Fields[^1]);
        Assert.Equal("dict_lottery.txt", entry.SourceFile);
        Assert.Equal(17, entry.LineNumber);
    }

    [Theory]
    [InlineData("1@@2@@3@@1007@@0@@20")]
    [InlineData("x@@2@@3@@1007@@0@@20@@")]
    public void LotteryDictionaryParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => LotteryParser.ParseDictionaryRow("dict_lottery.txt", 1, row));
    }

    [Fact]
    public void CollectionRewardParserReadsUniformAtAtRows()
    {
        var tier = CollectionParser.ParseRewardTierRow(
            "dict_collection.txt",
            11,
            "11@@700@@8400@@2147483647@@1@@1500@@600@@");

        Assert.Equal(11, tier.Id);
        Assert.Equal(700, tier.CollectionId);
        Assert.Equal(8400, tier.MinInclusive);
        Assert.Equal(2_147_483_647, tier.MaxInclusive);
        Assert.Equal(1, tier.RewardType);
        Assert.Equal(1500, tier.RewardValue);
        Assert.Equal(600, tier.RewardAmount);
        Assert.Equal("", tier.Fields[^1]);
        Assert.Equal("dict_collection.txt", tier.SourceFile);
        Assert.Equal(11, tier.LineNumber);
        Assert.Equal("11@@700@@8400@@2147483647@@1@@1500@@600@@", tier.RawLine);
    }

    [Theory]
    [InlineData("1@@700@@1@@1000@@1@@100@@50")]
    [InlineData("1@@700@@1@@1000@@1@@100@@50@@extra")]
    [InlineData("x@@700@@1@@1000@@1@@100@@50@@")]
    public void CollectionRewardParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => CollectionParser.ParseRewardTierRow("dict_collection.txt", 1, row));
    }

    [Fact]
    public void SyndicateLevelParserReadsUniformAtAtRows()
    {
        var config = SyndicateParser.ParseLevelRow(
            "syndicate_level.txt",
            1,
            "1@@999@@2@@8@@10@@3@@3@@3@@1@@3@@5@@5@@");

        Assert.Equal(1, config.Level);
        Assert.Equal([999, 2, 8, 10, 3, 3, 3, 1, 3, 5, 5], config.Parameters);
        Assert.Equal("", config.Fields[^1]);
        Assert.Equal("syndicate_level.txt", config.SourceFile);
        Assert.Equal(1, config.LineNumber);
        Assert.Equal("1@@999@@2@@8@@10@@3@@3@@3@@1@@3@@5@@5@@", config.RawLine);
    }

    [Theory]
    [InlineData("1@@999@@2@@8@@10@@3@@3@@3@@1@@3@@5@@5")]
    [InlineData("1@@999@@2@@8@@10@@3@@3@@3@@1@@3@@5@@5@@extra")]
    [InlineData("x@@999@@2@@8@@10@@3@@3@@3@@1@@3@@5@@5@@")]
    [InlineData("1@@999@@x@@8@@10@@3@@3@@3@@1@@3@@5@@5@@")]
    public void SyndicateLevelParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => SyndicateParser.ParseLevelRow("syndicate_level.txt", 1, row));
    }

    [Fact]
    public void RuneLevelExperienceParserReadsUniformAtAtRows()
    {
        var entry = RuneParser.ParseLevelExperienceRow(
            "rune_levexp.txt",
            28,
            "28@@402@@27@@341500@@");

        Assert.Equal(28, entry.Id);
        Assert.Equal(402, entry.RuneType);
        Assert.Equal(27, entry.Level);
        Assert.Equal(341_500, entry.Experience);
        Assert.Equal("", entry.Fields[^1]);
        Assert.Equal("rune_levexp.txt", entry.SourceFile);
        Assert.Equal(28, entry.LineNumber);
        Assert.Equal("28@@402@@27@@341500@@", entry.RawLine);
    }

    [Theory]
    [InlineData("28@@402@@27@@341500")]
    [InlineData("28@@402@@27@@341500@@extra")]
    [InlineData("x@@402@@27@@341500@@")]
    [InlineData("28@@402@@x@@341500@@")]
    public void RuneLevelExperienceParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => RuneParser.ParseLevelExperienceRow("rune_levexp.txt", 1, row));
    }

    [Fact]
    public void RuneStorageAttributeParserReadsUniformAtAtRows()
    {
        var entry = RuneParser.ParseStorageAttributeRow(
            "rune_storage_attr.txt",
            21,
            "21@@335000@@11@@300@@12@@300@@0@@0@@");

        Assert.Equal(21, entry.Id);
        Assert.Equal(335_000, entry.RequiredStorageScore);
        Assert.Equal(
            [
                new RuneStorageAttributeBonus(11, 300),
                new RuneStorageAttributeBonus(12, 300),
                new RuneStorageAttributeBonus(0, 0),
            ],
            entry.Bonuses);
        Assert.Equal("", entry.Fields[^1]);
        Assert.Equal("rune_storage_attr.txt", entry.SourceFile);
        Assert.Equal(21, entry.LineNumber);
        Assert.Equal("21@@335000@@11@@300@@12@@300@@0@@0@@", entry.RawLine);
    }

    [Theory]
    [InlineData("1@@2000@@2@@200@@3@@200@@0@@0")]
    [InlineData("1@@2000@@2@@200@@3@@200@@0@@0@@extra")]
    [InlineData("x@@2000@@2@@200@@3@@200@@0@@0@@")]
    [InlineData("1@@2000@@x@@200@@3@@200@@0@@0@@")]
    public void RuneStorageAttributeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => RuneParser.ParseStorageAttributeRow("rune_storage_attr.txt", 1, row));
    }

    [Fact]
    public void ItemRefineEffectParserPreservesLiveAtAtRows()
    {
        var effect = ItemRefineParser.ParseEffectRow(
            "item_refine_effect.txt",
            648,
            "648@@6480@@6480@@8080@@2020@@1010@@2007@@3007@@4008@@5005@@6005@@7008@@8008@@9005@@10005@@11008@@12008@@13008@@14006@@15006@@16002@@");

        Assert.Equal(648, effect.Id);
        Assert.Equal(
            [6480, 6480, 8080, 2020, 1010, 2007, 3007, 4008, 5005, 6005, 7008, 8008, 9005, 10005, 11008, 12008, 13008, 14006, 15006, 16002],
            effect.Values);
        Assert.Equal("", effect.Fields[^1]);
        Assert.Equal("item_refine_effect.txt", effect.SourceFile);
        Assert.Equal(648, effect.LineNumber);
        Assert.Equal("648@@6480@@6480@@8080@@2020@@1010@@2007@@3007@@4008@@5005@@6005@@7008@@8008@@9005@@10005@@11008@@12008@@13008@@14006@@15006@@16002@@", effect.RawLine);
    }

    [Fact]
    public void ItemRefineEffectExParserPreservesItemSpecificRows()
    {
        var effect = ItemRefineParser.ParseEffectExRow(
            "item_refine_effect_ex.txt",
            1,
            "1@@2100095@@100@@5@@7@@");

        Assert.Equal(1, effect.Id);
        Assert.Equal(2_100_095, effect.ItemTypeId);
        Assert.Equal([100, 5, 7], effect.Values);
        Assert.Equal("", effect.Fields[^1]);
        Assert.Equal("item_refine_effect_ex.txt", effect.SourceFile);
        Assert.Equal(1, effect.LineNumber);
        Assert.Equal("1@@2100095@@100@@5@@7@@", effect.RawLine);
    }

    [Fact]
    public void ItemRefineUpgradeParserPreservesRefineLevelRows()
    {
        var upgrade = ItemRefineParser.ParseUpgradeRow(
            "item_refine_upgrade.txt",
            54,
            "1@@54@@12000@@");

        Assert.Equal(1, upgrade.Id);
        Assert.Equal(54, upgrade.Level);
        Assert.Equal(12_000, upgrade.Value);
        Assert.Equal("", upgrade.Fields[^1]);
        Assert.Equal("item_refine_upgrade.txt", upgrade.SourceFile);
        Assert.Equal(54, upgrade.LineNumber);
        Assert.Equal("1@@54@@12000@@", upgrade.RawLine);
    }

    [Theory]
    [InlineData("1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@7001@@0@@0@@0@@11001@@0@@0@@0@@0@@0")]
    [InlineData("x@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@7001@@0@@0@@0@@11001@@0@@0@@0@@0@@0@@")]
    public void ItemRefineEffectParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => ItemRefineParser.ParseEffectRow("item_refine_effect.txt", 1, row));
    }

    [Theory]
    [InlineData("1@@2100095@@100@@5@@7")]
    [InlineData("1@@item@@100@@5@@7@@")]
    public void ItemRefineEffectExParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => ItemRefineParser.ParseEffectExRow("item_refine_effect_ex.txt", 1, row));
    }

    [Theory]
    [InlineData("1@@54@@12000")]
    [InlineData("1@@x@@12000@@")]
    public void ItemRefineUpgradeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => ItemRefineParser.ParseUpgradeRow("item_refine_upgrade.txt", 1, row));
    }

    [Fact]
    public void TitleTypeParserPreservesTitleRows()
    {
        var entry = TitleTypeParser.ParseRow(
            "title_type.txt",
            269,
            "8043@@8043@@CS Team PK Champion Halo@@0@@0@@0@@0@@400@@");

        Assert.Equal(8043, entry.Id);
        Assert.Equal(8043, entry.TypeId);
        Assert.Equal("CS Team PK Champion Halo", entry.Name);
        Assert.Equal(0, entry.Unknown0);
        Assert.Equal(0, entry.Unknown1);
        Assert.Equal(0, entry.Unknown2);
        Assert.Equal(0, entry.Unknown3);
        Assert.Equal(400, entry.Score);
        Assert.Equal("", entry.Fields[^1]);
        Assert.Equal("title_type.txt", entry.SourceFile);
        Assert.Equal(269, entry.LineNumber);
        Assert.Equal("8043@@8043@@CS Team PK Champion Halo@@0@@0@@0@@0@@400@@", entry.RawLine);
    }

    [Theory]
    [InlineData("1@@1000@@Overlord@@0@@0@@0@@0@@150")]
    [InlineData("1@@x@@Overlord@@0@@0@@0@@0@@150@@")]
    public void TitleTypeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => TitleTypeParser.ParseRow("title_type.txt", 1, row));
    }

    [Fact]
    public void CombatGearParserPreservesCombatGearRows()
    {
        var gear = CombatGearParser.ParseRow(
            "combat_gear.txt",
            1,
            "1@@201@@1@@300@@60@@30@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@240@@0@@0@@0@@");

        Assert.Equal(1, gear.Id);
        Assert.Equal(201, gear.GearTypeId);
        Assert.Equal(1, gear.Level);
        Assert.Equal(300, gear.UpExperience);
        Assert.Equal(60, gear.Life);
        Assert.Equal(30, gear.Attack);
        Assert.Equal(0, gear.Defense);
        Assert.Equal(0, gear.UnknownStat0);
        Assert.Equal(0, gear.MagicDefense);
        Assert.Equal([17640000, 0, 0, 17650000, 0, 0, 0, 0, 0], gear.MagicTypeIds);
        Assert.Equal(240, gear.Score);
        Assert.Equal(0, gear.Unknown0);
        Assert.Equal(0, gear.Unknown1);
        Assert.Equal(0, gear.Unknown2);
        Assert.Equal("", gear.Fields[^1]);
        Assert.Equal("combat_gear.txt", gear.SourceFile);
        Assert.Equal(1, gear.LineNumber);
    }

    [Theory]
    [InlineData("1@@201@@1@@300@@60@@30@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@240@@0@@0@@0")]
    [InlineData("1@@201@@x@@300@@60@@30@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@240@@0@@0@@0@@")]
    public void CombatGearParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => CombatGearParser.ParseRow("combat_gear.txt", 1, row));
    }

    [Fact]
    public void TrapParserPreservesTrapRowsWithOptionalFlags()
    {
        var trap = TrapParser.ParseRow("Traps.txt", 1, "44463,77,190,1618,1");

        Assert.Equal(44463, trap.MapId);
        Assert.Equal(77, trap.X);
        Assert.Equal(190, trap.Y);
        Assert.Equal(1618, trap.TrapTypeId);
        Assert.Equal(1, trap.OptionalFlag);
        Assert.Equal("Traps.txt", trap.SourceFile);
        Assert.Equal(1, trap.LineNumber);
        Assert.Equal("44463,77,190,1618,1", trap.RawLine);
        Assert.Equal(["44463", "77", "190", "1618", "1"], trap.Fields);

        var withoutFlag = TrapParser.ParseRow("Traps.txt", 194, "10090,361,314,24");

        Assert.Equal(10090, withoutFlag.MapId);
        Assert.Equal(361, withoutFlag.X);
        Assert.Equal(314, withoutFlag.Y);
        Assert.Equal(24, withoutFlag.TrapTypeId);
        Assert.Null(withoutFlag.OptionalFlag);
        Assert.Equal(["10090", "361", "314", "24"], withoutFlag.Fields);
    }

    [Theory]
    [InlineData("44463,77,190")]
    [InlineData("44463,77,190,1618,1,0")]
    [InlineData("44463,77,x,1618,1")]
    public void TrapParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => TrapParser.ParseRow("Traps.txt", 1, row));
    }

    [Fact]
    public void FurnitureParserPreservesFurnitureRows()
    {
        var furniture = FurnitureParser.ParseRow(
            "furnitures.txt",
            1,
            "8924,26,837,1511,143,130,FlowerDressingTable,726083,23000");

        Assert.Equal(8924, furniture.Id);
        Assert.Equal(26, furniture.Kind);
        Assert.Equal(837, furniture.Lookface);
        Assert.Equal(1511, furniture.MapId);
        Assert.Equal(143, furniture.X);
        Assert.Equal(130, furniture.Y);
        Assert.Equal("FlowerDressingTable", furniture.Name);
        Assert.Equal(726083, furniture.ItemTypeId);
        Assert.Equal(23000, furniture.Price);
        Assert.Equal("furnitures.txt", furniture.SourceFile);
        Assert.Equal(1, furniture.LineNumber);
        Assert.Equal("8924,26,837,1511,143,130,FlowerDressingTable,726083,23000", furniture.RawLine);
        Assert.Equal(["8924", "26", "837", "1511", "143", "130", "FlowerDressingTable", "726083", "23000"], furniture.Fields);
    }

    [Theory]
    [InlineData("8924,26,837,1511,143,130,FlowerDressingTable,726083")]
    [InlineData("8924,26,837,1511,143,130,FlowerDressingTable,726083,23000,extra")]
    [InlineData("x,26,837,1511,143,130,FlowerDressingTable,726083,23000")]
    [InlineData("8924,26,837,1511,143,130,FlowerDressingTable,nope,23000")]
    public void FurnitureParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => FurnitureParser.ParseRow("furnitures.txt", 1, row));
    }

    [Fact]
    public void CosmeticParserPreservesHairColorRows()
    {
        var entry = CosmeticParser.ParseHairColorTypeRow(
            "hair_color_type.txt",
            275,
            "78@@5@@1@@3326778@@1@@");

        Assert.Equal(78, entry.HairColorId);
        Assert.Equal(5, entry.ColorIndex);
        Assert.Equal(1, entry.UnlockType);
        Assert.Equal(3326778, entry.ItemTypeId);
        Assert.Equal(1, entry.Cost);
        Assert.Equal("hair_color_type.txt", entry.SourceFile);
        Assert.Equal(275, entry.LineNumber);
        Assert.Equal("78@@5@@1@@3326778@@1@@", entry.RawLine);
        Assert.Equal(["78", "5", "1", "3326778", "1", ""], entry.Fields);
    }

    [Theory]
    [InlineData("78@@5@@1@@3326778@@1")]
    [InlineData("78@@5@@1@@3326778@@1@@extra")]
    [InlineData("x@@5@@1@@3326778@@1@@")]
    [InlineData("78@@5@@1@@nope@@1@@")]
    public void CosmeticHairColorParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => CosmeticParser.ParseHairColorTypeRow("hair_color_type.txt", 1, row));
    }

    [Fact]
    public void CosmeticParserPreservesHairFaceStorageRows()
    {
        var entry = CosmeticParser.ParseHairFaceStorageTypeRow(
            "hairface_storage_type.txt",
            1,
            "0@@1@@0@@YouthSpark@@0@@0@@0@@10000@@0@@@@0@@");

        Assert.Equal(0, entry.CategoryType);
        Assert.Equal(1, entry.Id);
        Assert.Equal("YouthSpark", entry.Name);
        Assert.Equal(0, entry.UnlockType);
        Assert.Equal(0, entry.RequiredValue);
        Assert.Equal(10_000, entry.Cost);
        Assert.Equal("", entry.LinkedColorIdsRaw);
        Assert.Equal(0, entry.Sort);
        Assert.Equal("hairface_storage_type.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal("0@@1@@0@@YouthSpark@@0@@0@@0@@10000@@0@@@@0@@", entry.RawLine);
        Assert.Equal(["0", "1", "0", "YouthSpark", "0", "0", "0", "10000", "0", "", "0", ""], entry.Fields);
    }

    [Theory]
    [InlineData("0@@1@@0@@YouthSpark@@0@@0@@0@@10000@@0@@@@0")]
    [InlineData("0@@1@@0@@YouthSpark@@0@@0@@0@@10000@@0@@@@0@@extra")]
    [InlineData("0@@x@@0@@YouthSpark@@0@@0@@0@@10000@@0@@@@0@@")]
    [InlineData("0@@1@@0@@YouthSpark@@0@@0@@0@@nope@@0@@@@0@@")]
    public void CosmeticHairFaceStorageParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => CosmeticParser.ParseHairFaceStorageTypeRow("hairface_storage_type.txt", 1, row));
    }

    [Fact]
    public void CosmeticParserPreservesCoatStorageTypeRows()
    {
        var entry = CosmeticParser.ParseCoatStorageTypeRow(
            "coat_storage_type.txt",
            1,
            "181305@@31@@1@@0@@0@@0@@0@@1@@");

        Assert.Equal(181305, entry.ItemTypeId);
        Assert.Equal(31, entry.StorageType);
        Assert.Equal([1, 0, 0, 0, 0, 1], entry.Parameters);
        Assert.Equal("", entry.Fields[^1]);
        Assert.Equal("coat_storage_type.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal("181305@@31@@1@@0@@0@@0@@0@@1@@", entry.RawLine);
    }

    [Theory]
    [InlineData("181305@@31@@1@@0@@0@@0@@0@@1")]
    [InlineData("181305@@31@@1@@0@@0@@0@@0@@1@@extra")]
    [InlineData("x@@31@@1@@0@@0@@0@@0@@1@@")]
    [InlineData("181305@@31@@x@@0@@0@@0@@0@@1@@")]
    public void CosmeticCoatStorageTypeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => CosmeticParser.ParseCoatStorageTypeRow("coat_storage_type.txt", 1, row));
    }

    [Fact]
    public void CosmeticParserPreservesCoatStorageAttributeRows()
    {
        var entry = CosmeticParser.ParseCoatStorageAttributeRow(
            "coat_storage_attr.txt",
            3,
            "17@@1000000000@@30@@3@@300@@5@@100@@0@@0@@0@@0@@0@@");

        Assert.Equal(17, entry.Id);
        Assert.Equal(1_000_000_000, entry.StorageType);
        Assert.Equal(30, entry.RequiredStorageScore);
        Assert.Equal(
            [
                new CosmeticAttributeBonus(3, 300),
                new CosmeticAttributeBonus(5, 100),
                new CosmeticAttributeBonus(0, 0),
                new CosmeticAttributeBonus(0, 0),
            ],
            entry.Bonuses);
        Assert.Equal(0, entry.Reserved0);
        Assert.Equal("", entry.Fields[^1]);
        Assert.Equal("coat_storage_attr.txt", entry.SourceFile);
        Assert.Equal(3, entry.LineNumber);
        Assert.Equal("17@@1000000000@@30@@3@@300@@5@@100@@0@@0@@0@@0@@0@@", entry.RawLine);
    }

    [Fact]
    public void CosmeticCoatStorageAttributeParserPreservesLargeStorageTypes()
    {
        var entry = CosmeticParser.ParseCoatStorageAttributeRow(
            "coat_storage_attr.txt",
            91,
            "108@@3000000000@@1@@23@@100@@0@@0@@0@@0@@0@@0@@0@@");

        Assert.Equal(108, entry.Id);
        Assert.Equal(3_000_000_000, entry.StorageType);
        Assert.Equal(1, entry.RequiredStorageScore);
        Assert.Equal(new CosmeticAttributeBonus(23, 100), entry.Bonuses[0]);
        Assert.Equal("", entry.Fields[^1]);
    }

    [Theory]
    [InlineData("17@@1000000000@@30@@3@@300@@5@@100@@0@@0@@0@@0@@0")]
    [InlineData("17@@1000000000@@30@@3@@300@@5@@100@@0@@0@@0@@0@@0@@extra")]
    [InlineData("x@@1000000000@@30@@3@@300@@5@@100@@0@@0@@0@@0@@0@@")]
    [InlineData("17@@1000000000@@30@@x@@300@@5@@100@@0@@0@@0@@0@@0@@")]
    public void CosmeticCoatStorageAttributeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => CosmeticParser.ParseCoatStorageAttributeRow("coat_storage_attr.txt", 1, row));
    }

    [Fact]
    public void CosmeticParserPreservesCoatColorRuleRows()
    {
        var entry = CosmeticParser.ParseCoatColorRuleRow(
            "coat_color_rule.txt",
            1,
            "200412@@1@@5@@499@@3326059@@1@@3328359@@5@@300@@232503150@@232503150@@232503150@@232503150@@0@@0@@0@@0@@250200@@50@@");

        Assert.Equal(200412, entry.ItemTypeId);
        Assert.Equal([1, 5, 499, 3326059, 1, 3328359, 5, 300, 232503150, 232503150, 232503150, 232503150, 0, 0, 0, 0, 250200, 50], entry.Parameters);
        Assert.Equal("", entry.Fields[^1]);
        Assert.Equal("coat_color_rule.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal("200412@@1@@5@@499@@3326059@@1@@3328359@@5@@300@@232503150@@232503150@@232503150@@232503150@@0@@0@@0@@0@@250200@@50@@", entry.RawLine);
    }

    [Theory]
    [InlineData("200412@@1@@5@@499@@3326059@@1@@3328359@@5@@300@@232503150@@232503150@@232503150@@232503150@@0@@0@@0@@0@@250200@@50")]
    [InlineData("200412@@1@@5@@499@@3326059@@1@@3328359@@5@@300@@232503150@@232503150@@232503150@@232503150@@0@@0@@0@@0@@250200@@50@@extra")]
    [InlineData("x@@1@@5@@499@@3326059@@1@@3328359@@5@@300@@232503150@@232503150@@232503150@@232503150@@0@@0@@0@@0@@250200@@50@@")]
    [InlineData("200412@@1@@x@@499@@3326059@@1@@3328359@@5@@300@@232503150@@232503150@@232503150@@232503150@@0@@0@@0@@0@@250200@@50@@")]
    public void CosmeticCoatColorRuleParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => CosmeticParser.ParseCoatColorRuleRow("coat_color_rule.txt", 1, row));
    }

    [Fact]
    public void XuanbaoParserPreservesAdditionAttributeRows()
    {
        var entry = XuanbaoParser.ParseAdditionAttributeRow(
            "xuanbao_addition_attr.txt",
            1,
            "1@@14@@1@@0@@50@@");

        Assert.Equal(1, entry.Id);
        Assert.Equal(14, entry.AttributeType);
        Assert.Equal(1, entry.Level);
        Assert.Equal([0, 50], entry.Values);
        Assert.Equal("xuanbao_addition_attr.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal("1@@14@@1@@0@@50@@", entry.RawLine);
        Assert.Equal(["1", "14", "1", "0", "50", ""], entry.Fields);
    }

    [Theory]
    [InlineData("1@@14@@1@@0@@50")]
    [InlineData("1@@14@@1@@0@@50@@extra")]
    [InlineData("x@@14@@1@@0@@50@@")]
    [InlineData("1@@14@@1@@nope@@50@@")]
    public void XuanbaoAdditionAttributeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => XuanbaoParser.ParseAdditionAttributeRow("xuanbao_addition_attr.txt", 1, row));
    }

    [Fact]
    public void XuanbaoParserPreservesComposeAttributeLimitRows()
    {
        var entry = XuanbaoParser.ParseComposeAttributeLimitRow(
            "xuanbao_compose_attr_limit.txt",
            1,
            "1@@0@@4@@400@@");

        Assert.Equal(1, entry.Id);
        Assert.Equal([0, 4, 400], entry.Values);
        Assert.Equal("xuanbao_compose_attr_limit.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal("1@@0@@4@@400@@", entry.RawLine);
        Assert.Equal(["1", "0", "4", "400", ""], entry.Fields);
    }

    [Theory]
    [InlineData("1@@0@@4@@400")]
    [InlineData("1@@0@@4@@400@@extra")]
    [InlineData("x@@0@@4@@400@@")]
    [InlineData("1@@0@@nope@@400@@")]
    public void XuanbaoComposeAttributeLimitParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => XuanbaoParser.ParseComposeAttributeLimitRow("xuanbao_compose_attr_limit.txt", 1, row));
    }

    [Fact]
    public void XuanbaoParserPreservesRandomAttributeRows()
    {
        var entry = XuanbaoParser.ParseRandomAttributeRow(
            "xuanbao_rand_attr.txt",
            1,
            "4100001@@1@@30@@101@@");

        Assert.Equal(4_100_001, entry.ItemTypeId);
        Assert.Equal(1, entry.AttributeType);
        Assert.Equal(30, entry.MinValue);
        Assert.Equal(101, entry.MaxValue);
        Assert.Equal("xuanbao_rand_attr.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal("4100001@@1@@30@@101@@", entry.RawLine);
        Assert.Equal(["4100001", "1", "30", "101", ""], entry.Fields);
    }

    [Theory]
    [InlineData("4100001@@1@@30@@101")]
    [InlineData("4100001@@1@@30@@101@@extra")]
    [InlineData("x@@1@@30@@101@@")]
    [InlineData("4100001@@1@@nope@@101@@")]
    public void XuanbaoRandomAttributeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => XuanbaoParser.ParseRandomAttributeRow("xuanbao_rand_attr.txt", 1, row));
    }

    [Fact]
    public void ChristmasNpcParserPreservesLiveCsvRows()
    {
        var npc = ChristmasNpcParser.ParseRow(
            "christmasnpcs.txt",
            1,
            "52301,,2,15820,1002,313,231,0,0,1,1");

        Assert.Equal(52301, npc.Id);
        Assert.Null(npc.Name);
        Assert.Equal(2, npc.Kind);
        Assert.Equal(15820, npc.Lookface);
        Assert.Equal(1002, npc.MapId);
        Assert.Equal(313, npc.X);
        Assert.Equal(231, npc.Y);
        Assert.Equal([0, 0, 1, 1], npc.Parameters);
        Assert.Equal("christmasnpcs.txt", npc.SourceFile);
        Assert.Equal(1, npc.LineNumber);
        Assert.Equal("52301,,2,15820,1002,313,231,0,0,1,1", npc.RawLine);
        Assert.Equal(["52301", "", "2", "15820", "1002", "313", "231", "0", "0", "1", "1"], npc.Fields);
    }

    [Theory]
    [InlineData("52301,,2,15820,1002,313,231,0,0,1")]
    [InlineData("52301,,2,15820,1002,313,231,0,0,1,1,extra")]
    [InlineData("x,,2,15820,1002,313,231,0,0,1,1")]
    [InlineData("52301,,2,15820,1002,x,231,0,0,1,1")]
    public void ChristmasNpcParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => ChristmasNpcParser.ParseRow("christmasnpcs.txt", 1, row));
    }

    [Fact]
    public void TutorTypeParserPreservesLiveCsvRows()
    {
        var entry = TutorParser.ParseTypeRow(
            "cq_tutor_type.txt",
            1,
            "1,70,109,2,10");

        Assert.Equal(1, entry.Id);
        Assert.Equal(70, entry.MinLevel);
        Assert.Equal(109, entry.MaxLevel);
        Assert.Equal([2, 10], entry.Values);
        Assert.Equal("cq_tutor_type.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
        Assert.Equal("1,70,109,2,10", entry.RawLine);
        Assert.Equal(["1", "70", "109", "2", "10"], entry.Fields);
    }

    [Theory]
    [InlineData("1,70,109,2")]
    [InlineData("1,70,109,2,10,extra")]
    [InlineData("x,70,109,2,10")]
    [InlineData("1,70,nope,2,10")]
    public void TutorTypeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => TutorParser.ParseTypeRow("cq_tutor_type.txt", 1, row));
    }

    [Fact]
    public void TutorBattleLimitTypeParserPreservesLiveCsvRows()
    {
        var entry = TutorParser.ParseBattleLimitTypeRow(
            "cq_tutor_battle_limit_type.txt",
            400,
            "400,5");

        Assert.Equal(400, entry.Id);
        Assert.Equal(5, entry.BattlePowerLimit);
        Assert.Equal("cq_tutor_battle_limit_type.txt", entry.SourceFile);
        Assert.Equal(400, entry.LineNumber);
        Assert.Equal("400,5", entry.RawLine);
        Assert.Equal(["400", "5"], entry.Fields);
    }

    [Theory]
    [InlineData("400")]
    [InlineData("400,5,extra")]
    [InlineData("x,5")]
    [InlineData("400,nope")]
    public void TutorBattleLimitTypeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => TutorParser.ParseBattleLimitTypeRow("cq_tutor_battle_limit_type.txt", 1, row));
    }

    [Fact]
    public void TaskRewardTypeParserPreservesRewardTableRows()
    {
        var entry = TaskRewardParser.ParseTypeRow(
            "task_reward_type.txt",
            1,
            "755@@2@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@3301408@@3301409@@3308803@@3301410@@3301411@@3301412@@3301413@@3301414@@3000@@3000@@3000@@600@@280@@100@@10@@10@@0@@0@@");

        Assert.Equal(755, entry.Id);
        Assert.Equal([2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], entry.Parameters);
        Assert.Equal([3301408, 3301409, 3308803, 3301410, 3301411, 3301412, 3301413, 3301414], entry.RewardValues);
        Assert.Equal([3000, 3000, 3000, 600, 280, 100, 10, 10], entry.RewardWeights);
        Assert.Equal(0, entry.Extra0);
        Assert.Equal(0, entry.Extra1);
        Assert.Equal("", entry.Fields[^1]);
        Assert.Equal("task_reward_type.txt", entry.SourceFile);
        Assert.Equal(1, entry.LineNumber);
    }

    [Theory]
    [InlineData("755@@2@@0@@0")]
    [InlineData("755@@x@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@3301408@@3301409@@3308803@@3301410@@3301411@@3301412@@3301413@@3301414@@3000@@3000@@3000@@600@@280@@100@@10@@10@@0@@0@@")]
    public void TaskRewardTypeParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => TaskRewardParser.ParseTypeRow("task_reward_type.txt", 1, row));
    }

    [Fact]
    public void TaskRewardTypeIniParserPreservesWhitespaceRows()
    {
        var entry = TaskRewardParser.ParseTypeIniRow(
            "task_reward_type.ini",
            2,
            "3651 2 1 0002 0000 0002 0 0 0 0 0 0 0 0 3007285 3007284 3007386 3008731 3007311 3008729 3600012 3008732 1000 1000 1000 1000 1500 1500 1500 1500 98579160 98579161");

        Assert.Equal(3651, entry.Id);
        Assert.Equal([2, 1, 2, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0], entry.Parameters);
        Assert.Equal([3007285, 3007284, 3007386, 3008731, 3007311, 3008729, 3600012, 3008732], entry.RewardValues);
        Assert.Equal([1000, 1000, 1000, 1000, 1500, 1500, 1500, 1500], entry.RewardWeights);
        Assert.Equal(98_579_160, entry.Extra0);
        Assert.Equal(98_579_161, entry.Extra1);
        Assert.Equal("0002", entry.Fields[3]);
        Assert.Equal("task_reward_type.ini", entry.SourceFile);
        Assert.Equal(2, entry.LineNumber);
        Assert.Equal("3651 2 1 0002 0000 0002 0 0 0 0 0 0 0 0 3007285 3007284 3007386 3008731 3007311 3008729 3600012 3008732 1000 1000 1000 1000 1500 1500 1500 1500 98579160 98579161", entry.RawLine);
    }

    [Theory]
    [InlineData("3651 2 1")]
    [InlineData("3651 2 1 0002 0000 0002 0 0 0 0 0 0 0 0 3007285 3007284 3007386 3008731 3007311 3008729 3600012 3008732 1000 1000 1000 1000 1500 1500 1500 1500 98579160")]
    [InlineData("3651 2 1 0002 0000 nope 0 0 0 0 0 0 0 0 3007285 3007284 3007386 3008731 3007311 3008729 3600012 3008732 1000 1000 1000 1000 1500 1500 1500 1500 98579160 98579161")]
    public void TaskRewardTypeIniParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => TaskRewardParser.ParseTypeIniRow("task_reward_type.ini", 1, row));
    }

    [Fact]
    public void ProfessionParserReadsPromotionAndBenefitRows()
    {
        var levelUp = ProfessionParser.ParseLevelUpRow(
            "prof_lev_up.txt",
            571,
            "571@@16051@@120@@0@@920000@@800000@@");
        var levelBenefit = ProfessionParser.ParseLevelBenefitRow(
            "prof_lev_benefit.txt",
            571,
            "571@@16051@@5@@62600@@71200@@0@@0@@2600@@");
        var titleBenefit = ProfessionParser.ParseTitleBenefitRow(
            "prof_title_benefit.txt",
            33,
            "33@@16@@3@@60500@@70100@@100100@@120100@@0@@");

        Assert.Equal(571, levelUp.Id);
        Assert.Equal(16051, levelUp.ProfessionCode);
        Assert.Equal(120, levelUp.RequiredLevel);
        Assert.Equal(0, levelUp.RequiredMetempsychosis);
        Assert.Equal(920_000, levelUp.RequiredMoney);
        Assert.Equal(800_000, levelUp.RequiredProficiency);
        Assert.Equal("", levelUp.Fields[^1]);

        Assert.Equal(16051, levelBenefit.ProfessionCode);
        Assert.Equal(5, levelBenefit.Rank);
        Assert.Equal([62600, 71200, 0, 0, 2600], levelBenefit.BonusValues);
        Assert.Equal("", levelBenefit.Fields[^1]);

        Assert.Equal(16, titleBenefit.ProfessionGroup);
        Assert.Equal(3, titleBenefit.Rank);
        Assert.Equal([60500, 70100, 100100, 120100, 0], titleBenefit.BonusValues);
        Assert.Equal("prof_title_benefit.txt", titleBenefit.SourceFile);
        Assert.Equal(33, titleBenefit.LineNumber);
        Assert.Equal("", titleBenefit.Fields[^1]);
    }

    [Theory]
    [InlineData("571@@16051@@120@@0@@920000@@800000")]
    [InlineData("571@@x@@120@@0@@920000@@800000@@")]
    public void ProfessionLevelUpParserRejectsWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => ProfessionParser.ParseLevelUpRow("prof_lev_up.txt", 1, row));
    }

    [Theory]
    [InlineData("571@@16051@@5@@62600@@71200@@0@@0@@2600")]
    [InlineData("571@@16051@@x@@62600@@71200@@0@@0@@2600@@")]
    public void ProfessionBenefitParsersRejectWrongFieldCountsAndNonNumericRows(string row)
    {
        Assert.Throws<FormatException>(() => ProfessionParser.ParseLevelBenefitRow("prof_lev_benefit.txt", 1, row));
        Assert.Throws<FormatException>(() => ProfessionParser.ParseTitleBenefitRow("prof_title_benefit.txt", 1, row));
    }

    [Fact]
    public void StageGoalParserReadsStagesTasksAndRewards()
    {
        var catalog = StageGoalParser.Parse(
            """
            [StageGoal]
            // comment lines are part of the live file
            StageAmount=1

            [1]
            OpenLev=0001
            StageAni=Stage_01goalBtn
            GoalNum=2
            Prize1=3005107@@1@@0

            [1-1]
            Title=[Novice] Level-up
            Intro=Upgrade to Level 20+.
            Progress=20
            Prize1=723700@@1@@1
            TaskType=1

            [1-2]
            Title=[Novice] Obtain Equipment
            Intro=A set of Refined+ equipment (6 pieces)
            Progress=6
            Prize1=1088001@@1@@1
            TaskType=5
            Require=6
            """);

        Assert.Equal(1, catalog.Info.StageAmount);
        var stage = Assert.Single(catalog.Stages);
        Assert.Equal(1, stage.StageId);
        Assert.Equal("0001", stage.OpenLevelRaw);
        Assert.Equal(1, stage.OpenLevel);
        Assert.Equal("Stage_01goalBtn", stage.StageAnimation);
        Assert.Equal(2, stage.GoalCount);
        Assert.Equal(3005107, stage.Rewards.Single().ItemTypeId);
        Assert.Equal(0, stage.Rewards.Single().BoundFlag);

        Assert.Equal(2, catalog.Tasks.Count);
        Assert.Equal("[Novice] Level-up", catalog.Tasks[0].Title);
        Assert.Null(catalog.Tasks[0].RequireRaw);
        Assert.Equal(723700, catalog.Tasks[0].Rewards.Single().ItemTypeId);
        Assert.Equal(6, catalog.Tasks[1].Require);
        Assert.Equal(["1088001", "1", "1"], catalog.Tasks[1].Rewards.Single().Fields);
    }

    [Fact]
    public void StageGoalParserRejectsDeclaredGoalCountMismatch()
    {
        var ex = Assert.Throws<FormatException>(() => StageGoalParser.Parse(
            """
            [StageGoal]
            StageAmount=1

            [1]
            OpenLev=0001
            StageAni=Stage_01goalBtn
            GoalNum=2
            Prize1=3005107@@1@@0

            [1-1]
            Title=Only one
            Intro=Only one task
            Progress=1
            Prize1=723700@@1@@1
            TaskType=1
            """));

        Assert.Contains("declares 2 goals", ex.Message);
    }

    [Fact]
    public void ItemTypeParserReadsIdNameAndAllFields()
    {
        var line = "101000@@MysticWindrobe@@16000@@Windrobe@@None@@";

        var item = ItemTypeParser.ParseLine(line, expectedFieldCount: 6);

        Assert.Equal(101000, item.Id);
        Assert.Equal("MysticWindrobe", item.Name);
        Assert.Equal(6, item.Fields.Count);
    }

    [Fact]
    public void ItemTypeSubParserReadsItemTypeIdAndPreservesAllFields()
    {
        var entry = ItemTypeSubParser.ParseRow(
            "ItemtypeSub.txt",
            7,
            "1000000@@7@@Stancher@@0@@");

        Assert.Equal(1_000_000, entry.ItemTypeId);
        Assert.Equal("ItemtypeSub.txt", entry.SourceFile);
        Assert.Equal(7, entry.LineNumber);
        Assert.Equal("1000000@@7@@Stancher@@0@@", entry.RawLine);
        Assert.Equal(["1000000", "7", "Stancher", "0", ""], entry.Fields);
    }

    [Fact]
    public void ItemTypeSubParserLoadsEmptyLegacyFileAsEmptyList()
    {
        var entries = ItemTypeSubParser.Parse("", "ItemtypeSub.txt");

        Assert.Empty(entries);
    }

    [Fact]
    public void ItemTypeSubParserRejectsRowsWithoutNumericItemTypeId()
    {
        var ex = Assert.Throws<FormatException>(
            () => ItemTypeSubParser.ParseRow("ItemtypeSub.txt", 1, "not-an-id@@7@@"));

        Assert.Contains("Invalid item type sub item type id", ex.Message);
    }

    [Fact]
    public void ItemTypeGoldWorthReadsLegacyFieldAndDefaultsToZero()
    {
        var item = ItemTypeParser.ParseLine("101000@@MysticWindrobe@@0@@0@@0@@0@@0@@0@@0@@0@@0@@7000@@");
        var missing = ItemTypeParser.ParseLine("1@@Short@@");

        Assert.Equal(7000u, item.GoldWorth);
        Assert.Equal("7000", item.Fields[ItemType.GoldWorthFieldIndex]);
        Assert.Equal(0u, missing.GoldWorth);
    }

    [Fact]
    public void ItemTypeRequiredClassReadsLegacyFieldAndDefaultsToZero()
    {
        var item = ItemTypeParser.ParseLine("101000@@MysticWindrobe@@16000@@0@@15@@0@@0@@0@@");
        var missing = ItemTypeParser.ParseLine("1@@Short@@");

        Assert.Equal(16000u, item.RequiredClass);
        Assert.Equal("16000", item.Fields[ItemType.RequiredClassFieldIndex]);
        Assert.Equal(0u, missing.RequiredClass);
    }

    [Fact]
    public void ItemTypeRequiredProficiencyReadsLegacyFieldAndDefaultsToZero()
    {
        var item = ItemTypeParser.ParseLine("410023@@DemonBlade@@0@@7@@15@@0@@28@@12@@");
        var missing = ItemTypeParser.ParseLine("1@@Short@@");

        Assert.Equal(7u, item.RequiredProficiency);
        Assert.Equal("7", item.Fields[ItemType.RequiredProficiencyFieldIndex]);
        Assert.Equal(0u, missing.RequiredProficiency);
    }

    [Fact]
    public void ItemTypeRequiredLevelReadsLegacyFieldAndDefaultsToZero()
    {
        var item = ItemTypeParser.ParseLine("101000@@MysticWindrobe@@16000@@0@@15@@0@@");
        var missing = ItemTypeParser.ParseLine("1@@Short@@");

        Assert.Equal(15u, item.RequiredLevel);
        Assert.Equal("15", item.Fields[ItemType.RequiredLevelFieldIndex]);
        Assert.Equal(0u, missing.RequiredLevel);
    }

    [Fact]
    public void ItemTypeRequiredGenderReadsLegacyFieldAndDefaultsToZero()
    {
        var item = ItemTypeParser.ParseLine("150320@@LoveForever@@0@@0@@20@@2@@0@@0@@");
        var missing = ItemTypeParser.ParseLine("1@@Short@@");

        Assert.Equal(2u, item.RequiredGender);
        Assert.Equal("2", item.Fields[ItemType.RequiredGenderFieldIndex]);
        Assert.Equal(0u, missing.RequiredGender);
    }

    [Fact]
    public void ItemTypeRequiredStatsReadLegacyFieldsAndDefaultToZero()
    {
        var item = ItemTypeParser.ParseLine("410023@@DemonBlade@@0@@0@@15@@0@@28@@12@@");
        var missing = ItemTypeParser.ParseLine("1@@Short@@");

        Assert.Equal(28u, item.RequiredStrength);
        Assert.Equal("28", item.Fields[ItemType.RequiredStrengthFieldIndex]);
        Assert.Equal(12u, item.RequiredAgility);
        Assert.Equal("12", item.Fields[ItemType.RequiredAgilityFieldIndex]);
        Assert.Equal(0u, missing.RequiredStrength);
        Assert.Equal(0u, missing.RequiredAgility);
    }

    [Fact]
    public void ItemTypeFrequencyReadsLegacyAttackSpeedFieldAndDefaultsToZero()
    {
        var fields = Enumerable.Repeat("0", ItemType.FrequencyFieldIndex + 1).ToArray();
        fields[0] = "410023";
        fields[1] = "DemonBlade";
        fields[ItemType.FrequencyFieldIndex] = "45";
        var item = ItemTypeParser.ParseLine(string.Join("@@", fields));
        var missing = ItemTypeParser.ParseLine("1@@Short@@");

        Assert.Equal(45u, item.Frequency);
        Assert.Equal("45", item.Fields[ItemType.FrequencyFieldIndex]);
        Assert.Equal(0u, missing.Frequency);
    }

    [Fact]
    public void ItemTypeBuyWorthAndDurabilityReadLegacyFieldsAndDefaultToZero()
    {
        var item = ItemTypeParser.ParseLine("1000000@@Stancher@@2000@@0@@1@@0@@0@@0@@0@@0@@0@@5@@0@@0@@0@@0@@0@@0@@0@@0@@3599@@1@@0@@0@@0@@0@@0@@0@@0@@0@@8@@1@@1000@@0@@0@@0@@3@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Stancher@@None@@5@@0@@0@@0@@3090@@0@@0@@0@@0@@0@@0@@", expectedFieldCount: 66);
        var missing = ItemTypeParser.ParseLine("1@@Short@@");

        Assert.Equal(1u, item.LegacyDurability);
        Assert.Equal("1", item.Fields[ItemType.DurabilityFieldIndex]);
        Assert.Equal(3u, item.ConquerPointsWorth);
        Assert.Equal("3", item.Fields[ItemType.ConquerPointsWorthFieldIndex]);
        Assert.Equal(0u, missing.LegacyDurability);
        Assert.Equal(0u, missing.ConquerPointsWorth);
    }

    [Fact]
    public void ItemTypeParserReadsRuneSpellIdFromLiveRuneField()
    {
        var line = "4036205@@Consolidation(+5)@@0@@0@@0@@0@@0@@0@@0@@0@@8@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1@@0@@0@@0@@4036205@@1@@0@@0@@0@@0@@1@@800@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Rare Yellow Rune@@Equip the Rare Yellow Rune to get the skill Consolidation. Passive: There is a 55% chance to be immune to the Stamina Reduction effect brought by other skills like Frost Gaze, Water Release and Caprice. The trigger chance will be increased to 60% for next level.@@0@@0@@0@@0@@16010@@0@@0@@0@@0@@23@@0@@";

        var item = ItemTypeParser.ParseLine(line, expectedFieldCount: 66);

        Assert.Equal(4036205u, item.RuneSpellId);
        Assert.Equal("4036205", item.Fields[ItemType.RuneSpellIdFieldIndex]);
    }

    [Fact]
    public void ItemTypeRuneSpellIdDefaultsToZeroForNormalOrMissingFields()
    {
        var normal = ItemTypeParser.ParseLine("101000@@MysticWindrobe@@16000@@0@@15@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@23@@0@@0@@0@@0@@3599@@3599@@0@@0@@0@@0@@0@@0@@0@@0@@8@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Windrobe@@None@@5@@0@@0@@0@@3090@@0@@0@@0@@0@@0@@0@@", expectedFieldCount: 66);
        var missing = ItemTypeParser.ParseLine("1@@Short@@");

        Assert.Equal(0u, normal.RuneSpellId);
        Assert.Equal("0", normal.Fields[ItemType.RuneSpellIdFieldIndex]);
        Assert.Equal(0u, missing.RuneSpellId);
    }

    [Theory]
    [InlineData(111003, 1)]
    [InlineData(120001, 2)]
    [InlineData(130003, 3)]
    [InlineData(500109, 4)]
    [InlineData(900109, 5)]
    [InlineData(150000, 6)]
    [InlineData(2100025, 7)]
    [InlineData(160013, 8)]
    [InlineData(181305, 9)]
    [InlineData(201003, 10)]
    [InlineData(202003, 11)]
    [InlineData(300000, 12)]
    [InlineData(4100001, 13)]
    [InlineData(360001, 15)]
    [InlineData(370001, 16)]
    [InlineData(200731, 17)]
    [InlineData(203003, 18)]
    [InlineData(204003, 19)]
    [InlineData(4010001, 101)]
    public void ItemTypeLegacyEquipmentPositionMatchesLegacyIdFamilies(uint itemId, ushort expectedPosition)
    {
        Assert.Equal(expectedPosition, ItemType.GetLegacyEquipmentPosition(itemId));
    }

    [Theory]
    [InlineData(899999, false)]
    [InlineData(900000, true)]
    [InlineData(900109, true)]
    [InlineData(900309, true)]
    [InlineData(900310, false)]
    public void ItemTypeLegacyShieldMatchesLegacyRange(uint itemId, bool expected)
    {
        Assert.Equal(expected, ItemType.IsLegacyShield(itemId));
    }

    [Theory]
    [InlineData(612999, false)]
    [InlineData(613000, true)]
    [InlineData(613214, true)]
    [InlineData(613429, true)]
    [InlineData(613430, false)]
    public void ItemTypeLegacyKnifeMatchesLegacyRange(uint itemId, bool expected)
    {
        Assert.Equal(expected, ItemType.IsLegacyKnife(itemId));
    }

    [Theory]
    [InlineData(610999, false, false)]
    [InlineData(611000, false, true)]
    [InlineData(611219, false, true)]
    [InlineData(611439, false, true)]
    [InlineData(611440, false, false)]
    [InlineData(611999, false, false)]
    [InlineData(612000, true, false)]
    [InlineData(612219, true, false)]
    [InlineData(612439, true, false)]
    [InlineData(612440, false, false)]
    public void ItemTypeLegacyPistolAndRapierRanges(uint itemId, bool expectedPistol, bool expectedRapier)
    {
        Assert.Equal(expectedPistol, ItemType.IsLegacyPistol(itemId));
        Assert.Equal(expectedRapier, ItemType.IsLegacyRapier(itemId));
    }

    [Theory]
    [InlineData(490002, false)]
    [InlineData(490003, true)]
    [InlineData(490076, true)]
    [InlineData(490149, true)]
    [InlineData(490150, false)]
    public void ItemTypeLegacyDaggerMatchesLegacyRange(uint itemId, bool expected)
    {
        Assert.Equal(expected, ItemType.IsLegacyDagger(itemId));
    }

    [Theory]
    [InlineData(421002, false)]
    [InlineData(421003, true)]
    [InlineData(421221, true)]
    [InlineData(421439, true)]
    [InlineData(421440, false)]
    public void ItemTypeLegacyBackswordMatchesLegacyRange(uint itemId, bool expected)
    {
        Assert.Equal(expected, ItemType.IsLegacyBacksword(itemId));
    }

    [Theory]
    [InlineData(1049999, false)]
    [InlineData(1050000, true)]
    [InlineData(1050500, true)]
    [InlineData(1051000, true)]
    [InlineData(1051001, false)]
    public void ItemTypeLegacyArrowMatchesLegacyRange(uint itemId, bool expected)
    {
        Assert.Equal(expected, ItemType.IsLegacyArrow(itemId));
    }

    [Theory]
    [InlineData(618999, false)]
    [InlineData(619000, true)]
    [InlineData(619219, true)]
    [InlineData(619439, true)]
    [InlineData(619440, false)]
    public void ItemTypeLegacyHossuMatchesLegacyRange(uint itemId, bool expected)
    {
        Assert.Equal(expected, ItemType.IsLegacyHossu(itemId));
    }

    [Theory]
    [InlineData(499999, false)]
    [InlineData(500000, true)]
    [InlineData(500109, true)]
    [InlineData(500999, true)]
    [InlineData(501000, false)]
    public void ItemTypeLegacyBowMatchesEquipArrowGuardFamily(uint itemId, bool expected)
    {
        Assert.Equal(expected, ItemType.IsLegacyBow(itemId));
    }

    [Theory]
    [InlineData(500109, true)]
    [InlineData(421003, true)]
    [InlineData(410003, false)]
    [InlineData(900109, false)]
    public void ItemTypeLegacyTwoHandWeaponMatchesLegacyFamilies(uint itemId, bool expected)
    {
        Assert.Equal(expected, ItemType.IsLegacyTwoHandWeapon(itemId));
    }

    [Fact]
    public void ItemAddParserReadsLevelAndEightRawValues()
    {
        var bonus = ItemAddParser.ParseLine("101000 2 0 0 0 4 0 2 0 0", sourceLine: 37);

        Assert.Equal(101000, bonus.ItemTypeId);
        Assert.Equal(2, bonus.Level);
        Assert.Equal([0, 0, 0, 4, 0, 2, 0, 0], bonus.Values);
        Assert.Equal(37, bonus.SourceLine);
        Assert.Equal("101000 2 0 0 0 4 0 2 0 0", bonus.RawLine);
    }

    [Theory]
    [InlineData("101000 2 0 0 0 4 0 2 0")]
    [InlineData("101000 2 0 0 0 nope 0 2 0 0")]
    [InlineData("101000 13 0 0 0 4 0 2 0 0")]
    public void ItemAddParserRejectsWrongFieldCountsAndNonNumericRows(string line)
    {
        Assert.Throws<FormatException>(() => ItemAddParser.ParseLine(line, sourceLine: 1));
    }

    [Fact]
    public void LevelExperienceParserReadsLargeExperienceRows()
    {
        var entry = LevelExperienceParser.ParseLine("0 139 2641561501496 3925301 600", sourceLine: 139);

        Assert.Equal(0, entry.Type);
        Assert.Equal(139, entry.Level);
        Assert.Equal(2_641_561_501_496, entry.Experience);
        Assert.Equal(3_925_301, entry.UpLevTime);
        Assert.Equal(600, entry.MentorUpLevTime);
        Assert.Equal(139, entry.SourceLine);
        Assert.Equal(["0", "139", "2641561501496", "3925301", "600"], entry.Fields);
        Assert.Equal("0 139 2641561501496 3925301 600", entry.RawLine);
    }

    [Theory]
    [InlineData("0 139 2641561501496 3925301")]
    [InlineData("0 139 nope 3925301 600")]
    public void LevelExperienceParserRejectsWrongFieldCountsAndNonNumericRows(string line)
    {
        Assert.Throws<FormatException>(() => LevelExperienceParser.ParseLine(line, sourceLine: 1));
    }

    [Fact]
    public void TransformInfoParserReadsFixedWhitespaceRows()
    {
        var entry = TransformInfoParser.ParseLine(
            "TransformInfo.txt",
            4,
            "1350 3 DivineHare 267 3000");

        Assert.Equal(1350, entry.TransformId);
        Assert.Equal(3, entry.Level);
        Assert.Equal("DivineHare", entry.Name);
        Assert.Equal(267, entry.Lookface);
        Assert.Equal(3000, entry.MaxLife);
        Assert.Equal("TransformInfo.txt", entry.SourceFile);
        Assert.Equal(4, entry.LineNumber);
        Assert.Equal(["1350", "3", "DivineHare", "267", "3000"], entry.Fields);
        Assert.Equal("1350 3 DivineHare 267 3000", entry.RawLine);
    }

    [Theory]
    [InlineData("1350 3 DivineHare 267")]
    [InlineData("1350 3 DivineHare 267 3000 extra")]
    [InlineData("x 3 DivineHare 267 3000")]
    [InlineData("1350 x DivineHare 267 3000")]
    [InlineData("1350 3 DivineHare nope 3000")]
    [InlineData("1350 3 DivineHare 267 nope")]
    public void TransformInfoParserRejectsWrongFieldCountsAndNonNumericRows(string line)
    {
        Assert.Throws<FormatException>(() => TransformInfoParser.ParseLine("TransformInfo.txt", 1, line));
    }

    [Fact]
    public void ClassLevelStatParserReadsStatsRows()
    {
        var stats = ClassLevelStatParser.Parse(
            """
            [Stats]
            WindWalker[1]=5,3,2,0
            Thunderstriker[120]=91,144,132,0
            """);

        Assert.Collection(
            stats,
            stat =>
            {
                Assert.Equal("WindWalker", stat.ClassName);
                Assert.Equal(1, stat.Level);
                Assert.Equal(5, stat.Strength);
                Assert.Equal(2, stat.Agility);
                Assert.Equal(3, stat.Vitality);
                Assert.Equal(0, stat.Spirit);
                Assert.Equal("WindWalker[1]", stat.RawKey);
                Assert.Equal(["5", "3", "2", "0"], stat.Fields);
            },
            stat =>
            {
                Assert.Equal("Thunderstriker", stat.ClassName);
                Assert.Equal(120, stat.Level);
                Assert.Equal(132, stat.Agility);
                Assert.Equal(144, stat.Vitality);
                Assert.Equal("Stats.ini", stat.SourceFile);
                Assert.Equal(3, stat.LineNumber);
                Assert.Equal("Thunderstriker[120]=91,144,132,0", stat.RawLine);
            });
    }

    [Theory]
    [InlineData("[Stats]\nWindWalker=5,3,2,0")]
    [InlineData("[Stats]\nWindWalker[x]=5,3,2,0")]
    [InlineData("[Stats]\nWindWalker[1]=5,3,2")]
    [InlineData("[Stats]\nWindWalker[1]=5,nope,2,0")]
    [InlineData("[Other]\nWindWalker[1]=5,3,2,0")]
    public void ClassLevelStatParserRejectsInvalidRows(string content)
    {
        Assert.Throws<FormatException>(() => ClassLevelStatParser.Parse(content));
    }

    [Fact]
    public void QuestInfoParserPreservesDuplicateKeysAndParsesMissionDetails()
    {
        const string content = """
        [TaskType]
        Num=1
        TypeId1=1
        TypeName1=Recruit Quest
        TaskNameColor=0xFFCFC058

        [ActionBase]
        ActionBase=20000000

        [Option]
        RecommendInterval=300

        [TotalMission]
        TotalMission=1

        [1]
        TypeId=1
        CompleteFlag=2
        ActivityType=0
        MissionId=650
        Name=The Armorer
        Lv_min=8
        Lv_max=50
        Auto=0
        First=1
        Prequest=608|609
        Map=1002
        Profession=1000,1001
        Sex=0
        FinishTime=0
        ActivityBeginTime=0
        ActivityEndTime=0
        ActivityType=1
        BeginNpcId=2001,1002,439,464,TCCaptain,TwinCity
        BeginNpcId_4=27364,1002,339,461,MaXiaoyun,TwinCity
        FinishNpcId=0004,1002,413,303,Armorer,TwinCity
        Prize=EXP: 1000 [item 130006,0,0,0,3,0,0][item 118006,0,0,0,3,0,0]
        IntentionDesp=Speak with the Armorer.
        IntentAmount=1
        Intention1=Pay a visit to the [npc Armorer,0004,1002,413,303]
        Intention1_4=Variant visit.
        Content=Have a word with the Armorer.
        """;

        var catalog = QuestInfoParser.Parse(content);

        Assert.Equal(20_000_000, catalog.Metadata.ActionBase);
        Assert.Equal(300, catalog.Metadata.RecommendInterval);
        Assert.Equal(1, catalog.Metadata.TotalMission);
        var taskType = Assert.Single(catalog.TaskTypes);
        Assert.Equal(1, taskType.Id);
        Assert.Equal("Recruit Quest", taskType.Name);

        var mission = Assert.Single(catalog.Missions);
        Assert.Equal(1, mission.SectionId);
        Assert.Equal(650, mission.MissionId);
        Assert.Equal([608, 609], mission.PrerequisiteMissionIds);
        Assert.Equal("The Armorer", mission.Name);
        Assert.Equal(1002, mission.MapId);
        Assert.Equal(2, mission.Entries.Count(entry => entry.Key == "ActivityType"));

        Assert.Collection(
            mission.BeginNpcAnchors,
            anchor =>
            {
                Assert.Null(anchor.Variant);
                Assert.Equal(2001, anchor.NpcId);
                Assert.Equal("TCCaptain", anchor.Name);
            },
            anchor =>
            {
                Assert.Equal(4, anchor.Variant);
                Assert.Equal(27364, anchor.NpcId);
                Assert.Equal("BeginNpcId_4", anchor.Key);
            });

        Assert.Equal(2, mission.PrizeItems.Count);
        Assert.Equal(130006, mission.PrizeItems[0].ItemTypeId);
        Assert.Collection(
            mission.Intentions,
            intention =>
            {
                Assert.Null(intention.Variant);
                Assert.Equal("Pay a visit to the [npc Armorer,0004,1002,413,303]", intention.Text);
            },
            intention =>
            {
                Assert.Equal(4, intention.Variant);
                Assert.Equal("Variant visit.", intention.Text);
            });
    }

    [Fact]
    public void QuestInfoParserKeepsCommaContainingAnchorMapNames()
    {
        const string content = """
        [902]
        TypeId=1
        MissionId=2365
        BeginNpcId=23851,1002,376,451,YeXiu,TwinCity(Eu_EndeavorMarket262,271)
        FinishNpcId=23851,1002,376,451,YeXiu,TwinCity(Eu_EndeavorMarket262,271)
        """;

        var mission = Assert.Single(QuestInfoParser.Parse(content).Missions);

        Assert.Equal("TwinCity(Eu_EndeavorMarket262,271)", Assert.Single(mission.BeginNpcAnchors).MapName);
        Assert.Equal(6, Assert.Single(mission.BeginNpcAnchors).Fields.Count);
    }

    [Fact]
    public void MagicTypeParserReadsIdTypeAndName()
    {
        var line = "10@@10@@1@@HuntingPunch@@1@@0@@";

        var magic = MagicTypeParser.ParseLine(line, expectedFieldCount: 7);

        Assert.Equal(10, magic.Id);
        Assert.Equal(10, magic.TypeId);
        Assert.Equal("HuntingPunch", magic.Name);
    }

    [Fact]
    public void MagicEffectParserPreservesDuplicateSectionsKeysAndLongIds()
    {
        var effects = MagicEffectParser.Parse(
            """
            // live file has one whole-line script comment
            [100000]
            Name=Thunder
            Status=5
            Status=145
            Desc=Upgradable
            DescEx=Magic~attack

            [100000]
            Desc=Upgrade~after~lvl~10
            DescEx=DescEx=Value~with~equals

            [5002142000]
            Name=TenDigit
            Desc=
            DescEx=Long~id
            """);

        Assert.Equal(3, effects.Count);
        Assert.Equal(100000, effects[0].Id);
        Assert.Equal(1, effects[0].Occurrence);
        Assert.Equal("Thunder", effects[0].Name);
        Assert.Equal("145", effects[0].Attributes["status"]);
        Assert.Equal(2, effects[0].Entries.Count(entry => entry.Key == "Status"));
        Assert.Equal(2, effects[1].Occurrence);
        Assert.Equal("DescEx=Value~with~equals", effects[1].DescriptionEx);
        Assert.Equal(5_002_142_000, effects[2].Id);
        Assert.Equal("", effects[2].Description);
        Assert.Equal("5002142000", effects[2].SourceSection);
    }

    [Fact]
    public void NpcParserAcceptsSixOrSevenColumns()
    {
        var npc = NpcParser.ParseLine("200124,2,45310,44462,354,286,1");

        Assert.Equal(200124, npc.Id);
        Assert.Equal(44462, npc.MapId);
        Assert.Equal(354, npc.X);
        Assert.Equal(286, npc.Y);
        Assert.Equal(1, npc.OptionalFlag);
        Assert.Equal("200124,2,45310,44462,354,286,1", npc.RawLine);
        Assert.Equal(["200124", "2", "45310", "44462", "354", "286", "1"], npc.Fields);

        var withoutFlag = NpcParser.ParseLine("200002,2,51700,44460,334,193");
        Assert.Null(withoutFlag.OptionalFlag);
    }

    [Fact]
    public void SobNpcParserAcceptsElevenOrTwelveColumnsAndPreservesRawFields()
    {
        var sobNpc = SobNpcParser.ParseLine("01021,Awakening,2,58460,11724,44,29,0,0,1,11,Archive", sourceLine: 5);

        Assert.Equal(1021, sobNpc.Id);
        Assert.Equal("Awakening", sobNpc.Name);
        Assert.Equal(2, sobNpc.Kind);
        Assert.Equal(58460, sobNpc.Lookface);
        Assert.Equal(11724, sobNpc.MapId);
        Assert.Equal(44, sobNpc.X);
        Assert.Equal(29, sobNpc.Y);
        Assert.Equal(0, sobNpc.Life);
        Assert.Equal(0, sobNpc.MaxLife);
        Assert.Equal(1, sobNpc.Unknown9);
        Assert.Equal(11, sobNpc.Unknown10);
        Assert.Equal("Archive", sobNpc.LocalizedName);
        Assert.Equal(5, sobNpc.SourceLine);
        Assert.Equal("01021", sobNpc.Fields[0]);
        Assert.Equal("01021,Awakening,2,58460,11724,44,29,0,0,1,11,Archive", sobNpc.RawLine);

        var withoutLocalizedName = SobNpcParser.ParseLine("25415,HosuPerfc,2,61260,5000,63,46,0,0,1,11");
        Assert.Null(withoutLocalizedName.LocalizedName);
    }

    [Fact]
    public void SobNpcParserPreservesEmptyLocalizedFieldWhenColumnExists()
    {
        var sobNpc = SobNpcParser.ParseLine("25005,Gate,21,0,3983,85,70,0,0,1,0,");

        Assert.Equal("", sobNpc.LocalizedName);
        Assert.Equal("", sobNpc.Fields[^1]);
        Assert.Equal(12, sobNpc.Fields.Count);
    }

    [Fact]
    public void SobNpcParserAcceptsBlankUnknown10Column()
    {
        var sobNpc = SobNpcParser.ParseLine("6505,Emperrors,10,1137,10702,119,120,50000000,50000000,17,");

        Assert.Null(sobNpc.Unknown10);
        Assert.Equal("", sobNpc.Fields[10]);
        Assert.Equal(11, sobNpc.Fields.Count);
    }

    [Theory]
    [InlineData("1,Name,2,3,4,5,6,7,8,9")]
    [InlineData("1,Name,kind,3,4,5,6,7,8,9,10")]
    public void SobNpcParserRejectsWrongFieldCountsAndNonNumericRows(string line)
    {
        Assert.Throws<FormatException>(() => SobNpcParser.ParseLine(line));
    }

    [Fact]
    public void PortalParserReadsPairedEntries()
    {
        const string ini = """
        [1000]
        Count=1
        PortalEnter0=1000 77 319
        PortalExit0=1001 312 646
        """;

        var portals = PortalParser.Parse(IniDocument.Parse(ini));

        var portal = Assert.Single(portals);
        Assert.Equal(1000, portal.SourceMapId);
        Assert.Equal(77, portal.SourceX);
        Assert.Equal(319, portal.SourceY);
        Assert.Equal(1001, portal.TargetMapId);
        Assert.Equal(312, portal.TargetX);
        Assert.Equal(646, portal.TargetY);
    }

    [Fact]
    public void PortalParserMergesDuplicateSectionsAndAcceptsLegacyExitTypo()
    {
        const string ini = """
        [1999]
        Count=1
        PortalEnter0=1999 331 442
        PortalExt0=1999 397 551

        [1999]
        Count=1
        """;

        var portal = Assert.Single(PortalParser.Parse(IniDocument.Parse(ini)));

        Assert.Equal(1999, portal.TargetMapId);
        Assert.Equal(397, portal.TargetX);
        Assert.Equal(551, portal.TargetY);
    }

    [Fact]
    public void PortalParserAcceptsSingleEntryOffByOneExit()
    {
        const string ini = """
        [1700]
        Count=1
        PortalEnter0=1700 595 640
        PortalExit1=1036 215 215
        """;

        var portal = Assert.Single(PortalParser.Parse(IniDocument.Parse(ini)));

        Assert.Equal(1036, portal.TargetMapId);
        Assert.Equal(215, portal.TargetX);
        Assert.Equal(215, portal.TargetY);
    }

    [Fact]
    public void MonsterParserPreservesAttributesAndSpecialDrops()
    {
        const string ini = """
        [cq_monstertype]
        id=1
        name=AgileRat
        level=2
        drop_money=0010

        [SpecialDrop]
        Count=1
        0=721538,4
        """;

        var monster = MonsterParser.Parse(IniDocument.Parse(ini));

        Assert.Equal(1, monster.Id);
        Assert.Equal("AgileRat", monster.Name);
        Assert.Equal("0010", monster.Attributes["drop_money"]);

        var drop = Assert.Single(monster.SpecialDrops);
        Assert.Equal(721538, drop.ItemTypeId);
        Assert.Equal(4, drop.ChanceOrCount);
    }

    [Fact]
    public void MonsterParserAppliesLegacyHighAttackHalvingForNonBossFamilies()
    {
        const string ini = """
        [cq_monstertype]
        id=2
        name=HardHitter
        attack_min=7001
        attack_max=9001
        Boss=0
        """;

        var monster = MonsterParser.Parse(IniDocument.Parse(ini));

        Assert.Equal("3500", monster.Attributes["attack_min"]);
        Assert.Equal("4500", monster.Attributes["attack_max"]);
    }

    [Fact]
    public void MonsterParserDoesNotHalveLegacyBossFamilies()
    {
        const string ini = """
        [cq_monstertype]
        id=3
        name=BossHitter
        attack_min=7001
        attack_max=9001
        Boss=1
        """;

        var monster = MonsterParser.Parse(IniDocument.Parse(ini));

        Assert.Equal("7001", monster.Attributes["attack_min"]);
        Assert.Equal("9001", monster.Attributes["attack_max"]);
    }

    [Fact]
    public void MonsterParserDoesNotHalveLegacyNormalAttackFamilies()
    {
        const string ini = """
        [cq_monstertype]
        id=4
        name=NormalHitter
        attack_min=2000
        attack_max=3000
        Boss=0
        """;

        var monster = MonsterParser.Parse(IniDocument.Parse(ini));

        Assert.Equal("2000", monster.Attributes["attack_min"]);
        Assert.Equal("3000", monster.Attributes["attack_max"]);
    }

    [Fact]
    public void MapMetadataParserReadsTypicalInfoRecord()
    {
        const string ini = """
        [info]
        id=1000
        name=Desert
        mapdoc=1000
        type=8192
        portal0_x=499
        portal0_y=650
        reborn_map=1000
        reborn_portal=0
        res_lev=0
        link_map=0
        link_x=0
        link_y=0
        color=4294967295
        """;

        var map = MapMetadataParser.Parse(IniDocument.Parse(ini), "1000.ini");

        Assert.Equal(1000, map.Id);
        Assert.Equal("1000.ini", map.SourceRelativePath);
        Assert.Equal("Desert", map.Name);
        Assert.Equal(1000, map.MapDoc);
        Assert.Equal("8192", map.TypeRaw);
        Assert.Equal(8192, map.Type);
        Assert.Equal(499, map.Portal0X);
        Assert.Equal(650, map.Portal0Y);
        Assert.Equal(4_294_967_295, map.Color);
        Assert.Equal("4294967295", map.ColorRaw);
    }

    [Fact]
    public void MapMetadataParserAllowsMissingNameAndPortal()
    {
        const string ini = """
        [info]
        id=10250
        mapdoc=10250
        type=0
        reborn_map=1002
        reborn_portal=0
        res_lev=0
        link_map=0
        link_x=0
        link_y=0
        color=0
        """;

        var map = MapMetadataParser.Parse(IniDocument.Parse(ini));

        Assert.Null(map.Name);
        Assert.Null(map.Portal0X);
        Assert.Null(map.Portal0Y);
    }

    [Fact]
    public void MapMetadataParserPreservesLargeAndLeadingZeroTypeValues()
    {
        const string ini = """
        [info]
        id=999
        name=HugeFlags
        mapdoc=1002
        type=070368744177678
        reborn_map=1002
        reborn_portal=0
        res_lev=0
        link_map=0
        link_x=0
        link_y=0
        color=0007
        """;

        var map = MapMetadataParser.Parse(IniDocument.Parse(ini));

        Assert.Equal("070368744177678", map.TypeRaw);
        Assert.Equal(70_368_744_177_678, map.Type);
        Assert.Equal("0007", map.ColorRaw);
        Assert.Equal(7, map.Color);
    }

    [Fact]
    public void MapMetadataParserPreservesExtendedOptionalKeys()
    {
        const string ini = """
        [info]
        id=1785
        name=Extended
        mapdoc=1002
        type=2147483648
        portal0_x=1
        portal0_y=2
        reborn_map=1002
        reborn_portal=0
        res_lev=0
        link_map=0
        link_x=0
        link_y=0
        color=4294967295
        describe_text=Event map
        mapgroup=3
        idxserver=1
        weather=2
        bgmusic=7
        bgmusic_show=1
        owner_type=4
        del_flag=0
        """;

        var map = MapMetadataParser.Parse(IniDocument.Parse(ini));

        Assert.Equal(2_147_483_648, map.Type);
        Assert.Equal("Event map", map.Attributes["describe_text"]);
        Assert.Equal("3", map.Attributes["mapgroup"]);
        Assert.Equal("1", map.Attributes["bgmusic_show"]);
        Assert.Equal("0", map.Attributes["del_flag"]);
    }

    [Fact]
    public void MapMetadataParserRejectsPartialPortalCoordinates()
    {
        const string ini = """
        [info]
        id=1000
        mapdoc=1000
        type=8192
        portal0_x=499
        reborn_map=1000
        reborn_portal=0
        res_lev=0
        link_map=0
        link_x=0
        link_y=0
        color=4294967295
        """;

        Assert.Throws<FormatException>(() => MapMetadataParser.Parse(IniDocument.Parse(ini)));
    }

    [Fact]
    public void MapNameParserPreservesGameMapExRows()
    {
        var rows = MapNameParser.Parse(
            "[5002]\r\n" +
            "Name=\u9677\u9631\r\n" +
            "\r\n" +
            "[3845]\r\n" +
            "Name=VIP\u7279\u6743\u5ea6\u5047\u6751",
            "GameMapEx.ini");

        Assert.Equal(2, rows.Count);
        Assert.Equal(5002, rows[0].MapId);
        Assert.Equal("\u9677\u9631", rows[0].Name);
        Assert.Equal("GameMapEx.ini", rows[0].SourceFile);
        Assert.Equal(1, rows[0].SectionLine);
        Assert.Equal(2, rows[0].NameLine);
        Assert.Equal("5002", rows[0].SourceSection);
        Assert.Equal("Name=\u9677\u9631", rows[0].RawNameLine);
        Assert.Equal(3845, rows[1].MapId);
        Assert.Equal("VIP\u7279\u6743\u5ea6\u5047\u6751", rows[1].Name);
        Assert.Equal("GameMapEx.ini", rows[1].SourceFile);
        Assert.Equal(4, rows[1].SectionLine);
        Assert.Equal(5, rows[1].NameLine);
        Assert.Equal("3845", rows[1].SourceSection);
        Assert.Equal("Name=VIP\u7279\u6743\u5ea6\u5047\u6751", rows[1].RawNameLine);
    }

    [Fact]
    public void MapNameParserReadsCp936Files()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var path = Path.Combine(Path.GetTempPath(), "GameMapEx-" + Guid.NewGuid() + ".ini");
        File.WriteAllBytes(
            path,
            Encoding.GetEncoding(936).GetBytes("[5002]\r\nName=\u9677\u9631\r\n"));

        try
        {
            var row = Assert.Single(MapNameParser.ParseFile(path));

            Assert.Equal(5002, row.MapId);
            Assert.Equal("\u9677\u9631", row.Name);
            Assert.Equal(Path.GetFileName(path), row.SourceFile);
            Assert.Equal("Name=\u9677\u9631", row.RawNameLine);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MapNameParserMergesFilesWithFirstSourceWinningDuplicateIds()
    {
        var dbServerPath = Path.Combine(Path.GetTempPath(), "db-GameMapEx-" + Guid.NewGuid() + ".ini");
        var clientPath = Path.Combine(Path.GetTempPath(), "client-GameMapEx-" + Guid.NewGuid() + ".ini");
        File.WriteAllText(
            dbServerPath,
            "[1002]\r\nName=DBServerTwinCity\r\n",
            Encoding.ASCII);
        File.WriteAllText(
            clientPath,
            "[1002]\r\nName=ClientTwinCity\r\n\r\n[10653]\r\nName=Dragland\r\n\r\n[10653]\r\nName=DuplicateDragland\r\n",
            Encoding.ASCII);

        try
        {
            var rows = MapNameParser.ParseFiles([dbServerPath, clientPath]);

            Assert.Equal(2, rows.Count);
            Assert.Equal(1002, rows[0].MapId);
            Assert.Equal("DBServerTwinCity", rows[0].Name);
            Assert.Equal(Path.GetFileName(dbServerPath), rows[0].SourceFile);
            Assert.Equal(10653, rows[1].MapId);
            Assert.Equal("Dragland", rows[1].Name);
            Assert.Equal(Path.GetFileName(clientPath), rows[1].SourceFile);
        }
        finally
        {
            File.Delete(dbServerPath);
            File.Delete(clientPath);
        }
    }

    [Fact]
    public void MapNameParserSkipsCommentRows()
    {
        var rows = MapNameParser.Parse(
            "; DBServer map names\r\n" +
            "# kept for compatibility\r\n" +
            "// local note\r\n" +
            "[1000]\r\n" +
            "Name=Desert",
            "GameMapEx.ini");

        Assert.Equal("Desert", Assert.Single(rows).Name);
    }

    [Theory]
    [InlineData("Name=TwinCity")]
    [InlineData("[1000]\r\nTitle=TwinCity")]
    [InlineData("[1000]\r\nName=TwinCity\r\nName=Other")]
    [InlineData("[abc]\r\nName=TwinCity")]
    [InlineData("[1000]\r\n\r\n[1001]\r\nName=TwinCity")]
    public void MapNameParserRejectsMalformedGameMapExRows(string content)
    {
        Assert.Throws<FormatException>(() => MapNameParser.Parse(content, "GameMapEx.ini"));
    }

    [Fact]
    public void MonsterSpawnParserReadsGeneratorFieldsAndPreservesAttributes()
    {
        const string ini = """
        [cq_generator]
        id=0021
        mapid=11127
        bound_x=0300
        bound_y=0300
        bound_cx=0008
        bound_cy=0050
        maxnpc=0015
        rest_secs=0001
        max_per_gen=0030
        npctype=0017
        timer_begin=0000
        timer_end=0000
        born_x=0000
        born_y=0000
        """;

        var spawn = MonsterSpawnParser.Parse(
            IniDocument.Parse(ini),
            sourceMapFolderId: 1000,
            sourceMonsterName: "BladeGhost",
            sourceRelativePath: Path.Combine("1000", "BladeGhost", "1.ini"));

        Assert.Equal(21, spawn.GeneratorId);
        Assert.Equal(1000, spawn.MapId);
        Assert.Equal(17, spawn.MonsterTypeId);
        Assert.Equal(300, spawn.BoundX);
        Assert.Equal(50, spawn.BoundHeight);
        Assert.Equal(15, spawn.MaxNpc);
        Assert.Equal(30, spawn.MaxPerGenerator);
        Assert.Equal(1000, spawn.SourceMapFolderId);
        Assert.Equal("BladeGhost", spawn.SourceMonsterName);
        Assert.Equal("0021", spawn.Attributes["id"]);
        Assert.Equal("11127", spawn.Attributes["mapid"]);
        Assert.Equal("0008", spawn.Attributes["bound_cx"]);
    }

    [Fact]
    public void MonsterSpawnParserPreservesExtraGeneratorKeys()
    {
        const string ini = """
        [cq_generator]
        id=3638
        mapid=1000
        bound_x=171
        bound_y=231
        bound_cx=50
        bound_cy=50
        maxnpc=12
        rest_secs=60
        max_per_gen=10
        npctype=2179
        timer_begin=0
        timer_end=0
        born_x=0
        born_y=0
        mask=0
        path=0
        return_mode=1
        cluster_type=0
        attribute=0
        """;

        var spawn = MonsterSpawnParser.Parse(IniDocument.Parse(ini));

        Assert.Equal(3638, spawn.GeneratorId);
        Assert.Equal("0", spawn.Attributes["mask"]);
        Assert.Equal("0", spawn.Attributes["path"]);
        Assert.Equal("1", spawn.Attributes["return_mode"]);
        Assert.Equal("0", spawn.Attributes["cluster_type"]);
        Assert.Equal("0", spawn.Attributes["attribute"]);
    }

    [Fact]
    public void MonsterSpawnParserRejectsMissingGeneratorSection()
    {
        const string ini = """
        [cq_monstertype]
        id=3968
        name=DarkSoul
        """;

        Assert.Throws<KeyNotFoundException>(() => MonsterSpawnParser.Parse(IniDocument.Parse(ini)));
    }

    [Fact]
    public void MonsterSpawnParserUsesSourceMapFolderAsEffectiveRuntimeMap()
    {
        const string ini = """
        [cq_generator]
        id=1
        mapid=11127
        bound_x=1
        bound_y=2
        bound_cx=3
        bound_cy=4
        maxnpc=5
        rest_secs=6
        max_per_gen=7
        npctype=8
        timer_begin=9
        timer_end=10
        born_x=11
        born_y=12
        """;

        var spawn = MonsterSpawnParser.Parse(IniDocument.Parse(ini), sourceMapFolderId: 1000);

        Assert.Equal(1000, spawn.MapId);
        Assert.Equal(1000, spawn.SourceMapFolderId);
        Assert.Equal("11127", spawn.Attributes["mapid"]);
    }

    [Fact]
    public void MonsterSpawnParserUsesMapIdFieldWhenNoSourceFolderIsProvided()
    {
        const string ini = """
        [cq_generator]
        id=1
        mapid=11127
        bound_x=1
        bound_y=2
        bound_cx=3
        bound_cy=4
        maxnpc=5
        rest_secs=6
        max_per_gen=7
        npctype=8
        timer_begin=9
        timer_end=10
        born_x=11
        born_y=12
        """;

        var spawn = MonsterSpawnParser.Parse(IniDocument.Parse(ini));

        Assert.Equal(11127, spawn.MapId);
        Assert.Null(spawn.SourceMapFolderId);
    }

    [Fact]
    public void MonsterSpawnParserReadsLegacyMapMonsterRowsAndResolvesNames()
    {
        const string content = """
        446664,231,Pheasant,465,477,1002
        455543,231,Pheasant,456,482,1002
        """;
        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pheasant"] = 1
        };

        var spawns = MonsterSpawnParser.ParseLegacyMapMonsterFile(
            content,
            monsterTypeIdsByName,
            "Monsters1002.txt");

        Assert.Equal(2, spawns.Count);
        var spawn = spawns[0];
        Assert.Equal(446664, spawn.GeneratorId);
        Assert.Equal(1002, spawn.MapId);
        Assert.Equal(1, spawn.MonsterTypeId);
        Assert.Equal(465, spawn.BoundX);
        Assert.Equal(477, spawn.BoundY);
        Assert.Equal(1, spawn.BoundWidth);
        Assert.Equal(1, spawn.BoundHeight);
        Assert.Equal(1, spawn.MaxNpc);
        Assert.Equal(0, spawn.RestSeconds);
        Assert.Equal(1, spawn.MaxPerGenerator);
        Assert.Equal("Pheasant", spawn.SourceMonsterName);
        Assert.Equal("Monsters1002.txt", spawn.SourceRelativePath);
        Assert.Equal("231", spawn.Attributes["legacy_lookface"]);
        Assert.Equal("Pheasant", spawn.Attributes["legacy_monster_name"]);
        Assert.Equal("LoadMapMonsters", spawn.Attributes["legacy_source"]);
        Assert.Equal("1", spawn.Attributes["source_line"]);
    }

    [Fact]
    public void MonsterSpawnParserReadsLegacyMyMonstersRows()
    {
        var spawn = MonsterSpawnParser.ParseLegacyMyMonsterLine(
            "7591 1927 324 530 50 50 30 1 25 20010 ",
            "myMonsters.txt",
            sourceLine: 1);

        Assert.Equal(7591, spawn.GeneratorId);
        Assert.Equal(1927, spawn.MapId);
        Assert.Equal(20010, spawn.MonsterTypeId);
        Assert.Equal(324, spawn.BoundX);
        Assert.Equal(530, spawn.BoundY);
        Assert.Equal(50, spawn.BoundWidth);
        Assert.Equal(50, spawn.BoundHeight);
        Assert.Equal(30, spawn.MaxNpc);
        Assert.Equal(1, spawn.RestSeconds);
        Assert.Equal(25, spawn.MaxPerGenerator);
        Assert.Null(spawn.SourceMonsterName);
        Assert.Equal("myMonsters.txt", spawn.SourceRelativePath);
        Assert.Equal("LoadMyMonsters", spawn.Attributes["legacy_source"]);
        Assert.Equal("20010", spawn.Attributes["npctype"]);
    }

    [Fact]
    public void MonsterSpawnParserRejectsUnknownLegacyMapMonsterName()
    {
        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var exception = Assert.Throws<FormatException>(() => MonsterSpawnParser.ParseLegacyMapMonsterLine(
            "446664,231,Pheasant,465,477,1002",
            monsterTypeIdsByName,
            "Monsters1002.txt",
            sourceLine: 1));

        Assert.Contains("Unknown legacy map monster name 'Pheasant'", exception.Message);
    }

    [Fact]
    public void MonsterSpawnParserUsesLegacyExactMonsterNameMatching()
    {
        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["pheasant"] = 1
        };

        var exception = Assert.Throws<FormatException>(() => MonsterSpawnParser.ParseLegacyMapMonsterLine(
            "446664,231,Pheasant,465,477,1002",
            monsterTypeIdsByName,
            "Monsters1002.txt",
            sourceLine: 1));

        Assert.Contains("Unknown legacy map monster name 'Pheasant'", exception.Message);
    }

    [Fact]
    public void MonsterSpawnParserSkipsUnknownLegacyMapMonsterNamesWhenReadingFile()
    {
        const string content = """
        446664,231,ShadowClone,465,477,1002
        455543,231,Pheasant,456,482,1002
        """;
        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pheasant"] = 1
        };

        var spawns = MonsterSpawnParser.ParseLegacyMapMonsterFile(
            content,
            monsterTypeIdsByName,
            "Monsters1002.txt");

        var spawn = Assert.Single(spawns);
        Assert.Equal(455543, spawn.GeneratorId);
        Assert.Equal("Pheasant", spawn.SourceMonsterName);
        Assert.Equal("2", spawn.Attributes["source_line"]);
    }

    [Fact]
    public void MonsterSpawnParserSkipsLegacyTitanMapMonsterRowsWhenReadingFile()
    {
        const string content = """
        500016,153,TitanL77,400,587,1020
        455543,231,Pheasant,456,482,1002
        """;
        var monsterTypeIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pheasant"] = 1,
            ["TitanL77"] = 3134
        };

        var spawns = MonsterSpawnParser.ParseLegacyMapMonsterFile(
            content,
            monsterTypeIdsByName,
            "Monsters1020.txt");

        var spawn = Assert.Single(spawns);
        Assert.Equal(455543, spawn.GeneratorId);
        Assert.Equal("Pheasant", spawn.SourceMonsterName);
        Assert.Equal("2", spawn.Attributes["source_line"]);
    }

    [Fact]
    public void ShopDatParserReadsShopDefinitionsAndItems()
    {
        const string ini = """
        [Header]
        Amount=1

        [Shop0]
        ID=3
        Name=Drugstore
        Type=1
        MoneyType=0
        ItemAmount=2
        Item0=1000000
        Item1=1000010
        """;

        var data = ShopParser.ParseShopDat(ini);

        var shop = Assert.Single(data.Shops);
        Assert.Equal(3, shop.Id);
        Assert.Equal("Drugstore", shop.Name);
        Assert.Equal(1, shop.Type);
        Assert.Equal(0, shop.MoneyType);
        Assert.Equal("2", shop.Attributes["ItemAmount"]);
        Assert.Equal([1000000, 1000010], data.Items.Select(item => item.ItemTypeId).ToArray());
        Assert.All(data.Items, item => Assert.Equal("ShopDat", item.SourceDialect));
    }

    [Fact]
    public void LegacyEmoneyShopParserReadsSectionsStatusesAndDescriptions()
    {
        const string ini = """
        ID=432
        MoneyType=1

        [item]
        Amount=2
        Item0=203009 new
        Item1=720671_hot

        [recommend]
        Amount=1
        Item0=724002 null Open~to~get~3~Small~Lottery~Tickets.
        """;

        var data = ShopParser.ParseLegacyEmoneyShop(ini);

        var shop = Assert.Single(data.Shops);
        Assert.Equal(432, shop.Id);
        Assert.Equal(1, shop.MoneyType);
        Assert.Equal("2", shop.Attributes["item.Amount"]);
        Assert.Equal("1", shop.Attributes["recommend.Amount"]);

        Assert.Collection(
            data.Items,
            item =>
            {
                Assert.Equal("item", item.Category);
                Assert.Equal("new", item.Status);
                Assert.Null(item.Description);
            },
            item =>
            {
                Assert.Equal("item", item.Category);
                Assert.Equal(720671, item.ItemTypeId);
                Assert.Equal("hot", item.Status);
            },
            item =>
            {
                Assert.Equal("recommend", item.Category);
                Assert.Null(item.Status);
                Assert.Equal("Open~to~get~3~Small~Lottery~Tickets.", item.Description);
            });
    }

    [Fact]
    public void EmoneyShopV2ParserReadsFlagsAndDescriptions()
    {
        const string content = """
        ID=2888
        MoneyType=1
        3324226 other|new Collect~30~hammers.
        724002 item Open~to~get~3~Small~Lottery~Tickets.

        [recommend]
        Amount=1
        Item0=3324226 new Collect~30~hammers.
        """;

        var data = ShopParser.ParseEmoneyShopV2(content);

        var shop = Assert.Single(data.Shops);
        Assert.Equal(2888, shop.Id);
        Assert.Equal(1, shop.MoneyType);

        Assert.Collection(
            data.Items,
            item =>
            {
                Assert.Equal(3324226, item.ItemTypeId);
                Assert.Equal("other", item.Category);
                Assert.Equal("new", item.Status);
                Assert.Equal(["other", "new"], item.Flags);
                Assert.Equal("Collect~30~hammers.", item.Description);
            },
            item =>
            {
                Assert.Equal(724002, item.ItemTypeId);
                Assert.Equal("item", item.Category);
                Assert.Null(item.Status);
                Assert.Equal("Open~to~get~3~Small~Lottery~Tickets.", item.Description);
            },
            item =>
            {
                Assert.Equal(3324226, item.ItemTypeId);
                Assert.Equal("recommend", item.Category);
                Assert.Equal("new", item.Status);
                Assert.Equal("Collect~30~hammers.", item.Description);
            });
    }

    [Fact]
    public void EmoneyShopV2ParserMarksPresentRowsAsBound()
    {
        const string content = """
        ID=2888
        MoneyType=1
        102008 present|new Bound~armor.
        730001 present Bound~stone.
        136008 item Normal~armor.
        """;

        var data = ShopParser.ParseEmoneyShopV2(content);

        Assert.Collection(
            data.Items,
            item =>
            {
                Assert.Equal(102008, item.ItemTypeId);
                Assert.Equal(["present", "new"], item.Flags);
                Assert.Equal("present", item.Category);
                Assert.Equal("new", item.Status);
                Assert.True(item.IsBound);
            },
            item =>
            {
                Assert.Equal(730001, item.ItemTypeId);
                Assert.Equal(["present"], item.Flags);
                Assert.Equal("present", item.Category);
                Assert.Null(item.Status);
                Assert.True(item.IsBound);
            },
            item =>
            {
                Assert.Equal(136008, item.ItemTypeId);
                Assert.Equal(["item"], item.Flags);
                Assert.Equal("item", item.Category);
                Assert.Null(item.Status);
                Assert.False(item.IsBound);
            });
    }

    [Fact]
    public void ExchangeShopParserPreservesRawAtAtFieldsAndTrailingEmptyField()
    {
        var row = ShopParser.ParseExchangeRow(
            "exchange_shop_goods.txt",
            1,
            "444@@3303526@@23@@1@@0@@3303483@@300@@3303485@@1@@0@@0@@0@@0@@10000@@0@@");

        Assert.Equal("exchange_shop_goods.txt", row.SourceFile);
        Assert.Equal(1, row.LineNumber);
        Assert.Equal("444", row.Fields[0]);
        Assert.Equal("3303526", row.Fields[1]);
        Assert.Equal("", row.Fields[^1]);
    }

    [Fact]
    public void HonorShopParserPreservesEmptyCsvFieldsAndValidatesCounts()
    {
        const string content = """
        ItemAmount=3
        3333012,10000,0,item
        3303085,3000,0,item,,new
        3008101,10000,0,item,recommend,new
        RecommendAmount=1
        3008101=Open~to~receive~a~Class~6~House~Pack.
        """;

        var catalog = ShopParser.ParseHonorShop(content);

        Assert.Equal(3, catalog.Items.Count);
        Assert.Equal(3333012, catalog.Items[0].ItemTypeId);
        Assert.Equal(10_000, catalog.Items[0].Cost);
        Assert.Equal("item", catalog.Items[0].Category);
        Assert.False(catalog.Items[0].IsRecommended);
        Assert.Null(catalog.Items[0].Status);

        Assert.Equal(["3303085", "3000", "0", "item", "", "new"], catalog.Items[1].Fields);
        Assert.Equal("new", catalog.Items[1].Status);
        Assert.True(catalog.Items[2].IsRecommended);

        var recommendation = Assert.Single(catalog.Recommendations);
        Assert.Equal(3008101, recommendation.ItemTypeId);
        Assert.Equal("Open~to~receive~a~Class~6~House~Pack.", recommendation.Text);
    }

    [Fact]
    public void HonorShopParserRejectsDeclaredCountMismatches()
    {
        const string content = """
        ItemAmount=2
        3333012,10000,0,item
        RecommendAmount=0
        """;

        var ex = Assert.Throws<FormatException>(() => ShopParser.ParseHonorShop(content));

        Assert.Contains("ItemAmount", ex.Message);
    }

    [Fact]
    public void PointShopParserReadsItemsRecommendationsAndConfig()
    {
        const string content = """
        [Normal]
        3304720 15000 item other new
        730001 10 item equip other
        3306562 Open~to~select~a~red~Rune~(B).

        [Recommend]
        3304720 100 item other new
        3304720 Open~to~get~5~pills.

        [PointLimit]
        GoldenLeaguePointLimit=304400

        [Color]
        TipColor=0xffffffff
        """;

        var catalog = ShopParser.ParsePointShop(content, "GoldenLeagueShop.ini");

        Assert.Collection(
            catalog.Items,
            item =>
            {
                Assert.Equal(3304720, item.ItemTypeId);
                Assert.Equal(15_000, item.Cost);
                Assert.Equal("item", item.ItemClass);
                Assert.Equal("other", item.Category);
                Assert.Equal("new", item.Status);
                Assert.Equal("Normal", item.Section);
            },
            item =>
            {
                Assert.Equal(730001, item.ItemTypeId);
                Assert.Equal("equip", item.Category);
                Assert.Equal("other", item.Status);
            },
            item =>
            {
                Assert.Equal(3304720, item.ItemTypeId);
                Assert.Equal(100, item.Cost);
                Assert.Equal("Recommend", item.Section);
            });

        Assert.Collection(
            catalog.Recommendations,
            recommendation =>
            {
                Assert.Equal(3306562, recommendation.ItemTypeId);
                Assert.Equal("Open~to~select~a~red~Rune~(B).", recommendation.Text);
                Assert.Equal("Normal", recommendation.Section);
            },
            recommendation =>
            {
                Assert.Equal(3304720, recommendation.ItemTypeId);
                Assert.Equal("Open~to~get~5~pills.", recommendation.Text);
                Assert.Equal("Recommend", recommendation.Section);
            });
        Assert.Equal("304400", catalog.Config["PointLimit.GoldenLeaguePointLimit"]);
        Assert.Equal("0xffffffff", catalog.Config["Color.TipColor"]);
    }

    [Fact]
    public void BoothParserPreservesDuplicateItemKeysAndMalformedSections()
    {
        const string content = """
        [booth700050]
        ID=700050
        Name=Promoted
        Mesh=38194
        Map=1004
        X=43
        Y=58
        ItemAmount=99
        Item1=721080@@1000@@0@@0@0@0@0
        Item1=1080001@@2000@@0@@0@0@0@0@@

        booth700051]
        ID=700051
        Name=InnerPower[S]
        Mesh=49820
        Map=10364
        Type=16
        X=336
        Y=300
        ItemAmount=1
        Item17=3007232@@100000@@0@@0@0@0@0
        """;

        var catalog = ShopParser.ParseBooths(content);

        Assert.Collection(
            catalog.Booths,
            booth =>
            {
                Assert.Equal(700050, booth.Id);
                Assert.Equal("Promoted", booth.Name);
                Assert.Equal(38194, booth.Mesh);
                Assert.Equal(1004, booth.MapId);
                Assert.Null(booth.Type);
                Assert.Equal(99, booth.DeclaredItemAmount);
                Assert.Equal("booth700050", booth.SourceSection);
                Assert.Equal(2, booth.SourceLine);
            },
            booth =>
            {
                Assert.Equal(700051, booth.Id);
                Assert.Equal("InnerPower[S]", booth.Name);
                Assert.Equal(16, booth.Type);
            });

        Assert.Collection(
            catalog.Items,
            item =>
            {
                Assert.Equal(700050, item.BoothId);
                Assert.Equal(721080, item.ItemTypeId);
                Assert.Equal(1000, item.Price);
                Assert.Equal(1, item.ItemKey);
                Assert.Equal(0, item.Position);
                Assert.Equal(0, item.Unknown0);
                Assert.Equal("0@0@0@0", item.Tail);
                Assert.Null(item.ExtraField);
                Assert.Equal(["721080", "1000", "0", "0@0@0@0"], item.Fields);
            },
            item =>
            {
                Assert.Equal(700050, item.BoothId);
                Assert.Equal(1080001, item.ItemTypeId);
                Assert.Equal(1, item.ItemKey);
                Assert.Equal(1, item.Position);
                Assert.Equal("", item.Fields[^1]);
            },
            item =>
            {
                Assert.Equal(700051, item.BoothId);
                Assert.Equal(17, item.ItemKey);
                Assert.Equal(0, item.Position);
            });
    }
}
