namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerStatCalculationTests
{
    [Theory]
    [InlineData(0u, 33)]
    [InlineData(1000u, 33)]
    [InlineData(1001u, 34)]
    [InlineData(1002u, 35)]
    [InlineData(1003u, 36)]
    [InlineData(1004u, 36)]
    [InlineData(1005u, 37)]
    [InlineData(1006u, 37)]
    [InlineData(1051u, 37)]
    [InlineData(1052u, 33)]
    [InlineData(100000u, 33)]
    [InlineData(uint.MaxValue, 33)]
    public void HitpointCalculationUsesEveryLiteralNativePromotionBandAndTruncatesOnlyAfterSum(uint profession, ushort expected)
    {
        using var scope = new Scope(); Class(scope.Client, profession);
        scope.Client.Player.Agility = scope.Client.Player.Spirit = scope.Client.Player.Strength = scope.Client.Player.Vitality = 1;
        Assert.Equal(expected, scope.Client.CalculateHitPoint());
    }

    [Theory]
    [InlineData(1005u, 1, 0, 0, 0, 3)]
    [InlineData(1005u, 0, 1, 0, 0, 3)]
    [InlineData(1005u, 0, 0, 1, 0, 3)]
    [InlineData(1005u, 0, 0, 0, 1, 27)]
    [InlineData(1001u, 0, 0, 0, 1, 25)]
    [InlineData(1002u, 0, 0, 0, 1, 25)]
    [InlineData(1003u, 0, 0, 0, 1, 26)]
    [InlineData(1004u, 0, 0, 0, 1, 26)]
    [InlineData(0u, 0, 0, 0, 1, 24)]
    [InlineData(0u, 10, 20, 30, 40, 1140)]
    [InlineData(1001u, 10, 20, 30, 40, 1197)]
    [InlineData(1002u, 10, 20, 30, 40, 1230)]
    [InlineData(1003u, 10, 20, 30, 40, 1254)]
    [InlineData(1004u, 10, 20, 30, 40, 1273)]
    [InlineData(1005u, 10, 20, 30, 40, 1311)]
    public void HitpointCalculationUsesActualFourStatsAndPreservesIndependentClassCoefficients(uint profession,
        ushort agility, ushort spirit, ushort strength, ushort vitality, ushort expected)
    {
        using var scope = new Scope(); Class(scope.Client, profession);
        scope.Client.Player.Agility = agility; scope.Client.Player.Spirit = spirit;
        scope.Client.Player.Strength = strength; scope.Client.Player.Vitality = vitality;
        Assert.Equal(expected, scope.Client.CalculateHitPoint());
    }

    [Theory]
    [InlineData(0u, 65503)]
    [InlineData(1001u, 42563)]
    [InlineData(1002u, 40596)]
    [InlineData(1003u, 19624)]
    [InlineData(1004u, 57634)]
    [InlineData(1005u, 62221)]
    public void MaximalStatsRetainNativeLowUInt16BitsInsteadOfSaturatingHealth(uint profession, ushort expected)
    {
        using var scope = new Scope(); Class(scope.Client, profession);
        scope.Client.Player.Agility = scope.Client.Player.Spirit = scope.Client.Player.Strength = scope.Client.Player.Vitality = ushort.MaxValue;
        Assert.Equal(expected, scope.Client.CalculateHitPoint());
    }

    [Theory]
    [InlineData(0u, 5)]
    [InlineData(13001u, 5)]
    [InlineData(14001u, 5)]
    [InlineData(13002u, 15)]
    [InlineData(14002u, 15)]
    [InlineData(13003u, 20)]
    [InlineData(14003u, 20)]
    [InlineData(13004u, 25)]
    [InlineData(14004u, 25)]
    [InlineData(13005u, 30)]
    [InlineData(13051u, 30)]
    [InlineData(13052u, 5)]
    [InlineData(14005u, 30)]
    [InlineData(14051u, 30)]
    [InlineData(14052u, 5)]
    [InlineData(uint.MaxValue, 5)]
    public void ManaUsesLiteralWaterFireClassRangesAndRealSpiritWithUInt16Wrapping(uint profession, ushort multiplier)
    {
        using var scope = new Scope(); Class(scope.Client, profession);
        scope.Client.Player.Spirit = 1; Assert.Equal(multiplier, scope.Client.CalculateMana());
        scope.Client.Player.Spirit = ushort.MaxValue;
        Assert.Equal(unchecked((ushort)(65535 * multiplier)), scope.Client.CalculateMana());
    }

    [Fact]
    public void CalculatorsDoNotMutateStatusOwnersOrAllocateMissingGameplayOwners()
    {
        using var scope = new Scope(); var status = scope.Client.Status;
        status.MaxHitpoints = 900; status.MaxMana = 700;
        Assert.Equal((ushort)0, scope.Client.CalculateHitPoint()); Assert.Equal((ushort)0, scope.Client.CalculateMana());
        Assert.Equal(900u, status.MaxHitpoints); Assert.Equal(700u, status.MaxMana);
        Assert.Null(scope.Client.Equipment); Assert.Null(scope.Client.GuildSkill); Assert.Null(scope.Client.Socket);
        Assert.Equal(0, scope.Client.Player.CompletedRateDraws);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingActualPlayerStillFaultsRatherThanReadingAStatSnapshot(bool mana)
    {
        using var scope = new Scope(); scope.Client.Player = null!;
        Assert.Throws<NullReferenceException>(() =>
        {
            if (mana) _ = scope.Client.CalculateMana(); else _ = scope.Client.CalculateHitPoint();
        });
    }
}
