using System.Reflection;
using System.Threading;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTimerRegistrationTests
{
    private sealed class CounterScope : IDisposable
    {
        private readonly int old=LegacyNativeTimerSubscriptionBase.int_0;
        internal CounterScope() { LegacyNativeTimerSubscriptionBase.int_0=int.MinValue; }
        public void Dispose() { LegacyNativeTimerSubscriptionBase.int_0=old; }
    }

    [Fact]
    public void WholeSubscriptionBirthUsesSharedWrappingCounterAndNativeInitialDeadline()
    {
        if(!OperatingSystem.IsWindows()) return;
        using var scope=new CounterScope();int before=LegacyNativeTime32.Now.Value;
        var first=new LegacyNativeTimerSubscription(new LegacyNativeTimerRule(_=>{},25));
        var second=new LegacyNativeTimerSubscription<string>(new LegacyNativeTimerRule<string>((_,_)=>{},25),"x");
        int after=LegacyNativeTime32.Now.Value;
        Assert.Equal(int.MinValue+1,first.GetHashCode());Assert.Equal(int.MinValue+2,second.GetHashCode());
        Assert.True(first.bool_0);Assert.False(first.bool_1);
        Assert.InRange(unchecked(first.time32_0.Value-before),0,unchecked(after-before));
        LegacyNativeTimerSubscriptionBase.int_0=int.MaxValue;
        var wrapped=new LegacyNativeTimerSubscription(null!);Assert.Equal(int.MinValue,wrapped.GetHashCode());
        ((IDisposable)first).Dispose();((IDisposable)second).Dispose();((IDisposable)wrapped).Dispose();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteCallbackReschedulesPeriodicAndDisposesLazySubscription(bool lazy)
    {
        if(!OperatingSystem.IsWindows()) return;
        using var scope=new CounterScope();int hits=0,seen=0;
        Action<int> action=time=>{hits++;seen=time;};
        LegacyNativeTimerRule rule=lazy?new LegacyNativeLazyDelegate(action,321,ThreadPriority.AboveNormal):new LegacyNativeTimerRule(action,321,ThreadPriority.AboveNormal);
        var sub=new LegacyNativeTimerSubscription(rule);
        Assert.Same(action.Method,sub.vmethod_2());Assert.Equal(ThreadPriority.AboveNormal,sub.vmethod_3());
        int before=LegacyNativeTime32.Now.Value;sub.vmethod_0();int after=LegacyNativeTime32.Now.Value;
        Assert.Equal(1,hits);Assert.InRange(unchecked(seen-before),0,unchecked(after-before));
        if(lazy) { Assert.False(sub.bool_0);Assert.Throws<NullReferenceException>(()=>sub.vmethod_2()); }
        else { Assert.True(sub.bool_0);Assert.InRange(unchecked(sub.time32_0.Value-before),321,unchecked(after-before)+321); }
        ((IDisposable)sub).Dispose();sub.vmethod_0();Assert.Equal(1,hits);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteGenericCallbackRetainsParameterReferenceAndLazyDisposition(bool lazy)
    {
        if(!OperatingSystem.IsWindows()) return;
        using var scope=new CounterScope();object owner=new();object? seen=null;int count=0;
        Action<object,int> action=(value,_)=>{seen=value;count++;};
        LegacyNativeTimerRule<object> rule=lazy?new LegacyNativeLazyDelegate<object>(action,17):new LegacyNativeTimerRule<object>(action,17);
        var sub=new LegacyNativeTimerSubscription<object>(rule,owner);sub.vmethod_0();
        Assert.Same(owner,seen);Assert.Equal(1,count);Assert.Equal(!lazy,sub.bool_0);
        ((IDisposable)sub).Dispose();sub.vmethod_0();Assert.Equal(1,count);
    }

    [Fact]
    public void CallbackMayDisposeItsOwnSubscriptionAndSourceDoesNotRescheduleAfterward()
    {
        if(!OperatingSystem.IsWindows()) return;
        using var scope=new CounterScope();LegacyNativeTimerSubscription? sub=null;
        var rule=new LegacyNativeTimerRule(_=>((IDisposable)sub!).Dispose(),60000);
        sub=new(rule);var old=sub.time32_0;sub.vmethod_0();
        Assert.False(sub.bool_0);Assert.Equal(old,sub.time32_0);Assert.Throws<NullReferenceException>(()=>sub.vmethod_3());
    }

    [Fact]
    public void NullAndThrowingActionsKeepOriginalFaultAndMutationPrefix()
    {
        if(!OperatingSystem.IsWindows()) return;
        using var scope=new CounterScope();
        var absent=new LegacyNativeTimerSubscription(new LegacyNativeTimerRule(null!,19));
        var deadline=absent.time32_0;Assert.Throws<NullReferenceException>(()=>absent.vmethod_0());
        Assert.True(absent.bool_0);Assert.Equal(deadline,absent.time32_0);
        var marker=new InvalidOperationException("owned callback");
        var throwing=new LegacyNativeTimerSubscription(new LegacyNativeTimerRule(_=>throw marker,19));
        Assert.Same(marker,Assert.Throws<InvalidOperationException>(()=>throwing.vmethod_0()));
        Assert.True(throwing.bool_0);
        ((IDisposable)absent).Dispose();((IDisposable)throwing).Dispose();
    }

    [Theory]
    [InlineData(int.MinValue)] [InlineData(-1)] [InlineData(0)] [InlineData(int.MaxValue)]
    public void WholeDueComparisonUsesSignedIntegerBoundaryAndObservedTickRange(int deadline)
    {
        if(!OperatingSystem.IsWindows()) return;
        using var scope=new CounterScope();var sub=new LegacyNativeTimerSubscription(null!);
        sub.time32_0=new LegacyNativeTime32(deadline);
        int before=LegacyNativeTime32.Now.Value;bool actual=sub.method_0();int after=LegacyNativeTime32.Now.Value;
        uint elapsed=unchecked((uint)(after-before));Assert.InRange(elapsed,0u,10000u);
        bool matchesObservedRange=false;
        // Enumerate only the bounded observed tick range, including signed wrap.
        // No relative past/future assumption and no replacement production clock.
        for(uint offset=0;offset<=elapsed;offset++)
            matchesObservedRange|=(unchecked(before+(int)offset)>deadline)==actual;
        Assert.True(matchesObservedRange);
        if(deadline==int.MaxValue) Assert.False(actual);
        ((IDisposable)sub).Dispose();
    }
}
