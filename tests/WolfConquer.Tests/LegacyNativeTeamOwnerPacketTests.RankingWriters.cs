using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeamOwnerPacketTests
{
    [Theory]
    [InlineData(0u)] [InlineData(10u)] [InlineData(uint.MaxValue)]
    public Task Top10WholeWriterUsesRealSeasonFieldsWithoutCountOrTenRowNormalization(uint mode)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var row=Row();scope.Packet.TeamArenaRank10Create((LegacyNativeArenaRuntime.ArenaIDs)mode);
        scope.Packet.AddItemTeamArenaRank10(row);scope.Packet.AddItemTeamArenaRank10(row);scope.Packet.TeamArenaRank10Finalize();
        var body=new List<byte>();U32(body,mode);TopRow(body,"WolfOne",3);TopRow(body,"WolfOne",3);
        Assert.Equal(Frame(2287,body),Bytes(scope.Packet));Assert.Equal(128,scope.Packet.Size);Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData((ushort)0,(ushort)0)] [InlineData((ushort)65535,(ushort)65535)]
    public Task TodayWholeWriterRetainsRawHeaderAndAllRawRowWidths(ushort page,ushort count)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Packet.TeamArenaRankingCreate(page,count,(LegacyNativeArenaRuntime.ArenaIDs)uint.MaxValue);
        scope.Packet.AddItemTeamArenaRanking(uint.MaxValue,456,16004,137,255,"WolfOne");scope.Packet.MsgTeamArenaRankingFinalize();
        var body=new List<byte>();U16(body,page);U16(body,count);U32(body,uint.MaxValue);TodayRow(body,uint.MaxValue,"WolfOne",gender:255);
        Assert.Equal(Frame(2186,body),Bytes(scope.Packet));Assert.Equal(68,scope.Packet.Size);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task TodayReaderConsumesOnlyTwoBytesAndLeavesNativeFollowingPayloadUntouched()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Packet.InitWriter();scope.Packet.Write((ushort)65535);scope.Packet.Write(0xAABBCCDDu);scope.Packet.Seek(4);
        scope.Packet.GetTeamArenaRanking(out ushort page);Assert.Equal(ushort.MaxValue,page);Assert.Equal(6,scope.Packet.Position);
        Assert.Equal(0xAABBCCDDu,scope.Packet.ReadUInt32());Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NameRowsRetainNativeCharacterCountUtf8PrefixQuirkRatherThanByteSafeTruncation()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Packet.TeamArenaRank10Create(LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
        scope.Packet.AddItemTeamArenaRank10(Row(name:"狼队"));scope.Packet.TeamArenaRank10Finalize();
        byte[] frame=Bytes(scope.Packet);Assert.Equal(new byte[]{0xE7,0x8B},frame[8..10]);Assert.All(frame[10..40],value=>Assert.Equal((byte)0,value));
        Assert.Equal(68,frame.Length);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullTopRecordFaultsAfterBeginPrefixAndDoesNotFinalizeOrAcknowledge()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Packet.TeamArenaRank10Create(LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
        Assert.Throws<NullReferenceException>(()=>scope.Packet.AddItemTeamArenaRank10(null!));
        Assert.Equal(8,scope.Packet.Position);Assert.Equal(321,scope.Packet.Size);Assert.Empty(scope.Queue);
    });
}
