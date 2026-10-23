using System.Threading;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTimerRegistrationTests
{
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)] [InlineData(32)]
    public void BothWholePoolConstructorsAllocateIndependentStateWithoutStartingAnyThreads(int maximum)
    {
        var a=new LegacyNativeStaticPool(maximum);var b=new LegacyNativeStandalonePool(-7,maximum);
        Assert.False(a.bool_0);Assert.False(a.bool_1);Assert.False(b.bool_0);Assert.False(b.bool_1);
        Assert.Equal(maximum,a.int_3);Assert.Equal(maximum,a.int_4);
        Assert.Equal(-7,b.int_3);Assert.Equal(maximum,b.int_4);
        Assert.Null(a.propagationThread);Assert.Null(b.propagationThread);
        Assert.Empty(a.list_0!);Assert.Empty(b.list_0!);Assert.Equal(0,a.Threads);Assert.Equal(0,b.Threads);
        Assert.Empty(a.dictionary_0!);Assert.Empty(b.dictionary_0!);Assert.NotSame(a.object_0,a.object_1);
        Assert.NotSame(a.object_0,b.object_0);Assert.NotSame(a.dictionary_0,b.dictionary_0);
        ((IDisposable)a).Dispose();((IDisposable)b).Dispose();
    }

    [Fact]
    public void BothFullSubscribeOverloadsPublishRealSubscriptionAndLazyDeadlineWithoutRun()
    {
        if(!OperatingSystem.IsWindows()) return;
        using var count=new CounterScope();var pool=new LegacyNativeStaticPool();
        var plain=new LegacyNativeTimerRule(_=>{},17);
        var lazy=new LegacyNativeLazyDelegate<string>((_,_)=>{},60000,ThreadPriority.Highest);
        var first=Assert.IsType<LegacyNativeTimerSubscription>(pool.Subscribe(plain));
        var second=Assert.IsType<LegacyNativeTimerSubscription<string>>(pool.Subscribe(lazy,"same"));
        Assert.Same(first,pool.dictionary_0![first.GetHashCode()]);Assert.Same(second,pool.dictionary_0[second.GetHashCode()]);
        Assert.Equal(ThreadPriority.Highest,second.vmethod_3());Assert.True(second.time32_0.Value!=first.time32_0.Value);
        Assert.False(pool.bool_0);Assert.Empty(pool.list_0!);Assert.Empty(pool.queue_0!);
        ((IDisposable)pool).Dispose();((IDisposable)first).Dispose();((IDisposable)second).Dispose();
    }

    [Fact]
    public void WholeStandaloneSubscribeUsesSameSourceFamiliesAndNoWorkerBirth()
    {
        if(!OperatingSystem.IsWindows()) return;
        using var count=new CounterScope();var pool=new LegacyNativeStandalonePool(0,0);
        var first=Assert.IsType<LegacyNativeTimerSubscription>(pool.Subscribe(new LegacyNativeLazyDelegate(_=>{},77)));
        var second=Assert.IsType<LegacyNativeTimerSubscription<int>>(pool.Subscribe(new LegacyNativeTimerRule<int>((_,_)=>{},17),8));
        Assert.Equal(2,pool.dictionary_0!.Count);Assert.Empty(pool.list_0!);Assert.Null(pool.propagationThread);
        ((IDisposable)pool).Dispose();((IDisposable)first).Dispose();((IDisposable)second).Dispose();
    }

    [Fact]
    public void QueueDequeueAndClearRetainDictionarySubscriptionsAndObjectIdentity()
    {
        if(!OperatingSystem.IsWindows()) return;
        using var count=new CounterScope();var pool=new LegacyNativeStaticPool();
        var a=Assert.IsType<LegacyNativeTimerSubscription>(pool.Subscribe(new LegacyNativeTimerRule(_=>{},1)));
        var b=Assert.IsType<LegacyNativeTimerSubscription>(pool.Subscribe(new LegacyNativeTimerRule(_=>{},1)));
        pool.queue_0!.Enqueue(a);pool.queue_0.Enqueue(b);
        Assert.True(pool.method_3(out var first));Assert.Same(a,first);Assert.Equal(1,pool.Treshold);
        pool.Clear();Assert.Equal(0,pool.Treshold);Assert.Equal(2,pool.dictionary_0!.Count);
        Assert.False(pool.method_3(out var empty));Assert.Null(empty);
        pool.method_4(a.GetHashCode());Assert.False(pool.dictionary_0.ContainsKey(a.GetHashCode()));
        Assert.Equal("0 waiting exec, 1 subscriptions, 0 threads: 0 in use, 0 idle",pool.ToString());
        ((IDisposable)pool).Dispose();((IDisposable)a).Dispose();((IDisposable)b).Dispose();
    }

    [Fact]
    public void DisposeUnstartedPoolRetainsOriginalNullingIdempotenceAndLaterFaultPrefixes()
    {
        if(!OperatingSystem.IsWindows()) return;
        using var count=new CounterScope();var pool=new LegacyNativeStaticPool();
        ((IDisposable)pool).Dispose();((IDisposable)pool).Dispose();
        Assert.True(pool.bool_1);Assert.False(pool.bool_0);Assert.Null(pool.dictionary_0);Assert.Null(pool.list_0);Assert.Null(pool.queue_0);
        Assert.Throws<NullReferenceException>(()=>pool.Clear());Assert.Throws<NullReferenceException>(()=>pool.ToString());
        int before=LegacyNativeTimerSubscriptionBase.int_0;
        Assert.Throws<NullReferenceException>(()=>pool.Subscribe(new LegacyNativeTimerRule(_=>{},1)));
        Assert.Equal(unchecked(before+1),LegacyNativeTimerSubscriptionBase.int_0);
    }
}
