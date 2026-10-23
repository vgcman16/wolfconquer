using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeamOwnerPacketTests
{
    [Fact]
    public Task WholePrivateTop10HandlerFiltersNullsButKeepsActualArrayOrderAndAllRows()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var first=Row(9,"First");var second=Row(1,"Second");
        LegacyNativeTeamArenaRankings.Top10=[null!,first,null!,second];TopHandler(scope);
        var body=new List<byte>();U32(body,10);TopRow(body,"First",9);TopRow(body,"Second",1);
        Assert.Equal(Frame(2287,body),Assert.Single(scope.Queue));Assert.Same(first,LegacyNativeTeamArenaRankings.Top10[1]);
        Assert.Equal(9u,first.Info.TodayRank);Assert.Null(typeof(LegacyNativeMsgTeamArenaRank10).GetMethod("Handler",BindingFlags.Static|BindingFlags.Public));
    });

    [Fact]
    public Task WholeTop10HandlerDoesNotInventTenEntryCapWhenNativeArrayIsLonger()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();LegacyNativeTeamArenaRankings.Top10=Enumerable.Range(1,12).Select(i=>Row((uint)i)).ToArray();
        TopHandler(scope);byte[] frame=Assert.Single(scope.Queue);Assert.Equal(728,frame.Length);
        Assert.Equal(12u,BitConverter.ToUInt32(frame,8+11*60+32));
    });

    [Theory]
    [InlineData((ushort)1,10)] [InlineData((ushort)2,2)] [InlineData((ushort)3,0)] [InlineData((ushort)65535,0)]
    public Task WholeTodayHandlerPagesRealNonemptyRowsAndPreservesTotalCount(ushort page,int rows)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();LegacyNativeTeamArenaRankings.Top1000Today=Enumerable.Range(1,12).Select(i=>Row((uint)i,"Wolf"+i)).ToArray();
        Query(scope.Packet,page);LegacyNativeMsgTeamArenaRanking.Handler(scope.Client,scope.Packet);
        var body=new List<byte>();U16(body,page);U16(body,12);U32(body,10);
        for(int i=0;i<rows;i++){uint rank=(uint)((page-1)*10+i+1);TodayRow(body,rank,"Wolf"+rank);}
        Assert.Equal(Frame(2186,body),Assert.Single(scope.Queue));
    });

    [Fact]
    public Task WholeTodayHandlerFiltersNullsThenSkipsRank1000WithoutRepairingCountOrIndexRank()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var hidden=Row(1000,"Hidden");var included=Row(999,"Included");
        LegacyNativeTeamArenaRankings.Top1000Today=[null!,hidden,null!,included];Query(scope.Packet,1);
        LegacyNativeMsgTeamArenaRanking.Handler(scope.Client,scope.Packet);
        var body=new List<byte>();U16(body,1);U16(body,2);U32(body,10);TodayRow(body,2,"Included");
        Assert.Equal(Frame(2186,body),Assert.Single(scope.Queue));Assert.Equal(999u,included.Info.TodayRank);
    });

    [Fact]
    public Task TodayHandlerTotalCountUsesNativeUshortWrapAndDoesNotSuppressRealRows()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var row=Row();LegacyNativeTeamArenaRankings.Top1000Today=Enumerable.Repeat(row,65536).ToArray();
        Query(scope.Packet,1);LegacyNativeMsgTeamArenaRanking.Handler(scope.Client,scope.Packet);
        byte[] frame=Assert.Single(scope.Queue);Assert.Equal((ushort)0,BitConverter.ToUInt16(frame,6));Assert.Equal(572,frame.Length);
        Assert.Same(row,LegacyNativeTeamArenaRankings.Top1000Today[65535]);
    });

    [Fact]
    public Task EmptyWholeHandlersStillPublishTheirActualDifferentHeaderShapes()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();TopHandler(scope);Query(scope.Packet,1);LegacyNativeMsgTeamArenaRanking.Handler(scope.Client,scope.Packet);
        Assert.Equal(new byte[]{8,0,239,8,10,0,0,0},scope.Queue.Dequeue());
        Assert.Equal(new byte[]{12,0,138,8,1,0,0,0,10,0,0,0},Assert.Single(scope.Queue));
    });
}
