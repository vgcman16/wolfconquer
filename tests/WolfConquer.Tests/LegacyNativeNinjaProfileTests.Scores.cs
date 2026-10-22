using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaProfileTests
{
    [Fact]
    public void EmptyOrOutOfBandRowsReturnOnlyMasteriesWithoutReadingMetadataOrUser()
    {
        using var scope = new Scope();
        Assert.Equal(new uint[6], Components(scope.Ninja));
        Assert.Equal(0u, scope.Ninja.Score);
        scope.Ninja.FireMastery = 1;
        scope.Ninja.WaterMastery = 2;
        scope.Ninja.EarthMastery = 3;
        scope.Ninja.WindMastery = 4;
        scope.Ninja.LightningMastery = 5;
        scope.Ninja.Fire = scope.Ninja.Water = scope.Ninja.Earth = scope.Ninja.Wind = scope.Ninja.Lightning = uint.MaxValue;
        LegacyNativeNinjaFile.gouyu_type = null!;
        scope.Ninja.user = null!;
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 0 }, Components(scope.Ninja));
        foreach (uint id in new uint[] { 0, 99, 106, 199, 206, 299, 306, 399, 406, 499, 506, 599, 606, uint.MaxValue })
            scope.Item(id, id);
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 0 }, Components(scope.Ninja));
        Assert.Equal(15u, scope.Ninja.Score);
    }

    [Theory]
    [InlineData(100u)]
    [InlineData(200u)]
    [InlineData(300u)]
    [InlineData(400u)]
    [InlineData(500u)]
    [InlineData(600u)]
    public void ComponentBandsAreInclusiveUsePhysicalIdsAndChargeElementForEveryMatchingRow(uint band)
    {
        using var scope = new Scope();
        SetAllElements(scope.Ninja, 3, 7);
        scope.Meta(9001, band, 7, 10);
        scope.Meta(9002, band + 5, 8, 20);
        scope.Meta(9003, band - 1, 1, 1000);
        scope.Meta(9004, band + 6, 1, 2000);
        scope.Item(10101, band, 7, 0);
        scope.Item(10102, band, 7, 17);
        scope.Item(10103, band + 5, 8, uint.MaxValue);
        scope.Item(band, band - 1, 1, 1); // Matching map key cannot make a physical ID eligible.
        scope.Item(band + 5, band + 6, 1, 9);
        Assert.Equal(band == 600 ? 40u : 83u, Component(scope.Ninja, band));
    }

    [Fact]
    public void TotalScoreAddsAllSixComponentsAndIgnoresTemporaryElementAndExperienceState()
    {
        using var scope = new Scope();
        scope.Ninja.Fire = 1; scope.Ninja.Water = 2; scope.Ninja.Earth = 3;
        scope.Ninja.Wind = 4; scope.Ninja.Lightning = 5;
        scope.Ninja.FireMastery = 11; scope.Ninja.WaterMastery = 12; scope.Ninja.EarthMastery = 13;
        scope.Ninja.WindMastery = 14; scope.Ninja.LightningMastery = 15;
        scope.Ninja.tmp_Fire = scope.Ninja.tmp_Water = scope.Ninja.tmp_Earth =
            scope.Ninja.tmp_Wind = scope.Ninja.tmp_Lightning = uint.MaxValue;
        scope.Ninja.Flag = ulong.MaxValue; scope.Ninja.Exp = uint.MaxValue; scope.Ninja.UnlockMastery = uint.MaxValue;
        for (uint index = 1; index <= 6; index++)
        {
            scope.Item(index + 900, index * 100, index, index);
            scope.Meta(index + 700, index * 100, index, index * 10);
        }
        Assert.Equal(new uint[] { 25, 40, 55, 70, 85, 60 }, Components(scope.Ninja));
        Assert.Equal(335u, scope.Ninja.Score);
    }

    [Fact]
    public void DefaultDbItemLookupReturnsFreshZeroRowsButMatchingItemsStillReceiveElementPoints()
    {
        using var scope = new Scope();
        scope.Ninja.Fire = 5;
        scope.Ninja.FireMastery = 7;
        var first = scope.Item(701, 100, 1);
        var second = scope.Item(702, 105, 2);
        scope.Meta(100, 999, 1, 900); // Key matches, physical metadata ItemID does not.
        scope.Meta(105, 105, 3, 900); // Physical metadata level does not match.
        Assert.Equal(0u, first.DBItem.Score);
        Assert.NotSame(first.DBItem, first.DBItem);
        Assert.Equal(0u, second.DBItem.Score);
        Assert.Equal(47u, scope.Ninja.Fire_Score);
    }

    [Fact]
    public void ScoresResolveCurrentPhysicalMetadataOnEveryReadWithoutCaching()
    {
        using var scope = new Scope();
        scope.Ninja.Fire = 3;
        scope.Ninja.FireMastery = 1;
        var item = scope.Item(700, 100, 2);
        var metadata = scope.Meta(42, 100, 2, 10);
        Assert.Same(metadata, item.DBItem);
        Assert.Equal(23u, scope.Ninja.Fire_Score);
        metadata.Score = 100;
        Assert.Equal(113u, scope.Ninja.Fire_Score);
        metadata.ItemID = 101;
        Assert.Equal(13u, scope.Ninja.Fire_Score);
        item.ItemID = 101;
        Assert.Equal(113u, scope.Ninja.Fire_Score);
        item.Level = 3;
        Assert.Equal(13u, scope.Ninja.Fire_Score);
        LegacyNativeNinjaFile.gouyu_type = new()
        {
            [999] = new() { ItemID = 101, Level = 3, Score = 500 },
        };
        Assert.Equal(513u, scope.Ninja.Fire_Score);
        Assert.Equal(700u, scope.Ninja.Items.Single().Key);
    }

    [Fact]
    public void ComponentAndTotalArithmeticWrapAtUInt32WithoutSaturation()
    {
        using var scope = new Scope();
        scope.Ninja.Fire = uint.MaxValue;
        scope.Ninja.FireMastery = uint.MaxValue;
        scope.Item(1, 100);
        scope.Meta(1, 100, 1, 10);
        Assert.Equal(5u, scope.Ninja.Fire_Score); // (Max *4 +10 +Max) modulo 2^32.
        scope.Item(2, 600);
        scope.Item(3, 605);
        scope.Meta(2, 600, 1, uint.MaxValue);
        scope.Meta(3, 605, 1, 2);
        Assert.Equal(1u, scope.Ninja.a_Score);
        Assert.Equal(6u, scope.Ninja.Score);
        scope.Ninja.Items.Clear();
        scope.Metadata.Clear();
        SetAllElements(scope.Ninja, 0, uint.MaxValue);
        scope.Item(4, 600);
        scope.Meta(4, 600, 1, 10);
        Assert.Equal(10u, scope.Ninja.a_Score);
        Assert.Equal(5u, scope.Ninja.Score); // Five Max masteries plus ten.
    }

    [Fact]
    public void NullItemDictionaryEligibleMetadataOwnerAndNullRowsFaultWithoutRepair()
    {
        using var scope = new Scope();
        scope.Ninja.Items = null!;
        Assert.Throws<NullReferenceException>(() => _ = scope.Ninja.Fire_Score);
        scope.Ninja.Items = new();
        scope.Item(1, 100);
        LegacyNativeNinjaFile.gouyu_type = null!;
        Assert.Throws<NullReferenceException>(() => _ = scope.Ninja.Fire_Score);
        LegacyNativeNinjaFile.gouyu_type = new() { [1] = null! };
        Assert.Throws<NullReferenceException>(() => _ = scope.Ninja.Fire_Score);
        LegacyNativeNinjaFile.gouyu_type = new();
        scope.Ninja.Items.Clear();
        scope.Ninja.Items[1] = null!;
        Assert.Throws<NullReferenceException>(() => _ = scope.Ninja.a_Score);
    }
}
