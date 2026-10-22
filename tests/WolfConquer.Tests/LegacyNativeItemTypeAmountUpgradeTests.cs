using System.Reflection;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTypeAmountUpgradeTests
{
    [Fact]
    public void AmountExperienceAndUpgradeMethodsRetainFourNativeStaticSignatures()
    {
        (string Name, Type Parameter, Type Result)[] expected =
        [
            (nameof(Table.MoneyItemID), typeof(uint), typeof(uint)),
            (nameof(Table.CalculateExpBall), typeof(byte), typeof(ulong)),
            (nameof(Table.GetUpEpLevelInfo), typeof(uint), typeof(int)),
            (nameof(Table.RateSuccForQuality), typeof(uint), typeof(int))
        ];

        foreach (var signature in expected)
        {
            var method = typeof(Table).GetMethod(signature.Name,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            Assert.NotNull(method);
            Assert.Equal(signature.Result, method.ReturnType);
            Assert.Equal(signature.Parameter, Assert.Single(method.GetParameters()).ParameterType);
        }
    }

    [Fact]
    public void MoneyFirstTierIncludesZeroAndNinetyNineOnly()
    {
        Assert.Equal(1090000u, Table.MoneyItemID(0u));
        Assert.Equal(1090000u, Table.MoneyItemID(1u));
        Assert.Equal(1090000u, Table.MoneyItemID(99u));
        Assert.Equal(1090010u, Table.MoneyItemID(100u));
    }

    [Fact]
    public void MoneySecondTierEndsAtThreeHundredNinetyEight()
    {
        Assert.Equal(1090010u, Table.MoneyItemID(100u));
        Assert.Equal(1090010u, Table.MoneyItemID(398u));
        Assert.Equal(1090020u, Table.MoneyItemID(399u));
    }

    [Fact]
    public void MoneyThirdTierEndsAtFiveThousandNinetyEight()
    {
        Assert.Equal(1090020u, Table.MoneyItemID(399u));
        Assert.Equal(1090020u, Table.MoneyItemID(5098u));
        Assert.Equal(1091000u, Table.MoneyItemID(5099u));
    }

    [Fact]
    public void MoneyFourthTierEndsAtEightThousandNinetyEight()
    {
        Assert.Equal(1091000u, Table.MoneyItemID(5099u));
        Assert.Equal(1091000u, Table.MoneyItemID(8098u));
        Assert.Equal(1091010u, Table.MoneyItemID(8099u));
    }

    [Fact]
    public void MoneyFifthTierEndsAtTwelveThousandNinetyEight()
    {
        Assert.Equal(1091010u, Table.MoneyItemID(8099u));
        Assert.Equal(1091010u, Table.MoneyItemID(12098u));
        Assert.Equal(1091020u, Table.MoneyItemID(12099u));
    }

    [Fact]
    public void MoneyFinalTierHasNoUpperClampOrSignedReinterpretation()
    {
        Assert.Equal(1091020u, Table.MoneyItemID(12099u));
        Assert.Equal(1091020u, Table.MoneyItemID(2147483648u));
        Assert.Equal(1091020u, Table.MoneyItemID(uint.MaxValue));
    }

    [Fact]
    public void ExperienceZeroAndOneKeepTheTruncatedConstant()
    {
        Assert.Equal(680462ul, Table.CalculateExpBall(0));
        Assert.Equal(680462ul, Table.CalculateExpBall(1));
    }

    [Fact]
    public void ExperienceOddLevelsUseIntegerHalfBeforeDoubleMultiplication()
    {
        Assert.Equal(687421ul, Table.CalculateExpBall(2));
        Assert.Equal(690901ul, Table.CalculateExpBall(3));
        Assert.Equal(708299ul, Table.CalculateExpBall(4));
        Assert.Equal(715258ul, Table.CalculateExpBall(5));
    }

    [Fact]
    public void ExperiencePreOneThirtyBranchHasNoRemainderMultiplier()
    {
        Assert.Equal(28520188ul, Table.CalculateExpBall(127));
        Assert.Equal(29184779ul, Table.CalculateExpBall(128));
        Assert.Equal(29407469ul, Table.CalculateExpBall(129));
    }

    [Fact]
    public void ExperienceOneThirtyThroughOneThirtyFourUsesRemainderPlusSix()
    {
        ulong[] expected = [180494988ul, 212160672ul, 247953080ul, 281014057ul, 319196902ul];
        for (var offset = 0; offset < expected.Length; offset++)
        {
            Assert.Equal(expected[offset], Table.CalculateExpBall((byte)(130 + offset)));
        }
    }

    [Fact]
    public void ExperienceOneThirtyFiveSwitchAndDecadeDropRemainLiteral()
    {
        Assert.Equal(417986644ul, Table.CalculateExpBall(135));
        Assert.Equal(460028290ul, Table.CalculateExpBall(136));
        Assert.Equal(578894925ul, Table.CalculateExpBall(139));
        Assert.Equal(278238916ul, Table.CalculateExpBall(140));
        Assert.True(Table.CalculateExpBall(140) < Table.CalculateExpBall(139));
    }

    [Fact]
    public void ExperienceMaximumByteRetainsUnsignedUlongReturnWithoutLevelClamp()
    {
        Assert.Equal(1355078007ul, Table.CalculateExpBall(254));
        Assert.Equal(1473745880ul, Table.CalculateExpBall(byte.MaxValue));
    }

    [Fact]
    public void HeadLevelFiveThroughNineUsesNativeSmallCostTableAndDefault()
    {
        uint[] ids = [111040u, 111050u, 111060u, 111070u, 111080u, 111090u];
        int[] expected = [1, 3, 3, 4, 7, 7];
        for (var index = 0; index < ids.Length; index++)
        {
            Assert.Equal(expected[index], Table.GetUpEpLevelInfo(ids[index]));
        }
    }

    [Fact]
    public void HeadQualityDigitsSixThroughNineReduceBeforeFinalIntegerDivisions()
    {
        uint[] ids = [111050u, 111056u, 111057u, 111058u, 111059u];
        int[] expected = [3, 3, 3, 8, 25];
        for (var index = 0; index < ids.Length; index++)
        {
            Assert.Equal(expected[index], Table.GetUpEpLevelInfo(ids[index]));
        }
    }

    [Fact]
    public void HeadLevelEightAndNineQualityNineRetainSixtyOneCost()
    {
        Assert.Equal(61, Table.GetUpEpLevelInfo(111089u));
        Assert.Equal(61, Table.GetUpEpLevelInfo(111099u));
        Assert.Equal(25, Table.GetUpEpLevelInfo(111159u));
    }

    [Fact]
    public void ArmorShieldAndNativePositionExceptionUseGenuineDependencies()
    {
        Assert.Equal(25, Table.GetUpEpLevelInfo(130059u));
        Assert.Equal(25, Table.GetUpEpLevelInfo(900059u));
        Assert.Equal((ushort)9, LegacyNativeItemPosition.ItemPosition(134155u));
        Assert.Equal((byte)15, Table.GetLevel(134155u));
        Assert.Equal(2, Table.GetUpEpLevelInfo(134155u));
        Assert.Equal(3, Table.GetUpEpLevelInfo(130055u));
        Assert.Equal(3, Table.GetUpEpLevelInfo(900359u));
    }

    [Fact]
    public void OrdinaryLevelsElevenThroughTwentyTwoPreserveBaseAndQualityNineTruncations()
    {
        int[] plain = [2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3];
        int[] qualityNine = [14, 14, 15, 15, 18, 18, 20, 20, 25, 25, 31, 31];
        for (var index = 0; index < plain.Length; index++)
        {
            var id = 410000u + (uint)(11 + index) * 10u;
            Assert.Equal(plain[index], Table.GetUpEpLevelInfo(id));
            Assert.Equal(qualityNine[index], Table.GetUpEpLevelInfo(id + 9u));
        }
    }

    [Fact]
    public void OrdinaryQualityFactorsUseTwoSuccessiveIntegerTruncationStages()
    {
        uint[] ids = [410110u, 410116u, 410117u, 410118u, 410119u];
        int[] expected = [2, 2, 2, 4, 14];
        for (var index = 0; index < ids.Length; index++)
        {
            Assert.Equal(expected[index], Table.GetUpEpLevelInfo(ids[index]));
        }
    }

    [Fact]
    public void UnknownIdAndOutOfTableLevelsAreNotRejectedOrNormalized()
    {
        Assert.Equal(1, Table.GetUpEpLevelInfo(0u));
        Assert.Equal(1, Table.GetUpEpLevelInfo(uint.MaxValue));
        Assert.Equal(1, Table.GetUpEpLevelInfo(410100u));
        Assert.Equal(1, Table.GetUpEpLevelInfo(410230u));
        Assert.Equal(3, Table.GetUpEpLevelInfo(410239u));
    }

    [Fact]
    public void QualitySuccessValuesZeroThroughSixReturnThirty()
    {
        for (uint quality = 0; quality <= 6; quality++)
        {
            Assert.Equal(30, Table.RateSuccForQuality(quality));
        }
    }

    [Fact]
    public void QualitySuccessSevenEightAndNineReturnTwelveSixAndFive()
    {
        Assert.Equal(12, Table.RateSuccForQuality(7u));
        Assert.Equal(6, Table.RateSuccForQuality(8u));
        Assert.Equal(5, Table.RateSuccForQuality(9u));
    }

    [Fact]
    public void QualitySuccessTakesTheWholeQualityNotAnItemIdOrModuloDigit()
    {
        Assert.Equal(0, Table.RateSuccForQuality(10u));
        Assert.Equal(0, Table.RateSuccForQuality(19u));
        Assert.Equal(0, Table.RateSuccForQuality(410009u));
        Assert.Equal(0, Table.RateSuccForQuality(uint.MaxValue));
    }

    [Fact]
    public void DeclaredNullItemsBaseModelDoesNotTurnPureCalculationsIntoLookups()
    {
        var original = LegacyNativePoolItems.ItemsBase;
        LegacyNativePoolItems.ItemsBase = null;
        try
        {
            Assert.Equal(1091020u, Table.MoneyItemID(uint.MaxValue));
            Assert.Equal(417986644ul, Table.CalculateExpBall(135));
            Assert.Equal(14, Table.GetUpEpLevelInfo(410119u));
            Assert.Equal(5, Table.RateSuccForQuality(9u));
            Assert.Null(LegacyNativePoolItems.ItemsBase);
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = original;
        }
    }
}
