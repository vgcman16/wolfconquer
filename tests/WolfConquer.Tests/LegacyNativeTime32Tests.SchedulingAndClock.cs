using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTime32Tests
{
    [Theory]
    [InlineData(10, 5, 14, false)]
    [InlineData(10, 5, 15, true)]
    [InlineData(10, 5, 16, true)]
    [InlineData(int.MaxValue, 1, -1, true)]
    [InlineData(int.MinValue, -1, int.MaxValue, true)]
    [InlineData(-100, -50, -151, false)]
    [InlineData(-100, -50, -150, true)]
    public void ExplicitNextInputsRetainInclusiveSignedDueThresholdAndWrap(int value, int due, int time, bool expected)
    {
        var stamp = new LegacyNativeTime32(value);
        Assert.Equal(expected, stamp.Next(due, time));
        Assert.Equal(value, stamp.Value);
    }

    [Theory]
    [InlineData(5, 7)]
    [InlineData(-5, 7)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MinValue, -1)]
    [InlineData(0, -1)]
    public void ExplicitSetAndSetSecondsOverwriteWithTimePlusDueRatherThanAddingToTheOldValue(int due, int time)
    {
        var value = new LegacyNativeTime32(123);
        value.Set(due, time);
        Assert.Equal(unchecked((int)((long)time + due)), value.Value);
        value.SetSeconds(due, time);
        Assert.Equal(unchecked((int)((long)time + (long)due * 1000)), value.Value);
    }

    [Fact]
    public void ActualClockAndOptionalParameterMetadataKeepOriginalWinmmAndNoConversionOperators()
    {
        Type type = typeof(LegacyNativeTime32);
        var import = type.GetMethod("timeGetTime", BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(type, import.ReturnType); Assert.Empty(import.GetParameters());
        var dll = import.GetCustomAttribute<DllImportAttribute>()!;
        Assert.Equal("winmm.dll", dll.Value); Assert.Equal("timeGetTime", dll.EntryPoint);
        Assert.Equal(CallingConvention.Winapi, dll.CallingConvention); Assert.True(dll.PreserveSig);
        Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name is "op_Implicit" or "op_Explicit" or "op_Addition");
        var next = type.GetMethod("Next")!.GetParameters();
        Assert.All(next, parameter => { Assert.True(parameter.IsOptional); Assert.Equal(0, parameter.DefaultValue); });
        foreach (string method in new[] { "Set", "SetSeconds" })
        {
            var args = type.GetMethod(method)!.GetParameters();
            Assert.False(args[0].IsOptional); Assert.True(args[1].IsOptional); Assert.Equal(0, args[1].DefaultValue);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealWindowsClockBoundsBothOriginalStructReturnAndNowProperty(bool property)
    {
        Assert.True(OperatingSystem.IsWindows());
        uint before = ObserveRealWindowsTicks();
        LegacyNativeTime32 observed = property ? LegacyNativeTime32.Now : LegacyNativeTime32.timeGetTime();
        uint after = ObserveRealWindowsTicks();
        AssertClockBracket(before, unchecked((uint)observed.Value), after);
        Assert.Equal(observed.Value, observed.TotalMilliseconds);
    }

    [Fact]
    public void DefaultZeroTimeSetBranchesSampleTheRealClockWithoutSleepingOrSubstitutingIt()
    {
        Assert.True(OperatingSystem.IsWindows());
        var stamp = new LegacyNativeTime32(123);
        uint before = ObserveRealWindowsTicks();
        stamp.Set(5);
        uint after = ObserveRealWindowsTicks();
        AssertClockBracket(before, unchecked((uint)(stamp.Value - 5)), after);
        before = ObserveRealWindowsTicks();
        stamp.SetSeconds(2, time: 0);
        after = ObserveRealWindowsTicks();
        AssertClockBracket(before, unchecked((uint)(stamp.Value - 2000)), after);
    }

    [Fact]
    public void DefaultNextClockBranchStillUsesInclusiveSignedMinimumAfterOverflow()
    {
        Assert.True(OperatingSystem.IsWindows());
        Assert.True(new LegacyNativeTime32(int.MinValue).Next());
        Assert.True(new LegacyNativeTime32(int.MaxValue).Next(due: 1));
    }

    // Independent read of the same genuine OS API; not a provider replacement,
    // fake clock, original assembly invocation or DateTime approximation.
    [DllImport("winmm.dll", EntryPoint = "timeGetTime")]
    private static extern uint ObserveRealWindowsTicks();

    private static void AssertClockBracket(uint before, uint observed, uint after)
    {
        uint elapsed = unchecked(after - before);
        Assert.InRange(elapsed, 0u, 60000u);
        Assert.InRange(unchecked(observed - before), 0u, elapsed);
    }
}
