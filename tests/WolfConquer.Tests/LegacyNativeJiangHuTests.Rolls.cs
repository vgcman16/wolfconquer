using System.Reflection;
using WolfConquer.Core.World;
using Jiang=WolfConquer.Core.World.LegacyNativeJiangHu;
using Table=WolfConquer.Core.Data.LegacyNativeJianHuTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeJiangHuTests
{
    private static byte RollModel(Random local,Random global,byte trying)
    {
        byte level=(byte)local.Next(trying==0?1:3,7);
        bool first=global.Next()%100==0;
        bool second=first&&global.Next()%100==0;
        if(second&&level>=6)level=(byte)Math.Min(6,local.Next(1,20));
        return level;
    }

    private static Random BindLocal(Jiang value,int seed)
    {
        var random=new Random(seed);
        typeof(Jiang).GetField("Rand",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(value,random);
        return random; // Owned physical native field, not a provider or injected random result.
    }

    [Theory]
    [InlineData((byte)0,71,88)]
    [InlineData((byte)1,71,88)]
    [InlineData((byte)255,12,99)]
    public void StarLevelUsesBothRealRngOwnersAndKeepsNativeShortCircuitDrawOrder(byte trying,int localSeed,int globalSeed)
    {
        using var scope=new Scope();var value=New();
        var actualLocal=BindLocal(value,localSeed);LegacyNativePoolRandom.GetRandom=new(globalSeed);
        var local=new Random(localSeed);var global=new Random(globalSeed);
        byte expected=RollModel(local,global,trying);
        Assert.Equal(expected,value.CreateStarLevel(trying));
        Assert.Equal(local.Next(),actualLocal.Next());
        Assert.Equal(global.Next(),LegacyNativePoolRandom.GetRandom.Next());
    }

    [Fact]
    public void RareDoubleNativeRateSuccessMakesSecondLocalDrawAndRetainsSixCap()
    {
        using var scope=new Scope();var value=New();
        int globalSeed=0,localSeed=0;
        for(int seed=1;seed<=200000;seed++)
        {var random=new Random(seed);if(random.Next()%100==0&&random.Next()%100==0){globalSeed=seed;break;}}
        for(int seed=1;seed<=10000;seed++)
        {var random=new Random(seed);if(random.Next(1,7)==6&&random.Next(1,20)>6){localSeed=seed;break;}}
        Assert.NotEqual(0,globalSeed);Assert.NotEqual(0,localSeed);
        var actual=BindLocal(value,localSeed);LegacyNativePoolRandom.GetRandom=new(globalSeed);
        var model=new Random(localSeed);Assert.Equal(6,model.Next(1,7));Assert.True(model.Next(1,20)>6);
        var global=new Random(globalSeed);Assert.Equal(0,global.Next()%100);Assert.Equal(0,global.Next()%100);
        Assert.Equal((byte)6,value.CreateStarLevel(0));
        Assert.Equal(model.Next(),actual.Next());Assert.Equal(global.Next(),LegacyNativePoolRandom.GetRandom.Next());
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)9)]
    [InlineData((byte)255)]
    public Task WholeCreateRollUsesRealCatalogAndRngAndDoesNotAddItsOwnMissingCallerStarGuard(byte star)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();var value=New();
        var actual=BindLocal(value,71);LegacyNativePoolRandom.GetRandom=new(88);
        var local=new Random(71);var global=new Random(88);byte level=RollModel(local,global,255);
        byte type=(byte)local.Next(1,20);ushort uid=(ushort)(type+level*256);
        Table.CultivateStatus[1]=Enumerable.Range(1,19).Select(i=>(byte)i).ToList();
        native.Owner.Player.JingTryngUltra=255;value.FreeTimeToday=257;value.RoundBuyPoints=1000;
        value.CreateRollValue(native.Packet,native.Owner,star,1);
        Assert.True(value.ArrayStages[0].Activate);var backup=Assert.IsType<Jiang.BackUpStar>(value.Buckupstar);
        Assert.Equal(star,backup.PositionStar);Assert.Equal((byte)1,backup.Stage);
        Assert.True(backup.Star.Activate);Assert.Equal(level,backup.Star.Level);Assert.Equal(uid,backup.Star.UID);
        Assert.Equal((byte)255,native.Owner.Player.JingTryngUltra); // Argument decrement is local only.
        Assert.Equal(Wire(2070,w=>{w.Write(30000u);w.Write((byte)0);w.Write((byte)0);w.Write(star);w.Write((byte)1);
            w.Write((byte)0);w.Write(uid);w.Write((byte)1);w.Write(1000u);}),Assert.Single(native.Queue));
        Assert.Equal(local.Next(),actual.Next());Assert.Equal(global.Next(),LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task LockedStageDoesNothingButMissingCatalogFaultPublishesBackupPrefixBeforeSend()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();var value=New();
        var local=BindLocal(value,71);var model=new Random(71);
        value.CreateRollValue(native.Packet,native.Owner,1,2);
        Assert.Null(value.Buckupstar);Assert.Empty(native.Queue);Assert.Equal(model.Next(),local.Next());
        Assert.Throws<KeyNotFoundException>(()=>value.CreateRollValue(native.Packet,native.Owner,1,1));
        Assert.True(value.ArrayStages[0].Activate);var backup=Assert.IsType<Jiang.BackUpStar>(value.Buckupstar);
        Assert.True(backup.Star.Activate);Assert.InRange(backup.Star.Level,(byte)1,(byte)6);
        Assert.InRange((int)backup.Star.Typ,1,19);Assert.Equal((ushort)0,backup.Star.UID);Assert.Empty(native.Queue);
    });

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)10)]
    [InlineData((byte)255)]
    public Task InvalidStageFaultsBeforeMutatingStagesOrBackup(byte stage)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();var value=New();
        var old=new Jiang.BackUpStar();value.Buckupstar=old;
        Assert.Throws<IndexOutOfRangeException>(()=>value.CreateRollValue(native.Packet,native.Owner,1,stage));
        Assert.Same(old,value.Buckupstar);Assert.All(value.ArrayStages,item=>Assert.False(item.Activate));Assert.Empty(native.Queue);
    });
}
