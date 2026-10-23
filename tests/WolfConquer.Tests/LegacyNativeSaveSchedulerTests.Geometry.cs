using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSaveSchedulerTests
{
    [Theory]
    [InlineData((ushort)0, (ushort)0, (ushort)0, (ushort)0, (short)0)]
    [InlineData((ushort)1, (ushort)9, (ushort)4, (ushort)3, (short)6)]
    [InlineData((ushort)65535, (ushort)0, (ushort)0, (ushort)0, (short)0)]
    [InlineData((ushort)65535, (ushort)65535, (ushort)0, (ushort)0, (short)-1)]
    [InlineData((ushort)32768, (ushort)10, (ushort)0, (ushort)0, (short)10)]
    public void FootballDistancePreservesSignedShortNarrowing(ushort x, ushort y, ushort a, ushort b, short expected) =>
        Assert.Equal(expected, LegacyNativeMsgFootball.GetDistance(x, y, a, b));

    [Theory]
    [InlineData((ushort)80, (ushort)123, true, false)]
    [InlineData((ushort)95, (ushort)138, true, false)]
    [InlineData((ushort)77, (ushort)120, true, false)]
    [InlineData((ushort)76, (ushort)119, false, false)]
    [InlineData((ushort)85, (ushort)39, false, true)]
    [InlineData((ushort)100, (ushort)54, false, true)]
    [InlineData((ushort)0, (ushort)0, false, false)]
    public void GoalRegionsUseSixNativeDiagonalCentersAndInclusiveDistanceThree(ushort x, ushort y, bool left, bool right)
    {
        var f = new LegacyNativeMsgFootball(LegacyNativeTournamentType.FootBall);
        Assert.Equal(left, f.LeftNaked(x, y)); Assert.Equal(right, f.RightNaked(x, y));
    }
}
