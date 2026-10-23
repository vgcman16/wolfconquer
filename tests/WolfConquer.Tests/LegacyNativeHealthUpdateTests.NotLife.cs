namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHealthUpdateTests
{
    [Theory]
    [InlineData(int.MinValue, true)]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(int.MaxValue, false)]
    public Task NotLifeReadsTheActualSignedHpSlotWithoutFlagsOrSideEffects(int current,
        bool expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.SetPreexistingHealth(100, current);
        Assert.Equal(expected, scope.Player.NotLife);
        Assert.Equal(!expected, scope.Player.Alive);
        Assert.Equal(current, scope.Player.HitPoints);
        Assert.Empty(scope.Queue);
        Assert.Equal(1, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NotLifeSetterRetainsTheOriginalUnconditionalThrowInsteadOfChangingHp(bool value) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.SetPreexistingHealth(100, -9);
        Assert.Throws<NotImplementedException>(() => scope.Player.NotLife = value);
        Assert.Equal(-9, scope.Player.HitPoints);
        Assert.Equal(100u, scope.Client.Status.MaxHitpoints);
        Assert.True(scope.Player.NotLife);
        Assert.Empty(scope.Queue);
        Assert.Equal(1, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });
}
