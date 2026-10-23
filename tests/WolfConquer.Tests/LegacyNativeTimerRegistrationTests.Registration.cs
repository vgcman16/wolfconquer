using System.Threading;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTimerRegistrationTests
{
    [Fact]
    public Task WholeRegisterPublishesFiveRealSubscriptionsInNativeArrayOrderWithoutRun() => NativePacketTestThread.Run(() =>
    {
        if(!OperatingSystem.IsWindows()) return;
        using var count=new CounterScope();
        var oldRandom=LegacyNativePoolRandom.GetRandom;var oldReset=LegacyNativePoolRandom.ResetRandom;
        var oldPool=LegacyNativeThreadPool.GenericThreadPool;
        var oldCharacters=LegacyNativeThreadPool.Characters;var oldAttack=LegacyNativeThreadPool.AutoAttack;
        var oldBuffers=LegacyNativeThreadPool.Buffers;var oldMonsters=LegacyNativeThreadPool.Monsters;var oldFloor=LegacyNativeThreadPool.Floor;
        var pool=new LegacyNativeStaticPool();LegacyNativePoolRandom.GetRandom=new(71);LegacyNativePoolRandom.ResetRandom=new DateTime(2026,1,1);
        try
        {
            using var native=new NativeSpellTestFixture();var client=native.Owner;client.TimerSyncRoot=new object();
            var seen=new List<int>();
            // Actual public TimerRule API input controls; not production callbacks,
            // not Create acceptance and not a replacement timer/worker provider.
            LegacyNativeTimerRule<LegacyNativeGameClientRuntime> Rule(int id)=>new((value,_)=>{Assert.Same(client,value);seen.Add(id);},17);
            LegacyNativeThreadPool.GenericThreadPool=pool;
            LegacyNativeThreadPool.Characters=Rule(1);LegacyNativeThreadPool.AutoAttack=Rule(2);
            LegacyNativeThreadPool.Buffers=Rule(3);LegacyNativeThreadPool.Monsters=Rule(4);LegacyNativeThreadPool.Floor=Rule(5);
            Assert.True(LegacyNativeThreadPool.Register(client));var subscriptions=client.TimerSubscriptions!;
            Assert.Equal(5,subscriptions.Length);Assert.Equal(5,pool.dictionary_0!.Count);
            foreach(var value in subscriptions) Assert.IsType<LegacyNativeTimerSubscription<LegacyNativeGameClientRuntime>>(value).vmethod_0();
            Assert.Equal(new[]{1,2,3,4,5},seen);Assert.False(LegacyNativeThreadPool.Register(client));Assert.Same(subscriptions,client.TimerSubscriptions);
            LegacyNativeThreadPool.Unregister(client);Assert.Null(client.TimerSubscriptions);
            Assert.All(subscriptions,p=>Assert.False(((LegacyNativeTimerSubscriptionBase)p).bool_0));
            Assert.Equal(5,pool.dictionary_0.Count); // disposed records removed only by the real propagation loop
            Assert.False(pool.bool_0);Assert.Empty(pool.list_0!);Assert.Null(pool.propagationThread);
        }
        finally
        {
            ((IDisposable)pool).Dispose();LegacyNativeThreadPool.GenericThreadPool=oldPool;
            LegacyNativeThreadPool.Characters=oldCharacters;LegacyNativeThreadPool.AutoAttack=oldAttack;LegacyNativeThreadPool.Buffers=oldBuffers;
            LegacyNativeThreadPool.Monsters=oldMonsters;LegacyNativeThreadPool.Floor=oldFloor;
            LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolRandom.ResetRandom=oldReset;
        }
    });

    [Fact]
    public Task FullRegisterDisposedPoolFaultKeepsSubscriptionsNullAfterRealSubscriptionBirth() => NativePacketTestThread.Run(() =>
    {
        if(!OperatingSystem.IsWindows()) return;
        using var count=new CounterScope();var oldPool=LegacyNativeThreadPool.GenericThreadPool;var oldRule=LegacyNativeThreadPool.Characters;
        var oldRandom=LegacyNativePoolRandom.GetRandom;var oldReset=LegacyNativePoolRandom.ResetRandom;
        LegacyNativePoolRandom.GetRandom=new(71);LegacyNativePoolRandom.ResetRandom=new DateTime(2026,1,1);
        var pool=new LegacyNativeStaticPool();((IDisposable)pool).Dispose();
        try
        {
            using var native=new NativeSpellTestFixture();LegacyNativeThreadPool.GenericThreadPool=pool;
            LegacyNativeThreadPool.Characters=new((_,_)=>{},17);
            int before=LegacyNativeTimerSubscriptionBase.int_0;
            Assert.Throws<NullReferenceException>(()=>LegacyNativeThreadPool.Register(native.Owner));
            Assert.Null(native.Owner.TimerSubscriptions);Assert.Equal(unchecked(before+1),LegacyNativeTimerSubscriptionBase.int_0);
        }
        finally { LegacyNativeThreadPool.GenericThreadPool=oldPool;LegacyNativeThreadPool.Characters=oldRule;LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolRandom.ResetRandom=oldReset; }
    });

    [Fact]
    public Task ExactClientTimerAndLoginSlotsRetainImplicitNullAndLiteralDefaults() => NativePacketTestThread.Run(() =>
    {
        var oldRandom=LegacyNativePoolRandom.GetRandom;var oldReset=LegacyNativePoolRandom.ResetRandom;
        LegacyNativePoolRandom.GetRandom=new(71);LegacyNativePoolRandom.ResetRandom=new DateTime(2026,1,1);
        try
        {
            using var native=new NativeSpellTestFixture();Assert.Null(native.Owner.TimerSubscriptions);Assert.Null(native.Owner.TimerSyncRoot);
            Assert.False(native.Owner.ProjectManager);Assert.Equal(0u,native.Owner.ConnectionUID);
            Assert.Throws<ArgumentNullException>(()=>LegacyNativeThreadPool.Unregister(native.Owner));
        }
        finally { LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolRandom.ResetRandom=oldReset; }
    });
}
