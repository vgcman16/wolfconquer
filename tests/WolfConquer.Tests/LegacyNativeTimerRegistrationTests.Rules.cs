using System.Reflection;
using System.Threading;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeTimerRegistrationTests
{
    [Theory]
    [InlineData(-1,0)] [InlineData(0,1)] [InlineData(1,2)] [InlineData(int.MaxValue,4)]
    public void BothWholeRuleConstructorsKeepDelegatePeriodPriorityAndPeriodicFlag(int period,int priority)
    {
        Action<int> action=_=>{};Action<string,int> generic=(_,_)=>{};
        var a=new LegacyNativeTimerRule(action,period,(ThreadPriority)priority);
        var b=new LegacyNativeTimerRule<string>(generic,period,(ThreadPriority)priority);
        Assert.Same(action,a.action_0);Assert.Same(generic,b.action_0);
        Assert.Equal(period,a.int_0);Assert.Equal(period,b.int_0);
        Assert.True(a.bool_0);Assert.True(b.bool_0);
        Assert.Equal((ThreadPriority)priority,a.threadPriority_0);Assert.Equal((ThreadPriority)priority,b.threadPriority_0);
    }

    [Fact]
    public void WholeLazyConstructorsOnlyChangePeriodicFlagAndRetainNullActions()
    {
        var a=new LegacyNativeLazyDelegate(null!,17,ThreadPriority.Highest);
        var b=new LegacyNativeLazyDelegate<object>(null!,23,ThreadPriority.Lowest);
        Assert.Null(a.action_0);Assert.Null(b.action_0);Assert.False(a.bool_0);Assert.False(b.bool_0);
        Assert.Equal(17,a.int_0);Assert.Equal(23,b.int_0);
        Assert.Equal(ThreadPriority.Highest,a.threadPriority_0);Assert.Equal(ThreadPriority.Lowest,b.threadPriority_0);
    }

    [Fact]
    public void RulesAndPoolsRetainTheirNativePublicDefaultArgumentsAndNoCtorRun()
    {
        Assert.Equal(32,typeof(LegacyNativeStaticPool).GetConstructors().Single().GetParameters()[0].DefaultValue);
        Assert.Equal(new object[]{6,32},typeof(LegacyNativeStandalonePool).GetConstructors().Single().GetParameters().Select(p=>p.DefaultValue));
        Assert.Equal(ThreadPriority.Normal,typeof(LegacyNativeTimerRule).GetConstructors().Single().GetParameters()[2].DefaultValue);
        var direct=typeof(LegacyNativeTimerRule).GetField("action_0",BindingFlags.NonPublic|BindingFlags.Instance)!;
        Assert.False(direct.IsInitOnly);Assert.False(direct.IsPublic);
    }
}
