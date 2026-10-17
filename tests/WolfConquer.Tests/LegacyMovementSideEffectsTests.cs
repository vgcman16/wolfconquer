using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMovementSideEffectsTests
{
    [Fact]
    public void ApplyMsgMovementClearsAwayAndRequestsVisiblePeerSpawnRefresh()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Away"] = "1",
            ["Angle"] = "7",
        };
        var character = CreateCharacter(attributes);

        var result = LegacyMovementSideEffects.ApplyMsgMovement(character);

        Assert.True(result.ClearedAway);
        Assert.True(result.RefreshVisiblePeerSpawn);
        Assert.NotSame(character, result.Character);
        Assert.Equal("0", result.Character.Attributes["Away"]);
        Assert.Equal("7", result.Character.Attributes["Angle"]);
        Assert.Equal("1", character.Attributes["Away"]);
    }

    [Fact]
    public void ApplyMsgDataPlayerJumpClearsAwayAndRequestsVisiblePeerSpawnRefresh()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Away"] = "true",
        };
        var character = CreateCharacter(attributes);

        var result = LegacyMovementSideEffects.ApplyMsgDataPlayerJump(character);

        Assert.True(result.ClearedAway);
        Assert.True(result.RefreshVisiblePeerSpawn);
        Assert.Equal("0", result.Character.Attributes["Away"]);
        Assert.Equal("true", character.Attributes["Away"]);
    }

    [Fact]
    public void ApplyMsgMovementWithoutAwayStateKeepsCharacterAndSkipsRefresh()
    {
        var character = CreateCharacter();

        var result = LegacyMovementSideEffects.ApplyMsgMovement(character);

        Assert.False(result.ClearedAway);
        Assert.False(result.RefreshVisiblePeerSpawn);
        Assert.Same(character, result.Character);
        Assert.False(result.Character.Attributes.ContainsKey("Away"));
    }

    [Fact]
    public void ApplyMsgDataPlayerJumpWithAwayZeroKeepsCharacterAndSkipsRefresh()
    {
        var character = CreateCharacter(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Away"] = "0",
            });

        var result = LegacyMovementSideEffects.ApplyMsgDataPlayerJump(character);

        Assert.False(result.ClearedAway);
        Assert.False(result.RefreshVisiblePeerSpawn);
        Assert.Same(character, result.Character);
        Assert.Equal("0", result.Character.Attributes["Away"]);
    }

    [Theory]
    [InlineData(7U, 5U)]
    [InlineData(1U, 0U)]
    [InlineData(0U, 0U)]
    public void ApplyMsgMovementSteedVigorSubtractsTwoAndRequestsServerInfo(uint currentVigor, uint expectedVigor)
    {
        var result = LegacyMovementSideEffects.ApplyMsgMovementSteedVigor(currentVigor);

        Assert.Equal(currentVigor, result.CurrentVigor);
        Assert.Equal((ushort)2, result.VigorCost);
        Assert.Equal(expectedVigor, result.UpdatedVigor);
        Assert.True(result.SendServerInfo);
        Assert.Equal(expectedVigor != currentVigor, result.VigorChanged);
    }

    [Fact]
    public void ApplyMsgDataPlayerJumpRideVigorUsesLegacyOddDistanceCost()
    {
        var result = LegacyMovementSideEffects.ApplyMsgDataPlayerJumpRideVigor(
            currentVigor: 15,
            legacyDistance: 15);

        Assert.Equal(15U, result.CurrentVigor);
        Assert.Equal((ushort)10, result.VigorCost);
        Assert.Equal(5U, result.UpdatedVigor);
        Assert.True(result.SendServerInfo);
        Assert.True(result.VigorChanged);
    }

    [Fact]
    public void ApplyMsgDataPlayerJumpRideVigorFloorsAtZero()
    {
        var result = LegacyMovementSideEffects.ApplyMsgDataPlayerJumpRideVigor(
            currentVigor: 9,
            legacyDistance: 16);

        Assert.Equal((ushort)12, result.VigorCost);
        Assert.Equal(0U, result.UpdatedVigor);
        Assert.True(result.SendServerInfo);
        Assert.True(result.VigorChanged);
    }

    [Fact]
    public void ApplyMsgDataPlayerJumpRideVigorStillRequestsServerInfoWhenCostIsZero()
    {
        var result = LegacyMovementSideEffects.ApplyMsgDataPlayerJumpRideVigor(
            currentVigor: 9,
            legacyDistance: 1);

        Assert.Equal((ushort)0, result.VigorCost);
        Assert.Equal(9U, result.UpdatedVigor);
        Assert.True(result.SendServerInfo);
        Assert.False(result.VigorChanged);
    }

    [Fact]
    public void ApplyMsgDataPlayerJumpRideVigorRejectsNegativeDistance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyMovementSideEffects.ApplyMsgDataPlayerJumpRideVigor(9, -1));
    }

    private static CharacterSnapshot CreateCharacter(IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
            Body: 1008,
            Face: 154,
            Hair: 27,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1015,
            X: 830,
            Y: 584,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61_258,
            MinMana: 5000,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "",
            Attributes: attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
