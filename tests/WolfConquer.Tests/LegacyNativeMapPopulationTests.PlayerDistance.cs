namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapPopulationTests
{
    [Theory]
    [InlineData(0, 0, 65535, 65535, -1)]
    [InlineData(0, 0, 32768, 32768, -32768)]
    [InlineData(10, 20, 12, 30, 10)]
    [InlineData(65535, 0, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(10, 10, 28, 28, 18)]
    [InlineData(10, 10, 29, 10, 19)]
    public Task WholePlayerDistanceFamiliesUseActualCurrentAndPreviousSlotsWithNativeSignedWidth(
        ushort x, ushort y, ushort targetX, ushort targetY, short expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.X = x; scope.Player.Y = y;
        Assert.Equal(expected, scope.Player.GetMyDistance(targetX, targetY));
        scope.Player.X = 11; scope.Player.Y = 12;
        scope.Player.Px = x; scope.Player.Py = y;
        Assert.Equal(expected, scope.Player.OldGetDistance(targetX, targetY));
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(19, 18, true)]
    [InlineData(18, 17, false)]
    [InlineData(19, 19, false)]
    [InlineData(0, 0, false)]
    [InlineData(19, 0, true)]
    public Task WholePlayerInViewKeepsStrictOldAndInclusiveNewThresholdUnlikeMonster(
        ushort oldDistance, ushort currentDistance, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.X = currentDistance; scope.Player.Y = 0;
        scope.Player.Px = oldDistance; scope.Player.Py = 0;
        Assert.Equal(expected, scope.Player.InView(0, 0, 18));
        Assert.Empty(scope.Queue);
    });
}
