using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-3)]
    public void FlagConstructorSamplesRealNowAndRetainsVerbatimInputs(int invoker)
    {
        var before = DateTime.Now;
        var flag = new Flags.Flag(-7, -11, true, invoker);
        var after = DateTime.Now;
        Assert.Equal(-7, flag.Key);
        Assert.Equal(-11, flag.Secounds);
        Assert.Equal(invoker, flag.InvokerSecouds);
        Assert.True(flag.RemoveOnDead);
        Assert.InRange(flag.Timer, before, after);
        if (invoker == 0) Assert.Equal(default(DateTime), flag.TimerInvoker);
        else Assert.InRange(flag.TimerInvoker, before, after);
    }

    [Fact]
    public void ExpireIncludesItsExactDeadlineWithoutMutatingTheTimer()
    {
        var timer = new DateTime(2000, 1, 1);
        var flag = new Flags.Flag(6, 10, false, 0) { Timer = timer };
        Assert.False(flag.Expire(timer.AddSeconds(10).AddTicks(-1)));
        Assert.True(flag.Expire(timer.AddSeconds(10)));
        Assert.True(flag.Expire(timer.AddSeconds(11)));
        Assert.Equal(timer, flag.Timer);
        Assert.Equal(10, flag.Secounds);
    }

    [Fact]
    public void CheckInvokeUsesCallerDeadlineButRenewsWithActualNowNotCallerTime()
    {
        var timer = new DateTime(2000, 1, 1);
        var flag = new Flags.Flag(6, 10, false, 3) { TimerInvoker = timer };
        Assert.False(flag.CheckInvoke(timer.AddSeconds(3).AddTicks(-1)));
        Assert.Equal(timer, flag.TimerInvoker);
        var before = DateTime.Now;
        Assert.True(flag.CheckInvoke(timer.AddSeconds(3)));
        var after = DateTime.Now;
        Assert.InRange(flag.TimerInvoker, before, after);
        Assert.NotEqual(timer.AddSeconds(3), flag.TimerInvoker);
        Assert.False(flag.CheckInvoke(timer.AddSeconds(3)));
        flag.InvokerSecouds = 0;
        var retained = flag.TimerInvoker;
        Assert.True(flag.CheckInvoke(DateTime.MinValue));
        Assert.Equal(retained, flag.TimerInvoker);
    }

    [Fact]
    public void InLifeMeansExpiredAndNeitherExpiryNorInvokeRemovesRegistration()
    {
        var flags = new Flags(32);
        var timer = new DateTime(2000, 1, 1);
        Assert.False(flags.InLife(6, timer));
        Assert.False(flags.CheckInvoke(6, timer));
        flags.TryAdd(6, 10, false, 0);
        var value = flags.ArrayFlags[6];
        value.Timer = timer;
        Assert.False(flags.InLife(6, timer.AddSeconds(9)));
        Assert.True(flags.InLife(6, timer.AddSeconds(10)));
        Assert.True(flags.CheckInvoke(6, DateTime.MinValue));
        Assert.Same(value, flags.ArrayFlags[6]);
        Assert.True(flags.ContainFlag(6));
        Assert.True(flags.Contain(6));
    }

    [Theory]
    [InlineData("reset", 100)]
    [InlineData("cap", 60)]
    [InlineData("extend", 18)]
    public void UpdateFlagRetainsTheSameObjectAndNativeThreeBranchOrdering(string mode, int expectedSeconds)
    {
        var flags = new Flags(32);
        Assert.False(flags.UpdateFlag(6, 1, true, 1));
        flags.TryAdd(6, 7, true, 3);
        var value = flags.ArrayFlags[6];
        var cache = flags.GetFlags();
        var invoker = value.TimerInvoker;
        var old = mode == "cap" ? DateTime.Now.AddHours(1) : new DateTime(2000, 1, 1);
        value.Timer = old;
        var before = DateTime.Now;
        Assert.True(flags.UpdateFlag(6, mode == "extend" ? 11 : 100, mode == "reset", mode == "reset" ? 3 : 60));
        var after = DateTime.Now;
        Assert.Same(value, flags.ArrayFlags[6]);
        Assert.Same(cache, flags.GetFlags());
        Assert.Equal(expectedSeconds, value.Secounds);
        if (mode == "extend") Assert.Equal(old, value.Timer);
        else Assert.InRange(value.Timer, before, after);
        Assert.Equal(invoker, value.TimerInvoker);
        Assert.True(value.RemoveOnDead);
    }

    [Fact]
    public void ExtendUsesUncheckedSecondsAdditionAfterItsDeadlineComparison()
    {
        var flags = new Flags(32);
        flags.TryAdd(6, int.MaxValue, false, 0);
        var value = flags.ArrayFlags[6];
        value.Timer = new DateTime(1900, 1, 1); // +Int32.Max seconds remains before current Now.
        var timer = value.Timer;
        Assert.True(flags.UpdateFlag(6, 1, false, 0));
        Assert.Equal(int.MinValue, value.Secounds);
        Assert.Equal(timer, value.Timer);
    }

    [Theory]
    [InlineData("expire")]
    [InlineData("invoke")]
    [InlineData("update")]
    public void DateBoundsFaultsPropagateWithoutRenewalOrRegistrationRepair(string method)
    {
        var flags = new Flags(32);
        flags.TryAdd(6, 1, false, 1);
        var value = flags.ArrayFlags[6];
        value.Timer = value.TimerInvoker = DateTime.MaxValue;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            if (method == "expire") flags.InLife(6, DateTime.MaxValue);
            else if (method == "invoke") flags.CheckInvoke(6, DateTime.MaxValue);
            else flags.UpdateFlag(6, 9, false, 60);
        });
        Assert.Equal(DateTime.MaxValue, value.Timer);
        Assert.Equal(DateTime.MaxValue, value.TimerInvoker);
        Assert.Equal(1, value.Secounds);
        Assert.Same(value, flags.ArrayFlags[6]);
        Assert.True(flags.Contain(6));
    }
}
