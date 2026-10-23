using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Chi = WolfConquer.Core.World.LegacyNativeChiRuntime;
using Attr = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiAttribute;
using Power = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiPower;
using Kind = WolfConquer.Core.World.LegacyNativeMsgChiInfo.ChiPowerType;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeChiTests
{
    [Fact]
    public void ChiMetadataPreservesNativeWidthsValuesAndOneByteMarker()
    {
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgChiInfo>());
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(Kind)));
        Assert.Equal(new[] {"None","Dragon","Phoenix","Tiger","Turtle"}, Enum.GetNames<Kind>());
        Assert.Equal(new byte[] {0,1,2,3,4}, Enum.GetValues<Kind>().Select(value => (byte)value));
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(LegacyNativeMsgChiInfo.LockedFlags)));
        Assert.Equal(new byte[] {0,1,2,4,8,15}, Enum.GetValues<LegacyNativeMsgChiInfo.LockedFlags>().Select(value => (byte)value));
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(LegacyNativeMsgChiInfo.Action)));
        Assert.Equal(new ushort[] {0,1,2}, Enum.GetValues<LegacyNativeMsgChiInfo.Action>().Select(value => (ushort)value));
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Stat)));
        Assert.Equal(new[] {"None","CriticalStrike","SkillCriticalStrike","Immunity","Breakthrough","Counteraction","HPAdd","AddAttack","AddMagicAttack","AddMagicDefense","PhysicalDamageIncrease","MagicDamageIncrease","PhysicalDamageDecrease","MagicDamageDecrease","LukyStrike"}, Enum.GetNames<Stat>());
    }

    [Theory]
    [InlineData(1,0,-5)] [InlineData(6,0,-40)] [InlineData(0,0,-11)]
    [InlineData(1,9,0)] [InlineData(999,65535,72805)]
    [InlineData(9,65535,32742)] [InlineData(7,1,-33)]
    public Task ScoreKeepsFrameworkX86UnsignedConversionThenFinalSignedWrap(int type, int value, int expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power { UnLocked = true, Attributes = new[] { new Attr((Stat)type, (ushort)value, false) } };
        Assert.Equal(expected, power.Score);
    });

    [Fact]
    public Task NativeUIntTermsBindToInt64SumThenWrapOnlyFinalIntScore() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power { UnLocked = true, Attributes = new[] {new Attr(Stat.CriticalStrike,0,false),new Attr(Stat.CriticalStrike,0,true)} };
        Assert.Equal(-10, power.Score); // Real Enumerable.Sum binds the uint selector to Int64.
        power.Attributes = Enumerable.Repeat(new Attr(Stat.CriticalStrike,0,false),4).ToArray();
        Assert.Equal(-20,power.Score);
        power.Attributes = new[] {new Attr(Stat.CriticalStrike,0,false),new Attr(Stat.CriticalStrike,200,false)};
        Assert.Equal(95,power.Score); // 4294967291 +100, then final low32 signed narrowing.
        power.UnLocked = false;
        Assert.Equal(0, power.Score);
        var chi = new Chi(7) { Dragon = new Power { UnLocked = true, Attributes = new[] {new Attr(Stat.CriticalStrike,0,false)} } };
        Assert.Equal(4294967291u, chi.AllScore());
    });

    [Theory]
    [InlineData(25.0,25,100)] [InlineData(1.0,1,10)] [InlineData(0.25,25,100)]
    [InlineData(100.0,1,10)] [InlineData(1000.0,1,10)] [InlineData(-1.5,-15,10)]
    [InlineData(0.00001,0,1)]
    public Task DoubleRateKeepsNativeScalingRoundingAndOneActualSharedDraw(double percent, int numerator, int denominator) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var control = new LegacyNativeSafeRandom(71); _ = control.Next();
        bool expected = numerator > control.Next() % denominator;
        Assert.Equal(expected, LegacyNativeRoleCore.Rate(percent));
        Assert.Equal(control.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task DoubleRateZeroSkipsDrawAndUIntStudyLuckUsesDoubleNotIntOverload() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var control = new LegacyNativeSafeRandom(71); _ = control.Next();
        Assert.False(LegacyNativeRoleCore.Rate(0.0));
        uint studyLuck = 10000;
        Assert.Equal(1 > control.Next() % 10, LegacyNativeRoleCore.Rate(studyLuck / 100));
        Assert.Equal(control.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    [InlineData(2147483648.0)] [InlineData(-2147483649.0)]
    public Task DoubleRateNonfiniteAndOutOfRangeIntsKeepNativeX86SentinelAndOneDraw(double percent) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var control = new LegacyNativeSafeRandom(71); _ = control.Next(); _ = control.Next();
        Assert.False(LegacyNativeRoleCore.Rate(percent));
        Assert.Equal(control.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task DoubleRateNaNReachesNativeGuardMessageWithoutConsumingSharedDraw() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var control = new LegacyNativeSafeRandom(71); _ = control.Next();
        TextWriter original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.True(LegacyNativeRoleCore.Rate(double.NaN));
            Assert.Equal("Problem While in Kernel 2  ===============" + Environment.NewLine, output.ToString());
            Assert.Equal(control.Next(), LegacyNativePoolRandom.GetRandom.Next());
        }
        finally { Console.SetOut(original); }
    });
}
