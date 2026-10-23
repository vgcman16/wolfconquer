using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Jiang=WolfConquer.Core.World.LegacyNativeJiangHu;
using Mode=WolfConquer.Core.World.LegacyNativeMsgJiangHuInfo.JiangMode;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeJiangHuTests
{
    private static byte[] Wire(ushort opcode,Action<BinaryWriter> body)
    {
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.UTF8,leaveOpen:true);
        writer.Write((ushort)0);writer.Write(opcode);body(writer);
        var bytes=stream.ToArray();Assert.InRange(bytes.Length,4,1024);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes,checked((ushort)bytes.Length));return bytes;
    }

    private static void Text(BinaryWriter writer,string value,int width)
    {
        var bytes=Encoding.UTF8.GetBytes(value);int count=Math.Min(value.Length,width);
        writer.Write(bytes,0,count);writer.Write(new byte[width-count]);
    }

    private static void Strings(BinaryWriter writer,params string[] values)
    {
        writer.Write((byte)values.Length);
        foreach(string value in values){var bytes=Encoding.UTF8.GetBytes(value);writer.Write((ushort)bytes.Length);writer.Write(bytes);}
    }

    private static unsafe void Put(LegacyNativePacket packet,byte[] bytes,int cursor=4)
    {
        Assert.InRange(bytes.Length,4,1024);Marshal.Copy(bytes,0,(IntPtr)packet.Memory,bytes.Length);
        packet.Size=bytes.Length;packet.Seek(cursor);
    }

    private static (Mode Mode,string[] Values) ReadInfo(LegacyNativePacket packet,byte[] bytes)
    {
        Assert.Equal((ushort)2082,BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        Put(packet,bytes);packet.GetJiangHuInfo(out var mode,out var data);Assert.Equal(bytes.Length,packet.Position);
        return(mode,data);
    }

    [Fact]
    public Task InfoAndUpdatesPrimitiveCodecsKeepExactNativeBytesAndCurrentCursorDecoders()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();
        native.Packet.JiangHuInfoCreate(Mode.SetName,"狼","", "Hero");
        var expected=Wire(2082,w=>{w.Write((byte)14);Strings(w,"狼","","Hero");});
        Assert.Equal(expected,NativeSpellTestFixture.Bytes(native.Packet));Assert.Equal(4,native.Packet.Position);
        var decoded=ReadInfo(native.Packet,expected);Assert.Equal(Mode.SetName,decoded.Mode);Assert.Equal(new[]{"狼","","Hero"},decoded.Values);
        var shifted=new byte[20+expected.Length-4];expected.AsSpan(4).CopyTo(shifted.AsSpan(20));
        Put(native.Packet,shifted,cursor:20);native.Packet.GetJiangHuInfo(out var shiftedMode,out var shiftedText);
        Assert.Equal(Mode.SetName,shiftedMode);Assert.Equal(decoded.Values,shiftedText);Assert.Equal(shifted.Length,native.Packet.Position);
        native.Packet.JiangHuUpdatesCreate(0x12345678,LegacyNativeMsgJiangHuUpdates.Action.TrainingPill,9,8,0xABCD,7,0x87654321);
        expected=Wire(2070,w=>{w.Write(0x12345678u);w.Write((byte)0);w.Write((byte)2);w.Write((byte)9);w.Write((byte)8);
            w.Write((byte)0);w.Write((ushort)0xABCD);w.Write((byte)7);w.Write(0x87654321u);});
        Assert.Equal(expected,NativeSpellTestFixture.Bytes(native.Packet));
        native.Packet.Seek(4);native.Packet.GetJiangHuUpdates(out var action,out var star,out var stage);
        Assert.Equal(LegacyNativeMsgJiangHuUpdates.Action.TrainingPill,action);Assert.Equal((byte)9,star);Assert.Equal((byte)8,stage);
        Assert.Equal(12,native.Packet.Position); // Reader deliberately stops before result suffix.
        Assert.Empty(native.Queue);
    });

    [Fact]
    public Task StatusCodecWritesAllStarsOfActiveStagesIncludingInactiveStarsAndNativeFixedTextTruncation()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();
        var value=New();value.ArrayStages[0].Activate=true;
        for(int i=0;i<9;i++)value.ArrayStages[0].ArrayStars[i].UID=(ushort)(100+i);
        value.ArrayStages[1].ArrayStars[0].UID=999;
        native.Packet.JiangHuStatusCreate("狼",0,5,77,0x123456789ABCDEF0,88,7,99,value.ArrayStages);
        var expected=Wire(2085,w=>{Text(w,"狼",32);w.Write((byte)1);w.Write((byte)5);w.Write(77u);w.Write((byte)0);
            w.Write(0x123456789ABCDEF0ul);w.Write(88u);w.Write(9999999);w.Write((byte)7);w.Write(99u);
            for(int i=0;i<9;i++)w.Write((ushort)(100+i));});
        Assert.Equal(expected,NativeSpellTestFixture.Bytes(native.Packet));Assert.Equal(82,native.Packet.Size);
        Assert.Equal(4,native.Packet.Position);Assert.All(value.ArrayStages[0].ArrayStars,star=>Assert.False(star.Activate));
        Assert.Empty(native.Queue);
    });

    [Fact]
    public Task RankCodecAndWholePkFlagComponentUseActualMutableOwnersWithoutRegisteringRoutes()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();
        var value=New();value.Rank=7;value.Inner_Strength=0x12345678;value.Level=255;
        native.Packet.Size=321;native.Packet.JiangHuRankCreate(2,3,1,100);
        Assert.Equal(9,native.Packet.Position);Assert.Equal(321,native.Packet.Size);
        native.Packet.AddItemJiangHuRank(value);Assert.Equal(82,native.Packet.Position);
        native.Packet.JiangHuRankFinalize();
        Assert.Equal(Wire(2073,w=>{w.Write((byte)0);w.Write((byte)2);w.Write((byte)3);w.Write((byte)1);w.Write((byte)100);
            w.Write((byte)7);w.Write(0x12345678u);w.Write(255u);Text(w,"Hero",32);Text(w,"Cultivation",32);}),NativeSpellTestFixture.Bytes(native.Packet));
        var input=Wire(2012,w=>w.Write(uint.MaxValue));Put(native.Packet,input);
        LegacyNativeMsgJiangPkMode.Process(native.Owner,native.Packet);
        Assert.Equal((Jiang.AttackFlag)uint.MaxValue,native.Owner.Player.JiangPkFlag);
        Assert.Equal(8,native.Packet.Position);Assert.Empty(native.Queue);
    });

    [Theory]
    [InlineData((byte)0,0,0)]
    [InlineData((byte)1,10,10)]
    [InlineData((byte)10,10,9)]
    [InlineData((byte)11,0,0)]
    public Task WholeRankHandlerPreservesPageUnderflowAndAdvertisesTenButOmitsRankHundred(byte page,int advertised,int included)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();
        var rows=Enumerable.Range(1,100).Select(i=>New((uint)i)).ToArray();
        for(int i=0;i<rows.Length;i++){rows[i].Rank=(byte)(i+1);rows[i].Inner_Strength=(uint)(100-i);}
        Jiang.JiangHuRanking.TopRank100=rows;native.Owner.Player.MyJiangHu=rows[99];
        Put(native.Packet,Wire(2073,w=>{w.Write((byte)99);w.Write(page);}));
        LegacyNativeMsgJiangHuRank.Process(native.Owner,native.Packet);
        var frame=Assert.Single(native.Queue);
        Assert.Equal((ushort)2073,BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal(new byte[]{0,page,0,(byte)advertised,100},frame.AsSpan(4,5).ToArray());
        Assert.Equal(9+73*included,frame.Length);
    });

    [Fact]
    public Task NullRankArrayFaultsBeforeNativeRedundantNullCheckAndBeforeSend()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();
        Put(native.Packet,Wire(2073,w=>{w.Write((byte)0);w.Write((byte)1);}));
        Assert.Throws<ArgumentNullException>(()=>LegacyNativeMsgJiangHuRank.Process(native.Owner,native.Packet));
        Assert.Equal(6,native.Packet.Position);Assert.Empty(native.Queue);
    });

    [Theory]
    [InlineData(false,256000000u)]
    [InlineData(true,13751297u)]
    public Task SendStatusUsesThisJiangFieldsButAttackedPlayersStudyAndCurrentIdentityTimer(bool foreign,uint timer)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();using var native=new NativeSpellTestFixture();
        var value=New();value.Talent=5;value.RoundBuyPoints=33;value.FreeTimeToday=44;
        var attacked=foreign?new LegacyNativeGameClientRuntime():native.Owner;
        attacked.Player.UID=foreign?77u:17u;attacked.Player.SubClass=new(){StudyPoints=123};
        value.SendStatus(native.Packet,native.Owner,attacked);
        var expected=Wire(2085,w=>{Text(w,"Cultivation",32);w.Write((byte)1);w.Write((byte)5);w.Write(timer);w.Write((byte)0);
            w.Write(123ul);w.Write(44u);w.Write(9999999);w.Write((byte)0);w.Write(33u);});
        Assert.Equal(expected,Assert.Single(native.Queue));
    });
}
