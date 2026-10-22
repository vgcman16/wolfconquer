using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLevelPrerequisiteTests
{
    [Fact]
    public void ActualPlayerBirthHasThreeZeroMutablePhysicalFieldsWithNativeWidths()
    {
        using var scope = new Scope();
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        Assert.Equal(0u, player.FirstClass);
        Assert.Equal(0u, player.SecoundeClass);
        Assert.Equal(0ul, player.Experience);
        var type = typeof(LegacyNativePlayerRuntime);
        foreach (var (name, fieldType) in new[]
        {
            (nameof(LegacyNativePlayerRuntime.FirstClass), typeof(uint)),
            (nameof(LegacyNativePlayerRuntime.SecoundeClass), typeof(uint)),
            (nameof(LegacyNativePlayerRuntime.Experience), typeof(ulong)),
        })
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public)!;
            Assert.Equal(fieldType, field.FieldType);
            Assert.False(field.IsInitOnly); Assert.False(field.IsStatic); Assert.False(field.IsLiteral);
            Assert.Null(type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }
        _ = scope.Control.Next(); // Genuine Player.MyRandom field birth consumed one shared seed draw.
        Assert.Equal(scope.Control.Next(), scope.Actual.Next());
        Assert.Equal(scope.ResetMarker, LegacyNativePoolRandom.ResetRandom);
    }

    [Fact]
    public void PhysicalFieldsRetainFullUnsignedWidthsAndMutateThroughSamePlayerAlias()
    {
        using var scope = new Scope();
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        var alias = player;
        player.FirstClass = uint.MaxValue;
        player.SecoundeClass = uint.MaxValue - 1;
        player.Experience = ulong.MaxValue;
        Assert.Equal(uint.MaxValue, alias.FirstClass);
        Assert.Equal(uint.MaxValue - 1, alias.SecoundeClass);
        Assert.Equal(ulong.MaxValue, alias.Experience);
        alias.FirstClass = 16051;
        alias.SecoundeClass = 1;
        alias.Experience = ulong.MaxValue - uint.MaxValue;
        Assert.Same(player, client.Player);
        Assert.Equal(16051u, player.FirstClass); Assert.Equal(1u, player.SecoundeClass);
        Assert.Equal(ulong.MaxValue - uint.MaxValue, player.Experience);
        Assert.Equal(0u, player.Class); Assert.Equal((ushort)0, player.Level);
    }

    [Fact]
    public void SeparateActualPlayerStoresStayIndependentUntilExplicitClientReferenceAdoption()
    {
        using var scope = new Scope();
        var first = new LegacyNativeGameClientRuntime();
        var second = new LegacyNativeGameClientRuntime();
        var oldSecondPlayer = second.Player;
        Assert.NotSame(first.Player, oldSecondPlayer);
        first.Player.FirstClass = 1051; first.Player.SecoundeClass = 16051; first.Player.Experience = ulong.MaxValue;
        second.Player.FirstClass = 4001; second.Player.SecoundeClass = 5001; second.Player.Experience = 7;
        Assert.Equal(1051u, first.Player.FirstClass); Assert.Equal(16051u, first.Player.SecoundeClass);
        Assert.Equal(ulong.MaxValue, first.Player.Experience);
        Assert.Equal(4001u, second.Player.FirstClass); Assert.Equal(5001u, second.Player.SecoundeClass);
        Assert.Equal(7ul, second.Player.Experience);
        second.Player = first.Player; // Genuine existing direct reference assignment, not a UID lookup or clone.
        second.Player.Experience = 19;
        Assert.Equal(19ul, first.Player.Experience);
        Assert.Same(first, second.Player.Owner);
        Assert.Equal(7ul, oldSecondPlayer.Experience);
        Assert.Equal(4001u, oldSecondPlayer.FirstClass); Assert.Equal(5001u, oldSecondPlayer.SecoundeClass);
        _ = scope.Control.Next(); _ = scope.Control.Next();
        Assert.Equal(scope.Control.Next(), scope.Actual.Next());
    }
}
