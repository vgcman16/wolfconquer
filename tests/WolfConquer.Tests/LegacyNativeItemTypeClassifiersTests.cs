using System.Globalization;
using System.Reflection;
using WolfConquer.Core.World;
using Items = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTypeClassifiersTests
{
    [Fact]
    public void AllThirtySevenMethodsStayOnTheSameRealPartialOwnerWithNativeWidths()
    {
        Assert.Equal(typeof(Dictionary<uint, LegacyNativeItemRuneMetadata>), typeof(Items).BaseType);
        string[] names =
        {
            "IsMoneyBag",
            "IsAEpicWeapon",
            "IsWarriorEpicWeapons",
            "GetSashCounts",
            "IsSash",
            "IsPistol",
            "IsPickAxe",
            "IsRapier",
            "IsKnife",
            "IsDagger",
            "IsShield",
            "IsTrojanEpicWeapon",
            "IsPrayedBead",
            "IsMonkEpicWeapon",
            "IsPirateEpicWeapon",
            "IsTalisman",
            "isRedRune",
            "isBlueRune",
            "isYellowRune",
            "MaxRuneLevel",
            "FlashShieldRunePoints",
            "TypeofItem",
            "IsWindWalkerWeapon",
            "IsDragonWorroir",
            "IsHossu",
            "IsHolyGrail",
            "IsTaoistEpicWeapon",
            "IsArrow",
            "IsTwoHand",
            "IsBow",
            "IsAccessory",
            "IsBacksword",
            "IsNinjaEpicWeapon",
            "IsKatana",
            "IsScythe",
            "IsMonkWeapon",
            "GetLevel"
        };
        foreach (var name in names)
        {
            var method = typeof(Items).GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.NotNull(method);
            Assert.True(method.IsStatic);
            Assert.False(method.IsVirtual);
            Assert.Equal(typeof(uint), Assert.Single(method.GetParameters()).ParameterType);
            var expected = name is "GetSashCounts" or "MaxRuneLevel" or "GetLevel" ? typeof(byte)
                : name == "TypeofItem" ? typeof(int)
                : name == "FlashShieldRunePoints" ? typeof(uint) : typeof(bool);
            Assert.Equal(expected, method.ReturnType);
        }
        Assert.Null(typeof(Items).GetMethod("isRune", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
        Assert.NotNull(typeof(Items).GetMethod("IsRune", BindingFlags.NonPublic | BindingFlags.Static));
    }

    [Fact]
    public void AEpicAndWarriorUseWholeThousandQuotientsRatherThanNarrowRanges()
    {
        AssertRange(Items.IsAEpicWeapon, 606000, 606999);
        AssertRange(Items.IsWarriorEpicWeapons, 624000, 624999);
    }

    [Fact]
    public void PirateAcceptsExactlyTwoAdjacentThousandBlocks()
    {
        AssertRange(Items.IsPirateEpicWeapon, 670000, 671999);
        Assert.True(Items.IsPirateEpicWeapon(670999));
        Assert.True(Items.IsPirateEpicWeapon(671000));
    }

    [Fact]
    public void PistolRapierAndKnifeKeepTheirDifferentUpperEndpoints()
    {
        AssertRange(Items.IsPistol, 612000, 612439);
        AssertRange(Items.IsRapier, 611000, 611439);
        AssertRange(Items.IsKnife, 613000, 613429);
        Assert.False(Items.IsKnife(613439));
    }

    [Fact]
    public void PickAxeIsOnlyTheExactSingleIdentifier()
    {
        AssertRange(Items.IsPickAxe, 562000, 562000);
    }

    [Fact]
    public void DaggerShieldAndTrojanPreserveInclusiveLimits()
    {
        AssertRange(Items.IsDagger, 490003, 490149);
        AssertRange(Items.IsShield, 900000, 900309);
        AssertRange(Items.IsTrojanEpicWeapon, 614000, 614439);
    }

    [Fact]
    public void PrayedBeadAndMonkWeaponShareARangeButNotTheEpicRange()
    {
        AssertRange(Items.IsPrayedBead, 610000, 610439);
        AssertRange(Items.IsMonkWeapon, 610000, 610439);
        AssertRange(Items.IsMonkEpicWeapon, 622000, 622439);
        Assert.False(Items.IsMonkEpicWeapon(610000));
        Assert.False(Items.IsMonkWeapon(622000));
    }

    [Fact]
    public void HossuTaoistWindAndDragonRetainDistinctLowerBounds()
    {
        AssertRange(Items.IsHossu, 619000, 619439);
        AssertRange(Items.IsTaoistEpicWeapon, 620003, 620439);
        AssertRange(Items.IsWindWalkerWeapon, 626003, 626439);
        AssertRange(Items.IsDragonWorroir, 617393, 617439);
        Assert.False(Items.IsDragonWorroir(617000));
    }

    [Fact]
    public void NinjaAndScytheRetainTheirNativeShortIdentifierExceptions()
    {
        AssertRange(Items.IsNinjaEpicWeapon, 616000, 616439);
        AssertRange(Items.IsScythe, 511000, 511439);
        Assert.True(Items.IsNinjaEpicWeapon(616));
        Assert.False(Items.IsNinjaEpicWeapon(615));
        Assert.False(Items.IsNinjaEpicWeapon(617));
        Assert.True(Items.IsScythe(511));
        Assert.False(Items.IsScythe(510));
        Assert.False(Items.IsScythe(512));
    }

    [Fact]
    public void KatanaBowBackswordAndArrowKeepTheirOwnRanges()
    {
        AssertRange(Items.IsKatana, 601000, 601439);
        AssertRange(Items.IsBow, 500003, 500429);
        AssertRange(Items.IsBacksword, 421003, 421439);
        AssertRange(Items.IsArrow, 1050000, 1051000);
    }

    [Fact]
    public void TwoHandRetainsDecimalPrefixInsteadOfAFixedSixDigitRange()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            foreach (uint id in new uint[] { 5, 50, 500, 511, 500000, 599999, 500000000, 599999999 })
                Assert.True(Items.IsTwoHand(id));
            AssertRange(Items.IsTwoHand, 421003, 421439);
            foreach (uint id in new uint[] { 0, 4, 6, 49, 60, 499999, 600000, uint.MaxValue })
                Assert.False(Items.IsTwoHand(id));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void SashMembershipAndCapacityUseOnlyTheThreeExactIds()
    {
        Assert.True(Items.IsSash(1100009));
        Assert.True(Items.IsSash(1100006));
        Assert.True(Items.IsSash(1100003));
        Assert.Equal((byte)12, Items.GetSashCounts(1100009));
        Assert.Equal((byte)6, Items.GetSashCounts(1100006));
        Assert.Equal((byte)3, Items.GetSashCounts(1100003));
        foreach (uint id in new uint[] { 0, 1100002, 1100004, 1100005, 1100007, 1100008, 1100010, uint.MaxValue })
        {
            Assert.False(Items.IsSash(id));
            Assert.Equal((byte)0, Items.GetSashCounts(id));
        }
    }

    [Fact]
    public void MoneyBagKeepsElevenIdIntervalAndTwoSeparateExceptions()
    {
        AssertRange(Items.IsMoneyBag, 723713, 723723);
        Assert.True(Items.IsMoneyBag(3005945));
        Assert.True(Items.IsMoneyBag(3008452));
        Assert.False(Items.IsMoneyBag(3005944));
        Assert.False(Items.IsMoneyBag(3005946));
        Assert.False(Items.IsMoneyBag(3008451));
        Assert.False(Items.IsMoneyBag(3008453));
    }

    [Fact]
    public void RuneColorsUseExactTenThousandBucketsAndReuseExistingRuneMapping()
    {
        AssertRange(Items.isRedRune, 4010000, 4019999);
        AssertRange(Items.isBlueRune, 4020000, 4029999);
        AssertRange(Items.isYellowRune, 4030000, 4039999);
        foreach (uint id in new uint[] { 4009999, 4010000, 4019999, 4020000, 4029999, 4030000, 4039999, 4040000, uint.MaxValue })
            Assert.Equal(Items.IsRune(id), Items.isRedRune(id) || Items.isBlueRune(id) || Items.isYellowRune(id));
    }

    [Fact]
    public void MaxRuneLevelPreservesTheRepeatedRedConditionAndBlueDefault()
    {
        Assert.Equal((byte)1, Items.MaxRuneLevel(4010000));
        Assert.Equal((byte)1, Items.MaxRuneLevel(4019999));
        Assert.Equal((byte)27, Items.MaxRuneLevel(4020000));
        Assert.Equal((byte)27, Items.MaxRuneLevel(4029999));
        Assert.Equal((byte)9, Items.MaxRuneLevel(4030000));
        Assert.Equal((byte)9, Items.MaxRuneLevel(4039999));
        Assert.Equal((byte)27, Items.MaxRuneLevel(0));
        Assert.Equal((byte)27, Items.MaxRuneLevel(4040000));
        Assert.Equal((byte)27, Items.MaxRuneLevel(uint.MaxValue));
    }

    [Fact]
    public void FlashShieldPointsPreserveEveryRemainderAndDoNotValidateRuneFamily()
    {
        uint[] expected = { 5000, 5000, 6000, 7000, 7500, 8000, 8500, 9000, 9500, 10000,
            11000, 11500, 12000, 12500, 13000, 13500, 14000, 14500, 15000, 15500,
            16000, 16500, 17000, 17500, 18000, 18500, 19000, 20000, 5000 };
        for (uint remainder = 0; remainder < expected.Length; remainder++)
        {
            Assert.Equal(expected[remainder], Items.FlashShieldRunePoints(remainder));
            Assert.Equal(expected[remainder], Items.FlashShieldRunePoints(4000000 + remainder));
        }
        Assert.Equal(5000u, Items.FlashShieldRunePoints(100));
        Assert.Equal(5000u, Items.FlashShieldRunePoints(uint.MaxValue));
    }

    [Fact]
    public void TypeCategoryFirstSwitchRetainsSpecialThousandGroups()
    {
        (uint Id, int Expected)[] cases = { (123000, 1), (203000, 21), (204000, 22),
            (300001, 14), (300000, -1), (601000, 4), (201000, 12), (202000, 12),
            (200000, 20), (350000, 16), (360000, 15), (370000, 17), (380000, 18) };
        foreach (var item in cases)
            Assert.Equal(item.Expected, Items.TypeofItem(item.Id));
    }

    [Fact]
    public void TypeCategorySecondSwitchKeepsDefaultAndListedRangeQuirks()
    {
        (uint Id, int Expected)[] cases = { (110000, 1), (140000, 1), (170000, 1),
            (180000, 11), (190000, 11), (120000, 2), (130000, 3), (150000, 7),
            (160000, 8), (100000, -1), (400000, 4), (500000, 5), (700000, 9),
            (900000, 6), (1000000, 0), (0, -1), (600000, -1), (800000, 9),
            (1100000, 9), (2999999, 9), (3000000, -1) };
        foreach (var item in cases)
            Assert.Equal(item.Expected, Items.TypeofItem(item.Id));
    }

    [Fact]
    public void TypeCategoryUsesBothNativeModuliAndEarlyReturnOrder()
    {
        Assert.Equal(1, Items.TypeofItem(10123001));
        Assert.Equal(21, Items.TypeofItem(10203000));
        Assert.Equal(14, Items.TypeofItem(1300001));
        Assert.Equal(4, Items.TypeofItem(10400000));
        Assert.Equal(9, Items.TypeofItem(1400000));
        Assert.Equal(-1, Items.TypeofItem(uint.MaxValue));
    }

    [Fact]
    public void TalismanUsesTheExistingLiteralPositionOwnerWithoutAWrapper()
    {
        foreach (uint id in new uint[] { 201003, 201009, 202003, 202009, 204003, 204009 })
        {
            Assert.True(Items.IsTalisman(id));
            Assert.Contains(LegacyNativeItemPosition.ItemPosition(id), new ushort[] { 10, 11, 19 });
        }
        foreach (uint id in new uint[] { 0, 201002, 201010, 202002, 202010, 204002, 204010, uint.MaxValue })
            Assert.False(Items.IsTalisman(id));
    }

    [Fact]
    public void AccessoryIntervalAndHolyGrailSingletonAreNotPositionAliases()
    {
        AssertRange(Items.IsAccessory, 350001, 380046);
        AssertRange(Items.IsHolyGrail, 2100095, 2100095);
        Assert.True(Items.IsAccessory(360323));
        Assert.Equal((ushort)15, LegacyNativeItemPosition.ItemPosition(360323));
        Assert.False(Items.IsHolyGrail(2100096));
    }

    [Fact]
    public void LevelUsesTwoDigitsForHeadArmorShieldAndThreeElsewhere()
    {
        Assert.Equal((byte)2, Items.GetLevel(111123));
        Assert.Equal((byte)5, Items.GetLevel(130259));
        Assert.Equal((byte)2, Items.GetLevel(900129));
        Assert.Equal((byte)12, Items.GetLevel(410123));
        Assert.Equal((byte)12, Items.GetLevel(4010123));
        Assert.Equal((byte)99, Items.GetLevel(4019999));
        Assert.Equal((byte)29, Items.GetLevel(uint.MaxValue));
    }

    [Fact]
    public void UnsignedExtremesAndExplicitCallerWrapDoNotNarrowIdentifiers()
    {
        foreach (var predicate in BoolPredicates())
        {
            Assert.False(predicate(0));
            Assert.False(predicate(uint.MaxValue));
        }
        uint wrapped = unchecked(uint.MaxValue + 1u);
        Assert.Equal(0u, wrapped);
        Assert.Equal(-1, Items.TypeofItem(wrapped));
        Assert.Equal((byte)0, Items.GetLevel(wrapped));
        Assert.Equal((byte)0, Items.GetSashCounts(wrapped));
        Assert.Equal((byte)27, Items.MaxRuneLevel(wrapped));
        Assert.Equal(5000u, Items.FlashShieldRunePoints(wrapped));
    }

    [Fact]
    public void PureHelpersDoNotNeedOrReplaceTheGlobalItemTable()
    {
        var original = LegacyNativePoolItems.ItemsBase;
        var purification = Items.PurificationItems;
        var accessories = Items.Accessorys;
        var refinery = Items.Refinary;
        var garments = Items.Garments;
        var mounts = Items.SteedMounts;
        try
        {
            LegacyNativePoolItems.ItemsBase = null; // Explicit MODEL, not a native cold-start claim.
            foreach (var predicate in BoolPredicates())
                _ = predicate(4010000);
            _ = Items.GetSashCounts(1100009);
            _ = Items.MaxRuneLevel(4020000);
            _ = Items.FlashShieldRunePoints(27);
            _ = Items.TypeofItem(300001);
            _ = Items.GetLevel(410123);
            Assert.Null(LegacyNativePoolItems.ItemsBase);
            Assert.Same(purification, Items.PurificationItems);
            Assert.Same(accessories, Items.Accessorys);
            Assert.Same(refinery, Items.Refinary);
            Assert.Same(garments, Items.Garments);
            Assert.Same(mounts, Items.SteedMounts);
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = original;
        }
    }

    private static void AssertRange(Func<uint, bool> predicate, uint first, uint last)
    {
        Assert.True(predicate(first));
        Assert.True(predicate(last));
        Assert.False(predicate(first - 1u));
        Assert.False(predicate(last + 1u));
    }

    private static Func<uint, bool>[] BoolPredicates() => new Func<uint, bool>[]
    {
        Items.IsMoneyBag,
        Items.IsAEpicWeapon,
        Items.IsWarriorEpicWeapons,
        Items.IsSash,
        Items.IsPistol,
        Items.IsPickAxe,
        Items.IsRapier,
        Items.IsKnife,
        Items.IsDagger,
        Items.IsShield,
        Items.IsTrojanEpicWeapon,
        Items.IsPrayedBead,
        Items.IsMonkEpicWeapon,
        Items.IsPirateEpicWeapon,
        Items.IsTalisman,
        Items.isRedRune,
        Items.isBlueRune,
        Items.isYellowRune,
        Items.IsWindWalkerWeapon,
        Items.IsDragonWorroir,
        Items.IsHossu,
        Items.IsHolyGrail,
        Items.IsTaoistEpicWeapon,
        Items.IsArrow,
        Items.IsTwoHand,
        Items.IsBow,
        Items.IsAccessory,
        Items.IsBacksword,
        Items.IsNinjaEpicWeapon,
        Items.IsKatana,
        Items.IsScythe,
        Items.IsMonkWeapon
    };
}
