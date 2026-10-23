using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

public sealed class LegacyNativeItemPointsTests
{
    // Literal observations of the complete source-shaped byte-domain expression,
    // executed by root on Framework4/x86 in both default and optimized builds.
    private static readonly uint[] LevelCosts =
    [
        0,3,6,9,12,15,18,21,24,27,30,33,36,39,42,45,48,51,54,57,60,63,66,69,72,75,78,81,84,87,90,93,96,99,102,105,108,111,114,117,120,122,126,129,132,135,138,141,144,147,150,153,156,159,162,165,168,171,174,177,180,183,186,189,192,195,198,201,204,207,210,213,216,219,222,225,228,231,234,237,240,243,245,249,252,255,258,261,264,267,270,273,276,279,282,285,288,291,294,297,300,303,306,309,312,315,318,321,324,327,330,333,336,339,342,345,348,351,354,357,360,605,610,615,620,625,630,635,640,645,650,786,792,798,804,810,816,822,828,834,840,846,852,858,864,870,876,882,888,894,900,906,912,918,924,930,936,942,948,954,960,966,972,978,983,990,996,1002,1008,1013,1020,1026,1032,1038,1044,1050,1056,1062,1068,1074,1080,1086,1092,1098,1104,1110,1116,1122,1128,1134,1140,1146,1152,1158,1164,1170,1176,1182,1188,1194,1200,1206,1212,1218,1224,1230,1236,1242,1248,1254,1260,1266,1272,1278,1284,1290,1296,1302,1308,1314,1320,1326,1332,1338,1344,1350,1356,1362,1368,1374,1380,1386,1392,1398,1404,1410,1416,1422,1428,1434,1440,1446,1452,1458,1464,1470,1476,1482,1488,1494,1500,1506,1512,1518,1524,1530
    ];

