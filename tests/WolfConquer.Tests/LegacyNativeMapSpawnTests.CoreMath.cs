using WolfConquer.Core.World;
using Angle = WolfConquer.Core.World.LegacyNativePlayerMapFlags.ConquerAngle;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapSpawnTests
{
    private static byte AngleExpression(ushort x, ushort y, ushort targetX, ushort targetY)
    {
        double radians = Math.Atan2(targetY - y, targetX - x);
        if (radians < 0) radians += Math.PI * 2;
        double degrees = 360 - radians * 180 / Math.PI;
        return (byte)(unchecked((byte)(int)(7 - Math.Floor(degrees) / 45 % 8 - 1)) % 8);
    }

    [Fact]
    public void ActualCoreAngleMatchesAll16723FrameworkProbeControlsAndItsIndependentDigest()
    {
        ushort[,] axes = { {100,100,100,100,6}, {100,100,101,100,6}, {100,100,99,100,2},
            {100,100,100,101,0}, {100,100,100,99,4}, {0,0,65535,0,6}, {65535,0,0,0,2},
            {0,65535,0,0,4}, {0,0,0,65535,0} };
        int controls = 0; uint digest = 2166136261, independent = 2166136261;
        for (int i = 0; i < axes.GetLength(0); i++)
        {
            byte actual = (byte)LegacyNativeRoleCore.GetAngle(axes[i,0], axes[i,1], axes[i,2], axes[i,3]);
            Assert.Equal((byte)axes[i,4], actual); controls++;
            Assert.Equal(AngleExpression(axes[i,0], axes[i,1], axes[i,2], axes[i,3]), actual); controls++;
        }
        void Vector(ushort x, ushort y, ushort xx, ushort yy)
        {
            byte actual = (byte)LegacyNativeRoleCore.GetAngle(x,y,xx,yy), expected = AngleExpression(x,y,xx,yy);
            Assert.Equal(expected, actual); controls++;
            digest = unchecked((digest ^ actual) * 16777619u);
            independent = unchecked((independent ^ expected) * 16777619u);
        }
        for (int dx = -64; dx <= 64; dx++) for (int dy = -64; dy <= 64; dy++)
            Vector(100, 100, (ushort)(100 + dx), (ushort)(100 + dy));
        ushort[] values = [0,1,2,32767,32768,65533,65534,65535];
        foreach (ushort x in values) foreach (ushort y in values) Vector(x,y,y,x);
        Assert.Equal(16723, controls); Assert.Equal(2201143353u, digest); Assert.Equal(2201143353u, independent);
        Assert.Equal(new byte[] { 7, 1, 3, 5 }, new[] {
            (byte)LegacyNativeRoleCore.GetAngle(100,100,101,101), (byte)LegacyNativeRoleCore.GetAngle(100,100,99,101),
            (byte)LegacyNativeRoleCore.GetAngle(100,100,99,99), (byte)LegacyNativeRoleCore.GetAngle(100,100,101,99) });
    }

    [Theory]
    [InlineData(0,10,11)] [InlineData(1,9,11)] [InlineData(2,9,10)] [InlineData(3,9,9)]
    [InlineData(4,10,9)] [InlineData(5,11,9)] [InlineData(6,11,10)] [InlineData(7,11,11)]
    [InlineData(8,10,10)] [InlineData(255,10,10)]
    public void ActualCoreIncXYRetainsEachNativeDirectionAndUnknownNoMove(byte facing, ushort xx, ushort yy)
    {
        ushort x = 10, y = 10; LegacyNativeRoleCore.IncXY((Angle)facing, ref x, ref y);
        Assert.Equal(xx, x); Assert.Equal(yy, y);
    }

    [Fact]
    public void CoreIncXYRetainsNativeUshortWrap()
    {
        ushort x = 0, y = 0; LegacyNativeRoleCore.IncXY(Angle.North, ref x, ref y);
        Assert.Equal(ushort.MaxValue, x); Assert.Equal(ushort.MaxValue, y);
        LegacyNativeRoleCore.IncXY(Angle.South, ref x, ref y); Assert.Equal((ushort)0, x); Assert.Equal((ushort)0, y);
    }

    [Theory]
    [InlineData(1900,1,1,0,0,0,1000000UL)]
    [InlineData(1900,1,2,0,0,0,102000000UL)]
    [InlineData(1900,2,1,0,0,0,103101000000UL)]
    [InlineData(2000,1,1,0,0,0,1000000001000000UL)]
    [InlineData(2000,2,29,23,59,58,1000105929235958UL)]
    public void WholeNativeTqTimerKeepsItsDistinctMonthDayOfYearDayAndClockFields(int year, int month, int day,
        int hour, int minute, int second, ulong expected) =>
        Assert.Equal(expected, LegacyNativeRoleCore.TqTimer(new DateTime(year,month,day,hour,minute,second)));
}
