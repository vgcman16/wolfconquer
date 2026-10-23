using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeamOwnerPacketTests
{
    [Theory]
    [InlineData(0u,0u)] [InlineData(3u,1u)] [InlineData(uint.MaxValue,uint.MaxValue)]
    public Task WholeSignupCodecUsesActualCurrentPlayerAndArenaRecordNotTheStructSize(uint type,uint button)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Client.TeamArenaStatistic!.Info.TodayRank=123;
        scope.Packet.TeamArenaSignupCreate((LegacyNativeMsgTeamArenaSignup.DialogType)type,(LegacyNativeMsgTeamArenaSignup.DialogButton)button,scope.Client);
        var body=new List<byte>();U32(body,type);U32(body,button);U32(body,17);Fixed(body,"OwnerWolf",32);
        foreach(uint value in new uint[]{123,16004,0,456,137})U32(body,value);
        Assert.Equal(Frame(2060,body),Bytes(scope.Packet));Assert.Equal(68,scope.Packet.Size);Assert.Equal(60,Marshal.SizeOf<LegacyNativeMsgTeamArenaSignup>());
        scope.Packet.Seek(4);scope.Packet.GetTeamArenaSignup(out var readType,out var readButton);
        Assert.Equal(type,(uint)readType);Assert.Equal(button,(uint)readButton);Assert.Equal(12,scope.Packet.Position);Assert.Equal(17u,scope.Packet.ReadUInt32());Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task WholeSignupWriterReadsReplacementRecordAndCurrentPlayerOnEveryCall()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Packet.TeamArenaSignupCreate(0,0,scope.Client);var original=Bytes(scope.Packet);
        scope.Client.TeamArenaStatistic=Row(987,"UnusedArenaName");scope.Client.TeamArenaStatistic.Info.ArenaPoints=654;
        scope.Client.Player.Name="CurrentPlayer";scope.Client.Player.UID=uint.MaxValue;
        scope.Packet.TeamArenaSignupCreate(0,0,scope.Client);byte[] frame=Bytes(scope.Packet);
        Assert.NotEqual(original,frame);Assert.Equal(uint.MaxValue,BitConverter.ToUInt32(frame,12));
        Assert.Equal(987u,BitConverter.ToUInt32(frame,48));Assert.Equal(654u,BitConverter.ToUInt32(frame,60));
        Assert.Contains("CurrentPlayer",System.Text.Encoding.UTF8.GetString(frame));Assert.DoesNotContain("UnusedArenaName",System.Text.Encoding.UTF8.GetString(frame));
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MissingSignupRecordOrInfoFaultsAfterActualPlayerPrefixWithoutFinalization(bool missingInfo)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();if(missingInfo)scope.Client.TeamArenaStatistic!.Info=null!;else scope.Client.TeamArenaStatistic=null;
        Assert.Throws<NullReferenceException>(()=>scope.Packet.TeamArenaSignupCreate(0,0,scope.Client));
        Assert.Equal(48,scope.Packet.Position);Assert.Equal(321,scope.Packet.Size);
        byte[] raw=Bytes(scope.Packet);Assert.Equal(17u,BitConverter.ToUInt32(raw,12));Assert.All(raw[48..68],value=>Assert.Equal((byte)0xA5,value));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullSignupClientStillRetainsTwoRawDialogWordsBeforeFault()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();Assert.Throws<NullReferenceException>(()=>scope.Packet.TeamArenaSignupCreate((LegacyNativeMsgTeamArenaSignup.DialogType)11,(LegacyNativeMsgTeamArenaSignup.DialogButton)5,null!));
        Assert.Equal(12,scope.Packet.Position);Assert.Equal(321,scope.Packet.Size);byte[] raw=Bytes(scope.Packet);
        Assert.Equal(11u,BitConverter.ToUInt32(raw,4));Assert.Equal(5u,BitConverter.ToUInt32(raw,8));Assert.Empty(scope.Queue);
    });
}
