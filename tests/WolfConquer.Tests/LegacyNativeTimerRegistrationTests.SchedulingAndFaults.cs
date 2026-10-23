using System.Threading;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTimerRegistrationTests
{
    private sealed class ThirdHashFault : IEqualityComparer<int>
    {
        internal int Calls;
        internal readonly InvalidOperationException Fault=new("owned third dictionary hash");
        public bool Equals(int a,int b)=>a==b;
        public int GetHashCode(int key) { if(++Calls==3) throw Fault;return key; }
    }

    [Fact]
    public Task RegisterThirdRealDictionaryFailureRetainsTwoPublishedSubscriptionsButNoArrayAssignment() => NativePacketTestThread.Run(() =>
    {
        if(!OperatingSystem.IsWindows()) return;
        using var count=new CounterScope();var oldPool=LegacyNativeThreadPool.GenericThreadPool;
        var oldCharacters=LegacyNativeThreadPool.Characters;var oldAttack=LegacyNativeThreadPool.AutoAttack;
        var oldBuffers=LegacyNativeThreadPool.Buffers;var oldMonsters=LegacyNativeThreadPool.Monsters;var oldFloor=LegacyNativeThreadPool.Floor;
        var oldRandom=LegacyNativePoolRandom.GetRandom;var oldReset=LegacyNativePoolRandom.ResetRandom;
        LegacyNativePoolRandom.GetRandom=new(71);LegacyNativePoolRandom.ResetRandom=new DateTime(2026,1,1);
        var pool=new LegacyNativeStaticPool();var comparer=new ThirdHashFault();
        pool.dictionary_0=new Dictionary<int,LegacyNativeTimerSubscriptionBase>(comparer);
        try
        {
            using var native=new NativeSpellTestFixture();var seen=new List<int>();
            LegacyNativeTimerRule<LegacyNativeGameClientRuntime> Rule(int n)=>new((_,_)=>seen.Add(n),1);
            LegacyNativeThreadPool.GenericThreadPool=pool;LegacyNativeThreadPool.Characters=Rule(1);LegacyNativeThreadPool.AutoAttack=Rule(2);
            LegacyNativeThreadPool.Buffers=Rule(3);LegacyNativeThreadPool.Monsters=Rule(4);LegacyNativeThreadPool.Floor=Rule(5);
            int before=LegacyNativeTimerSubscriptionBase.int_0;
            Assert.Same(comparer.Fault,Assert.Throws<InvalidOperationException>(()=>LegacyNativeThreadPool.Register(native.Owner)));
            Assert.Null(native.Owner.TimerSubscriptions);Assert.Equal(unchecked(before+3),LegacyNativeTimerSubscriptionBase.int_0);
            Assert.Equal(2,pool.dictionary_0.Count);
            foreach(var sub in pool.dictionary_0.Values.OrderBy(p=>p.GetHashCode())) sub.vmethod_0();
            Assert.Equal(new[]{1,2},seen);
        }
        finally
        {
            foreach(var value in pool.dictionary_0!.Values) ((IDisposable)value).Dispose();
            ((IDisposable)pool).Dispose();LegacyNativeThreadPool.GenericThreadPool=oldPool;
            LegacyNativeThreadPool.Characters=oldCharacters;LegacyNativeThreadPool.AutoAttack=oldAttack;LegacyNativeThreadPool.Buffers=oldBuffers;
            LegacyNativeThreadPool.Monsters=oldMonsters;LegacyNativeThreadPool.Floor=oldFloor;
            LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolRandom.ResetRandom=oldReset;
        }
    });

    [Fact]
    public Task WholeUnregisterNullEntryFaultKeepsNativeDisposalPrefixAndArrayUntilSuccessfulRetry() => NativePacketTestThread.Run(() =>
    {
        if(!OperatingSystem.IsWindows()) return;
        using var count=new CounterScope();var oldRandom=LegacyNativePoolRandom.GetRandom;var oldReset=LegacyNativePoolRandom.ResetRandom;
        LegacyNativePoolRandom.GetRandom=new(71);LegacyNativePoolRandom.ResetRandom=new DateTime(2026,1,1);
        try
        {
            using var native=new NativeSpellTestFixture();var a=new LegacyNativeTimerSubscription(new LegacyNativeTimerRule(_=>{},1));
            var b=new LegacyNativeTimerSubscription(new LegacyNativeTimerRule(_=>{},1));
            var array=new IDisposable[]{a,null!,b};native.Owner.TimerSyncRoot=new object();native.Owner.TimerSubscriptions=array;
            Assert.Throws<NullReferenceException>(()=>LegacyNativeThreadPool.Unregister(native.Owner));
            Assert.Same(array,native.Owner.TimerSubscriptions);Assert.False(a.bool_0);Assert.True(b.bool_0);
            array[1]=new LegacyNativeTimerSubscription(null!);LegacyNativeThreadPool.Unregister(native.Owner);
            Assert.Null(native.Owner.TimerSubscriptions);Assert.All(array,p=>Assert.False(((LegacyNativeTimerSubscriptionBase)p).bool_0));
            LegacyNativeThreadPool.Unregister(native.Owner);Assert.Null(native.Owner.TimerSubscriptions);
        }
        finally { LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolRandom.ResetRandom=oldReset; }
    });

    [Fact]
    public void EverySchedulingOverloadUsesRealPoolsRulesLazySemanticsAndParameterIdentityWithoutRun()
    {
        if(!OperatingSystem.IsWindows()) return;
        using var count=new CounterScope();var oldPool=LegacyNativeThreadPool.GenericThreadPool;
        var pool=new LegacyNativeStaticPool();var explicitPool=new LegacyNativeStaticPool();var standalone=new LegacyNativeStandalonePool();
        object parameter=new();var hits=new List<string>();
        try
        {
            LegacyNativeThreadPool.GenericThreadPool=pool;
            LegacyNativeThreadPool.Execute(_=>hits.Add("execute"),17,ThreadPriority.AboveNormal);
            LegacyNativeThreadPool.Execute<object>((value,_)=>{Assert.Same(parameter,value);hits.Add("execute-g");},parameter,19,ThreadPriority.Highest);
            var a=LegacyNativeThreadPool.Subscribe(_=>hits.Add("subscribe"),23,ThreadPriority.Lowest);
            var b=LegacyNativeThreadPool.Subscribe<object>((value,_)=>{Assert.Same(parameter,value);hits.Add("subscribe-g");},parameter,29,ThreadPriority.BelowNormal);
            var shared=new LegacyNativeTimerRule<object>((value,_)=>{Assert.Same(parameter,value);hits.Add("shared");},31,ThreadPriority.Normal);
            var c=LegacyNativeThreadPool.Subscribe(shared,parameter,standalone);
            var d=LegacyNativeThreadPool.Subscribe(shared,parameter,explicitPool);
            var e=LegacyNativeThreadPool.Subscribe(shared,parameter);
            Assert.Equal(5,pool.dictionary_0!.Count);Assert.Single(explicitPool.dictionary_0!);Assert.Single(standalone.dictionary_0!);
            Assert.Same(c,standalone.dictionary_0!.Values.Single());Assert.Same(d,explicitPool.dictionary_0!.Values.Single());
            Assert.Same(e,pool.dictionary_0[((LegacyNativeTimerSubscriptionBase)e).GetHashCode()]);
            var ordered=pool.dictionary_0.Values.OrderBy(p=>p.GetHashCode()).ToArray();
            Assert.Equal(ThreadPriority.AboveNormal,ordered[0].vmethod_3());Assert.Equal(ThreadPriority.Highest,ordered[1].vmethod_3());
            foreach(var sub in ordered) sub.vmethod_0();
            Assert.Equal(new[]{"execute","execute-g","subscribe","subscribe-g","shared"},hits);
            Assert.False(ordered[0].bool_0);Assert.False(ordered[1].bool_0);Assert.True(((LegacyNativeTimerSubscriptionBase)a).bool_0);Assert.True(((LegacyNativeTimerSubscriptionBase)b).bool_0);
            ((LegacyNativeTimerSubscriptionBase)c).vmethod_0();((LegacyNativeTimerSubscriptionBase)d).vmethod_0();
            Assert.Equal(7,hits.Count);
            Assert.False(pool.bool_0);Assert.False(explicitPool.bool_0);Assert.False(standalone.bool_0);
            Assert.Empty(pool.list_0!);Assert.Empty(explicitPool.list_0!);Assert.Empty(standalone.list_0!);
        }
        finally
        {
            foreach(var p in new[]{pool,explicitPool}) { foreach(var sub in p.dictionary_0!.Values) ((IDisposable)sub).Dispose();((IDisposable)p).Dispose(); }
            foreach(var sub in standalone.dictionary_0!.Values) ((IDisposable)sub).Dispose();((IDisposable)standalone).Dispose();
            LegacyNativeThreadPool.GenericThreadPool=oldPool;
        }
    }
}
