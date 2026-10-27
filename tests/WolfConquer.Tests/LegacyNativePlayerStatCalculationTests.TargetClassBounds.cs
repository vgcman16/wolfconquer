namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerStatCalculationTests
{
    [Theory]
    [InlineData(1052u)]
    [InlineData(1053u)]
    [InlineData(1054u)]
    [InlineData(1055u)]
    [InlineData(1056u)]
    [InlineData(1057u)]
    public void ConfirmedTrojanStagesRetainTheExistingPromotedPhysicalStatFormula(uint profession)
    {
        using var scope = new Scope();
        var player = scope.Client.Player;
        var status = scope.Client.Status;
        Class(scope.Client, profession);
        player.Agility = 10;
        player.Spirit = 20;
        player.Strength = 30;
        player.Vitality = 40;
        status.MaxHitpoints = 900;
        status.MaxMana = 700;

        Assert.Same(scope.Client, player.Owner);
        Assert.Equal((ushort)1311, scope.Client.CalculateHitPoint());
        player.Agility = player.Spirit = player.Strength = player.Vitality = ushort.MaxValue;
        Assert.Equal((ushort)62221, scope.Client.CalculateHitPoint());
        Assert.Same(player, scope.Client.Player);
        Assert.Same(status, scope.Client.Status);
        Assert.Equal(profession, player.Class);
        Assert.Equal(ushort.MaxValue, player.Vitality);
        Assert.Equal(900u, status.MaxHitpoints);
        Assert.Equal(700u, status.MaxMana);
    }

    [Theory]
    [InlineData(13052u)]
    [InlineData(13053u)]
    [InlineData(13054u)]
    [InlineData(13055u)]
    [InlineData(13056u)]
    [InlineData(13057u)]
    [InlineData(14052u)]
    [InlineData(14053u)]
    [InlineData(14054u)]
    [InlineData(14055u)]
    [InlineData(14056u)]
    [InlineData(14057u)]
    public void ConfirmedWaterFireStagesRetainTheExistingThirtyManaPerSpirit(uint profession)
    {
        using var scope = new Scope();
        var player = scope.Client.Player;
        var status = scope.Client.Status;
        Class(scope.Client, profession);
        player.Spirit = 37;
        status.MaxHitpoints = 900;
        status.MaxMana = 700;

        Assert.Same(scope.Client, player.Owner);
        Assert.Equal((ushort)1110, scope.Client.CalculateMana());
        player.Spirit = ushort.MaxValue;
        Assert.Equal((ushort)65506, scope.Client.CalculateMana());
        Assert.Same(player, scope.Client.Player);
        Assert.Same(status, scope.Client.Status);
        Assert.Equal(profession, player.Class);
        Assert.Equal(ushort.MaxValue, player.Spirit);
        Assert.Equal(900u, status.MaxHitpoints);
        Assert.Equal(700u, status.MaxMana);
    }

    [Fact]
    public void FirstUnconfirmedStageRetainsExistingFallbackArithmetic()
    {
        using var scope = new Scope();
        var player = scope.Client.Player;
        Class(scope.Client, 1058);
        player.Agility = 10;
        player.Spirit = 20;
        player.Strength = 30;
        player.Vitality = 40;
        Assert.Equal((ushort)1140, scope.Client.CalculateHitPoint());

        foreach (var profession in new uint[] { 13058, 14058 })
        {
            Class(scope.Client, profession);
            player.Spirit = 37;
            Assert.Equal((ushort)185, scope.Client.CalculateMana());
        }
    }
}