    [Fact]
    public void EveryByteLevelRetainsAllThreeNativeFloatingBranchesIncludingTheirAnomalies()
    {
        Assert.Equal(256, LevelCosts.Length);
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 111003 };
        for (int level = 0; level < 256; level++)
        {
            var row = new LegacyNativeItemRuneMetadata { ID = 111003, Level = (byte)level };
            Assert.Equal(50u + LevelCosts[level], LegacyNativeItemTypeRuneMetadata.GetItemPoints(row, item));
        }
    }

    [Fact]
    public void AllFiftyFourPerfectionThresholdsAccumulateAndHigherLevelsDoNotInventMorePoints()
    {
        uint[] bonuses = [0,180,360,540,720,900,1080,1260,1440,1620,4000,4400,4800,5200,5600,6000,6400,6800,7200,12350,13000,13650,14300,14950,15600,16250,16900,17550,17650,17750,17850,17950,18050,18150,18250,18350,18450,18550,18650,18750,18850,18950,19050,19150,19250,19350,19450,19550,19650,19750,19850,19950,20050,20150,20250];
        var row = new LegacyNativeItemRuneMetadata();
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 111003 };
        for (uint level = 0; level <= 54; level++)
        {
            item.PerfectionLevel = level;
            Assert.Equal(50u + bonuses[level], LegacyNativeItemTypeRuneMetadata.GetItemPoints(row, item));
        }
        item.PerfectionLevel = uint.MaxValue;
        Assert.Equal(20300u, LegacyNativeItemTypeRuneMetadata.GetItemPoints(row, item));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(6, 50)] [InlineData(7, 150)]
    [InlineData(8, 250)] [InlineData(9, 450)]
    public void QualityUsesTheDatabaseIdRatherThanTheActualItemId(uint quality, uint points)
    {
        var row = new LegacyNativeItemRuneMetadata { ID = 111000 + quality };
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 111003 };
        Assert.Equal(50u + points, LegacyNativeItemTypeRuneMetadata.GetItemPoints(row, item));
    }

    [Theory]
    [InlineData(134155u)] [InlineData(200000u)] [InlineData(350001u)]
    [InlineData(370001u)] [InlineData(4100000u)]
    public void ExcludedPositionsDiscardBaseLevelAndQualityBeforeAddingBless(uint id)
    {
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = id, Bless = 3 };
        var row = new LegacyNativeItemRuneMetadata { ID = 111009, Level = 130 };
        Assert.Equal(300u, LegacyNativeItemTypeRuneMetadata.GetItemPoints(row, item));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 100)] [InlineData(2, 0)]
    [InlineData(3, 300)] [InlineData(5, 500)] [InlineData(7, 700)] [InlineData(255, 0)]
    public void BlessOnlyRecognizesTheFourExactNativeValues(byte bless, uint points)
    {
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 111003, Bless = bless };
        Assert.Equal(50u + points, LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 200)] [InlineData(2, 600)]
    [InlineData(3, 1200)] [InlineData(4, 1800)] [InlineData(5, 2600)]
    [InlineData(6, 3500)] [InlineData(7, 4800)] [InlineData(8, 5800)]
    [InlineData(9, 6800)] [InlineData(10, 7800)] [InlineData(11, 8800)]
    [InlineData(12, 10000)] [InlineData(13, 0)]
    public void PlusIsNotClampedByTheScoringMethod(byte plus, uint points)
    {
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 111003, Plus = plus };
        Assert.Equal(50u + points, LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
    }

    [Fact]
    public void SocketAsymmetryEmptySocketsEnchantAndAnimaAreAllIndependent()
    {
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 111003,
            SocketOne = (Flags.Gem)11, SocketTwo = (Flags.Gem)23, Enchant = 255,
            AnimaItemID = 1234567 };
        Assert.Equal(38560u, LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
        item.SocketOne = Flags.Gem.EmptySocket;
        item.SocketTwo = Flags.Gem.NoSocket;
        Assert.Equal(35060u, LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
        item.ITEM_ID = 300000; // Perfection position9: no50 base and no enchant.
        item.AnimaItemID = 0;
        Assert.Equal(1000u, LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(3, 500)] [InlineData(4, 800)]
    [InlineData(5, 1200)] [InlineData(6, 1600)] [InlineData(7, 2000)] [InlineData(8, 0)]
    public void PurificationFourHasItsSeparateRingException(uint level, uint points)
    {
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 111003 };
        item.Purification.PurificationLevel = level;
        Assert.Equal(50u + points, LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
        item.ITEM_ID = 150000;
        Assert.Equal(50u + (level == 4 ? 2000u : points), LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 100)] [InlineData(2, 200)]
    [InlineData(3, 800)] [InlineData(4, 1200)] [InlineData(5, 1600)]
    [InlineData(6, 2000)] [InlineData(7, 0)]
    public void RefineryUsesItsOwnCompleteSevenWaySchedule(uint level, uint points)
    {
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 111003 };
        item.Refinary.EffectLevel = level;
        Assert.Equal(50u + points, LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
    }

    [Fact]
    public void RelicUsesAllThirteenSourceMultipliersAndSkipsUnknownTypesWithoutMutatingAttributes()
    {
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 4100000 };
        // Attribute enum ordinals are validated individually below, not assumed contiguous.
        var expected = new Dictionary<LegacyNativeChiAttribute, uint>
        {
            [LegacyNativeChiAttribute.CriticalStrike] = 40, [LegacyNativeChiAttribute.SkillCriticalStrike] = 40,
            [LegacyNativeChiAttribute.Immunity] = 40, [LegacyNativeChiAttribute.Breakthrough] = 40,
            [LegacyNativeChiAttribute.Counteraction] = 40, [LegacyNativeChiAttribute.HPAdd] = 10,
            [LegacyNativeChiAttribute.AddAttack] = 20, [LegacyNativeChiAttribute.AddMagicAttack] = 10,
            [LegacyNativeChiAttribute.AddMagicDefense] = 50, [LegacyNativeChiAttribute.PhysicalDamageIncrease] = 10,
            [LegacyNativeChiAttribute.MagicDamageIncrease] = 10, [LegacyNativeChiAttribute.PhysicalDamageDecrease] = 10,
            [LegacyNativeChiAttribute.MagicDamageDecrease] = 10
        };
        foreach (var pair in expected)
        {
            item.RelicAttributes = [new(pair.Key, 10, false), new((LegacyNativeChiAttribute)99, 10, false)];
            var original = item.RelicAttributes;
            Assert.Equal(pair.Value, LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
            Assert.Same(original, item.RelicAttributes);
            Assert.Equal((ushort)10, item.RelicAttributes[0].Value);
        }
        item.RelicAttributes = null!;
        Assert.Throws<ArgumentNullException>(() => LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
        item.ITEM_ID = 111003; // Non-relic never reads the absent relic array.
        Assert.Equal(50u, LegacyNativeItemTypeRuneMetadata.GetItemPoints(new(), item));
    }
}
