using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.World;
using Chi = WolfConquer.Core.World.LegacyNativeChiRuntime;
using Power = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiPower;
using Kind = WolfConquer.Core.World.LegacyNativeMsgChiInfo.ChiPowerType;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;
using Attr = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiAttribute;
using Mode = WolfConquer.Core.World.LegacyNativeMsgGenericRanking.Action;
using Ranking = WolfConquer.Core.World.LegacyNativeMsgGenericRanking.RankType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeChiTests
{
    private static byte[] RankingFrame(byte mode, uint type, ushort registered, byte page, byte finish,
        int count, byte rank, uint amount, uint uid1, uint uid2, string name, uint[]? details = null)
    {
        var bytes = new byte[130]; Put16(bytes,0,130); Put16(bytes,2,2165);
        bytes[4]=mode; Put32(bytes,5,type); Put16(bytes,9,registered); bytes[11]=page; bytes[12]=finish;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(13,4),count);
        bytes[21]=rank; BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(22,8),amount);
        Put32(bytes,30,uid1); Put32(bytes,34,uid2);
        byte[] text=Encoding.UTF8.GetBytes(name); int length=Math.Min(name.Length,32);
        text.AsSpan(0,length).CopyTo(bytes.AsSpan(38)); text.AsSpan(0,length).CopyTo(bytes.AsSpan(70));
        if(details!=null) {Put32(bytes,102,details[0]);Put32(bytes,106,details[1]);Put32(bytes,110,details[2]);}
        return bytes;
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task GenericRankingAllEntryOverloadsPreserveByteNarrowingLongAmountRepeatedNamesAndReservedTail(int overload) => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); string name="狼界AB";
        Assert.Same(scope.Packet,scope.Packet.GenericRankingCreate((Mode)0x01020304,(Ranking)0xAABBCCDD,0xBEEF,0x1234,-2,0xEE));
        Assert.Equal(21,scope.Packet.Position); Assert.Equal(321,scope.Packet.Size);
        if(overload==0) scope.Packet.AddItemGenericRankingCreate(300,uint.MaxValue,0xA1B2C3D4,name);
        else if(overload==1) scope.Packet.AddItemGenericRankingCreate(300,uint.MaxValue,0xA1B2C3D4,name,7,8,9);
        else scope.Packet.AddItemGenericRankingCreate(300,uint.MaxValue,0xA1B2C3D4,0x12345678,name,7,8,9);
        Assert.Equal(130,scope.Packet.Position);
        Assert.Same(scope.Packet,scope.Packet.GenericRankingFinalize());
        byte[] expected=RankingFrame(4,0xAABBCCDD,0xBEEF,0x34,0xEE,-2,44,uint.MaxValue,0xA1B2C3D4,
            overload==2?0x12345678u:0xA1B2C3D4u,name,overload==0?null:new uint[]{7,8,9});
        Assert.Equal(expected,NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(4,scope.Packet.Position);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task GenericRankingReaderUsesCurrentCursorIgnoresHeaderAndLeavesReservedWordUnread() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope();
        scope.Packet.Seek(23); scope.Packet.Write((byte)255); scope.Packet.Write(0xAABBCCDDu); scope.Packet.Write((ushort)0xBEEF);
        scope.Packet.Write((byte)254); scope.Packet.Write((byte)253); scope.Packet.Write(-7); scope.Packet.Write(0x12345678u);
        scope.Packet.Size=1; scope.Packet.Seek(23);
        scope.Packet.GetGenericRanking(out var mode,out var type,out var registered,out var page,out var count);
        Assert.Equal((Mode)255,mode); Assert.Equal((Ranking)0xAABBCCDD,type); Assert.Equal((ushort)0xBEEF,registered);
        Assert.Equal((ushort)254,page); Assert.Equal(-7,count); Assert.Equal(36,scope.Packet.Position);
        Assert.Equal(0x12345678u,scope.Packet.ReadUInt32()); Assert.Equal(1,scope.Packet.Size);
    });

    [Fact]
    public Task GenericRankingContinuationFinalizerPatchesOnlyNativeFinishAndCountOffsets() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope();
        scope.Packet.GenericRankingCreate(Mode.QueryCount,Ranking.Chi,7,256,99,9);
        scope.Packet.AddItemGenericRankingCreate(-1,5,6,"name");
        scope.Packet.GenericRankingFinalize(finish:false,count:-55);
        Assert.Equal(RankingFrame(2,60000000,7,0,1,-55,255,5,6,6,"name"),NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4,scope.Packet.Position);
    });

    [Fact]
    public Task WholeChiQueryUsesActualPowerScoreUidNameAndCallerTransportWithoutRankingMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope();
        var chi=new Chi(999); var power=PerfectPower(Kind.Tiger,0xA1B2C3D4,300);
        chi.SendQueryUpdate(scope.Client,power,scope.Packet);
        byte[] expected=RankingFrame(2,60000003,0,0,0,1,44,400,0xA1B2C3D4,0xA1B2C3D4,"ChiTester");
        Assert.Equal(expected,Assert.Single(scope.Queue)); Assert.Equal(expected,NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(300,power.Rank); Assert.Equal(400,power.Score); Assert.Empty(Chi.ChiPool);
        Assert.Null(scope.Client.Player.MyChi); Assert.Equal(999u,chi.UID);
    });

    [Fact]
    public Task ChiQueryNullPowerFaultsBeforeWriterButScoreFaultKeepsOnlyNativeHeaderPrefix() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); var chi=new Chi(7);
        Assert.Throws<NullReferenceException>(()=>chi.SendQueryUpdate(scope.Client,null!,scope.Packet));
        Assert.Equal(237,scope.Packet.Position); Assert.Equal(321,scope.Packet.Size);
        var power=new Power(Kind.Dragon){UnLocked=true,Attributes=null!};
        Assert.Throws<ArgumentNullException>(()=>chi.SendQueryUpdate(scope.Client,power,scope.Packet));
        Assert.Equal(21,scope.Packet.Position); Assert.Equal(321,scope.Packet.Size);
        byte[] actual=NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.All(actual.Take(4),value=>Assert.Equal((byte)0xA5,value));
        Assert.All(actual.Skip(21),value=>Assert.Equal((byte)0xA5,value));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ChiQueryNullUserFaultsAfterFinalizedWholeFrameInsteadOfInventingSendSuccess() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); var chi=new Chi(7); var power=MinimumPower(Kind.Phoenix,8,2);
        Assert.Throws<NullReferenceException>(()=>chi.SendQueryUpdate(null!,power,scope.Packet));
        Assert.Equal(RankingFrame(2,60000002,0,0,0,1,2,0,8,8,"ChiTester"),NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4,scope.Packet.Position); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ChiMessageCodecUsesBytePowerTypeIntPackedAttributeAndNativeUtf8CharacterCountBug() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope(); var attr=new Attr((Stat)511,65535,true);
        Assert.Same(scope.Packet,scope.Packet.ChiMessageCreate((Kind)255,attr,"狼界AB"));
        Assert.Equal(ChiMessage((Kind)255,attr,"狼界AB"),NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4,scope.Packet.Position); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ChiMessageNullNameFaultLeavesTypeAndPackedUidPrefixWithoutFinalHeader() => NativePacketTestThread.Run(() =>
    {
        using var scope=new Scope();
        Assert.Throws<NullReferenceException>(()=>scope.Packet.ChiMessageCreate(Kind.Dragon,123,null!));
        Assert.Equal(9,scope.Packet.Position); Assert.Equal(321,scope.Packet.Size);
        byte[] partial=NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.All(partial.Take(4),value=>Assert.Equal((byte)0xA5,value));
        Assert.Equal((byte)1,partial[4]); Assert.Equal(123u,BinaryPrimitives.ReadUInt32LittleEndian(partial.AsSpan(5,4)));
        Assert.All(partial.Skip(9),value=>Assert.Equal((byte)0xA5,value));
        Assert.Empty(scope.Queue);
    });
}
