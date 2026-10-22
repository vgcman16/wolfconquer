using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLevelPrerequisiteTests
{
    [Theory]
    [InlineData(17, 100)]
    [InlineData(0, 100)]
    [InlineData(100, 100)]
    [InlineData(-1, 100)]
    [InlineData(int.MaxValue, 100)]
    [InlineData(int.MinValue, 100)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    [InlineData(50, -100)]
    [InlineData(int.MaxValue, int.MinValue)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void RateUsesExactSignedModuloThenStrictComparisonAndConsumesOneRealDraw(int value, int discriminant)
    {
        using var scope = new Scope();
        int draw = scope.Control.Next();
        bool expected = value > draw % discriminant;
        Assert.Equal(expected, LegacyNativeRoleCore.Rate(value, discriminant));
        Assert.Same(scope.Actual, LegacyNativePoolRandom.GetRandom);
        Assert.Equal(scope.Control.Next(), scope.Actual.Next());
        Assert.Equal(scope.ResetMarker, LegacyNativePoolRandom.ResetRandom);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(int.MinValue)]
    public void ZeroDiscriminantFaultStillConsumesExactlyOneRealDrawBeforeModulo(int value)
    {
        using var scope = new Scope();
        Assert.Throws<DivideByZeroException>(() => LegacyNativeRoleCore.Rate(value, 0));
        _ = scope.Control.Next();
        Assert.Equal(scope.Control.Next(), scope.Actual.Next());
        Assert.Same(scope.Actual, LegacyNativePoolRandom.GetRandom);
        Assert.Equal(scope.ResetMarker, LegacyNativePoolRandom.ResetRandom);
    }

    [Fact]
    public void RateRereadsGenuineMutablePoolServiceAndDoesNotAdvanceReplacedOwner()
    {
        using var scope = new Scope();
        Assert.Equal(17 > scope.Control.Next() % 100, LegacyNativeRoleCore.Rate(17, 100));
        var replacement = new LegacyNativeSafeRandom(97);
        var control = new LegacyNativeSafeRandom(97);
        LegacyNativePoolRandom.GetRandom = replacement;
        Assert.Equal(29 > control.Next() % 37, LegacyNativeRoleCore.Rate(29, 37));
        Assert.Equal(control.Next(), replacement.Next());
        Assert.Equal(scope.Control.Next(), scope.Actual.Next());
        Assert.Same(replacement, LegacyNativePoolRandom.GetRandom);
        Assert.Equal(scope.ResetMarker, LegacyNativePoolRandom.ResetRandom);
    }

    [Fact]
    public void MissingActualPoolServiceFaultsBeforeZeroModuloWithoutFallbackDraw()
    {
        using var scope = new Scope();
        LegacyNativePoolRandom.GetRandom = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeRoleCore.Rate(0, 0));
        Assert.Equal(scope.Control.Next(), scope.Actual.Next());
        Assert.Equal(scope.ResetMarker, LegacyNativePoolRandom.ResetRandom);
    }

    [Fact]
    public void ActualPlayerBirthAndRateShareOneOrderedNativePoolDrawStream()
    {
        using var scope = new Scope();
        _ = new LegacyNativeGameClientRuntime();
        _ = scope.Control.Next();
        Assert.Equal(31 > scope.Control.Next() % 101, LegacyNativeRoleCore.Rate(31, 101));
        _ = new LegacyNativeGameClientRuntime();
        _ = scope.Control.Next();
        Assert.Equal(-1 > scope.Control.Next() % -37, LegacyNativeRoleCore.Rate(-1, -37));
        Assert.Equal(scope.Control.Next(), scope.Actual.Next());
        Assert.Equal(scope.ResetMarker, LegacyNativePoolRandom.ResetRandom);
    }
}
