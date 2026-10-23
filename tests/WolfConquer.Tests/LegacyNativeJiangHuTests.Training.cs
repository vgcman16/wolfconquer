using WolfConquer.Core.World;
using Jiang=WolfConquer.Core.World.LegacyNativeJiangHu;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeJiangHuTests
{
    [Theory]
    [InlineData(274,110,true)]
    [InlineData(357,277,true)]
    [InlineData(273,110,false)]
    [InlineData(358,277,false)]
    [InlineData(370,232,true)]
    [InlineData(370,235,true)]
    [InlineData(371,235,false)]
    [InlineData(350,322,true)]
    [InlineData(281,322,true)]
    [InlineData(350,323,false)]
    [InlineData(247,200,true)]
    [InlineData(247,243,true)]
    [InlineData(246,243,false)]
    public void InTwinCastlePreservesAllFourInclusiveRectanglesAndMapGate(int x,int y,bool expected)
    {
        using var scope=new Scope();var value=New();var user=new LegacyNativeGameClientRuntime();
        user.Player.Map=1002;user.Player.X=(ushort)x;user.Player.Y=(ushort)y;
        Assert.Equal(expected,value.InTwinCastle(user.Player));user.Player.Map=1000;
        Assert.False(value.InTwinCastle(user.Player));
    }

    [Theory]
    [InlineData(30000u,31000u)]
    [InlineData(30001u,31000u)]
    [InlineData(30999u,31000u)]
    [InlineData(uint.MaxValue,0u)]
    public Task RewardAlwaysIncrementsBeforeRoundingSendsOldDeadlineThenCreatesRealNewTime(uint course,uint expected)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();var value=New();
        value.FreeCourse=course;value.CountDownEnd=new DateTime(1970,1,1).ToLocalTime().AddSeconds(77);
        var before=DateTime.Now;value.GetReward(native.Owner);var after=DateTime.Now;
        Assert.Equal(expected,value.FreeCourse);var info=ReadInfo(native.Packet,Assert.Single(native.Queue));
        Assert.Equal(new[]{expected.ToString(),"77"},info.Values);
        Assert.InRange(value.StartCountDwon,before,after);
        Assert.InRange(value.CountDownEnd,before.AddMinutes(385),after.AddMinutes(385));
    });

    [Theory]
    [InlineData(false,26u,1d)]
    [InlineData(true,833u,32d)]
    public Task WholeTimerTickUsesActualMapModeTalentAndCourseTablesWithoutWaiting(bool castle,uint gain,double advance)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();var value=New();
        var start=new DateTime(2025,1,1);value.StartCountDwon=start;value.CountDownEnd=DateTime.Now.AddDays(1);
        value.ThreadStamp=DateTime.Now.AddMinutes(-2);value.FreeCourse=30001;
        native.Owner.Player.Map=1002;native.Owner.Player.X=274;native.Owner.Player.Y=110;
        native.Owner.Player.PkMode=castle?LegacyNativePkMode.Jiang:LegacyNativePkMode.Capture;
        var before=DateTime.Now;value.CheckStatus(native.Owner);var after=DateTime.Now;
        Assert.Equal(30001u+gain,value.FreeCourse);Assert.Equal(start.AddMinutes(advance),value.StartCountDwon);
        Assert.InRange(value.ThreadStamp,before,after);Assert.Single(native.Queue);
        if(!castle)Assert.InRange(value.CountDownMode,before.AddMinutes(10),after.AddMinutes(10));
    });

    [Fact]
    public Task ExpiredModeKeepsTalentOnPlayerWhileExplicitDisableClearsItAndBothBroadcastRealInfo()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();var value=New();
        value.OnJiangMode=true;value.ThreadStamp=DateTime.Now.AddMinutes(-2);value.CountDownMode=DateTime.Now.AddMinutes(-1);
        value.FreeCourse=1000001;CheckMode(value,true);native.Owner.Player.JiangHuActive=1;
        value.CheckStatus(native.Owner);
        Assert.False(value.OnJiangMode);Assert.Equal((byte)0,native.Owner.Player.JiangHuActive);
        Assert.Equal((byte)3,native.Owner.Player.JiangHuTalent);Assert.Equal(1000000u,value.FreeCourse);
        Assert.Equal(new[]{"17","3","2"},ReadInfo(native.Packet,native.Queue.Dequeue()).Values);
        value.OnJiangMode=true;native.Owner.Player.JiangHuActive=1;value.DisableJiang(native.Owner);
        Assert.False(value.OnJiangMode);Assert.Equal((byte)0,native.Owner.Player.JiangHuTalent);
        Assert.Equal(new[]{"17","3","2"},ReadInfo(native.Packet,Assert.Single(native.Queue)).Values);
        value.ThreadStamp=DateTime.Now.AddDays(1);value.CheckStatus(null!); // Whole first time gate precedes owner access.
    });

    [Fact]
    public Task KillOverwritesRatherThanAddsCourseAndTransfersTalentThroughActualCurrentOwner()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();var value=New();
        var opponent=new LegacyNativeGameClientRuntime();opponent.Player.MyJiangHu=New(77);
        opponent.Player.MyJiangHu.FreeCourse=12345;opponent.Player.MyJiangHu.Talent=2;
        native.Owner.Player.MyJiangHu=value;value.FreeCourse=900000;value.Talent=3;
        value.Kill(native.Owner,opponent);
        Assert.Equal(703u,value.FreeCourse);Assert.Equal((byte)4,value.Talent);
        Assert.Equal((byte)1,opponent.Player.MyJiangHu.Talent);
        Assert.Equal(2,native.Queue.Count);
        Assert.Equal("703",ReadInfo(native.Packet,native.Queue.Dequeue()).Values[0]);
        Assert.Equal(new[]{"17","4"},ReadInfo(native.Packet,native.Queue.Dequeue()).Values);
    });
}
